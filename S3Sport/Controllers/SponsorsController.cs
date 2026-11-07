using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;

namespace S3Sport.Controllers;

/// <summary>Sponsor click-through: counts the click, then opens the sponsor's site, WhatsApp or phone.</summary>
public class SponsorsController(ApplicationDbContext db) : Controller
{
    [HttpGet("/sponsors/{id:int}/go")]
    public async Task<IActionResult> Go(int id)
    {
        var s = await db.Sponsors.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new { x.WebsiteUrl, x.Phone }).FirstOrDefaultAsync();
        if (s == null) return RedirectToAction("Index", "Home");
        if (!SponsorsViewComponent.IsBot(Request.Headers.UserAgent.ToString()))
            await db.Sponsors.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Clicks, x => x.Clicks + 1));
        // Only admin-entered http(s) links are stored (checked on save), so this is not an open redirect.
        if (s.WebsiteUrl != null) return Redirect(s.WebsiteUrl);
        if (s.Phone != null) return Redirect(PhoneHelper.WhatsAppUrl(s.Phone, "مرحباً، وصلت لكم عن طريق منصة S3Sport"));
        return RedirectToAction("Index", "Home");
    }
}
