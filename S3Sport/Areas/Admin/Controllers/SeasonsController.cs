using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class SeasonsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Seasons.AsNoTracking().OrderByDescending(s => s.StartDate)
            .Select(s => new SeasonRow
            {
                Id = s.Id, Name = s.Name, StartDate = s.StartDate, EndDate = s.EndDate, IsCurrent = s.IsCurrent,
                EditionsCount = db.CompetitionSeasons.Count(cs => cs.SeasonId == s.Id)
            }).ToListAsync());

    [HttpGet]
    public IActionResult Create()
    {
        var year = DateTime.UtcNow.Year;
        return View("Edit", new SeasonFormViewModel
        {
            Name = $"{year}-{year + 1}",
            StartDate = new DateOnly(year, 9, 1),
            EndDate = new DateOnly(year + 1, 6, 30)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await db.Seasons.FindAsync(id);
        if (s == null) return NotFound();
        return View(new SeasonFormViewModel { Id = s.Id, Name = s.Name, StartDate = s.StartDate, EndDate = s.EndDate, IsCurrent = s.IsCurrent });
    }

    [HttpPost]
    public async Task<IActionResult> Save(SeasonFormViewModel model)
    {
        if (model.StartDate >= model.EndDate) ModelState.AddModelError(nameof(model.EndDate), "تاريخ النهاية يجب أن يكون بعد البداية.");
        if (await db.Seasons.AnyAsync(s => s.Id != model.Id && s.Name == model.Name.Trim()))
            ModelState.AddModelError(nameof(model.Name), "يوجد موسم بنفس الاسم.");
        if (!ModelState.IsValid) return View("Edit", model);

        var season = model.Id == 0 ? new Season() : await db.Seasons.FindAsync(model.Id);
        if (season == null) return NotFound();

        await db.InTransactionAsync(async () =>
        {
            // Only one current season (filtered unique index): clear the old one first.
            if (model.IsCurrent)
                await db.Seasons.Where(s => s.IsCurrent && s.Id != model.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));
            season.Name = model.Name.Trim();
            season.StartDate = model.StartDate!.Value;
            season.EndDate = model.EndDate!.Value;
            season.IsCurrent = model.IsCurrent;
            if (model.Id == 0) db.Seasons.Add(season);
            await db.SaveChangesAsync();
            db.AddAudit(HttpContext, model.Id == 0 ? "إضافة موسم" : "تعديل موسم", nameof(Season), season.Id, season.Name);
            await db.SaveChangesAsync();
        });

        TempData["Success"] = "تم حفظ الموسم.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Seasons.FindAsync(id);
        if (s == null) return NotFound();
        if (await db.CompetitionSeasons.AnyAsync(cs => cs.SeasonId == id))
        {
            TempData["Error"] = "لا يمكن حذف موسم مرتبط ببطولات.";
            return RedirectToAction(nameof(Index));
        }
        db.Seasons.Remove(s);
        db.AddAudit(HttpContext, "حذف موسم", nameof(Season), id, s.Name);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الموسم.";
        return RedirectToAction(nameof(Index));
    }
}
