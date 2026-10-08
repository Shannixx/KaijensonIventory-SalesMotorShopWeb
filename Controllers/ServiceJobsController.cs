using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
            int? serviceId, int page = 1)
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
                        j.ServiceNameSnapshot.Contains(term));
                }

                if (mechanicId.HasValue && mechanicId.Value > 0)
                    query = query.Where(j => j.MechanicId == mechanicId.Value);


                int total = await query.CountAsync();

                List<ServiceJob> jobs = await query
                    .OrderBy(j => j.ServiceJobId) // SV-001, SV-002, ... ascending
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                await PopulateMechanicListAsync(mechanicId);

                ViewBag.ServiceJobsCount = total;
                ViewData["CurrentFilter"] = searchString;

                ViewData["ServiceId"] = serviceId;
                ViewData["Page"] = page;
                ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)pageSize);

                if (serviceId.HasValue && serviceId.Value > 0)
                {
                    Service? service = await _context.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ServiceId == serviceId.Value && !s.IsDeleted);
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
                    .Include(j => j.AddOns)
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
                var model = new ServiceJobFormViewModel();
                // Generate a unique token for duplicate submission protection.
                model.SubmissionToken = Guid.NewGuid().ToString();
                return View(model);
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
        public async Task<IActionResult> Create([Bind("ServiceId,MechanicId,CustomerName,AmountReceived,SubmissionToken")] ServiceJobFormViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;

            // Create accepts only base-service details, even if a client posts removed fields.
            model.SelectedAddOnIds = new List<int>();
            model.Description = null;
            ValidateJobInput(model);
            if (ModelState.IsValid)
            {
                try
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    await ReserveMechanicsAsync(model.MechanicId);
                    // Check the token inside the same transaction as number generation and saving.
                    if (!string.IsNullOrWhiteSpace(model.SubmissionToken))
                    {
                        var duplicate = await _context.ServiceJobs.AsNoTracking()
                            .FirstOrDefaultAsync(j => j.SubmissionToken == model.SubmissionToken);
                        if (duplicate != null)
                            return RedirectToAction(nameof(Details), new { id = duplicate.ServiceJobId });
                    }

                    var pricing = await LoadJobPricingAsync(model);
                    var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == model.MechanicId && !m.IsDeleted);
                    if (mechanic == null || mechanic.Status != "Active")
                        ModelState.AddModelError(nameof(model.MechanicId), "Select an active, non-archived mechanic.");

                    if (ModelState.IsValid && pricing != null && mechanic != null)
                    {
                        bool hasWorkingJob = await HasWorkingJobAsync(mechanic.MechanicId);
                        var job = new ServiceJob
                        {
                            ServiceJobNumber = await GenerateServiceJobNumberAsync(),
                            ServiceId = model.ServiceId,
                            ServiceNameSnapshot = pricing.ServiceName,
                            BasePriceSnapshot = pricing.BasePrice,
                            TotalPrice = pricing.BasePrice,
                            AddOns = new List<ServiceJobAddOn>(),
                            MechanicId = model.MechanicId,
                            CustomerName = model.CustomerName,
                            Description = null,
                            AmountReceived = model.AmountReceived,
                            ChangeAmount = Math.Max(0m, model.AmountReceived - pricing.BasePrice),
                            PaymentStatus = ComputePaymentStatus(model.AmountReceived, pricing.BasePrice),
                            Status = hasWorkingJob ? ServiceJob.StatusPending : ServiceJob.StatusStillWorking,
                            ServiceDate = DateTime.Now,
                            CreatedAt = DateTime.Now,
                            ProcessedByStaffId = GetCurrentStaffId(),
                            SubmissionToken = string.IsNullOrWhiteSpace(model.SubmissionToken) ? Guid.NewGuid().ToString() : model.SubmissionToken
                        };
                        // The job table is authoritative even if the cached WorkStatus was stale.
                        mechanic.WorkStatus = "Working";
                        _context.ServiceJobs.Add(job);
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Create Service Job", Module = "Service",
                            Description = $"Created service job {job.ServiceJobNumber} for {job.CustomerName}",
                            StaffId = GetCurrentStaffId(), Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                        TempData["SuccessMessage"] = $"Service job {job.ServiceJobNumber} created successfully.";
                        return RedirectToAction(nameof(Details), new { id = job.ServiceJobId });
                    }
                    await tx.RollbackAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while creating service job.");
                    ModelState.AddModelError(string.Empty, "An error occurred while creating the service job. Please try again.");
                }
            }
            await PopulateCreateListsAsync(model);
            return View(model);
        }

        // GET: /ServiceJobs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id == null) return NotFound();
            try
            {
                var job = await _context.ServiceJobs.Include(j => j.AddOns).AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);
                if (job == null) return NotFound();
                var model = new ServiceJobFormViewModel
                {
                    ServiceJobId = job.ServiceJobId, ServiceId = job.ServiceId,
                    MechanicId = job.MechanicId, CustomerName = job.CustomerName,
                    Description = job.Description, AmountReceived = job.AmountReceived,
                    SelectedAddOnIds = job.AddOns.Select(a => a.AddOnServiceId).ToList()
                };
                await PopulateCreateListsAsync(model, job);
                return View(model);
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
        public async Task<IActionResult> Edit(int id, ServiceJobFormViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;
            if (id != model.ServiceJobId) return NotFound();
            ValidateJobInput(model);
            ServiceJob? existingJob = null;
            try
            {
                // Read the old assignment before the transaction so every queue writer
                // can reserve mechanics in the same order before reading job rows.
                int? assignedMechanicId = await _context.ServiceJobs.AsNoTracking()
                    .Where(j => j.ServiceJobId == id)
                    .Select(j => (int?)j.MechanicId).FirstOrDefaultAsync();
                if (!assignedMechanicId.HasValue) return NotFound();
                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                await ReserveMechanicsAsync(assignedMechanicId.Value, model.MechanicId);
                existingJob = await _context.ServiceJobs.Include(j => j.AddOns)
                    .FirstOrDefaultAsync(j => j.ServiceJobId == id);
                if (existingJob == null) return NotFound();
                if (existingJob.MechanicId != assignedMechanicId.Value)
                {
                    await tx.RollbackAsync();
                    TempData["ErrorMessage"] = "The mechanic assignment changed. Reload the job and try again.";
                    return RedirectToAction(nameof(Edit), new { id });
                }

                if (existingJob.Status == ServiceJob.StatusFinished)
                {
                    if (model.ServiceId != existingJob.ServiceId || model.MechanicId != existingJob.MechanicId ||
                        model.CustomerName != existingJob.CustomerName || model.Description != existingJob.Description ||
                        model.AmountReceived != existingJob.AmountReceived ||
                        !model.SelectedAddOnIds.ToHashSet().SetEquals(existingJob.AddOns.Select(a => a.AddOnServiceId)))
                        ModelState.AddModelError(string.Empty, "Completed service jobs cannot change service, add-ons, mechanic, customer, or payment information.");
                    if (ModelState.IsValid)
                    {
                        await tx.CommitAsync();
                        TempData["SuccessMessage"] = $"Service job {existingJob.ServiceJobNumber} updated successfully.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }
                else if (ModelState.IsValid)
                {
                    var pricing = await LoadJobPricingAsync(model, existingJob);
                    decimal historyTotal = await _context.ServiceHistories.Where(h => h.ServiceJobId == id)
                        .SumAsync(h => (decimal?)h.AmountReceived) ?? 0m;
                    if (model.AmountReceived < historyTotal)
                        ModelState.AddModelError(nameof(model.AmountReceived), "Amount received cannot be less than the total recorded payments in service history.");

                    bool mechanicChanged = model.MechanicId != existingJob.MechanicId;
                    var newMechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == model.MechanicId && !m.IsDeleted);
                    if (newMechanic == null || (mechanicChanged && newMechanic.Status != "Active"))
                        ModelState.AddModelError(nameof(model.MechanicId), "Select an active, non-archived mechanic.");

                    if (ModelState.IsValid && pricing != null && newMechanic != null)
                    {
                        decimal originalAmount = existingJob.AmountReceived;
                        if (mechanicChanged)
                        {
                            var oldMechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == existingJob.MechanicId);
                            existingJob.Status = await HasWorkingJobAsync(newMechanic.MechanicId, existingJob.ServiceJobId)
                                ? ServiceJob.StatusPending : ServiceJob.StatusStillWorking;
                            newMechanic.WorkStatus = "Working";
                            if (oldMechanic != null)
                                await RecalculateMechanicWorkStatusAsync(oldMechanic, existingJob.ServiceJobId);
                        }
                        existingJob.ServiceId = model.ServiceId;
                        existingJob.ServiceNameSnapshot = pricing.ServiceName;
                        existingJob.BasePriceSnapshot = pricing.BasePrice;
                        existingJob.TotalPrice = pricing.TotalPrice;
                        // Keep retained rows and their agreed snapshots; delete only removed selections.
                        foreach (var removed in existingJob.AddOns.Where(a => !model.SelectedAddOnIds.Contains(a.AddOnServiceId)).ToList())
                        {
                            existingJob.AddOns.Remove(removed);
                            _context.ServiceJobAddOns.Remove(removed);
                        }
                        foreach (var added in pricing.AddOns.Where(a => !existingJob.AddOns.Any(old => old.AddOnServiceId == a.AddOnServiceId)))
                            existingJob.AddOns.Add(added);

                        existingJob.MechanicId = model.MechanicId;
                        existingJob.CustomerName = model.CustomerName;
                        existingJob.Description = model.Description;
                        existingJob.AmountReceived = model.AmountReceived;
                        existingJob.ChangeAmount = Math.Max(0m, model.AmountReceived - existingJob.TotalPrice);
                        existingJob.PaymentStatus = ComputePaymentStatus(model.AmountReceived, existingJob.TotalPrice);
                        if (originalAmount != existingJob.AmountReceived)
                            _context.ActivityLogs.Add(new ActivityLog
                            {
                                Action = "Record Payment", Module = "Service",
                                Description = $"{existingJob.ServiceJobNumber}: received ₱{existingJob.AmountReceived:N2} of ₱{existingJob.TotalPrice:N2} ({existingJob.PaymentStatus})",
                                StaffId = GetCurrentStaffId(), Timestamp = DateTime.Now
                            });
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = "Edit Service Job", Module = "Service",
                            Description = $"Edited service job {existingJob.ServiceJobNumber}",
                            StaffId = GetCurrentStaffId(), Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                        TempData["SuccessMessage"] = $"Service job {existingJob.ServiceJobNumber} updated successfully.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }
                await tx.RollbackAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict while updating service job. ServiceJobId: {ServiceJobId}", id);
                if (!await _context.ServiceJobs.AnyAsync(j => j.ServiceJobId == id)) return NotFound();
                ModelState.AddModelError(string.Empty, "The service job was modified by another user. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating service job. ServiceJobId: {ServiceJobId}", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the service job. Please try again.");
            }
            // Reload saved snapshots after a failed transaction rather than redisplaying modified tracked values.
            var savedJob = await _context.ServiceJobs.AsNoTracking().Include(j => j.AddOns)
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);
            await PopulateCreateListsAsync(model, savedJob);
            return View(model);
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
        public async Task<IActionResult> AddHistory(int id, [Bind("WorkDate,Description,AmountReceived")] ServiceHistory history)
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
            // A history entry records a payment, not another service charge.
            history.PaymentStatus = history.AmountReceived > 0 ? ServiceJob.PaymentPaid : ServiceJob.PaymentUnpaid;
            ModelState.Remove(nameof(history.PaymentStatus));

            if (string.IsNullOrWhiteSpace(history.Description))
                ModelState.AddModelError("Description", "Work description is required.");
            if (history.AmountReceived < 0)
                ModelState.AddModelError("AmountReceived", "Amount cannot be negative.");
            if (!IsValidPaymentStatus(history.PaymentStatus))
                ModelState.AddModelError("PaymentStatus", "Please select a valid payment status.");

            // Payment consistency: the recorded amount must agree with the declared
            // status (Unpaid or Paid only), and the running job total must never
            // exceed the service price.
            decimal servicePrice = job.TotalPrice;
            if (ModelState.IsValid && IsValidPaymentStatus(history.PaymentStatus))
            {
                switch (history.PaymentStatus)
                {
                    case ServiceJob.PaymentUnpaid when history.AmountReceived != 0:
                        ModelState.AddModelError("AmountReceived",
                            "An unpaid work entry must record an amount of ₱0.00.");
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
                        // Recalculate change amount based on service price.
                        job.ChangeAmount = Math.Max(0m, job.AmountReceived - job.TotalPrice);
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

        // ------------------------------------------------------------------
        // Mark Done (payment confirmation workflow)
        // ------------------------------------------------------------------

        // POST: /ServiceJobs/MarkDone/5
        // Finishes a "Still Working" job after recording the customer's payment.
        // The agreed total always comes from the saved job, never from the browser or live catalog.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDone(int id, string? returnUrl = null)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null) return redirect;



            IActionResult Back() =>
                !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction(nameof(Details), new { id });

            int? assignedMechanicId = await _context.ServiceJobs.AsNoTracking()
                .Where(j => j.ServiceJobId == id)
                .Select(j => (int?)j.MechanicId).FirstOrDefaultAsync();
            if (!assignedMechanicId.HasValue) return NotFound();
            // Reserve this mechanic before reading job rows in the transaction.
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await ReserveMechanicsAsync(assignedMechanicId.Value);

            // Reload the ServiceJob inside the transaction to get the latest state.
            var job = await _context.ServiceJobs
                .Include(j => j.Service)
                .Include(j => j.Histories)
                    .Include(j => j.AddOns)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);
            if (job == null) return NotFound();
            if (job.MechanicId != assignedMechanicId.Value)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "The mechanic assignment changed. Reload the job and try again.";
                return Back();
            }

            if (job.Status != ServiceJob.StatusStillWorking)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = job.Status == ServiceJob.StatusPending
                    ? "This service job is Pending and cannot be marked Done until it starts."
                    : "This service job has already been completed.";
                return Back();
            }

            // Ensure full payment.
            decimal servicePrice = job.TotalPrice;
            decimal totalAfterPayment = job.AmountReceived;
            if (totalAfterPayment < servicePrice)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "Full payment is required before completing the service.";
                return Back();
            }



            // Update payment fields.
            job.ChangeAmount = Math.Max(0m, totalAfterPayment - servicePrice);
            job.PaymentStatus = ComputePaymentStatus(job.AmountReceived, servicePrice);
            job.Status = ServiceJob.StatusFinished;
            job.CompletedDate ??= DateTime.Now;

            // Exclude this still-persisted working row while finding the next turn:
            // its Finished status is not written until SaveChanges below.
            var mechanic = job.Mechanic ?? await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == job.MechanicId);
            if (mechanic != null)
                await RecalculateMechanicWorkStatusAsync(mechanic, job.ServiceJobId);

            // ---- Automatic ServiceHistory (single record) ----
                        // Determine the description that represents the completion entry.
                        var completionDescription = string.IsNullOrWhiteSpace(job.Description) ? job.ServiceNameSnapshot : job.Description;
                        // Check if a completion history already exists (by description and paid status).
                        bool hasCompletionHistory = job.Histories.Any(h =>
                            h.Description == completionDescription &&
                            h.PaymentStatus == ServiceJob.PaymentPaid);
                        if (!hasCompletionHistory)
                        {
                            var history = new ServiceHistory
                            {
                                ServiceJobId = job.ServiceJobId,
                                WorkDate = job.CompletedDate ?? DateTime.Now,
                                Description = completionDescription,
                                AmountReceived = job.AmountReceived,
                                PaymentStatus = ServiceJob.PaymentPaid
                            };
                            _context.ServiceHistories.Add(history);
                        }

            // Removed automatic SalesTransaction creation for Service Jobs as per requirement.
            // No SalesTransaction is created here.

            // Activity logs
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Change Service Status",
                Module = "Service",
                Description = $"{job.ServiceJobNumber}: Still Working -> {job.Status}",
                StaffId = GetCurrentStaffId(),
                Timestamp = DateTime.Now
            });
            // No separate payment record needed since payment is pre‑recorded.
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = "Create Service History",
                Module = "Service",
                Description = $"{job.ServiceJobNumber}: auto\u2011created work entry",
                StaffId = GetCurrentStaffId(),
                Timestamp = DateTime.Now
            });
            // Removed Service Sale activity logging as per requirement – no SalesTransaction is created for Service Jobs.
            // if (job.SalesTransactionId != null)
            // {
            //     _context.ActivityLogs.Add(new ActivityLog
            //     {
            //         Action = "Create Service Sale",
            //         Module = "Sales",
            //         Description = $"Service job {job.ServiceJobNumber} recorded in sale #{job.SalesTransactionId}",
            //         StaffId = GetCurrentStaffId(),
            //         Timestamp = DateTime.Now
            //     });
            // }

            // Persist all changes atomically.
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["SuccessMessage"] = $"{job.ServiceJobNumber} marked as finished. Payment status: {job.PaymentStatus}.";
            return Back();
        }


        // ------------------------------------------------------------------
        // Service receipt
        // ------------------------------------------------------------------

        // GET: /ServiceJobs/PrintPreviewHtml/5
        // HTML fragment rendered inside the shared receipt preview modal.
        public async Task<IActionResult> PrintPreviewHtml(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            ServiceJob? job = await LoadReceiptJobAsync(id);
            if (job == null) return NotFound();

            return PartialView("_ServiceReceiptPreview", job);
        }

        // GET: /ServiceJobs/ReceiptPdf/5
        // QuestPDF service receipt following the existing sales receipt layout.
        public async Task<IActionResult> ReceiptPdf(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            ServiceJob? job = await LoadReceiptJobAsync(id);
            if (job == null) return NotFound();

            try
            {
                byte[] pdfBytes = GenerateServiceReceiptPdfBytes(job);

                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = "Generate Service Receipt",
                    Module = "Service",
                    Description = $"{job.ServiceJobNumber}: generated service receipt ({job.PaymentStatus})",
                    StaffId = GetCurrentStaffId(),
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                return File(pdfBytes, "application/pdf", $"ServiceReceipt-{job.ServiceJobNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service receipt generation failed. ServiceJobId: {ServiceJobId}", id);
                TempData["ErrorMessage"] = "Service receipt could not be generated. You can view the details page and retry.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private async Task<ServiceJob?> LoadReceiptJobAsync(int id) =>
            await _context.ServiceJobs
                .Include(j => j.Service)
                .Include(j => j.Mechanic)
                .Include(j => j.Histories)
                    .Include(j => j.AddOns)
                .Include(j => j.ProcessedByStaff)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);

        // Helper to generate PDF – compact 80mm thermal-style service receipt,
        // mirroring the existing CPO/Sales receipt structure.
        private static byte[] GenerateServiceReceiptPdfBytes(ServiceJob job)
        {
            var ph = System.Globalization.CultureInfo.GetCultureInfo("en-PH");
            decimal total = job.TotalPrice;
            decimal paid = job.AmountReceived;
            decimal change = job.ChangeAmount;
            string customer = string.IsNullOrWhiteSpace(job.CustomerName) ? "Walk-in" : job.CustomerName;

            var doc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.ContinuousSize(227f);   // ≈ 80mm in points, like the sales receipt paper
                    page.Margin(11f);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor("#111111"));

                    page.Content().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Spacing(2);

                            // ── Header: centered business identity ──
                            col.Item().AlignCenter().Text("KAIJENSON MOTOR SHOP").FontSize(12.5f).Bold();
                            col.Item().AlignCenter().Text("Service Receipt").FontSize(9);
                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#111111");

                            // ── Job details ──
                            col.Item().Column(meta =>
                            {
                                meta.Spacing(1);

                                void MetaRow(string label, string value)
                                {
                                    meta.Item().Row(r =>
                                    {
                                        r.ConstantItem(52).Text(label);
                                        r.RelativeItem().AlignRight().Text(value);
                                    });
                                }

                                MetaRow("Service ID", job.ServiceJobNumber);
                                MetaRow("Service", job.ServiceNameSnapshot);
                                MetaRow("Mechanic", job.Mechanic?.MechanicName ?? "");
                                MetaRow("Customer", customer);
                                MetaRow("Date", job.ServiceDate.ToString("MMM dd, yyyy"));
                                MetaRow("Completed", job.CompletedDate?.ToString("MMM dd, yyyy") ?? "—");
                                MetaRow("Created By", job.ProcessedByStaff?.StaffName ?? "—");
                            });

                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#999999");

                            // Agreed charges are base plus add-ons, never history payment entries.

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(6);   // work
                                    columns.ConstantColumn(20);  // qty
                                    columns.RelativeColumn(4);   // amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).BorderColor("#111111").Text("Work").FontSize(7).Bold();
                                    header.Cell().BorderBottom(1).BorderColor("#111111").AlignCenter().Text("Qty").FontSize(7).Bold();
                                    header.Cell().BorderBottom(1).BorderColor("#111111").AlignRight().Text("Amount").FontSize(7).Bold();
                                });

                                table.Cell().PaddingVertical(1.5f).Text("Base Service: " + job.ServiceNameSnapshot);
                                table.Cell().PaddingVertical(1.5f).AlignCenter().Text("1");
                                table.Cell().PaddingVertical(1.5f).AlignRight().Text(job.BasePriceSnapshot.ToString("C", ph));
                                foreach (var addOn in job.AddOns.OrderBy(a => a.AddOnServiceId))
                                {
                                    table.Cell().PaddingVertical(1.5f).Text("Add-on: " + addOn.AddOnNameSnapshot);
                                    table.Cell().PaddingVertical(1.5f).AlignCenter().Text("1");
                                    table.Cell().PaddingVertical(1.5f).AlignRight().Text(addOn.PriceSnapshot.ToString("C", ph));
                                }
                            });

                            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor("#111111");

                            // ── Payment totals ──
                            col.Item().Column(tot =>
                            {
                                tot.Spacing(1);

                                void TotalRow(string label, string value)
                                {
                                    tot.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(label).FontSize(8);
                                        r.ConstantItem(60).AlignRight().Text(value).FontSize(8);
                                    });
                                }

                                TotalRow("TOTAL", total.ToString("C", ph));
                                tot.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("AMOUNT PAID").FontSize(10).Bold();
                                    r.ConstantItem(60).AlignRight().Text(paid.ToString("C", ph)).FontSize(10).Bold().FontColor("#E8650A");
                                });
                                decimal remaining = Math.Max(0m, total - paid);
                                TotalRow("REMAINING", remaining.ToString("C", ph));
                                TotalRow("CHANGE", change.ToString("C", ph));

                                tot.Item().PaddingTop(2).Row(r =>
                                {
                                    r.RelativeItem().Text($"Status: {job.PaymentStatus.ToUpperInvariant()}").Bold();
                                });
                            });

                            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#111111");

                            // ── Closing message ──
                            col.Item().PaddingTop(6).AlignCenter().Text("Thank you.").FontSize(8);
                        });
                    });
                });
            });

            using var ms = new System.IO.MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
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

        // All queue writers reserve the same mechanic resource before reading job
        // states. Sorted reservations also serialize transfers between mechanics.
        private async Task ReserveMechanicsAsync(params int[] mechanicIds)
        {
            foreach (int mechanicId in mechanicIds.Where(id => id > 0).Distinct().OrderBy(id => id))
            {
                string resource = $"ServiceJob:Mechanic:{mechanicId}";
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DECLARE @lockResult int;
                    EXEC @lockResult = sp_getapplock @Resource = {resource},
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
                    IF @lockResult < 0 THROW 51006, 'The mechanic is busy. Please retry.', 1;");
            }
        }

        private Task<bool> HasWorkingJobAsync(int mechanicId, int? departingJobId = null) =>
            _context.ServiceJobs.AnyAsync(job =>
                job.MechanicId == mechanicId && job.Status == ServiceJob.StatusStillWorking &&
                (!departingJobId.HasValue || job.ServiceJobId != departingJobId.Value));

        private async Task RecalculateMechanicWorkStatusAsync(Mechanic mechanic, int departingJobId)
        {
            // The departing job still has its old database state until SaveChanges.
            bool hasWorkingJob = await HasWorkingJobAsync(mechanic.MechanicId, departingJobId);
            if (!hasWorkingJob)
            {
                var next = await _context.ServiceJobs
                    .Where(job => job.MechanicId == mechanic.MechanicId &&
                                  job.Status == ServiceJob.StatusPending && job.ServiceJobId != departingJobId)
                    .OrderBy(job => job.ServiceJobId)
                    .FirstOrDefaultAsync();
                if (next != null)
                {
                    next.Status = ServiceJob.StatusStillWorking;
                    hasWorkingJob = true;
                }
            }
            mechanic.WorkStatus = mechanic.Status == "Active"
                ? (hasWorkingJob ? "Working" : "Available") : "Unavailable";
        }

        private static string ComputePaymentStatus(decimal amountReceived, decimal serviceAmount)
        {
            // Two payment states only: Unpaid until the full service price is received.
            if (amountReceived <= 0)
                return ServiceJob.PaymentUnpaid;
            if (amountReceived < serviceAmount)
                return ServiceJob.PaymentUnpaid;
            return ServiceJob.PaymentPaid;
        }

        private static bool IsValidPaymentStatus(string? status) =>
            ServiceJob.AllPaymentStatuses.Contains(status);

        private void ValidateJobInput(ServiceJobFormViewModel model)
        {
            model.SelectedAddOnIds ??= new List<int>();
            if (string.IsNullOrWhiteSpace(model.CustomerName) || model.CustomerName.Length > 150)
                ModelState.AddModelError(nameof(model.CustomerName), "Customer name is required and must be 150 characters or fewer.");
            if (model.Description?.Length > 500)
                ModelState.AddModelError(nameof(model.Description), "Description must be 500 characters or fewer.");
            if (model.AmountReceived < 0 || model.AmountReceived > 999999.99m || decimal.Round(model.AmountReceived, 2) != model.AmountReceived)
                ModelState.AddModelError(nameof(model.AmountReceived), "Amount received must be between 0 and 999999.99, with at most two decimal places.");
            if (model.SubmissionToken?.Length > 64)
                ModelState.AddModelError(nameof(model.SubmissionToken), "Invalid submission token.");
            if (model.SelectedAddOnIds.Count != model.SelectedAddOnIds.Distinct().Count())
                ModelState.AddModelError(nameof(model.SelectedAddOnIds), "Select each add-on only once.");
        }

        private sealed record JobPricing(string ServiceName, decimal BasePrice, List<ServiceJobAddOn> AddOns)
        {
            public decimal TotalPrice => BasePrice + AddOns.Sum(a => a.PriceSnapshot);
        }

        // Called only inside the job's serializable save transaction. Retained selections
        // use their agreement snapshots, even after catalog renames, repricing or archival.
        private async Task<JobPricing?> LoadJobPricingAsync(ServiceJobFormViewModel model, ServiceJob? existing = null)
        {
            string name;
            decimal basePrice;
            if (existing != null && model.ServiceId == existing.ServiceId)
            {
                name = existing.ServiceNameSnapshot;
                basePrice = existing.BasePriceSnapshot;
            }
            else
            {
                var service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == model.ServiceId && !s.IsDeleted && s.Status == "Active" && !s.IsAddOn);
                if (service == null)
                {
                    ModelState.AddModelError(nameof(model.ServiceId), "Select an active, non-archived base service.");
                    return null;
                }
                name = service.ServiceName;
                basePrice = service.ServicePrice;
            }
            var retained = existing?.AddOns.ToDictionary(a => a.AddOnServiceId) ?? new Dictionary<int, ServiceJobAddOn>();
            var newIds = model.SelectedAddOnIds.Where(id => !retained.ContainsKey(id)).ToList();
            var newAddOns = await _context.Services.AsNoTracking()
                .Where(s => newIds.Contains(s.ServiceId) && !s.IsDeleted && s.Status == "Active" && s.IsAddOn && s.ServicePrice > 0)
                .ToDictionaryAsync(s => s.ServiceId);
            if (newAddOns.Count != newIds.Count)
            {
                ModelState.AddModelError(nameof(model.SelectedAddOnIds), "Select only active, non-archived add-ons with a positive price.");
                return null;
            }
            var selections = model.SelectedAddOnIds.Select(id => retained.TryGetValue(id, out var agreed)
                ? new ServiceJobAddOn { AddOnServiceId = id, AddOnNameSnapshot = agreed.AddOnNameSnapshot, PriceSnapshot = agreed.PriceSnapshot }
                : new ServiceJobAddOn { AddOnServiceId = id, AddOnNameSnapshot = newAddOns[id].ServiceName, PriceSnapshot = newAddOns[id].ServicePrice }).ToList();
            return new JobPricing(name, basePrice, selections);
        }

        private async Task PopulateMechanicListAsync(int? selectedId)
        {
            List<Mechanic> mechanics = await _context.Mechanics.AsNoTracking()
                .Where(m => !m.IsDeleted && m.Status == "Active")
                .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicList = mechanics;
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", selectedId);
        }

        private async Task PopulateCreateListsAsync(ServiceJobFormViewModel? model = null, ServiceJob? existing = null)
        {
            var available = await _context.Services.AsNoTracking()
                .Where(s => !s.IsDeleted && s.Status == "Active")
                .OrderBy(s => s.ServiceId).ToListAsync();
            var services = available.Where(s => !s.IsAddOn).ToList();
            var addOns = available.Where(s => s.IsAddOn && s.ServicePrice > 0).ToList();
            if (existing != null)
            {
                // These display-only copies never update catalog records.
                services.RemoveAll(s => s.ServiceId == existing.ServiceId);
                services.Add(new Service { ServiceId = existing.ServiceId, ServiceName = existing.ServiceNameSnapshot, ServicePrice = existing.BasePriceSnapshot });
                foreach (var agreed in existing.AddOns)
                {
                    addOns.RemoveAll(s => s.ServiceId == agreed.AddOnServiceId);
                    addOns.Add(new Service { ServiceId = agreed.AddOnServiceId, ServiceName = agreed.AddOnNameSnapshot, ServicePrice = agreed.PriceSnapshot, IsAddOn = true });
                }
                if (model != null)
                {
                    model.ServiceJobNumber = existing.ServiceJobNumber;
                    model.Status = existing.Status;
                    model.CompletedDate = existing.CompletedDate;
                }
            }
            ViewBag.ServicesList = services.OrderBy(s => s.ServiceId).ToList();
            ViewBag.AddOnsList = addOns.OrderBy(s => s.ServiceId).ToList();
            var mechanics = await _context.Mechanics.AsNoTracking()
                .Where(m => (!m.IsDeleted && m.Status == "Active") ||
                            (existing != null && m.MechanicId == existing.MechanicId))
                .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", model?.MechanicId);
            ViewBag.CreateMechanics = mechanics.Where(m => !m.IsDeleted && m.Status == "Active").ToList();
            var mechanicIds = mechanics.Select(m => m.MechanicId).ToList();
            ViewBag.BusyMechanicIds = (await _context.ServiceJobs.AsNoTracking()
                .Where(j => mechanicIds.Contains(j.MechanicId) && j.Status == ServiceJob.StatusStillWorking)
                .Select(j => j.MechanicId).Distinct().ToListAsync()).ToHashSet();
        }
    }
}
