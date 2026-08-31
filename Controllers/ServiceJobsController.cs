using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
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
                        j.Service!.ServiceName.Contains(term));
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
                var model = new ServiceJob();
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
                public async Task<IActionResult> Create([Bind("ServiceId,MechanicId,CustomerName,Description,AmountReceived,SubmissionToken")] ServiceJob job)
                {
                    var redirect = RedirectIfNotAuthenticated();
                    if (redirect != null)
                        return redirect;

                    // Validate user-submitted fields only. Server-generated fields have
                                        // Required attributes on the model, which would cause ModelState
                                        // to be invalid before we assign them. Remove those entries so the
                                        // validation step focuses on the fields the client actually posts.
Service? service = await ValidateNewServiceJobAsync(job);
                                        ModelState.Remove(nameof(ServiceJob.ServiceJobNumber));
                                        ModelState.Remove(nameof(ServiceJob.Status));
                                        ModelState.Remove(nameof(ServiceJob.PaymentStatus));

                                        bool paymentValid = ModelState.IsValid ? await ValidateAmountAsync(job, service) : false;
                                        if (paymentValid && service != null)
                                            job.PaymentStatus = ComputePaymentStatus(job.AmountReceived, service.ServicePrice);

                                        // Duplicate submission protection: token must be unique.
                                        if (!string.IsNullOrWhiteSpace(job.SubmissionToken))
                                        {
                                            bool tokenExists = await _context.ServiceJobs.AnyAsync(j => j.SubmissionToken == job.SubmissionToken);
                                            if (tokenExists)
                                            {
                                                // Find existing job and redirect to its details.
                                                var existing = await _context.ServiceJobs.FirstOrDefaultAsync(j => j.SubmissionToken == job.SubmissionToken);
                                                if (existing != null)
return RedirectToAction(nameof(Details), new { id = existing.ServiceJobId });
                                            }
                                        }

                    if (ModelState.IsValid && service != null)
                    {
                        try
                        {
                            job.Status = ServiceJob.StatusStillWorking;
                            job.ServiceDate = DateTime.Now;
                            job.CreatedAt = DateTime.Now;

                            // Compute change amount server‑side.
                            job.ChangeAmount = Math.Max(0m, job.AmountReceived - service.ServicePrice);

await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                             job.ServiceJobNumber = await GenerateServiceJobNumberAsync();
                             job.ProcessedByStaffId = GetCurrentStaffId();
                             // Ensure token is set; generate if missing.
                             if (string.IsNullOrWhiteSpace(job.SubmissionToken))
                                 job.SubmissionToken = Guid.NewGuid().ToString();
// Reload mechanic and verify availability inside transaction.
                              var mechanic = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == job.MechanicId);
                              if (mechanic == null)
                              {
                                  ModelState.AddModelError("MechanicId", "The selected mechanic is no longer available. Please choose another mechanic.");
                                  await tx.RollbackAsync();
                                  // Re-populate lists and return view with errors.
                                  await PopulateCreateListsAsync(job);
                                  return View(job);
                              }
                              if (mechanic.Status != "Active" || mechanic.WorkStatus != "Available")
                              {
                                  ModelState.AddModelError("MechanicId", "The selected mechanic is no longer available. Please choose another mechanic.");
                                  await tx.RollbackAsync();
                                  await PopulateCreateListsAsync(job);
                                  return View(job);
                              }
                              // Assign mechanic to Working.
mechanic.WorkStatus = "Working";
                               _context.ServiceJobs.Add(job);
                               // Add audit log before persisting changes
                               _context.ActivityLogs.Add(new ActivityLog
                               {
                                   Action = "Create Service Job",
                                   Module = "Service",
                                   Description = $"Created service job {job.ServiceJobNumber} for {job.CustomerName}",
                                   StaffId = GetCurrentStaffId(),
                                   Timestamp = DateTime.Now
                               });
                               await _context.SaveChangesAsync();
                               await tx.CommitAsync();

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
        public async Task<IActionResult> Edit(int id, [Bind("ServiceJobId,ServiceId,MechanicId,CustomerName,Description,AmountReceived")] ServiceJob job)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != job.ServiceJobId) return NotFound();

            Service? service = await ValidateServiceJobEditAsync(job);
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






                    if (!ModelState.IsValid)
                    {
                        // Re-populate lists and return view with errors
                        await PopulateCreateListsAsync(job);
                        return View(job);
                    }

// Begin transaction to update job and mechanic statuses atomically
await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

// Reload the ServiceJob inside the transaction
var existingJob = await _context.ServiceJobs.FirstOrDefaultAsync(j => j.ServiceJobId == id);
if (existingJob == null) return NotFound();

// ---------- Finished job protection ----------
if (existingJob.Status == ServiceJob.StatusFinished)
{
    // Disallow changes to key fields (service, mechanic, customer, amount, description).
    if (job.ServiceId != existingJob.ServiceId ||
        job.MechanicId != existingJob.MechanicId ||
        job.CustomerName != existingJob.CustomerName ||
        job.AmountReceived != existingJob.AmountReceived ||
        job.Description != existingJob.Description)
    {
        ModelState.AddModelError(string.Empty, "Completed service jobs cannot change service, mechanic, customer, or payment information.");
        await tx.RollbackAsync();
        await PopulateCreateListsAsync(job);
        return View(job);
    }
    // No modifications needed; commit transaction.
    await tx.CommitAsync();
    TempData["SuccessMessage"] = $"Service job {existingJob.ServiceJobNumber} updated successfully.";
    return RedirectToAction(nameof(Details), new { id = existingJob.ServiceJobId });
}

// Preserve original amount for payment audit
decimal originalAmount = existingJob.AmountReceived;
int oldMechanicId = existingJob.MechanicId;
bool mechanicChanged = job.MechanicId != oldMechanicId;

// Update mutable fields
existingJob.ServiceId = job.ServiceId;
existingJob.MechanicId = job.MechanicId;
existingJob.CustomerName = job.CustomerName;
existingJob.Description = job.Description;
existingJob.AmountReceived = job.AmountReceived;
if (existingJob.Service != null)
    existingJob.ChangeAmount = Math.Max(0m, existingJob.AmountReceived - existingJob.Service.ServicePrice);
existingJob.PaymentStatus = ComputePaymentStatus(existingJob.AmountReceived, existingJob.Service?.ServicePrice ?? 0m);

// ---------- Reassignment handling for active jobs ----------
if (mechanicChanged && existingJob.Status == ServiceJob.StatusStillWorking)
{
    var oldMech = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == oldMechanicId);
    var newMech = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == job.MechanicId);

    if (newMech == null || newMech.Status != "Active" || newMech.WorkStatus != "Available")
    {
        ModelState.AddModelError("MechanicId", "The selected mechanic is no longer available. Please choose another mechanic.");
        await tx.RollbackAsync();
        await PopulateCreateListsAsync(job);
        return View(job);
    }

    if (oldMech != null)
        oldMech.WorkStatus = oldMech.Status == "Active" ? "Available" : "Unavailable";
    newMech.WorkStatus = "Working";
}

// ---------- Audit logging before commit ----------
if (originalAmount != existingJob.AmountReceived)
{
    _context.ActivityLogs.Add(new ActivityLog
    {
        Action = "Record Payment",
        Module = "Service",
        Description = $"{existingJob.ServiceJobNumber}: received ₱{existingJob.AmountReceived:N2} of ₱{service.ServicePrice:N2} ({existingJob.PaymentStatus})",
        StaffId = GetCurrentStaffId(),
        Timestamp = DateTime.Now
    });
}
_context.ActivityLogs.Add(new ActivityLog
{
    Action = "Edit Service Job",
    Module = "Service",
    Description = $"Edited service job {existingJob.ServiceJobNumber}",
    StaffId = GetCurrentStaffId(),
    Timestamp = DateTime.Now
});

// Persist all changes atomically
await _context.SaveChangesAsync();
await tx.CommitAsync();




                    TempData["SuccessMessage"] = $"Service job {existingJob.ServiceJobNumber} updated successfully.";
return RedirectToAction(nameof(Details), new { id = existingJob.ServiceJobId });
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
            // status (Unpaid or Paid only), and the running job total must never
            // exceed the service price.
            decimal servicePrice = job.Service?.ServicePrice ?? 0m;
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
                        if (job.Service != null)
                            job.ChangeAmount = Math.Max(0m, job.AmountReceived - job.Service.ServicePrice);
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
        // The service price always comes from the Service table, never from the browser.
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

            // Begin a serializable transaction to ensure atomicity and recheck state.
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            // Reload the ServiceJob inside the transaction to get the latest state.
            var job = await _context.ServiceJobs
                .Include(j => j.Service)
                .Include(j => j.Histories)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);
            if (job == null) return NotFound();

            if (job.Status != ServiceJob.StatusStillWorking)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "This service job has already been completed.";
                return Back();
            }

            // Ensure full payment.
            decimal servicePrice = job.Service?.ServicePrice ?? 0m;
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

                // Update mechanic work status based on employment status
                if (job.Mechanic != null)
                {
                    job.Mechanic.WorkStatus = job.Mechanic.Status == "Active" ? "Available" : "Unavailable";
                }
                else if (job.MechanicId != 0)
                {
                    var mech = await _context.Mechanics.FirstOrDefaultAsync(m => m.MechanicId == job.MechanicId);
                    if (mech != null)
                        mech.WorkStatus = mech.Status == "Active" ? "Available" : "Unavailable";
                }

            // ---- Automatic ServiceHistory (single record) ----
                        // Determine the description that represents the completion entry.
                        var completionDescription = string.IsNullOrWhiteSpace(job.Description) ? job.Service?.ServiceName ?? "Service" : job.Description;
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
                .Include(j => j.ProcessedByStaff)
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.ServiceJobId == id);

        // Helper to generate PDF – compact 80mm thermal-style service receipt,
        // mirroring the existing CPO/Sales receipt structure.
        private static byte[] GenerateServiceReceiptPdfBytes(ServiceJob job)
        {
            var ph = System.Globalization.CultureInfo.GetCultureInfo("en-PH");
            decimal total = job.Service?.ServicePrice ?? 0m;
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
                                MetaRow("Service", job.Service?.ServiceName ?? "");
                                MetaRow("Mechanic", job.Mechanic?.MechanicName ?? "");
                                MetaRow("Customer", customer);
                                MetaRow("Date", job.ServiceDate.ToString("MMM dd, yyyy"));
                                MetaRow("Completed", job.CompletedDate?.ToString("MMM dd, yyyy") ?? "—");
                                MetaRow("Created By", job.ProcessedByStaff?.StaffName ?? "—");
                            });

                            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#999999");

                            // ── Work performed (history lines when present) ──
                            List<ServiceHistory> histories = job.Histories?
                                .OrderBy(h => h.WorkDate).ThenBy(h => h.ServiceHistoryId).ToList()
                                ?? new List<ServiceHistory>();

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

                                if (histories.Any())
                                {
                                    foreach (ServiceHistory h in histories)
                                    {
                                        table.Cell().PaddingVertical(1.5f).Text(
                                            $"{h.WorkDate:ddd, MMM dd}: {h.Description}");
                                        table.Cell().PaddingVertical(1.5f).AlignCenter().Text("1");
                                        table.Cell().PaddingVertical(1.5f).AlignRight().Text(h.AmountReceived.ToString("C", ph));
                                    }
                                }
                                else
                                {
                                    table.Cell().PaddingVertical(1.5f).Text(job.Service?.ServiceName ?? "");
                                    table.Cell().PaddingVertical(1.5f).AlignCenter().Text("1");
                                    table.Cell().PaddingVertical(1.5f).AlignRight().Text(total.ToString("C", ph));
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

        /// <summary>Shared validations: service/mechanic exist and customer required. Job status is managed server-side (default Still Working, finished only via Mark Done).</summary>
        private async Task<Service?> ValidateNewServiceJobAsync(ServiceJob job)
        {
            if (string.IsNullOrWhiteSpace(job.CustomerName))
                ModelState.AddModelError("CustomerName", "Customer name is required.");
            else if (job.CustomerName.Length > 150)
                ModelState.AddModelError("CustomerName", "Customer name must be 150 characters or fewer.");

            if (job.Description != null && job.Description.Length > 500)
                ModelState.AddModelError("Description", "Description must be 500 characters or fewer.");

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
else if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == job.MechanicId && m.Status == "Active" && m.WorkStatus == "Available"))
                     {
                         ModelState.AddModelError("MechanicId", "Selected mechanic is not available for a new service job.");
                     }

            return service;
        }

        /// <summary>Validations for editing an existing ServiceJob. Does not enforce mechanic Availability unless changed.</summary>
        private async Task<Service?> ValidateServiceJobEditAsync(ServiceJob job)
        {
            // Customer name validation
            if (string.IsNullOrWhiteSpace(job.CustomerName))
                ModelState.AddModelError("CustomerName", "Customer name is required.");
            else if (job.CustomerName.Length > 150)
                ModelState.AddModelError("CustomerName", "Customer name must be 150 characters or fewer.");

            // Description length
            if (job.Description != null && job.Description.Length > 500)
                ModelState.AddModelError("Description", "Description must be 500 characters or fewer.");

            // Service validation
            Service? service = null;
            if (job.ServiceId <= 0)
                ModelState.AddModelError("ServiceId", "Please select a service.");
            else
            {
                service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == job.ServiceId);
                if (service == null)
                    ModelState.AddModelError("ServiceId", "Selected service does not exist.");
            }

            // Mechanic existence (no availability check)
            if (job.MechanicId <= 0)
                ModelState.AddModelError("MechanicId", "Please select a mechanic.");
            else if (!await _context.Mechanics.AnyAsync(m => m.MechanicId == job.MechanicId))
                ModelState.AddModelError("MechanicId", "Selected mechanic does not exist.");

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

            // Overpayment is allowed – the excess will be shown as change on the receipt.

            return true;
        }

        private async Task PopulateMechanicListAsync(int? selectedId)
        {
List<Mechanic> mechanics = await _context.Mechanics.AsNoTracking()
                 .Where(m => m.Status == "Active" && m.WorkStatus == "Available")
                 .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicList = mechanics;
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", selectedId);
        }

        private async Task PopulateCreateListsAsync(ServiceJob? job = null)
        {
            List<Service> services = await _context.Services.AsNoTracking()
                .OrderBy(s => s.ServiceId).ToListAsync();

            ViewBag.ServicesList = services;

            ViewBag.ServiceId = new SelectList(
                services.Select(s => new { s.ServiceId, Label = $"{s.ServiceName} — ₱{s.ServicePrice:N2}" }),
                "ServiceId", "Label", job?.ServiceId);

List<Mechanic> mechanics = await _context.Mechanics.AsNoTracking()
                 .Where(m => m.Status == "Active" && m.WorkStatus == "Available")
                 .OrderBy(m => m.MechanicName).ToListAsync();
            ViewBag.MechanicId = new SelectList(mechanics, "MechanicId", "MechanicName", job?.MechanicId);
        }
    }
}
