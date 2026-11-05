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

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class TeamsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment env) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, TeamStatus? status, int? governorateId, string sort = "name", int page = 1)
    {
        var query = db.Teams.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => t.NameAr.Contains(s) || t.NameEn.Contains(s) || t.TeamAreas.Any(ta => ta.Area.NameAr.Contains(s)));
        }
        if (status.HasValue) query = query.Where(t => t.Status == status);
        if (governorateId.HasValue) query = query.InRegion(new CurrentRegion(governorateId.Value, null, null, null));

        query = sort switch
        {
            "newest" => query.OrderByDescending(t => t.CreatedAt),
            "oldest" => query.OrderBy(t => t.CreatedAt),
            "players" => query.OrderByDescending(t => t.TeamPlayers.Count(p => p.IsActive)),
            _ => query.OrderBy(t => t.NameAr)
        };

        var projected = query.Select(t => new TeamListItem
        {
            Id = t.Id, NameAr = t.NameAr, NameEn = t.NameEn, LogoPath = t.LogoPath, Status = t.Status, CreatedAt = t.CreatedAt,
            PrimaryAreaText = t.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).FirstOrDefault(),
            PlayersCount = t.TeamPlayers.Count(p => p.IsActive),
            ManagerName = t.Members.Where(m => m.IsActive && m.Role == TeamMemberRole.Manager).Select(m => m.User.FullName).FirstOrDefault()
        });

        return View(new TeamListViewModel
        {
            Results = await PagedResult<TeamListItem>.CreateAsync(projected, page),
            Search = search, Status = status, GovernorateId = governorateId, Sort = sort,
            Governorates = await db.Governorates.AsNoTracking().OrderBy(g => g.SortOrder)
                .Select(g => new SelectListItem(g.NameAr, g.Id.ToString())).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new TeamEditViewModel();
        await TeamFormHelper.FillListsAsync(db, model);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var team = await db.Teams.AsNoTracking()
            .Include(t => t.TeamAreas).ThenInclude(ta => ta.Area).ThenInclude(a => a.Wilayat)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var primary = team.TeamAreas.FirstOrDefault(ta => ta.IsPrimary) ?? team.TeamAreas.FirstOrDefault();
        var model = new TeamEditViewModel
        {
            Id = team.Id, Slug = team.Slug, NameAr = team.NameAr, NameEn = team.NameEn, ShortName = team.ShortName,
            FoundedYear = team.FoundedYear, Description = team.Description, ContactPhone = team.ContactPhone,
            ContactEmail = team.ContactEmail, SocialUrl = team.SocialUrl, PrimaryColor = team.PrimaryColor,
            Status = team.Status, ExistingLogoPath = team.LogoPath, VillageId = team.VillageId,
            AreaId = primary?.AreaId, WilayatId = primary?.Area.WilayatId ?? 0, GovernorateId = primary?.Area.Wilayat.GovernorateId ?? 0,
            AdditionalAreaIds = team.TeamAreas.Where(ta => !ta.IsPrimary).Select(ta => ta.AreaId).ToList(),
            RowVersion = Convert.ToBase64String(team.RowVersion)
        };
        await FillEditAsync(model);
        return View(model);
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Save(TeamEditViewModel model)
    {
        var isNew = model.Id == 0;
        Team? team = null;
        if (!isNew)
        {
            team = await db.Teams.Include(t => t.TeamAreas).FirstOrDefaultAsync(t => t.Id == model.Id);
            if (team == null) return NotFound();
        }

        await TeamFormHelper.ValidateRegionAsync(db, model, ModelState); // admin form: an existing area is required
        await TeamFormHelper.ValidateAdditionalAreasAsync(db, model, ModelState);
        TeamFormHelper.ValidateLogo(model, ModelState, required: false);
        if (model.Status == TeamStatus.Pending)
            ModelState.AddModelError(nameof(model.Status), "حالة \"قيد المراجعة\" خاصة بطلبات التسجيل.");

        if (!ModelState.IsValid)
        {
            model.ExistingLogoPath = team?.LogoPath;
            await FillEditAsync(model);
            return View("Edit", model);
        }

        if (isNew)
        {
            team = new Team
            {
                SportId = SeedData.FootballSportId,
                Slug = await SlugHelper.GenerateUniqueAsync(model.NameEn, s => db.Teams.IgnoreQueryFilters().AnyAsync(t => t.Slug == s), "team"),
                ApprovedAt = DateTime.UtcNow
            };
            db.Teams.Add(team);
        }
        else if (!string.IsNullOrEmpty(model.RowVersion))
        {
            db.SetOriginalRowVersion(team!, model.RowVersion, t => t.RowVersion);
        }

        string? logoToDelete = null;
        if (model.Logo is { Length: > 0 })
        {
            logoToDelete = team!.LogoPath;
            team.LogoPath = await FileUploadHelper.SaveImageAsync(model.Logo, env, "teams");
        }
        else if (model.RemoveLogo)
        {
            logoToDelete = team!.LogoPath;
            team.LogoPath = null;
        }

        team!.NameAr = model.NameAr.Trim();
        team.NameEn = model.NameEn.Trim();
        team.ShortName = model.ShortName?.Trim();
        team.FoundedYear = model.FoundedYear;
        team.Description = model.Description?.Trim();
        team.ContactPhone = model.ContactPhone?.Trim();
        team.ContactEmail = model.ContactEmail?.Trim();
        team.SocialUrl = model.SocialUrl?.Trim();
        team.PrimaryColor = model.PrimaryColor;
        team.VillageId = model.VillageId;
        if (team.Status != model.Status && model.Status == TeamStatus.Approved) team.ApprovedAt ??= DateTime.UtcNow;
        team.Status = model.Status;
        SyncAreas(team, model.AreaId!.Value, model.AdditionalAreaIds);

        db.AddAudit(HttpContext, isNew ? "إضافة فريق" : "تعديل فريق", nameof(Team), isNew ? null : team.Id, team.NameAr);
        try
        {
            await db.InTransactionAsync(async () =>
            {
                // Clear the stored primary flag first: the filtered unique index allows one primary
                // area per team, and EF does not guarantee the order of the UPDATE statements.
                if (!isNew)
                {
                    await db.TeamAreas.Where(ta => ta.TeamId == team.Id && ta.IsPrimary)
                        .ExecuteUpdateAsync(s => s.SetProperty(ta => ta.IsPrimary, false));
                    // An unchanged primary row would not be re-written by SaveChanges, so force it.
                    foreach (var ta in team.TeamAreas.Where(ta => ta.IsPrimary && db.Entry(ta).State == EntityState.Unchanged))
                        db.Entry(ta).Property(x => x.IsPrimary).IsModified = true;
                }
                await db.SaveChangesAsync();
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            if (model.Logo is { Length: > 0 }) FileUploadHelper.Delete(env, team.LogoPath);
            TempData["Error"] = "تم تعديل بيانات الفريق من مستخدم آخر. تم تحميل أحدث نسخة، يرجى إعادة التعديل.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        // Keep the file if the originating application still references it.
        if (logoToDelete != null && !await db.TeamApplications.AnyAsync(a => a.LogoPath == logoToDelete))
            FileUploadHelper.Delete(env, logoToDelete);

        TempData["Success"] = isNew ? "تمت إضافة الفريق." : "تم حفظ بيانات الفريق.";
        return RedirectToAction(nameof(Edit), new { id = team.Id });
    }

    /// <summary>Suspend / re-activate a team and notify its managers.</summary>
    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, TeamStatus status)
    {
        if (status is not (TeamStatus.Approved or TeamStatus.Suspended)) return BadRequest();
        var team = await db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        team.Status = status;
        var title = status == TeamStatus.Suspended ? $"تم إيقاف فريق {team.NameAr}" : $"تم إعادة تفعيل فريق {team.NameAr}";
        foreach (var m in team.Members.Where(m => m.IsActive))
            db.AddNotification(m.UserId, NotificationType.General, title, teamId: team.Id);
        db.AddAudit(HttpContext, status == TeamStatus.Suspended ? "إيقاف فريق" : "تفعيل فريق", nameof(Team), team.Id, team.NameAr);
        await db.SaveChangesAsync();

        TempData["Success"] = title;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        if (await db.Matches.AnyAsync(m => m.HomeTeamId == id || m.AwayTeamId == id))
        {
            TempData["Error"] = "لا يمكن حذف فريق له مباريات مسجلة، حفاظاً على سجل النتائج. يمكنك إيقافه بدلاً من ذلك.";
            return RedirectToAction(nameof(Index));
        }

        db.Teams.Remove(team); // soft delete (see ApplicationDbContext)
        db.AddAudit(HttpContext, "حذف فريق", nameof(Team), team.Id, team.NameAr);
        await db.SaveChangesAsync();
        TempData["Success"] = $"تم حذف الفريق «{team.NameAr}».";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(int id, string email, TeamMemberRole role)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var user = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            TempData["Error"] = "لا يوجد مستخدم مسجل بهذا البريد الإلكتروني.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == user.Id);
        if (member == null)
            db.TeamMembers.Add(new TeamMember { TeamId = id, UserId = user.Id, Role = role });
        else
        {
            member.Role = role;
            member.IsActive = true;
        }

        db.AddNotification(user.Id, NotificationType.General, $"تمت إضافتك إلى إدارة فريق {team.NameAr}", teamId: id);
        db.AddAudit(HttpContext, "إضافة مسؤول لفريق", nameof(Team), id, $"{user.Email} ({role.GetDisplayName()})");
        await db.SaveChangesAsync();

        if (!await userManager.IsInRoleAsync(user, AppRoles.TeamManager))
            await userManager.AddToRoleAsync(user, AppRoles.TeamManager);

        TempData["Success"] = $"تمت إضافة {user.FullName} إلى إدارة الفريق.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        var member = await db.TeamMembers.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == id);
        if (member == null) return NotFound();

        db.TeamMembers.Remove(member);
        db.AddAudit(HttpContext, "إزالة مسؤول من فريق", nameof(Team), id, member.User.Email);
        await db.SaveChangesAsync();

        // Drop the TeamManager role once the user no longer manages any team.
        if (!await db.TeamMembers.AnyAsync(m => m.UserId == member.UserId && m.IsActive)
            && await userManager.IsInRoleAsync(member.User, AppRoles.TeamManager))
            await userManager.RemoveFromRoleAsync(member.User, AppRoles.TeamManager);

        TempData["Success"] = "تمت إزالة المسؤول من الفريق.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ---------- helpers ----------

    private async Task FillEditAsync(TeamEditViewModel model)
    {
        await TeamFormHelper.FillListsAsync(db, model);
        if (model.Id == 0) return;
        model.Members = await db.TeamMembers.AsNoTracking().Where(m => m.TeamId == model.Id)
            .OrderBy(m => m.Role).ThenBy(m => m.User.FullName)
            .Select(m => new TeamMemberRow { Id = m.Id, FullName = m.User.FullName, Email = m.User.Email, Role = m.Role, IsActive = m.IsActive })
            .ToListAsync();
        model.Slug ??= await db.Teams.Where(t => t.Id == model.Id).Select(t => t.Slug).FirstOrDefaultAsync();
    }

    private static void SyncAreas(Team team, int primaryAreaId, IEnumerable<int> additionalAreaIds)
    {
        var wanted = additionalAreaIds.Append(primaryAreaId).ToHashSet();
        foreach (var ta in team.TeamAreas.Where(ta => !wanted.Contains(ta.AreaId)).ToList())
            team.TeamAreas.Remove(ta);
        foreach (var areaId in wanted.Where(id => team.TeamAreas.All(ta => ta.AreaId != id)))
            team.TeamAreas.Add(new TeamArea { AreaId = areaId });
        foreach (var ta in team.TeamAreas)
            ta.IsPrimary = ta.AreaId == primaryAreaId;
    }
}
