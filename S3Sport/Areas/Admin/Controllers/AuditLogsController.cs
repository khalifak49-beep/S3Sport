using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class AuditLogsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, string? entityName, DateOnly? from, DateOnly? to, int page = 1)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(l => l.Action.Contains(s) || l.UserName!.Contains(s) || l.Details!.Contains(s) || l.EntityId == s);
        }
        if (!string.IsNullOrEmpty(entityName)) query = query.Where(l => l.EntityName == entityName);
        // Dates are entered in Muscat time; convert the day boundaries to UTC.
        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue) - DisplayExtensions.MuscatOffset;
            query = query.Where(l => l.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue) - DisplayExtensions.MuscatOffset;
            query = query.Where(l => l.CreatedAt < toUtc);
        }

        var projected = query.OrderByDescending(l => l.Id).Select(l => new AuditLogRow
        {
            Id = l.Id, CreatedAt = l.CreatedAt, UserName = l.UserName, Action = l.Action,
            EntityName = l.EntityName, EntityId = l.EntityId, Details = l.Details, IpAddress = l.IpAddress
        });

        return View(new AuditLogListViewModel
        {
            Results = await PagedResult<AuditLogRow>.CreateAsync(projected, page, 30),
            Search = search, EntityName = entityName, From = from, To = to,
            EntityNames = (await db.AuditLogs.Select(l => l.EntityName).Distinct().OrderBy(n => n).ToListAsync())
                .Select(n => new SelectListItem(n, n))
        });
    }
}
