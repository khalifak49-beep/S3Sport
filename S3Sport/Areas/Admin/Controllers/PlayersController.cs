using Microsoft.AspNetCore.Authorization;
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
public class PlayersController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? teamId, bool freeAgents = false, int page = 1)
    {
        var query = db.Players.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.FullNameAr.Contains(s) || (p.FullNameEn != null && p.FullNameEn.Contains(s)));
        }
        if (teamId.HasValue) query = query.Where(p => p.TeamPlayers.Any(tp => tp.IsActive && tp.TeamId == teamId));
        if (freeAgents) query = query.Where(p => !p.TeamPlayers.Any(tp => tp.IsActive));

        return View(new PlayerListViewModel
        {
            Results = await PagedResult<PlayerListItem>.CreateAsync(query.OrderBy(p => p.FullNameAr).ToListItems(db), page, 25),
            Search = search, TeamId = teamId, FreeAgentsOnly = freeAgents,
            Teams = await TeamOptionsAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await PlayerHelper.LoadDetailsAsync(db, id);
        if (model == null) return NotFound();
        model.TransferTeams = await TeamOptionsAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? teamId)
    {
        var model = new PlayerFormViewModel { TeamId = teamId, JoinedOn = DateOnly.FromDateTime(DateTime.UtcNow) };
        await PlayerHelper.FillListsAsync(db, model, includeTeams: true);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Players.AsNoTracking().Include(x => x.TeamPlayers.Where(tp => tp.IsActive)).ThenInclude(tp => tp.Team)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var active = p.TeamPlayers.FirstOrDefault();
        var model = new PlayerFormViewModel
        {
            Id = p.Id, FullNameAr = p.FullNameAr, FullNameEn = p.FullNameEn, DateOfBirth = p.DateOfBirth, Nationality = p.Nationality,
            PositionId = p.PositionId, Bio = p.Bio, ExistingPhotoPath = p.PhotoPath, RowVersion = Convert.ToBase64String(p.RowVersion),
            TeamId = active?.TeamId, TeamName = active?.Team.NameAr, ShirtNumber = active?.ShirtNumber, IsCaptain = active?.IsCaptain ?? false
        };
        await PlayerHelper.FillListsAsync(db, model, includeTeams: true);
        return View(model);
    }

    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Save(PlayerFormViewModel model)
    {
        if (model.Id != 0)
        {
            // On edit, the team can only change through a transfer; keep the current one.
            model.TeamId = await db.TeamPlayers.Where(tp => tp.PlayerId == model.Id && tp.IsActive).Select(tp => (int?)tp.TeamId).FirstOrDefaultAsync();
            if (!await db.Players.AnyAsync(p => p.Id == model.Id)) return NotFound();
        }
        await PlayerHelper.ValidateAsync(db, model, ModelState);
        if (!ModelState.IsValid)
        {
            await PlayerHelper.FillListsAsync(db, model, includeTeams: true);
            return View("Edit", model);
        }

        Player player;
        try
        {
            player = await PlayerHelper.SaveAsync(db, env, model);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "تم تعديل بيانات اللاعب من مستخدم آخر. أعد المحاولة.";
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }
        db.AddAudit(HttpContext, model.Id == 0 ? "إضافة لاعب" : "تعديل لاعب", nameof(Player), player.Id, player.FullNameAr);
        await db.SaveChangesAsync();

        TempData["Success"] = "تم حفظ بيانات اللاعب.";
        return RedirectToAction(nameof(Details), new { id = player.Id });
    }

    /// <summary>Transfer to another team, or release (toTeamId empty). History is kept.</summary>
    [HttpPost]
    public async Task<IActionResult> Transfer(int id, int? toTeamId, DateOnly? transferDate, int? shirtNumber, string? notes)
    {
        if (!ModelState.IsValid || shirtNumber is < 1 or > 99) { TempData["Error"] = "بيانات الانتقال غير صالحة (رقم القميص بين 1 و99)."; return RedirectToAction(nameof(Details), new { id }); }
        string? error = null;
        await db.InTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();
            error = await PlayerHelper.TransferAsync(db, id, toTeamId is > 0 ? toTeamId : null,
                transferDate ?? DateOnly.FromDateTime(DateTime.UtcNow), shirtNumber, notes, User.GetUserId());
            if (error != null) return;
            db.AddAudit(HttpContext, toTeamId is > 0 ? "انتقال لاعب" : "إنهاء ارتباط لاعب", nameof(Player), id, notes);
            await db.SaveChangesAsync();
        });
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = toTeamId is > 0 ? "تم تسجيل الانتقال." : "تم إنهاء ارتباط اللاعب بالفريق.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Players.FindAsync(id);
        if (p == null) return NotFound();
        if (await db.MatchEvents.AnyAsync(e => e.PlayerId == id || e.RelatedPlayerId == id) || await db.MatchLineups.AnyAsync(l => l.PlayerId == id))
        {
            TempData["Error"] = "لا يمكن حذف لاعب له مشاركات في مباريات، حفاظاً على الإحصائيات.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await db.InTransactionAsync(async () =>
        {
            await db.TeamPlayers.Where(tp => tp.PlayerId == id && tp.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.LeftOn, DateOnly.FromDateTime(DateTime.UtcNow)));
            db.Players.Remove(p); // soft delete
            db.AddAudit(HttpContext, "حذف لاعب", nameof(Player), id, p.FullNameAr);
            await db.SaveChangesAsync();
        });
        TempData["Success"] = "تم حذف اللاعب.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> TeamOptionsAsync() =>
        await db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved).OrderBy(t => t.NameAr)
            .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
}
