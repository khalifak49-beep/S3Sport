using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;

namespace S3Sport.Controllers;

/// <summary>
/// Registers this browser/device for Web Push. One row per endpoint; signing in on a shared device moves the
/// endpoint to the current user, so a phone never receives another account's notifications.
/// </summary>
[Authorize]
public class PushController(ApplicationDbContext db, PushKeys keys) : Controller
{
    [HttpGet]
    public IActionResult Key() => Json(new { publicKey = keys.PublicKey });

    [HttpPost]
    public async Task<IActionResult> Subscribe(string? endpoint, string? p256dh, string? auth)
    {
        if (!IsValidEndpoint(endpoint) || string.IsNullOrWhiteSpace(p256dh) || p256dh.Length > 200
            || string.IsNullOrWhiteSpace(auth) || auth.Length > 200)
            return BadRequest(new { ok = false, error = "بيانات الاشتراك غير صالحة." });

        var userId = User.GetUserId()!;
        var userAgent = Request.Headers.UserAgent.ToString();
        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (sub == null)
        {
            sub = new PushSubscription { Endpoint = endpoint!, UserId = userId };
            db.PushSubscriptions.Add(sub);
        }
        sub.UserId = userId;
        sub.P256dh = p256dh;
        sub.Auth = auth;
        sub.UserAgent = userAgent.Length > 300 ? userAgent[..300] : userAgent;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The same endpoint was registered by a parallel request; the unique index kept one row, which is enough.
        }
        return Ok(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Unsubscribe(string? endpoint)
    {
        var userId = User.GetUserId();
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint && s.UserId == userId).ExecuteDeleteAsync();
        return Ok(new { ok = true });
    }

    /// <summary>Push services of Chrome/Edge/Android, Firefox, Windows and Safari/iOS.</summary>
    private static readonly string[] PushServiceDomains = ["googleapis.com", "mozilla.com", "mozaws.net", "notify.windows.com", "push.apple.com"];

    /// <summary>
    /// Only HTTPS endpoints on the browsers' push services are accepted: the server POSTs to this URL,
    /// so an arbitrary address would let a user make the server call internal hosts.
    /// </summary>
    private static bool IsValidEndpoint(string? endpoint) =>
        endpoint is { Length: <= 800 } && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.HostNameType == UriHostNameType.Dns && uri.IsDefaultPort
        && PushServiceDomains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
}
