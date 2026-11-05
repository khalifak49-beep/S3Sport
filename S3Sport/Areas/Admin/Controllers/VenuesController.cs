using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.ManageCompetitions)]
public class VenuesController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search) =>
        View(await db.Venues.AsNoTracking()
            .Where(v => search == null || v.NameAr.Contains(search) || (v.Area != null && v.Area.Wilayat.NameAr.Contains(search)))
            .OrderBy(v => v.NameAr)
            .Select(v => new VenueRow
            {
                Id = v.Id, NameAr = v.NameAr, Capacity = v.Capacity,
                AreaText = v.Area != null ? v.Area.NameAr + "، " + v.Area.Wilayat.NameAr : null,
                MatchesCount = db.Matches.Count(m => m.VenueId == v.Id)
            }).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create() => View("Edit", await FillAsync(new VenueFormViewModel()));

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var v = await db.Venues.AsNoTracking().Include(x => x.Area).ThenInclude(a => a!.Wilayat).FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        return View(await FillAsync(new VenueFormViewModel
        {
            Id = v.Id, NameAr = v.NameAr, NameEn = v.NameEn, Address = v.Address, Capacity = v.Capacity,
            AreaId = v.AreaId, WilayatId = v.Area?.WilayatId, GovernorateId = v.Area?.Wilayat.GovernorateId
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Save(VenueFormViewModel model)
    {
        if (model.AreaId is > 0 && !await db.Areas.AnyAsync(a => a.Id == model.AreaId)) ModelState.AddModelError(nameof(model.AreaId), "المنطقة غير صالحة.");
        if (!ModelState.IsValid) return View("Edit", await FillAsync(model));

        var venue = model.Id == 0 ? new Venue() : await db.Venues.FindAsync(model.Id);
        if (venue == null) return NotFound();
        venue.NameAr = model.NameAr.Trim();
        venue.NameEn = model.NameEn?.Trim();
        venue.AreaId = model.AreaId is > 0 ? model.AreaId : null;
        venue.Address = model.Address?.Trim();
        venue.Capacity = model.Capacity;
        if (model.Id == 0) db.Venues.Add(venue);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة ملعب" : "تعديل ملعب", nameof(Venue), venue.Id, venue.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ الملعب.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await db.Venues.FindAsync(id);
        if (v == null) return NotFound();
        db.Venues.Remove(v); // soft delete: past matches keep their venue
        db.AddAudit(HttpContext, "حذف ملعب", nameof(Venue), id, v.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الملعب.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<VenueFormViewModel> FillAsync(VenueFormViewModel model)
    {
        model.Governorates = await db.Governorates.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.SortOrder)
            .Select(g => new SelectListItem(g.NameAr, g.Id.ToString())).ToListAsync();
        return model;
    }
}
