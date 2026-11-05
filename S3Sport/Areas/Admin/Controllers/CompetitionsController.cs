using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

/// <summary>
/// Competitions. Admins create competitions and assign tournament managers;
/// a TournamentManager sees and edits only the competitions assigned to them.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AppPolicies.ManageCompetitions)]
public class CompetitionsController(ApplicationDbContext db, IWebHostEnvironment env, UserManager<ApplicationUser> userManager, RoleRefresh roleRefresh) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.ManageableCompetitions(User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.NameAr.Contains(search) || (c.NameEn != null && c.NameEn.Contains(search)));
        var list = await query.ToListItems().ToListAsync();
        ViewData["Search"] = search;
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (!await db.CanManageCompetitionAsync(User, id)) return Forbid();
        var model = await db.Competitions.AsNoTracking().Where(c => c.Id == id).Select(c => new CompetitionAdminDetailsViewModel
        {
            Id = c.Id, NameAr = c.NameAr, Slug = c.Slug, LogoPath = c.LogoPath,
            RegionText = c.Area != null ? c.Area.NameAr + "، " + c.Area.Wilayat.NameAr : c.Wilayat != null ? c.Wilayat.NameAr : c.Governorate != null ? c.Governorate.NameAr : "سلطنة عُمان",
            Editions = c.Seasons.OrderByDescending(s => s.Season.StartDate).Select(s => new EditionRow
            {
                Id = s.Id, SeasonName = s.Season.Name, Format = s.Format, Status = s.Status,
                TeamsCount = s.Teams.Count, MatchesCount = s.Matches.Count, FinishedCount = s.Matches.Count(m => m.Status == MatchStatus.Finished)
            }).ToList()
        }).FirstOrDefaultAsync();
        if (model == null) return NotFound();
        model.Managers = (await db.CompetitionManagers.Where(m => m.CompetitionId == id)
            .Select(m => new { m.UserId, m.User.FullName, m.User.Email }).ToListAsync())
            .Select(m => (m.UserId, m.FullName, m.Email)).ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!User.IsAdmin()) return Forbid();
        return View("Edit", await FillAsync(new CompetitionFormViewModel()));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await db.CanManageCompetitionAsync(User, id)) return Forbid();
        var c = await db.Competitions.AsNoTracking().Include(x => x.Area).Include(x => x.Wilayat).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View(await FillAsync(new CompetitionFormViewModel
        {
            Id = c.Id, NameAr = c.NameAr, NameEn = c.NameEn ?? "", SportId = c.SportId, Description = c.Description,
            OrganizerName = c.OrganizerName, GovernorateId = c.GovernorateId, WilayatId = c.WilayatId, AreaId = c.AreaId,
            IsActive = c.IsActive, LogoPath = c.LogoPath, RowVersion = Convert.ToBase64String(c.RowVersion)
        }));
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Save(CompetitionFormViewModel model)
    {
        if (model.Id == 0 ? !User.IsAdmin() : !await db.CanManageCompetitionAsync(User, model.Id)) return Forbid();
        if (!await db.Sports.AnyAsync(s => s.Id == model.SportId && s.IsActive)) ModelState.AddModelError(nameof(model.SportId), "الرياضة غير صالحة.");
        if (model.Logo is { Length: > 0 } && FileUploadHelper.ValidateImage(model.Logo) is { } err) ModelState.AddModelError(nameof(model.Logo), err);

        // Region scope: keep only a consistent chain, deepest level wins.
        var region = await CurrentRegion.ResolveAsync(db, model.GovernorateId, model.WilayatId, model.AreaId, null);
        if (!ModelState.IsValid) return View("Edit", await FillAsync(model));

        var c = model.Id == 0 ? new Competition() : await db.Competitions.FindAsync(model.Id);
        if (c == null) return NotFound();
        if (model.Id == 0)
        {
            c.Slug = await SlugHelper.GenerateUniqueAsync(model.NameEn, s => db.Competitions.IgnoreQueryFilters().AnyAsync(x => x.Slug == s), "competition");
            db.Competitions.Add(c);
        }
        else db.SetOriginalRowVersion(c, model.RowVersion, x => x.RowVersion);

        if (model.Logo is { Length: > 0 })
        {
            FileUploadHelper.Delete(env, c.LogoPath);
            c.LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo, env, "competitions");
        }
        c.NameAr = model.NameAr.Trim();
        c.NameEn = model.NameEn.Trim();
        c.SportId = model.SportId;
        c.Description = model.Description?.Trim();
        c.OrganizerName = model.OrganizerName?.Trim();
        c.GovernorateId = region?.GovernorateId;
        c.WilayatId = region?.WilayatId;
        c.AreaId = region?.AreaId;
        c.IsActive = model.IsActive;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل البطولة من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة بطولة" : "تعديل بطولة", nameof(Competition), c.Id, c.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ البطولة.";
        return RedirectToAction(nameof(Details), new { id = c.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddManager(int id, string email)
    {
        if (!User.IsAdmin()) return Forbid();
        if (!await db.Competitions.AnyAsync(c => c.Id == id)) return NotFound();
        var user = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            TempData["Error"] = "لا يوجد مستخدم بهذا البريد.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await db.CompetitionManagers.AnyAsync(m => m.CompetitionId == id && m.UserId == user.Id))
        {
            db.CompetitionManagers.Add(new CompetitionManager { CompetitionId = id, UserId = user.Id, AssignedAt = DateTime.UtcNow });
            db.AddAudit(HttpContext, "تعيين مسؤول بطولة", nameof(Competition), id, user.Email);
            await db.SaveChangesAsync();
        }
        if (!await userManager.IsInRoleAsync(user, AppRoles.TournamentManager))
        {
            await userManager.AddToRoleAsync(user, AppRoles.TournamentManager);
            await userManager.UpdateSecurityStampAsync(user);
            roleRefresh.MarkChanged(user.Id);
        }
        TempData["Success"] = $"تم تعيين {user.FullName} مسؤولاً عن البطولة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveManager(int id, string userId)
    {
        if (!User.IsAdmin()) return Forbid();
        var m = await db.CompetitionManagers.FirstOrDefaultAsync(x => x.CompetitionId == id && x.UserId == userId);
        if (m == null) return NotFound();
        db.CompetitionManagers.Remove(m);
        db.AddAudit(HttpContext, "إزالة مسؤول بطولة", nameof(Competition), id, userId);
        await db.SaveChangesAsync();

        var user = await userManager.FindByIdAsync(userId);
        if (user != null && !await db.CompetitionManagers.AnyAsync(x => x.UserId == userId) && await userManager.IsInRoleAsync(user, AppRoles.TournamentManager))
        {
            await userManager.RemoveFromRoleAsync(user, AppRoles.TournamentManager);
            await userManager.UpdateSecurityStampAsync(user);
            roleRefresh.MarkChanged(user.Id);
        }
        TempData["Success"] = "تمت إزالة المسؤول.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.IsAdmin()) return Forbid();
        var c = await db.Competitions.FindAsync(id);
        if (c == null) return NotFound();
        if (await db.Matches.AnyAsync(m => m.CompetitionSeason != null && m.CompetitionSeason.CompetitionId == id))
        {
            TempData["Error"] = "لا يمكن حذف بطولة لها مباريات. يمكنك تعطيلها بدلاً من ذلك.";
            return RedirectToAction(nameof(Details), new { id });
        }
        db.Competitions.Remove(c);
        db.AddAudit(HttpContext, "حذف بطولة", nameof(Competition), id, c.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = "تم حذف البطولة.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CompetitionFormViewModel> FillAsync(CompetitionFormViewModel model)
    {
        model.Sports = await db.Sports.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder)
            .Select(s => new SelectListItem(s.NameAr, s.Id.ToString())).ToListAsync();
        model.Governorates = await db.Governorates.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.SortOrder)
            .Select(g => new SelectListItem(g.NameAr, g.Id.ToString())).ToListAsync();
        return model;
    }
}
