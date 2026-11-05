using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;

namespace S3Sport.Controllers;

/// <summary>
/// Follow / unfollow / notification toggle for any target, with no region restriction.
/// Called with fetch (returns the re-rendered _FollowButton partial) or as a plain form post (redirects back).
/// </summary>
[Authorize]
public class FollowsController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<IActionResult> Follow(FollowTargetType targetType, int targetId, bool compact = false, string? returnUrl = null)
    {
        if (!await TargetExistsAsync(targetType, targetId)) return NotFound();

        if (!await db.UserFollows.Where(f => f.UserId == UserId).ForTarget(targetType, targetId).AnyAsync())
        {
            var follow = new UserFollow { UserId = UserId, NotificationsEnabled = true };
            follow.SetTarget(targetType, targetId);
            db.UserFollows.Add(follow);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A double click raced us: the unique index already holds this follow, which is the desired state.
                db.ChangeTracker.Clear();
            }
        }
        return await RespondAsync(targetType, targetId, compact, returnUrl, "تمت المتابعة، وستصلك إشعاراته.");
    }

    [HttpPost]
    public async Task<IActionResult> Unfollow(FollowTargetType targetType, int targetId, bool compact = false, string? returnUrl = null)
    {
        await db.UserFollows.Where(f => f.UserId == UserId).ForTarget(targetType, targetId).ExecuteDeleteAsync();
        return await RespondAsync(targetType, targetId, compact, returnUrl, "تم إلغاء المتابعة.");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleNotifications(FollowTargetType targetType, int targetId, bool compact = false, string? returnUrl = null)
    {
        var follow = await db.UserFollows.Where(f => f.UserId == UserId).ForTarget(targetType, targetId).FirstOrDefaultAsync();
        if (follow == null) return NotFound();

        follow.NotificationsEnabled = !follow.NotificationsEnabled;
        await db.SaveChangesAsync();
        return await RespondAsync(targetType, targetId, compact, returnUrl,
            follow.NotificationsEnabled ? "تم تفعيل الإشعارات." : "تم إيقاف الإشعارات.");
    }

    /// <summary>Only publicly visible targets can be followed (from any region).</summary>
    private Task<bool> TargetExistsAsync(FollowTargetType type, int id) => type switch
    {
        FollowTargetType.Team => db.Teams.AnyAsync(t => t.Id == id && t.Status == TeamStatus.Approved),
        FollowTargetType.Player => db.Players.AnyAsync(p => p.Id == id),
        FollowTargetType.Competition => db.Competitions.AnyAsync(c => c.Id == id && c.IsActive),
        FollowTargetType.Match => db.Matches.Visible().AnyAsync(m => m.Id == id),
        _ => Task.FromResult(false)
    };

    private async Task<IActionResult> RespondAsync(FollowTargetType type, int id, bool compact, string? returnUrl, string message)
    {
        if (Request.Headers.XRequestedWith == "fetch")
        {
            // ASCII-safe header; the page shows it as a toast.
            Response.Headers["X-S3-Message"] = Uri.EscapeDataString(message);
            return PartialView("_FollowButton", await FollowHelper.BuildAsync(db, UserId, type, id, compact));
        }

        TempData["Success"] = message;
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Favorites");
    }
}
