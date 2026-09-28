using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Middleware;

public sealed class ActiveStaffSessionMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveStaffSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        int? staffId = context.Session.GetInt32("StaffId");
        if (staffId.HasValue)
        {
            var staff = await db.Staff
                .AsNoTracking()
                .Where(s => s.StaffId == staffId.Value)
                .Select(s => new { s.StaffId, s.Status, s.Role })
                .FirstOrDefaultAsync();

            if (staff == null || !string.Equals(staff.Status, Models.Staff.ActiveStatus, StringComparison.OrdinalIgnoreCase))
            {
                context.Session.Clear();

                // The account endpoints must remain reachable so the user can log in
                // again after an administrator changes the account status.
                if (!context.Request.Path.StartsWithSegments("/Account"))
                {
                    context.Response.Redirect("/Account/Login?inactive=true");
                    return;
                }
            }
            else if (!string.Equals(context.Session.GetString("StaffRole"), staff.Role, StringComparison.OrdinalIgnoreCase))
            {
                context.Session.SetString("StaffRole", staff.Role);
            }
        }

        await _next(context);
    }
}
