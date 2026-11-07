using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>Sponsors: where each one appears (country, governorate, wilayat or competition), when, and its views/clicks.</summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class SponsorsController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.ToMuscat());
        var rows = await db.Sponsors.AsNoTracking().OrderByDescending(s => s.IsActive).ThenBy(s => s.SortOrder).ThenBy(s => s.NameAr)
            .Select(s => new
            {
                s.Id, s.NameAr, s.LogoPath, s.Scope, s.StartsOn, s.EndsOn, s.IsActive, s.Views, s.Clicks,
                Governorate = s.Governorate != null ? s.Governorate.NameAr : null,
                Wilayat = s.Wilayat != null ? s.Wilayat.NameAr : null,
                Competition = s.Competition != null ? s.Competition.NameAr : null
            }).ToListAsync();
        return View(rows.Select(s => new SponsorRow
        {
            Id = s.Id, NameAr = s.NameAr, LogoPath = s.LogoPath, StartsOn = s.StartsOn, EndsOn = s.EndsOn, IsActive = s.IsActive,
            Views = s.Views, Clicks = s.Clicks,
            IsRunning = s.IsActive && (s.StartsOn == null || s.StartsOn <= today) && (s.EndsOn == null || s.EndsOn >= today),
            Placement = s.Scope switch
            {
                SponsorScope.Governorate => $"محافظة {s.Governorate}",
                SponsorScope.Wilayat => $"ولاية {s.Wilayat}",
                SponsorScope.Competition => $"بطولة {s.Competition}",
                _ => "كل السلطنة"
            }
        }).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View("Edit", await FillAsync(new SponsorFormViewModel()));

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await db.Sponsors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var governorateId = s.GovernorateId ?? (s.WilayatId != null ? await db.Wilayats.Where(w => w.Id == s.WilayatId).Select(w => (int?)w.GovernorateId).FirstOrDefaultAsync() : null);
        return View(await FillAsync(new SponsorFormViewModel
        {
            Id = s.Id, NameAr = s.NameAr, Tagline = s.Tagline, LogoPath = s.LogoPath, WebsiteUrl = s.WebsiteUrl, Phone = s.Phone,
            Scope = s.Scope, GovernorateId = governorateId, WilayatId = s.WilayatId, CompetitionId = s.CompetitionId,
            StartsOn = s.StartsOn, EndsOn = s.EndsOn, IsActive = s.IsActive, SortOrder = s.SortOrder
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Save(SponsorFormViewModel model)
    {
        var sponsor = model.Id == 0 ? new Sponsor() : await db.Sponsors.FindAsync(model.Id);
        if (sponsor == null) return NotFound();

        if (model.Logo != null && FileUploadHelper.ValidateImage(model.Logo) is { } logoError) ModelState.AddModelError(nameof(model.Logo), logoError);
        if (model.Logo == null && string.IsNullOrEmpty(sponsor.LogoPath)) ModelState.AddModelError(nameof(model.Logo), "شعار الراعي مطلوب.");

        var url = model.WebsiteUrl?.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            if (!url.Contains("://")) url = "https://" + url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                ModelState.AddModelError(nameof(model.WebsiteUrl), "الرابط غير صالح. مثال: instagram.com/store");
        }
        var phone = string.IsNullOrWhiteSpace(model.Phone) ? null : PhoneHelper.Normalize(model.Phone);
        if (!string.IsNullOrWhiteSpace(model.Phone) && phone == null) ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف غير صالح.");

        switch (model.Scope)
        {
            case SponsorScope.Governorate when model.GovernorateId is not > 0 || !await db.Governorates.AnyAsync(g => g.Id == model.GovernorateId):
                ModelState.AddModelError(nameof(model.GovernorateId), "اختر المحافظة."); break;
            case SponsorScope.Wilayat when model.WilayatId is not > 0 || !await db.Wilayats.AnyAsync(w => w.Id == model.WilayatId):
                ModelState.AddModelError(nameof(model.WilayatId), "اختر الولاية."); break;
            case SponsorScope.Competition when model.CompetitionId is not > 0 || !await db.Competitions.AnyAsync(c => c.Id == model.CompetitionId):
                ModelState.AddModelError(nameof(model.CompetitionId), "اختر البطولة."); break;
        }
        if (model.StartsOn != null && model.EndsOn != null && model.EndsOn < model.StartsOn)
            ModelState.AddModelError(nameof(model.EndsOn), "تاريخ النهاية قبل تاريخ البداية.");
        if (!ModelState.IsValid) { model.LogoPath = sponsor.LogoPath; return View("Edit", await FillAsync(model)); }

        if (model.Logo != null)
        {
            var old = sponsor.LogoPath;
            sponsor.LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo, env, "sponsors");
            if (!string.IsNullOrEmpty(old)) FileUploadHelper.Delete(env, old);
        }
        sponsor.NameAr = model.NameAr.Trim();
        sponsor.Tagline = string.IsNullOrWhiteSpace(model.Tagline) ? null : model.Tagline.Trim();
        sponsor.WebsiteUrl = string.IsNullOrEmpty(url) ? null : url;
        sponsor.Phone = phone;
        sponsor.Scope = model.Scope;
        sponsor.GovernorateId = model.Scope == SponsorScope.Governorate ? model.GovernorateId : null;
        sponsor.WilayatId = model.Scope == SponsorScope.Wilayat ? model.WilayatId : null;
        sponsor.CompetitionId = model.Scope == SponsorScope.Competition ? model.CompetitionId : null;
        sponsor.StartsOn = model.StartsOn;
        sponsor.EndsOn = model.EndsOn;
        sponsor.IsActive = model.IsActive;
        sponsor.SortOrder = model.SortOrder;
        if (model.Id == 0) db.Sponsors.Add(sponsor);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة راعٍ" : "تعديل راعٍ", nameof(Sponsor), sponsor.Id, sponsor.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ الراعي.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var s = await db.Sponsors.FindAsync(id);
        if (s == null) return NotFound();
        s.IsActive = !s.IsActive;
        db.AddAudit(HttpContext, s.IsActive ? "تفعيل راعٍ" : "إيقاف راعٍ", nameof(Sponsor), id, s.NameAr);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Sponsors.FindAsync(id);
        if (s == null) return NotFound();
        db.Sponsors.Remove(s);
        db.AddAudit(HttpContext, "حذف راعٍ", nameof(Sponsor), id, s.NameAr);
        await db.SaveChangesAsync();
        FileUploadHelper.Delete(env, s.LogoPath);
        TempData["Success"] = "تم حذف الراعي.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SponsorFormViewModel> FillAsync(SponsorFormViewModel model)
    {
        model.Governorates = await db.Governorates.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.SortOrder)
            .Select(g => new SelectListItem(g.NameAr, g.Id.ToString())).ToListAsync();
        model.Competitions = await db.Competitions.AsNoTracking().OrderBy(c => c.NameAr)
            .Select(c => new SelectListItem(c.NameAr, c.Id.ToString())).ToListAsync();
        return model;
    }
}
