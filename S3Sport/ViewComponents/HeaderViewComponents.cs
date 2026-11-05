using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;

namespace S3Sport.ViewComponents;

/// <summary>Current region name shown in the header (links to the region picker).</summary>
public class RegionBadgeViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var region = CurrentRegion.FromRequest(Request);
        // Explicit view name: View(string) would treat the region name as a view name.
        return View("Default", region == null ? null : await region.GetDisplayNameAsync(db));
    }
}

public class NotificationBellViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = userId == null ? 0 : await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return View(count);
    }
}

/// <summary>Count of pending team applications for the admin sidebar badge.</summary>
public class PendingApplicationsViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        Content((await db.TeamApplications.CountAsync(a => a.Status == TeamStatus.Pending)).ToString());
}
