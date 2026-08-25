using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ServiceJobsController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceJobsController> _logger;

        public ServiceJobsController(ApplicationDbContext context, ILogger<ServiceJobsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /ServiceJobs
        public async Task<IActionResult> Index(string? searchString, int? mechanicId,
            string? statusFilter, string? paymentFilter, int? serviceId, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                int pageSize = 10;
                IQueryable<ServiceJob> query = _context.ServiceJobs
                    .Include(j => j.Service)
                    .Include(j => j.Mechanic)
                    .AsNoTracking();

                if (serviceId.HasValue && serviceId.Value > 0)
                    query = query.Where(j => j.ServiceId == serviceId.Value);

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    string term = searchString.Trim();
                    query = query.Where(j =>
                        j.ServiceJobNumber.Contains(term) ||
                        j.CustomerName.Contains(term) ||
                        j.Service!.ServiceName.Contains(term));
                }

                if (mechanicId.HasValue && mechanicId.Value > 0)
                    query = query.Where(j => j.MechanicId == mechanicId.Value);

                if (!string.IsNullOrWhiteSpace(statusFilter))
                    query = query.Where(j => j.Status == statusFilter);

                if (!string.IsNullOrWhiteSpace(paymentFilter))
                    query = query.Where(j => j.PaymentStatus == paymentFilter);

                int total = await query.CountAsync();

                List<ServiceJob> jobs = await query
                    .OrderBy(j => j.ServiceJobId) // SV-001, SV-002, ... ascending
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                await PopulateMechanicListAsync(mechanicId);

                ViewBag.ServiceJobsCount = total;
                ViewData["CurrentFilter"] = searchString;
                ViewData["StatusFilter"] = statusFilter;
                ViewData["PaymentFilter"] = paymentFilter;
                ViewData["ServiceId"] = serviceId;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                if (serviceId.HasValue && serviceId.Value > 0)
                {
                    Service? service = await _context.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ServiceId == serviceId.Value);
                    ViewBag.FilteredServiceName = service?.ServiceName;
                }

                return View(jobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service jobs.");
                TempData["ErrorMessage"] = "An error occurred while loading service jobs. Please try again.";
                return View(new List<ServiceJob>());
            }
        }

        // GET: /ServiceJobs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                ServiceJob? job = await _context.ServiceJobs
                    .Include(j => j.Service)
                    .Include(j => j.Mechanic)
                    .Include(j => j.SalesTransaction)
                    .Include(j => j.Histories)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);

                if (job == null) return NotFound();

                ViewBag.Histories = job.Histories
                    .OrderBy(h => h.WorkDate)
                    .ThenBy(h => h.ServiceHistoryId)
                    .ToList();

                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service job details. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the service job details. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /ServiceJobs/Create
        public async Task<IActionResult> Create()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            try
            {
                await PopulateCreateListsAsync();
                return View(new ServiceJob());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading create service job form.");
                TempData["ErrorMessage"] = "An error occurred while loading the form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /ServiceJobs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceId,MechanicId,CustomerName,Description,Status,AmountReceived")] ServiceJob job)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            Service? service = await ValidateJobAsync(job);
            bool paymentValid = ModelState.IsValid ? await ValidateAmountAsync(job, service) : false;
            if (paymentValid)
                job.PaymentStatus = ComputePaymentStatus(job.AmountReceived, service!.ServicePrice);

            if (ModelState.IsValid && service != null)
            {
                try
                {
                    job.Status = IsValidStatus(job.Status) ? job.Status : ServiceJob.StatusStillWorking;
                    job.ServiceDate = DateTime.Now;
                    job.CreatedAt = DateTime.Now;
                    if (job.Status == ServiceJob.StatusFinished)
                        job.CompletedDate = DateTime.Now;

                    // Sequential SV-### numbering, generated safely against concurrent creation.
                    await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    job.ServiceJobNumber = await GenerateServiceJobNumberAsync();
                    _context.ServiceJobs.Add(job);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Create Service Job",
                        Module = "Service",
                        Description = $"Created service job {job.ServiceJobNumber} for {job.CustomerName}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Service job {job.ServiceJobNumber} created successfully.";
                    return RedirectToAction(nameof(Details), new { id = job.ServiceJobId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while creating service job.");
                    TempData["ErrorMessage"] = "An error occurred while creating the service job. Please try again.";
                }
            }

            await PopulateCreateListsAsync(job);
            return View(job);
        }

        // GET: /ServiceJobs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                ServiceJob? job = await _context.ServiceJobs
                    .Include(j => j.Service)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);
                if (job == null) return NotFound();

                await PopulateCreateListsAsync(job);
                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading service job for editing. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the service job. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /ServiceJobs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ServiceJobId,ServiceId,MechanicId,CustomerName,Description,Status,AmountReceived")] ServiceJob job)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != job.ServiceJobId) return NotFound();

            Service? service = await ValidateJobAsync(job);
            bool paymentValid = ModelState.IsValid ? await ValidateAmountAsync(job, service) : false;
            if (paymentValid)
                job.PaymentStatus = ComputePaymentStatus(job.AmountReceived, service!.ServicePrice);

            // Manual edits may never undercut payments already recorded in history.
            if (ModelState.IsValid)
            {
                decimal historyTotal = await _context.ServiceHistories
                    .Where(h => h.ServiceJobId == id)
                    .SumAsync(h => (decimal?)h.AmountReceived) ?? 0m;
                if (job.AmountReceived < historyTotal)
                {
                    ModelState.AddModelError("AmountReceived",
                        "Amount received cannot be less than the total recorded payments in service history.");
                }
            }

            if (ModelState.IsValid && service != null)
            {
                try
                {
                    ServiceJob? existing = await _context.ServiceJobs.FindAsync(id);
                    if (existing == null) return NotFound();

                    string originalStatus = existing.Status;
                    decimal originalAmount = existing.AmountReceived;

                    existing.ServiceId = job.ServiceId;
                    existing.MechanicId = job.MechanicId;
                    existing.CustomerName = job.CustomerName;
                    existing.Description = job.Description;
                    existing.AmountReceived = job.AmountReceived;
                    existing.PaymentStatus = job.PaymentStatus;

                    // Completion rule: stamp CompletedDate only on the first transition to Finished.
                    // Reverting to Still Working never creates or overwrites a CompletedDate.
                    existing.Status = IsValidStatus(job.Status) ? job.Status : existing.Status;
                    if (existing.Status == ServiceJob.StatusFinished &&
                        originalStatus != ServiceJob.StatusFinished &&
                        existing.CompletedDate == null)
                    {
                        existing.CompletedDate = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    bool statusChanged = originalStatus != existing.Status;
                    bool amountChanged = originalAmount != existing.AmountReceived;

                    if (statusChanged)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Change Service Status",
                            Module = "Service",
                            Description = $"{existing.ServiceJobNumber}: {originalStatus} -> {existing.Status}",
                            StaffId = GetCurrentStaffId(),
                            Timestamp = DateTime.Now
                        });
                    }
                    if (amountChanged)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Record Payment",
                            Module = "Service",
                            Description = $"{existing.ServiceJobNumber}: received ₱{existing.AmountReceived:N2} of ₱{service.ServicePrice:N2} ({existing.PaymentStatus})",
                            StaffId = GetCurrentStaffId(),
                            Timestamp = DateTime.Now
                        });
                    }
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Edit Service Job",
                        Module = "Service",
                        Description = $"Edited service job {existing.ServiceJobNumber}",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Service job {existing.ServiceJobNumber} updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = existing.ServiceJobId });
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict while updating service job. ServiceJobId: {ServiceJobId}", id);
                    if (!await _context.ServiceJobs.AnyAsync(j => j.ServiceJobId == id))
                        return NotFound();

                    TempData["ErrorMessage"] = "The service job was modified by another user. Please try again.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating service job. ServiceJobId: {ServiceJobId}", id);
                    TempData["ErrorMessage"] = "An error occurred while updating the service job. Please try again.";
                }
            }

            await PopulateCreateListsAsync(job);
            return View(job);
        }

        // GET: /ServiceJobs/AddHistory/5
        public async Task<IActionResult> AddHistory(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                ServiceJob? job = await _context.ServiceJobs
                    .Include(j => j.Service)
                    .Include(j => j.Mechanic)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);
                if (job == null) return NotFound();

                ViewBag.ServiceJob = job;
                return View(new ServiceHistory { ServiceJobId = job.ServiceJobId, WorkDate = DateTime.Now });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading add history form. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the form. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: /ServiceJobs/AddHistory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHistory(int id, [Bind("WorkDate,Description,AmountReceived,PaymentStatus")] ServiceHistory history)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            ServiceJob? job = await _context.ServiceJobs
                .Include(j => j.Service)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);
            if (job == null) return NotFound();

            history.ServiceJobId = id;

            if (string.IsNullOrWhiteSpace(history.Description))
                ModelState.AddModelError("Description", "Work description is required.");
            if (history.AmountReceived < 0)
                ModelState.AddModelError("AmountReceived", "Amount cannot be negative.");
            if (!IsValidPaymentStatus(history.PaymentStatus))
                ModelState.AddModelError("PaymentStatus", "Please select a valid payment status.");

            // Payment consistency: the recorded amount must agree with the declared
            // status, and the running job total must never exceed the service price.
            decimal servicePrice = job.Service?.ServicePrice ?? 0m;
            if (ModelState.IsValid && IsValidPaymentStatus(history.PaymentStatus))
            {
                decimal remainingBalance = Math.Max(0m, servicePrice - job.AmountReceived);

                switch (history.PaymentStatus)
                {
                    case ServiceJob.PaymentUnpaid when history.AmountReceived != 0:
                        ModelState.AddModelError("AmountReceived",
                            "An unpaid work entry must record an amount of ₱0.00.");
                        break;
                    case ServiceJob.PaymentPartiallyPaid when history.AmountReceived <= 0:
                        ModelState.AddModelError("AmountReceived",
                            "A partially paid work entry must record an amount greater than ₱0.00.");
                        break;
                    case ServiceJob.PaymentPartiallyPaid:
                        if (history.AmountReceived >= remainingBalance)
                            ModelState.AddModelError("AmountReceived",
                                $"A partially paid work entry must record an amount greater than ₱0.00 and less than the remaining balance of ₱{remainingBalance:N2}.");
                        break;
                    case ServiceJob.PaymentPaid when history.AmountReceived <= 0:
                        ModelState.AddModelError("AmountReceived",
                            "A paid work entry must record an amount greater than ₱0.00.");
                        break;
                }

                decimal newTotalReceived = job.AmountReceived + history.AmountReceived;
                if (newTotalReceived > servicePrice)
                {
                    ModelState.AddModelError("AmountReceived",
                        "Total amount received cannot exceed the service amount.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    history.CreatedAt = DateTime.Now;
                    _context.ServiceHistories.Add(history);

                    // History rows are append-only: only the job totals move forward,
                    // previously saved history records are never modified.
                    bool paymentRecorded = history.AmountReceived > 0;
                    if (paymentRecorded)
                    {
                        job.AmountReceived += history.AmountReceived;
                        job.PaymentStatus = ComputePaymentStatus(job.AmountReceived, servicePrice);
                    }

                    await _context.SaveChangesAsync();

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        Action = "Add Service History",
                        Module = "Service",
                        Description = $"{job.ServiceJobNumber}: recorded work '{history.Description}' ({history.WorkDate:MMM dd, yyyy})",
                        StaffId = GetCurrentStaffId(),
                        Timestamp = DateTime.Now
                    });
                    if (paymentRecorded)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Record Payment",
                            Module = "Service",
                            Description = $"{job.ServiceJobNumber}: received ₱{history.AmountReceived:N2}; total ₱{job.AmountReceived:N2} of ₱{servicePrice:N2} ({job.PaymentStatus})",
                            StaffId = GetCurrentStaffId(),
                            Timestamp = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Work entry added to {job.ServiceJobNumber}.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while adding service history. ServiceJobId: {ServiceJobId}", id);
                    TempData["ErrorMessage"] = "An error occurred while adding the work entry. Please try again.";
                }
            }

            ViewBag.ServiceJob = job;
            return View(history);
        }

        // GET: /ServiceJobs/ConnectToSale/5
        public async Task<IActionResult> ConnectToSale(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id == null) return NotFound();

            try
            {
                ServiceJob? job = await _context.ServiceJobs
                    .Include(j => j.Service)
                    .Include(j => j.SalesTransaction)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);
                if (job == null) return NotFound();

                List<SalesTransaction> transactions = await _context.SalesTransactions
                    .AsNoTracking()
                    .OrderByDescending(t => t.TransactionId)
                    .Take(100)
                    .ToListAsync();

                ViewBag.ServiceJob = job;
                ViewBag.TransactionId = new SelectList(
                    transactions.Select(t => new {
                        t.TransactionId,
                        Label = $"{t.InvoiceNumber} — {(string.IsNullOrWhiteSpace(t.CustomerName) ? "Walk-in" : t.CustomerName)} — ₱{t.TotalAmount:N2} — {t.TransactionDate:MMM dd, yyyy}"
                    }),
                    "TransactionId", "Label", job.SalesTransactionId);

                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading connect to sale. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading sales. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: /ServiceJobs/ConnectToSale/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConnectToSale(int id, int? transactionId)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            ServiceJob? job = await _context.ServiceJobs
                .Include(j => j.SalesTransaction)
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);
            if (job == null) return NotFound();

            if (!transactionId.HasValue || transactionId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Please select a sale to connect.";
                return RedirectToAction(nameof(ConnectToSale), new { id });
            }

            try
            {
                SalesTransaction? transaction = await _context.SalesTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId.Value);
                if (transaction == null) return NotFound();

                int? previous = job.SalesTransactionId;
                job.SalesTransactionId = transaction.TransactionId;
                await _context.SaveChangesAsync();

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Connect Service Job to Sale",
                    Module = "Service",
                    Description = previous.HasValue
                        ? $"{job.ServiceJobNumber}: moved sale link from invoice #{(await _context.SalesTransactions.AsNoTracking().Where(t => t.TransactionId == previous.Value).Select(t => t.InvoiceNumber).FirstOrDefaultAsync()) ?? previous.Value.ToString()} to #{transaction.InvoiceNumber}"
                        : $"{job.ServiceJobNumber}: connected to invoice #{transaction.InvoiceNumber}",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{job.ServiceJobNumber} connected to invoice #{transaction.InvoiceNumber}.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while connecting service job to sale. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "An error occurred while connecting the service job to a sale. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static readonly Regex ServiceJobNumberPattern = new(@"^SV-(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Next sequential number based on the highest existing SV-### suffix.
        /// Max-scan (not count) so deleted rows never cause duplicate numbers.
        /// Callers must run inside a serializable transaction; a unique index on
        /// ServiceJobNumber is the final guard against duplicates.
        /// </summary>
        private async Task<string> GenerateServiceJobNumberAsync()
        {
            List<string> numbers = await _context.ServiceJobs
                .Select(j => j.ServiceJobNumber)
                .ToListAsync();

            int max = 0;
            foreach (string number in numbers)
            {
                Match m = ServiceJobNumberPattern.Match(number ?? string.Empty);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int value) && value > max)
                    max = value;
            }

            return $"SV-{(max + 1):D3}";
        }

        private static string ComputePaymentStatus(decimal amountReceived, decimal serviceAmount)
        {
            if (amountReceived <= 0)
                return ServiceJob.PaymentUnpaid;
            if (amountReceived < serviceAmount)
                return ServiceJob.PaymentPartiallyPaid;
            return ServiceJob.PaymentPaid;
        }

        private static bool IsValidStatus(string? status) =>
            ServiceJob.AllStatuses.Contains(status);

        private static bool IsValidPaymentStatus(string? status) =>
            ServiceJob.AllPaymentStatuses.Contains(status);

        /// <summary>Shared validations: service/mechanic exist, customer required, status valid.</summary>
        private async Task<Service?> ValidateJobAsync(ServiceJob job)
        {
            if (string.IsNullOrWhiteSpace(job.CustomerName))
                ModelState.AddModelError("CustomerName", "Customer name is required.");
            else if (job.CustomerName.Length > 150)
                ModelState.AddModelError("CustomerName", "Customer name must be 150 characters or fewer.");

            if (job.Description != null && job.Description.Length > 500)
                ModelState.AddModelError("Description", "Description must be 500 characters or fewer.");

            if (!IsValidStatus(job.Status))
                ModelState.AddModelError("Status", "Please select a valid work status.");

            Service? service = null;
            if (job.ServiceId <= 0)
            {
                ModelState.AddModelError("ServiceId", "Please select a service.");
            }
            else
            {
                service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == job.ServiceId);
                if (service == null)
                    ModelState.AddModelError("ServiceId", "Selected service does not exist.");
            }

            if (job.MechanicId <= 0)
            {
                ModelState.AddModelError("MechanicId", "Please select a mechanic.");
            }
            else if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == job.MechanicId))
            {
                ModelState.AddModelError("MechanicId", "Selected mechanic does not exist.");
            }

            return service;
        }

        /// <summary>Validates AmountReceived >= 0 and <= service amount.</summary>
        private async Task<bool> ValidateAmountAsync(ServiceJob job, Service? service)
        {
            if (service == null) return false;

            if (job.AmountReceived < 0)
            {
                ModelState.AddModelError("AmountReceived", "Amount received cannot be negative.");
                return false;
            }

            if (job.AmountReceived > service.ServicePrice)
            {
                ModelState.AddModelError("AmountReceived",
                    $"Amount received cannot exceed the service amount of ₱{service.ServicePrice:N2}.");
                return false;
            }

            return true;
        }

        private async Task PopulateMechanicListAsync(int? selectedId)
        {
            List<Mechanic> mechanics = await _context.Mechanics.AsNoTracking()
                .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicList = mechanics;
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", selectedId);
        }

        private async Task PopulateCreateListsAsync(ServiceJob? job = null)
        {
            List<Service> services = await _context.Services.AsNoTracking()
                .Include(s => s.Mechanic)
                .OrderBy(s => s.ServiceId).ToListAsync();

            ViewBag.ServiceId = new SelectList(
                services.Select(s => new { s.ServiceId, Label = $"{s.ServiceName} — ₱{s.ServicePrice:N2}" }),
                "ServiceId", "Label", job?.ServiceId);

            List<Mechanic> mechanics = await _context.Mechanics.AsNoTracking()
                .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", job?.MechanicId);

            ViewBag.StatusOptions = new SelectList(
                ServiceJob.AllStatuses.Select(s => new { Value = s, Text = s }),
                "Value", "Text", job?.Status);
        }
    }
}
