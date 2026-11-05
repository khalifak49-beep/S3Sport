using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

[Authorize]
public class NotificationsController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = db.Notifications.AsNoTracking()
            .Where(n => n.UserId == UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItem
            {
                Id = n.Id, Type = n.Type, Title = n.Title, Message = n.Message,
                Url = n.Url, IsRead = n.IsRead, CreatedAt = n.CreatedAt
            });
        return View(await PagedResult<NotificationItem>.CreateAsync(query, page, 20));
    }

    /// <summary>Marks a notification as read and follows its link (local links only).</summary>
    [HttpPost]
    public async Task<IActionResult> Open(int id)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (n == null) return NotFound();
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return !string.IsNullOrEmpty(n.Url) && Url.IsLocalUrl(n.Url) ? LocalRedirect(n.Url) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Notifications.Where(x => x.UserId == UserId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, DateTime.UtcNow));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount() =>
        Json(await db.Notifications.CountAsync(x => x.UserId == UserId && !x.IsRead));
}
