# Soft Delete and Active/Inactive Staff Implementation Plan

> **For Hermes:** Use this plan as the implementation specification only after explicit user approval. This document was produced in planning mode; no application source, migration, or database was changed.

**Goal:** Replace the requested tab-level hard deletes with recoverable soft deletion and replace Staff approval states with an Admin-controlled Active/Inactive lifecycle without deleting rows, changing IDs, replaying inventory transactions, or weakening access control.

**Architecture:** Keep the existing ASP.NET Core MVC/session architecture and current `Status` fields for their existing business meanings. Add separate soft-delete metadata to the eight requested aggregate models, use explicit active/deleted predicates rather than EF global query filters, and add Admin-only restore actions. Preserve historical reads deliberately, because sales, delivery, PO, serial, and service history must still resolve deleted parent rows.

**Tech Stack:** ASP.NET Core MVC, .NET 8, EF Core 8.0.8, SQL Server, Razor views, session-based authorization, existing `ActivityLogService`.

---

## 1. Requirements and non-negotiable constraints

The primary requirements are the two supplied PDFs:

- `Downloads/change the function of the DELETE.pdf`: Product, Brand, Supplier, Category, Purchase Order, Delivery, Service, and Mechanic records must be soft-deleted and later restorable by an Admin.
- `Downloads/MANAGE STAFF.pdf`: remove Approve/Disapprove; use Active/Inactive Staff status; an inactive account cannot access the account; an Admin can reactivate it later; preserve the original staff record and data.

Implementation must also:

- never use `DbSet.Remove`/`RemoveRange` for the requested business delete flows;
- preserve primary keys, foreign keys, child rows, activity logs, sales history, PO history, delivery history, service history, and serial relationships;
- never reverse, replay, or recompute inventory merely because a PO, Delivery, or Product is archived/restored;
- keep normal operational lists active-only while giving Admin an explicit deleted/archive view;
- retain existing styling/layout and change only the controls, labels, badges, and filters needed for the workflow;
- leave unrelated modules and old migrations untouched;
- stop after implementation planning until the user approves execution.

---

## 2. Inspection baseline

### Repository and Git

- Repository: `C:/Users/Admin.DESKTOP-OO70H04/source/repos/KaijensonIventory-SalesMotorShopWeb`
- Current branch: `master`
- Current HEAD: `9ee234e` (`8/31/2026 6:05 PM`)
- Tracking: `origin/master`
- Working tree at inspection: clean; no uncommitted changes or untracked application work was present.
- No reset, revert, checkout, stash, commit, or database command was performed.
- Recent relevant history includes the Staff approval-status work (`20260827125934_AddStaffApprovalStatus` migration) and a recent Delivery cleanup commit (`9ee234e`) that removed an earlier `MarkDelivered` compatibility workflow. The current Delivery controller has no delete action.

### Application shape

- .NET 8 MVC application with EF Core SQL Server 8.0.8.
- Authorization is session-based; controllers do not use ASP.NET `[Authorize]` attributes.
- `Controllers/BaseController.cs` checks for a `StaffId` session value and role strings, but does not query the Staff row on each request.
- `Data/ApplicationDbContext.cs` configures restrictive relationships for most business references and cascade relationships for PO/Delivery child rows and ServiceJob histories.
- There is no `IsDeleted`, `DeletedAt`, `DeletedBy`, query filter, restore action, or restore view in the current application.
- There is no test project visible in the inspected repository tree.

### PDF extraction result

Both PDFs contain short text requirements and were successfully read with `pdftotext`; neither required OCR. No PDF content was treated as an implementation change.

---

## 3. Current architecture and delete findings

| Module | Current Delete UI/action | Current hard-delete behavior | Current authorization | Related-data risk | Planned behavior |
|---|---|---|---|---|---|
| **Product** | `Views/Products/Index.cshtml` inline form and `Views/Products/Delete.cshtml`; `ProductsController.Delete` GET and `DeleteConfirmed` POST delegate to `IProductService`. | `Services/ProductService.cs:256-260` calls `_context.Products.Remove(product)` and saves. | Any authenticated session; the delete button is rendered for authenticated users. | Product is referenced by SalesItems, SerialUnits, Notifications, PurchaseOrderItems, Delivery history, Category, Supplier, and optional PO. | Add separate soft-delete metadata; make archive/restore Admin-only; active product lists and sales exclude archived rows; preserve stock and all references. |
| **Brand** | Index posts directly to `BrandsController.Delete`; there is no Delete GET even though `Views/Brands/Delete.cshtml` exists. | `BrandsController.cs:369-370` calls `_context.Brands.Remove(brand)`. | CheckAccess allows Admin or Manager; Delete additionally requires Admin. | Products store the brand as a string, not an FK. Existing code blocks deletion/rename when matching products exist. | Soft-delete without touching Products; retain the unique `BrandName` index so a deleted name cannot be ambiguously reused; Admin restore checks the name/dependency state. |
| **Supplier** | Index inline form; Delete GET and `DeleteConfirmed` POST. Existing Delete view posts incorrectly without `method="post"`, but the index form is the working path. | `SuppliersController.cs:344-345` calls `_context.Suppliers.Remove(supplier)`. | Delete currently requires only an authenticated session; ToggleStatus requires Admin or Manager. | Products and PurchaseOrders have required Supplier FKs with Restrict; controller currently rejects any related rows before hard deletion. | Keep Supplier.Status as operational Active/Inactive and add independent soft-delete metadata; archive without clearing Products/POs; Admin-only archive/restore. |
| **Category** | Index posts directly to `CategoriesController.Delete`; no Delete GET although a Delete view exists. | `CategoriesController.cs:305-306` calls `_context.Categories.Remove(category)`. | CheckAccess allows Admin or Manager; Delete additionally requires Admin. | Products and Services reference Category with Restrict; existing controller blocks any related rows. | Add independent soft-delete metadata; keep CategoryName uniqueness across archived and active rows; Admin restore; active dropdowns exclude archived categories. |
| **Purchase Order** | `PurchaseOrders/Index.cshtml` inline form; controller calls `IPurchaseOrderService.DeleteAsync`. No dedicated Delete view. | `PurchaseOrderService.cs:308-313` removes all POItems and then the PO. | Admin or Manager for module access; Admin-only Delete. | PO -> Delivery is required Cascade; POItems -> PO is Cascade; DeliveryItems -> POItems is Restrict. Removing a PO can erase or violate delivery history. | Archive the PO aggregate transactionally, preserve POItems/DeliveryItems/Delivery, never reverse stock, and restore the existing aggregate without changing status, IDs, or quantities. |
| **Delivery** | **No current Delete button, Delete action, Delete service method, or Delivery Delete view.** Current flow is Index -> Details -> Receive. | No module-specific hard delete exists. `DemoDataController.Reset` has an unrelated bulk purge of demo data. | Index, Details, and Receive require Admin or Manager. | `DeliveryItem` is the receiving-event ledger; Receive updates Product stock, POItem.ReceivedQuantity, PO/Delivery statuses, and notifications. | Do not describe a current Delete workflow that does not exist. To satisfy the PDF, add the smallest explicit Admin-only `Archive/Restore Delivery` control, implemented as a PO/Delivery aggregate archive; it must never undo or replay receiving. |
| **Service** | Index inline form; Delete GET and `DeleteConfirmed` POST; Delete view exists but omits `method="post"`. | `ServicesController.cs:301-302` calls `_context.Services.Remove(service)`. | Any authenticated session; no Admin/Manager check on Delete. | ServiceJob.Service is Restrict; ServiceHistory is indirectly retained through ServiceJob. Existing code blocks deletion when a ServiceJob exists. | Add independent soft-delete metadata; archive/restore Admin-only; active ServiceJob creation/edit dropdowns exclude archived services; history reads remain resolvable. |
| **Mechanic** | Index inline form; Delete GET and `DeleteConfirmed` POST; Delete view omits `method="post"`. | `MechanicsController.cs:306-307` calls `_context.Mechanics.Remove(mechanic)`. | Any authenticated session on Delete; Edit POST requires Admin or Manager. | ServiceJob.Mechanic is Restrict; ServiceHistory is reachable through ServiceJob. Existing code blocks deletion when any job exists. | Add independent soft-delete metadata; archive/restore Admin-only; preserve jobs/history; archived mechanics cannot be assigned to new jobs. |

### Existing hard-delete code that must be removed from the requested business paths

- `Services/ProductService.cs`: `_context.Products.Remove(product)`.
- `Controllers/BrandsController.cs`: `_context.Brands.Remove(brand)`.
- `Controllers/SuppliersController.cs`: `_context.Suppliers.Remove(supplier)`.
- `Controllers/CategoriesController.cs`: `_context.Categories.Remove(category)`.
- `Services/PurchaseOrderService.cs`: `_context.PurchaseOrderItems.RemoveRange(order.Items)` and `_context.PurchaseOrders.Remove(order)` in the delete path.
- `Controllers/ServicesController.cs`: `_context.Services.Remove(service)`.
- `Controllers/MechanicsController.cs`: `_context.Mechanics.Remove(mechanic)`.
- `Controllers/StaffController.cs`: `_context.Staff.Remove(staff)` is also a hard delete and must not remain a normal staff-departure path.

`Controllers/DemoDataController.cs:29-43` is a separate Admin demo-data reset utility. It physically purges many tables, including Deliveries. It is not a tab Delete workflow and is proposed to remain out of scope; if the requirement is intended to prohibit every physical purge, that must be approved as a separate change.

---

## 4. Recommended soft-delete design

### 4.1 Do not overload existing Status values

Use separate lifecycle metadata because the existing `Status` fields have other meanings:

- Supplier `Status`: existing supplier availability (`Active`/`Inactive`).
- Service `Status`: existing catalog availability (`Active`/`Inactive`).
- Mechanic `Status`: existing employment availability (`Active`/`Inactive`), with separate `WorkStatus`.
- PurchaseOrder `Status`: `Pending`, `Approved`, `Partially Delivered`, `Delivered`, `Cancelled` workflow state.
- Delivery `Status`: receiving state (`Pending`, `Partially Delivered`, `Delivered`, `Cancelled`).
- Product `StockStatus`: inventory state, not record lifecycle.
- Brand and Category currently have no status field; Brand.Status was explicitly removed by `20260830084741_RemoveBrandStatus`.
- Staff `Status` will be deliberately repurposed from approval states to `Active`/`Inactive` under Task 2.

Adding a second `Status` or using `Status = Deleted` would make operational state and record visibility ambiguous and could break existing business rules.

### 4.2 Metadata fields

Add these scalar fields to each requested aggregate model:

- `bool IsDeleted { get; set; } = false;`
- `DateTime? DeletedAt { get; set; }`
- `int? DeletedBy { get; set; }`

Models: `Product`, `Brand`, `Supplier`, `Category`, `PurchaseOrder`, `Delivery`, `Service`, and `Mechanic`.

`DeletedBy` is an audit actor ID, not a replacement for the ActivityLog entry. It should not introduce a cascade path to Staff; the Staff row will remain, and the ActivityLog description will retain the actor/name context.

Add the same metadata to `PurchaseOrderItem` because the existing edit workflow physically removes/recreates POItem rows. A removed line must be archived rather than destroyed, and received lines must retain their original IDs for DeliveryItem FKs. `DeliveryItem` remains an immutable receipt-event row and does not need a new delete workflow.

### 4.3 Query strategy

Do **not** add EF global query filters in `OnModelCreating` for this first implementation. Use explicit predicates:

- operational queries: `!entity.IsDeleted`;
- Admin archive lists: `entity.IsDeleted` or an explicit `recordState` parameter;
- historical, audit, numbering, idempotency, and inventory-ledger queries: intentionally include both states.

This avoids required-navigation surprises in EF Core. A global filter on Product, PO, or Delivery could make SalesItems, DeliveryItems, or histories disappear when a required parent is archived. Explicit policy at each query site is more verbose but safer for this existing application.

### 4.4 Restore contract

Every restore action will:

1. require a valid session and Admin role;
2. load the archived row by its existing primary key, bypassing the normal active-only predicate;
3. validate business-key/dependency conflicts without creating a new row;
4. change only `IsDeleted = false`, `DeletedAt = null`, and `DeletedBy = null` (plus any aggregate flags needed for PO/Delivery consistency);
5. preserve all IDs, relationships, statuses, quantities, costs, serials, dates, and child history;
6. write an ActivityLog entry such as `Restore Product`, `Restore PurchaseOrder`, or `Restore Delivery`;
7. return the row to the normal active list.

Restore must never call Create, `DeliverAsync`, `ProcessSaleAsync`, stock adjustment code, or PO-item reconstruction.

### 4.5 Authorization policy

Use the current access checks for viewing ordinary module pages, but tighten lifecycle actions to Admin-only for consistency:

- Admin-only archive/soft-delete for all eight requested modules.
- Admin-only restore and deleted-record views.
- Existing Admin or Manager read/create/edit access remains otherwise unchanged.
- Ordinary staff never see Restore or deleted rows and receive `Forbid`/redirect if they post a restore URL.
- Staff deactivation/reactivation is Admin-only.
- Preserve the current last-Admin protection and add a check preventing an Admin from deactivating their own account.

This intentionally tightens the current Product/Supplier/Service/Mechanic delete permission because a lifecycle archive is still a privileged data operation; it does not weaken any existing permission.

---

## 5. Current Staff behavior and safe transition

### Current implementation

- `Models/Staff.cs`: `Status` defaults to `Approved`; `MustChangePassword` is separate.
- `Migrations/20260827125934_AddStaffApprovalStatus.cs`: adds required Staff.Status with database default `Approved`.
- `AccountController.Register` creates a `Manager` with `Status = "Pending"` and tells the applicant to await Admin approval.
- `AccountController.Login` permits a session only when `staff.Status == "Approved"`; Pending and Rejected are explicitly blocked and logged.
- `StaffController.Index` shows Pending/Approved/other badges and renders Approve/Disapprove only for a Pending Manager. It also has Edit, ChangePassword, and a hard-delete form.
- `StaffController.Approve` changes Pending -> Approved; `Disapprove` changes Pending -> Rejected.
- `StaffController.Edit` does not bind or update `Status`.
- Session keys are `StaffId`, `StaffName`, `StaffRole`, and optional `MustChangePassword`.
- `BaseController.IsSessionValid()` checks only whether `StaffId` exists; it does not re-query Staff.Status. An already logged-in person would currently retain access after an Admin changes the database row.

### Recommended status transition

Use only `Active` and `Inactive` in application behavior:

- Migration data conversion:
  - `Approved` -> `Active`.
  - `Pending` -> `Inactive`.
  - `Rejected` -> `Inactive`.
  - Any unexpected/non-empty status -> `Inactive` for fail-closed behavior.
- Admin-created accounts: explicitly set `Status = "Active"`.
- Public registrations: create `Status = "Inactive"` and show a neutral message that an Admin must activate the account. This removes Approve/Disapprove while preserving the current approval security boundary; it does not grant unreviewed public registrations immediate access.
- Existing approved staff retain access after the migration; existing pending/rejected users do not gain access automatically. Admin can activate them from Manage Staff.
- Deactivation/reactivation changes only Staff.Status and writes an ActivityLog. It never changes StaffId, username, password hash, role, MustChangePassword, or history.

If the business explicitly wants every new public registration to be immediately usable, that is a separate security decision; it would map new registrations to Active but would also remove the current approval gate.

### Authentication/session changes

1. `AccountController.Login` must accept only `Active`, after password verification and before creating session keys. Inactive login attempts must write `Login Blocked` and never set `StaffId`.
2. Add a centralized `Middleware/ActiveStaffSessionMiddleware.cs` registered immediately after `UseSession()` and before controller endpoint execution. For requests with a `StaffId` session value, it will load the Staff row `AsNoTracking`; if missing or not Active, it will clear the session and redirect to Login. Account login/register/reset/logout paths must be handled so an inactive session cannot be redirected back to the dashboard, while logout remains possible.
3. Update `AccountController.Login` GET so an existing session is not blindly redirected to Dashboard when the database row is inactive.
4. Keep `BaseController` role and forced-password checks. The middleware supplies the missing database-backed status check without requiring unsafe synchronous EF calls or changing every controller action to async helper variants.
5. Add an authentication test proving that deactivation invalidates an already-created session on the next request, not merely on the next login.

---

## 6. EF Core and database migration plan

### New migration

After approval and model/controller implementation, create one generated migration, for example:

`Migrations/<timestamp>_AddSoftDeleteMetadataAndStaffActiveStatus.cs`

The generated designer and `Migrations/ApplicationDbContextModelSnapshot.cs` will be updated by EF; existing migration files will not be edited.

### Migration operations

- Add `IsDeleted` (non-null `bit`, default `false`), `DeletedAt` (nullable `datetime2`), and `DeletedBy` (nullable `int`) to the eight aggregate tables.
- Add the same three fields to `PurchaseOrderItems` to preserve removed line items during PO edits.
- Add indexes suitable for active/deleted list predicates if the generated SQL review shows they are useful; do not alter current unique business-key indexes.
- Alter the Staff status default from `Approved` to `Active` if EF generates a default constraint change.
- Run data-only updates for existing Staff values as described above; no rows are deleted.
- Do not drop existing columns, primary keys, foreign keys, unique indexes, DeliveryItems, ServiceHistories, SalesItems, SerialUnits, or ActivityLogs.
- Do not change existing cascade relationships as a substitute for soft deletion. Since the business paths stop deleting parents, those cascade rules are not invoked by archive/restore.

### Uniqueness and IDs

Keep existing unique indexes across archived rows:

- `BrandName`
- `CategoryName`
- `PurchaseOrderNumber`
- `Staff.UserName`
- service-job number and serial number, which are historical sequence/identity domains

This prevents an active record from being created with a name/number that would make an archived history ambiguous. Restore must check for any active conflict before clearing `IsDeleted`.

### Data-protection verification before applying

Before any database update in implementation:

1. export/backup the target database through the existing operational backup process;
2. record row counts and representative IDs for all affected tables;
3. generate and inspect the SQL script rather than applying an unreviewed migration directly;
4. apply to a development copy first;
5. verify all affected counts and IDs are unchanged after migration;
6. only then apply to the approved environment.

No migration or database update is part of this planning turn.

---

## 7. Exact application files proposed for implementation

### Models and data

Modify:

- `Models/Product.cs`
- `Models/Brand.cs`
- `Models/Supplier.cs`
- `Models/Category.cs`
- `Models/PurchaseOrder.cs`
- `Models/PurchaseOrderItem.cs`
- `Models/Delivery.cs`
- `Models/Service.cs`
- `Models/Mechanic.cs`
- `Models/Staff.cs` (Active/Inactive constants/default only; no Staff soft-delete field is needed)
- `Data/ApplicationDbContext.cs` (metadata indexes/configuration and no global filters)
- `Program.cs` (ActiveStaffSessionMiddleware registration and Active seed value)
- new `Middleware/ActiveStaffSessionMiddleware.cs`
- generated new migration and generated snapshot update only.

Do not modify `Models/ActivityLog.cs`, `Models/DeliveryItem.cs`, `Models/ServiceHistory.cs`, `Models/SalesItem.cs`, `Models/SalesTransaction.cs`, `Models/SerialUnit.cs`, or `Models/Notification.cs` unless implementation testing exposes a concrete schema need; those rows are historical/ledger data to preserve.

### Controllers and services

Modify:

- `Controllers/BaseController.cs` only if needed for the middleware/session interaction and shared authorization messages.
- `Controllers/AccountController.cs` for Active-only login and inactive-session-safe Login GET.
- `Controllers/StaffController.cs` to remove Approve/Disapprove and hard Delete paths, add Admin-only Activate/Deactivate or ToggleStatus, preserve last-Admin/self safeguards, and log transitions.
- `Controllers/ProductsController.cs`
- `Services/IProductService.cs`
- `Services/ProductService.cs`
- `Controllers/BrandsController.cs`
- `Controllers/SuppliersController.cs`
- `Controllers/CategoriesController.cs`
- `Controllers/PurchaseOrdersController.cs`
- `Services/IPurchaseOrderService.cs`
- `Services/PurchaseOrderService.cs`
- `Controllers/DeliveryController.cs`
- `Services/IDeliveryService.cs`
- `Services/DeliveryService.cs`
- `Controllers/ServicesController.cs`
- `Controllers/MechanicsController.cs`
- `Services/NotificationService.cs` (exclude alerts for archived Products from active notification surfaces, while retaining notification history).
- `Controllers/DashboardController.cs`
- `Controllers/SalesController.cs`
- `Services/SaleService.cs`
- `Controllers/ServiceJobsController.cs`
- `Services/ReportService.cs`
- `Controllers/ReportsController.cs`

Specific service changes:

- Keep existing route names where practical (`Delete` can remain the POST route for compatibility) but change its implementation to archive and use “Archive”/“Restore” language in the UI.
- Add `Restore` POST methods with anti-forgery validation and Admin checks.
- Add an explicit `includeDeleted`/`recordState` path only for Admin archive views; default all existing methods to active-only.
- Replace PO `RemoveRange(order.Items)` edit logic with identity-preserving reconciliation. Existing received line items cannot be removed or reduced below `ReceivedQuantity`; unreceived removed lines become soft-deleted POItem rows.
- Add an aggregate transaction for PO/Delivery archive and restore so the parent and its generated Delivery do not become operationally inconsistent.

### ViewModels and Razor views

Modify as needed:

- `ViewModels/DeliveryViewModel.cs` and `ViewModels/PurchaseOrderViewModel.cs` to expose archive state for Admin-only controls.
- `Views/Products/Index.cshtml`, `Views/Products/Delete.cshtml`
- `Views/Brands/Index.cshtml`, `Views/Brands/Delete.cshtml`
- `Views/Suppliers/Index.cshtml`, `Views/Suppliers/Delete.cshtml`
- `Views/Categories/Index.cshtml`, `Views/Categories/Delete.cshtml`
- `Views/PurchaseOrders/Index.cshtml`
- `Views/Delivery/Index.cshtml`, `Views/Delivery/Details.cshtml` (new explicit Admin archive/restore control because none currently exists)
- `Views/Services/Index.cshtml`, `Views/Services/Delete.cshtml`
- `Views/Mechanics/Index.cshtml`, `Views/Mechanics/Delete.cshtml`
- `Views/Staff/Index.cshtml`, `Views/Staff/Edit.cshtml`, `Views/Staff/Details.cshtml`, `Views/Staff/Create.cshtml`, and `Views/Account/Login.cshtml`/`Register.cshtml` for status wording.
- `Views/Shared/_DeleteConfirmationModal.cshtml` to replace “This action cannot be undone” with an archive/restore-safe warning.
- `wwwroot/js/site.js` only if needed to make the shared confirmation modal use “Archive” labels and the new reversible warning; no styling redesign.

UI pattern: add a small `recordState`/“Show archived” filter for Admins, default to Active. Render Restore only on archived rows. Keep existing table, icons, pagination, and button CSS. Do not expose archived rows to Managers or ordinary staff.

The Staff page will replace the Approve/Disapprove forms with a single Admin-only Activate/Deactivate control and Active/Inactive badges. The normal Staff Delete button will be removed; a departure is a status change, not row deletion.

---

## 8. Cross-module query and business-rule changes

### Operational active-only reads

Add active predicates to:

- Product list, edit/create lookups, Sales search/cart/reorder/payment product reads, Product dropdowns, and current inventory reports.
- Brand, Category, Supplier, Service, Mechanic, PO, and Delivery normal indexes.
- Dashboard product/category/supplier/mechanic/PO counts, recent rows, and stock cards.
- ServiceJob creation/reassignment lists: active Service and active/available Mechanic only.
- PO validation and supplier/product lookups: active Supplier, Product, and PO only.
- Delivery awaiting list: active Delivery and active PO, and explicitly `Pending`/`Partially Delivered`; the current method name implies this but currently returns all statuses.
- Notifications shown in the active bell/count: do not surface active alerts for archived Products.

### Deliberately history-inclusive reads

Do not apply active-only predicates to:

- checkout-key idempotency and monthly receipt-number allocation in `SaleService`;
- serial-number uniqueness checks;
- service-job submission-token duplicate checks and service-job-number generation;
- DeliveryItems and stock-movement reports;
- SalesItems, SerialUnits, receipts, sales details, and historical reports that need deleted product names/relationships;
- historical PO/Delivery details and audit exports.

Because `SalesItem` stores ProductId/quantity/price but not a product-name snapshot, historical views must deliberately resolve archived Products rather than silently returning missing names. Preserve the existing `[Deleted]` fallback only where a referenced row genuinely cannot be loaded.

### Inventory safety

`DeliveryService.DeliverAsync` currently:

1. validates remaining PO quantity;
2. increments Product.QuantityOnHand;
3. recalculates weighted AverageCost and StockStatus;
4. increments POItem.ReceivedQuantity;
5. appends DeliveryItem history;
6. updates Delivery/PO statuses and notifications in a transaction.

Archive/restore must never call this method, undo any of those values, delete DeliveryItems, or generate a second receipt. Receiving must fail atomically when the Delivery, PO, POItem, or Product is archived. A restored aggregate retains its old status and received quantities; it may be receivable again only through the ordinary receiving workflow after active dependency checks.

### Product safety

Soft-deleting a Product must not set `QuantityOnHand` to zero, clear AverageCost, remove SerialUnits/SalesItems/POItems, or adjust stock. If the business later needs disposal or correction, that must be a separate audited inventory-adjustment feature. ProductService currently allows direct quantity edits; archive/restore must never use that edit path as compensation.

### PO/Delivery safety

- Archive PO and its generated Delivery as one aggregate action to avoid an active PO with a hidden receiving row.
- Preserve `PurchaseOrderItemId`, `ReceivedQuantity`, `DeliveryItemId`, dates, status, costs, and remarks.
- Restore the same aggregate without resetting `Pending`/`Partially Delivered`/`Delivered` state and without changing stock.
- A Delivery archive action is new, not a modification of an existing Delete action; the UI and activity log must make that explicit.

### Activity logs

Use the existing `IActivityLogService`/`ActivityLog` table and preserve all prior rows. Add entries for:

- `Archive`/`Soft Delete` and `Restore` for each module, including record type, original ID, stable name/number, and actor StaffId;
- `Deactivate Staff` and `Activate Staff`, including target StaffId/name and actor StaffId;
- blocked inactive login attempts.

State changes and their corresponding audit row should be saved transactionally where the service already uses a transaction. ActivityLog itself must not be soft-deleted by these operations.

---

## 9. Bite-sized implementation sequence after approval

### Task 1: Add model metadata and constants

**Files:** models listed in Section 7; `Data/ApplicationDbContext.cs`.

Add the scalar fields, Staff Active/Inactive defaults/constants, and indexes/configuration. Do not add global filters. Verify the model snapshot has the expected new columns after migration generation.

### Task 2: Add the migration and data conversion

**Files:** new generated migration and snapshot.

Generate, inspect, and script the migration. Map existing Staff statuses fail-closed as specified. Verify no DropTable/DropColumn/RemoveData operation appears.

### Task 3: Implement centralized inactive-session enforcement

**Files:** new middleware, `Program.cs`, `AccountController.cs`, optionally `BaseController.cs`.

Block inactive login and invalidate an already-created inactive session on the next request. Verify no session keys are set for blocked login.

### Task 4: Replace Staff approval/deletion with status lifecycle

**Files:** `StaffController.cs`, Staff views, account registration/login views.

Remove Approve/Disapprove actions/forms; add Admin-only Activate/Deactivate; remove hard Staff deletion; preserve last-admin and self-protection; log transitions.

### Task 5: Implement Product soft delete/restore and operational filters

**Files:** Product controller/service/interface/views; Sales and notification reads that depend on Product.

Archive without stock mutation; restore by existing ID; active-only catalog/sales behavior; history-inclusive receipts/reports.

### Task 6: Implement Brand, Category, and Supplier soft delete/restore

**Files:** three controllers, models/views, `Data/ApplicationDbContext.cs` as needed.

Preserve name/business-key uniqueness and related rows. Add Admin archive-state filters and Restore actions.

### Task 7: Implement PO/POItem archive/restore safely

**Files:** PO controller/service/interface, models/view model/view.

Replace PO hard delete and POItem `RemoveRange` edit behavior. Preserve received lines and DeliveryItem FKs. Archive/restore the PO/Delivery aggregate in a transaction.

### Task 8: Implement the new Delivery archive/restore workflow

**Files:** Delivery controller/service/interface/model/view model/views.

Add the explicitly new Admin-only archive/restore control, active awaiting predicate, and no-stock-reversal rules. Do not claim the repository previously had a Delivery Delete flow.

### Task 9: Implement Service and Mechanic archive/restore

**Files:** controllers/models/views; ServiceJob operational lookup changes.

Preserve ServiceJob/ServiceHistory and mechanic employment/work history; prevent archived records from new assignments.

### Task 10: Update dashboard, reports, sales, service jobs, and notifications

**Files:** `DashboardController.cs`, `ReportsController.cs`, `Services/ReportService.cs`, `SalesController.cs`, `Services/SaleService.cs`, `ServiceJobsController.cs`, `Services/NotificationService.cs`.

Apply the active-only/history-inclusive query policy in Section 8, with no accidental global-filter hiding of history.

### Task 11: Verify UI and authorization

**Files:** shared modal/JS and all listed Razor views.

Check default active view, Admin archived view, Restore controls, anti-forgery tokens, role checks, messages, pagination, and unchanged existing styling.

### Task 12: Build, test, migration-script review, and data verification

Run the verification commands in Section 10 and stop if any row count, ID, FK, or inventory value changes unexpectedly.

---

## 10. Testing and verification plan

### Automated/targeted tests

If the project gains a test project during implementation, cover at least:

1. archive changes only metadata and preserves the same primary key;
2. restore changes only metadata and preserves all child IDs/relationships;
3. ordinary index excludes archived rows; Admin archive view includes them;
4. ordinary staff cannot post Restore; Manager cannot view archive controls;
5. Product archive does not change quantity, AverageCost, StockStatus, SerialUnits, SalesItems, POItems, or Notifications history;
6. PO/Delivery archive and restore do not change Product quantity, AverageCost, POItem.ReceivedQuantity, DeliveryItem count, or status;
7. PO edit after receipt does not remove/recreate received POItems;
8. historical sales/receipt/stock-movement/service-history queries still resolve archived references;
9. Staff Approved -> Active, Pending/Rejected -> Inactive conversion is correct;
10. inactive Staff cannot log in;
11. a session created while Active is rejected after an Admin changes the row to Inactive;
12. Admin cannot deactivate the last active Admin or their own current account;
13. Activate/Deactivate actions create ActivityLog rows and preserve the target Staff row.

### Build and EF verification

From the repository root, after implementation:

```bash
dotnet restore
dotnet build --configuration Release
dotnet ef migrations list
dotnet ef migrations script <previous-migration> <new-migration> --output .hermes/plans/soft-delete-migration.sql
```

Review the generated SQL for:

- only AddColumn/AlterColumn/UpdateData/index operations expected;
- no DropTable, DeleteData, or destructive FK replacement;
- Staff status conversion and Active default;
- all new columns defaulting to non-deleted for existing rows.

Apply the migration only to an approved development database first, then run the application with `ApplyMigrationsOnStartup` only in that approved development environment. Verify:

- row counts before/after for all affected tables;
- representative primary keys unchanged;
- FK checks pass;
- ActivityLogs remain present;
- Product quantities/AverageCost and DeliveryItem counts are unchanged;
- Staff username uniqueness and existing logins behave as planned.

### Manual MVC test matrix

- Admin: archive one record in each module, switch to Archived, inspect details, restore, confirm it returns to Active.
- Manager/ordinary staff: confirm no archive-state selector or Restore action and direct Restore POST is denied.
- Delivery: archive a pending delivery and a delivered/partially delivered aggregate in a development copy; verify no stock reversal and correct history after restore.
- Staff: deactivate a Manager, confirm current request is redirected to Login, attempt login while inactive, reactivate, and confirm the original account/ID/password/history work again.
- Confirm no Delete confirmation says “cannot be undone”; it must explain that an Admin can restore.

---

## 11. Files explicitly proposed **not** to modify

Unless a concrete build/test failure proves otherwise, do not modify:

- all existing migration `.cs` or `.Designer.cs` files; only add one new generated migration and let EF update the snapshot;
- `Models/ActivityLog.cs`, `Models/DeliveryItem.cs`, `Models/ServiceHistory.cs`, `Models/SalesItem.cs`, `Models/SalesTransaction.cs`, `Models/SerialUnit.cs`, and `Models/Notification.cs` schema;
- `Controllers/ActivityLogController.cs` and `Views/ActivityLog/*` (existing logs remain viewable; no redesign);
- `Controllers/DemoDataController.cs` and its destructive demo reset, which is outside the eight tab workflows and must be treated as a separately approved feature if it is to be converted;
- backup/configuration files and unrelated controllers/services;
- `wwwroot/css/site.css`, `wwwroot/css/reports.css`, `wwwroot/css/print.css`, Bootstrap/jQuery vendor files, and print templates, unless a small existing class is demonstrably required;
- `appsettings.json` and `appsettings.Development.json` except for environment-specific migration testing outside source changes;
- Git history, branches, remotes, and existing uncommitted work.

The plan file itself is the only file written during this planning turn.

---

## 12. Approval gate and final recommendation

### Files proposed to modify

The complete proposed implementation set is:

- the eight aggregate models plus `PurchaseOrderItem` and `Staff`;
- `Data/ApplicationDbContext.cs`, `Program.cs`, and new `Middleware/ActiveStaffSessionMiddleware.cs`;
- `AccountController`, `BaseController` if required, `StaffController`, the eight requested module controllers, `DashboardController`, `SalesController`, `ServiceJobsController`, and `ReportsController`;
- `ProductService`, `PurchaseOrderService`, `DeliveryService`, `SaleService`, `ReportService`, `NotificationService`, and their Product/PO/Delivery interfaces;
- affected view models and the Razor views listed in Section 7;
- shared delete/archive confirmation partial and its small JavaScript label change;
- one new EF migration plus generated designer/snapshot update.

### Files proposed not to modify

All files in Section 11, including existing migrations and historical/ledger model schemas, remain untouched.

### Database changes proposed

Add nullable/boolean soft-delete metadata to the eight requested aggregates and POItems; convert Staff statuses to Active/Inactive; add only supporting indexes/default changes; preserve every existing row, primary key, relationship, and unique business key.

### Is a migration required?

**Yes.** New model columns and the Staff status data conversion require one new EF Core migration. It must be generated and reviewed after approval; no migration is created in planning mode.

### How existing data is protected

No business delete calls `Remove` or `RemoveRange`. Archive changes metadata only. PO/Delivery receipt rows and Product stock values are never reversed. History-inclusive queries deliberately include archived references. Existing IDs, usernames, PO numbers, receipt numbers, serial numbers, ActivityLogs, DeliveryItems, SalesItems, ServiceHistories, and relationships remain intact.

### How restore works

Admin posts a token-protected Restore action with the existing ID. The backend loads the archived row, checks conflicts/dependencies, clears only soft-delete metadata, logs the restore, and returns the same row to the active list. PO/Delivery restore is an aggregate transaction and never reprocesses inventory.

### How inactive Staff login is blocked

Login accepts only `Status == "Active"`, and a centralized post-session middleware rechecks the database Staff row on every request with an existing session. When an Admin deactivates a logged-in staff member, the next request clears the session and redirects to Login. Reactivation re-enables the original account without creating a new row.

**STOP HERE. Do not implement, create the migration, build, or modify the database until the user approves this plan.**
