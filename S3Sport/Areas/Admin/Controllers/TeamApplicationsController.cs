using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class TeamApplicationsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleRefresh roleRefresh,
    ILogger<TeamApplicationsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(TeamStatus? status = TeamStatus.Pending, string? search = null, int page = 1)
    {
        var query = db.TeamApplications.AsNoTracking();
        if (status.HasValue) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.NameAr.Contains(s) || a.NameEn.Contains(s)
                || a.ApplicantUser.FullName.Contains(s) || a.ApplicantUser.Email!.Contains(s)
                || a.Wilayat.NameAr.Contains(s) || a.Area!.NameAr.Contains(s) || a.ProposedAreaName!.Contains(s));
        }

        var projected = query
            .OrderBy(a => a.Status == TeamStatus.Pending ? 0 : 1)
            .ThenBy(a => a.Status == TeamStatus.Pending ? a.SubmittedAt : DateTime.MaxValue)
            .ThenByDescending(a => a.SubmittedAt)
            .Select(a => new TeamApplicationListItem
            {
                Id = a.Id, NameAr = a.NameAr, NameEn = a.NameEn, LogoPath = a.LogoPath,
                ApplicantName = a.ApplicantUser.FullName, ApplicantEmail = a.ApplicantUser.Email,
                RegionText = (a.Area != null ? a.Area.NameAr : a.ProposedAreaName) + "، " + a.Wilayat.NameAr + "، " + a.Governorate.NameAr,
                SubmittedAt = a.SubmittedAt, Status = a.Status, CreatedTeamId = a.CreatedTeamId
            });

        var counts = await db.TeamApplications.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        return View(new TeamApplicationListViewModel
        {
            Results = await PagedResult<TeamApplicationListItem>.CreateAsync(projected, page),
            Status = status,
            Search = search,
            StatusCounts = counts
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await db.TeamApplications.AsNoTracking().Where(a => a.Id == id).ToDetailsViewModel().FirstOrDefaultAsync();
        if (model == null) return NotFound();

        model.SimilarTeams = await db.Teams.AsNoTracking()
            .Where(t => t.NameAr == model.NameAr || t.NameEn == model.NameEn)
            .Select(t => t.NameAr + " — " + (t.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr).FirstOrDefault() ?? ""))
            .ToListAsync();
        return View(model);
    }

    /// <summary>
    /// Approval creates the team, its areas and manager membership, grants the TeamManager role
    /// and notifies the applicant — all in a single SQL transaction.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve(int id, string rowVersion, string? notes, string? areaNameAr, string? areaNameEn)
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Team? team = null;
        string? applicantId = null;
        try
        {
            await db.InTransactionAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var app = await db.TeamApplications.Include(a => a.AdditionalAreas).FirstOrDefaultAsync(a => a.Id == id)
                    ?? throw new InvalidOperationException("الطلب غير موجود.");
                applicantId = app.ApplicantUserId;
                if (app.Status != TeamStatus.Pending)
                    throw new InvalidOperationException("تمت مراجعة هذا الطلب مسبقاً.");
                db.SetOriginalRowVersion(app, rowVersion, a => a.RowVersion);

                // The applicant typed an area that was not registered: create it (or reuse a same-named one) first.
                if (app.AreaId == null)
                    app.AreaId = await ResolveProposedAreaAsync(app, areaNameAr, areaNameEn);

                team = new Team
                {
                    SportId = app.SportId,
                    NameAr = app.NameAr,
                    NameEn = app.NameEn,
                    Slug = await SlugHelper.GenerateUniqueAsync(app.NameEn,
                        s => db.Teams.IgnoreQueryFilters().AnyAsync(t => t.Slug == s), "team"),
                    LogoPath = app.LogoPath,
                    FoundedYear = app.FoundedYear,
                    Description = app.Description,
                    ContactPhone = app.ContactPhone,
                    ContactEmail = app.ContactEmail,
                    VillageId = app.VillageId,
                    Status = TeamStatus.Approved,
                    ApprovedAt = DateTime.UtcNow
                };
                team.TeamAreas.Add(new TeamArea { AreaId = app.AreaId.Value, IsPrimary = true });
                foreach (var extra in app.AdditionalAreas.Where(x => x.AreaId != app.AreaId))
                    team.TeamAreas.Add(new TeamArea { AreaId = extra.AreaId });
                team.Members.Add(new TeamMember { UserId = app.ApplicantUserId, Role = TeamMemberRole.Manager });
                db.Teams.Add(team);

                app.Status = TeamStatus.Approved;
                app.ReviewedByUserId = reviewerId;
                app.ReviewedAt = DateTime.UtcNow;
                app.ReviewNotes = notes?.Trim();
                app.CreatedTeam = team;

                await db.SaveChangesAsync();

                db.AddNotification(app.ApplicantUserId, NotificationType.TeamApplication,
                    $"تمت الموافقة على تسجيل فريق {app.NameAr}",
                    "أصبح فريقك منشوراً في المنصة. اضغط لإدارة لاعبيه ومبارياته.",
                    $"/MyTeams/Squad/{team.Id}", teamId: team.Id);
                db.AddAudit(HttpContext, "اعتماد طلب فريق", nameof(TeamApplication), app.Id, $"{app.NameAr} → فريق #{team.Id}");
                await db.SaveChangesAsync();

                // UserManager shares this scoped DbContext, so the role change joins the same transaction.
                var applicant = await userManager.FindByIdAsync(app.ApplicantUserId)
                    ?? throw new InvalidOperationException("صاحب الطلب غير موجود.");
                if (!await userManager.IsInRoleAsync(applicant, AppRoles.TeamManager))
                {
                    var result = await userManager.AddToRoleAsync(applicant, AppRoles.TeamManager);
                    if (!result.Succeeded) throw new InvalidOperationException("تعذر منح صلاحية مسؤول فريق.");
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل الطلب من مستخدم آخر أثناء المراجعة. يرجى مراجعته مجدداً.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // The applicant gets the team-manager permissions on their very next request (no re-login needed).
        roleRefresh.MarkChanged(applicantId!);
        logger.LogInformation("Team application {Id} approved, team {TeamId} created.", id, team!.Id);
        TempData["Success"] = $"تم اعتماد الفريق «{team.NameAr}» ونشره في المنصة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Finds or creates the area proposed by the applicant under the application's wilayat.
    /// The reviewer may correct the Arabic spelling and supply the English name (defaults to the Arabic one).
    /// </summary>
    private async Task<int> ResolveProposedAreaAsync(TeamApplication app, string? areaNameAr, string? areaNameEn)
    {
        var nameAr = (string.IsNullOrWhiteSpace(areaNameAr) ? app.ProposedAreaName : areaNameAr)?.Trim();
        if (string.IsNullOrEmpty(nameAr) || nameAr.Length > 100)
            throw new InvalidOperationException("اسم المنطقة الجديدة مطلوب (حتى 100 حرف).");
        var nameEn = string.IsNullOrWhiteSpace(areaNameEn) ? nameAr : areaNameEn.Trim();
        if (nameEn.Length > 100)
            throw new InvalidOperationException("الاسم الإنجليزي للمنطقة يجب ألا يتجاوز 100 حرف.");

        // Names are unique per wilayat (also inactive ones), so reuse rather than duplicate.
        var existing = await db.Areas.FirstOrDefaultAsync(a => a.WilayatId == app.WilayatId && a.NameAr == nameAr);
        if (existing != null)
        {
            existing.IsActive = true;
            return existing.Id;
        }

        var area = new Area { WilayatId = app.WilayatId, NameAr = nameAr, NameEn = nameEn };
        db.Areas.Add(area);
        await db.SaveChangesAsync();
        db.AddAudit(HttpContext, "إضافة منطقة (من طلب فريق)", nameof(RegionLevel.Area), area.Id, $"{nameAr} — طلب #{app.Id}");
        return area.Id;
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string rowVersion, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes) || notes.Trim().Length < 5)
        {
            TempData["Error"] = "يرجى كتابة سبب الرفض (5 أحرف على الأقل) ليتمكن صاحب الطلب من التصحيح.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var app = await db.TeamApplications.FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();
        if (app.Status != TeamStatus.Pending)
        {
            TempData["Error"] = "تمت مراجعة هذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Details), new { id });
        }

        db.SetOriginalRowVersion(app, rowVersion, a => a.RowVersion);
        app.Status = TeamStatus.Rejected;
        app.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        app.ReviewedAt = DateTime.UtcNow;
        app.ReviewNotes = notes.Trim()[..Math.Min(notes.Trim().Length, 1000)];

        db.AddNotification(app.ApplicantUserId, NotificationType.TeamApplication,
            $"تم رفض طلب تسجيل فريق {app.NameAr}", app.ReviewNotes, $"/TeamApplications/Details/{app.Id}");
        db.AddAudit(HttpContext, "رفض طلب فريق", nameof(TeamApplication), app.Id, $"{app.NameAr}: {app.ReviewNotes}");

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل الطلب من مستخدم آخر أثناء المراجعة. يرجى مراجعته مجدداً.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "تم رفض الطلب وإشعار صاحبه بالسبب.";
        return RedirectToAction(nameof(Index));
    }
}
