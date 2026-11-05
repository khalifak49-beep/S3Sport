using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Team registration requests submitted by signed-in users. Users only ever see their own requests.</summary>
[Authorize]
public class TeamApplicationsController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await db.TeamApplications.AsNoTracking()
            .Where(a => a.ApplicantUserId == UserId)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new TeamApplicationListItem
            {
                Id = a.Id, NameAr = a.NameAr, NameEn = a.NameEn, LogoPath = a.LogoPath,
                RegionText = (a.Area != null ? a.Area.NameAr : a.ProposedAreaName) + "، " + a.Wilayat.NameAr,
                SubmittedAt = a.SubmittedAt, Status = a.Status, CreatedTeamId = a.CreatedTeamId
            }).ToListAsync();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await LoadDetailsAsync(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new TeamApplicationFormViewModel();
        var region = CurrentRegion.FromRequest(Request);
        if (region != null)
        {
            model.GovernorateId = region.GovernorateId;
            model.WilayatId = region.WilayatId ?? 0;
            model.AreaId = region.AreaId;
            model.VillageId = region.VillageId;
        }
        model.ContactEmail = User.FindFirstValue(ClaimTypes.Email);
        await TeamFormHelper.FillListsAsync(db, model);
        return View(model);
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Create(TeamApplicationFormViewModel model)
    {
        await ValidateAsync(model, logoRequired: true);
        if (!ModelState.IsValid)
        {
            await TeamFormHelper.FillListsAsync(db, model);
            return View(model);
        }

        var application = new TeamApplication
        {
            ApplicantUserId = UserId,
            SportId = SeedData.FootballSportId,
            Status = TeamStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
            LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo!, env, "teams")
        };
        Map(model, application);

        db.TeamApplications.Add(application);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, "تقديم طلب تسجيل فريق", nameof(TeamApplication), application.Id, application.NameAr);
        await db.SaveChangesAsync();

        TempData["Success"] = "تم إرسال طلب تسجيل الفريق للمراجعة. ستصلك إشعار عند اتخاذ القرار.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    /// <summary>Pending requests can be corrected; rejected ones can be corrected and re-submitted.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var a = await db.TeamApplications.AsNoTracking().Include(x => x.AdditionalAreas)
            .FirstOrDefaultAsync(x => x.Id == id && x.ApplicantUserId == UserId);
        if (a == null) return NotFound();
        if (a.Status is not (TeamStatus.Pending or TeamStatus.Rejected))
        {
            TempData["Error"] = "لا يمكن تعديل طلب تمت الموافقة عليه.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new TeamApplicationFormViewModel
        {
            Id = a.Id, NameAr = a.NameAr, NameEn = a.NameEn, FoundedYear = a.FoundedYear, Description = a.Description,
            ContactPhone = a.ContactPhone, ContactEmail = a.ContactEmail, GovernorateId = a.GovernorateId,
            WilayatId = a.WilayatId, AreaId = a.AreaId, ProposedAreaName = a.AreaId == null ? a.ProposedAreaName : null,
            VillageId = a.VillageId, ExistingLogoPath = a.LogoPath,
            AdditionalAreaIds = a.AdditionalAreas.Select(x => x.AreaId).ToList(),
            RowVersion = Convert.ToBase64String(a.RowVersion), ReviewNotes = a.ReviewNotes
        };
        await TeamFormHelper.FillListsAsync(db, model);
        return View("Create", model);
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, TeamApplicationFormViewModel model)
    {
        var application = await db.TeamApplications.Include(x => x.AdditionalAreas)
            .FirstOrDefaultAsync(x => x.Id == id && x.ApplicantUserId == UserId);
        if (application == null) return NotFound();
        if (application.Status is not (TeamStatus.Pending or TeamStatus.Rejected))
        {
            TempData["Error"] = "لا يمكن تعديل طلب تمت الموافقة عليه.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await ValidateAsync(model, logoRequired: false, ignoreApplicationId: id);
        if (!ModelState.IsValid)
        {
            model.ExistingLogoPath = application.LogoPath;
            await TeamFormHelper.FillListsAsync(db, model);
            return View("Create", model);
        }

        if (!string.IsNullOrEmpty(model.RowVersion))
            db.SetOriginalRowVersion(application, model.RowVersion, x => x.RowVersion);

        string? oldLogo = null;
        if (model.Logo is { Length: > 0 })
        {
            oldLogo = application.LogoPath;
            application.LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo, env, "teams");
        }

        Map(model, application);
        var wasRejected = application.Status == TeamStatus.Rejected;
        application.Status = TeamStatus.Pending;
        if (wasRejected)
        {
            application.SubmittedAt = DateTime.UtcNow;
            application.ReviewedAt = null;
            application.ReviewedByUserId = null;
        }

        db.AddAudit(HttpContext, wasRejected ? "إعادة تقديم طلب فريق" : "تعديل طلب فريق", nameof(TeamApplication), id, application.NameAr);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (oldLogo != null) FileUploadHelper.Delete(env, application.LogoPath);
            TempData["Error"] = "تمت مراجعة الطلب أو تعديله أثناء تحريرك. يرجى مراجعة حالته.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (oldLogo != null) FileUploadHelper.Delete(env, oldLogo);

        TempData["Success"] = wasRejected ? "تمت إعادة تقديم الطلب للمراجعة." : "تم تحديث الطلب.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- helpers ----------

    private Task ValidateAsync(TeamApplicationFormViewModel model, bool logoRequired, int? ignoreApplicationId = null) =>
        TeamFormHelper.ValidateApplicationAsync(db, model, ModelState, UserId, logoRequired, ignoreApplicationId);

    private static void Map(TeamApplicationFormViewModel model, TeamApplication application) =>
        TeamFormHelper.MapApplication(model, application);

    private Task<TeamApplicationDetailsViewModel?> LoadDetailsAsync(int id) =>
        db.TeamApplications.AsNoTracking()
            .Where(a => a.Id == id && a.ApplicantUserId == UserId)
            .ToDetailsViewModel()
            .FirstOrDefaultAsync();
}
