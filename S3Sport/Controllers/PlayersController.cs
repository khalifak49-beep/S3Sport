using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Public players directory and player profile (career, statistics per season, transfers).</summary>
public class PlayersController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? teamId, bool all = false, int page = 1)
    {
        var region = CurrentRegion.FromRequest(Request);
        var query = db.Players.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.FullNameAr.Contains(s) || (p.FullNameEn != null && p.FullNameEn.Contains(s)));
        }
        if (teamId.HasValue) query = query.Where(p => p.TeamPlayers.Any(tp => tp.IsActive && tp.TeamId == teamId));
        else if (!all && region != null && string.IsNullOrWhiteSpace(search))
        {
            var teamsInRegion = db.Teams.Where(t => t.Status == TeamStatus.Approved).InRegion(region).Select(t => t.Id);
            query = query.Where(p => p.TeamPlayers.Any(tp => tp.IsActive && teamsInRegion.Contains(tp.TeamId)));
        }

        ViewData["All"] = all || region == null;
        ViewData["HasRegion"] = region != null;
        return View(new PlayerListViewModel
        {
            Results = await PagedResult<PlayerListItem>.CreateAsync(query.OrderBy(p => p.FullNameAr).ToListItems(db), page, 24),
            Search = search, TeamId = teamId,
            Teams = await db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved).OrderBy(t => t.NameAr)
                .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync()
        });
    }

    [HttpGet("/players/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = await PlayerHelper.LoadDetailsAsync(db, id);
        if (model == null) return NotFound();
        model.Follow = await FollowHelper.BuildAsync(db, User.GetUserId(), FollowTargetType.Player, id, compact: false);
        model.CanAdminEdit = User.IsAdmin();
        return View(model);
    }
}
