using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Controllers;

/// <summary>Everything the user follows, with per-item notification switches.</summary>
[Authorize]
public class FavoritesController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var teams = await db.UserFollows.AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetType == FollowTargetType.Team && f.Team!.Status == TeamStatus.Approved)
            .OrderBy(f => f.Team!.NameAr)
            .Select(f => new FavoriteTeamItem
            {
                TeamId = f.TeamId!.Value,
                Slug = f.Team!.Slug,
                NameAr = f.Team.NameAr,
                LogoPath = f.Team.LogoPath,
                AreaText = f.Team.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).FirstOrDefault(),
                FollowedAt = f.CreatedAt,
                Follow = new FollowButtonViewModel
                {
                    TargetType = FollowTargetType.Team, TargetId = f.TeamId.Value, IsAuthenticated = true,
                    IsFollowing = true, NotificationsEnabled = f.NotificationsEnabled, Compact = false,
                    FollowersCount = db.UserFollows.Count(x => x.TeamId == f.TeamId)
                }
            })
            .ToListAsync();

        FollowButtonViewModel Follow(FollowTargetType type, int id, bool notify) => new()
        {
            TargetType = type, TargetId = id, IsAuthenticated = true, IsFollowing = true, NotificationsEnabled = notify, Compact = false,
            FollowersCount = -1 // hidden in lists
        };

        var players = (await db.UserFollows.AsNoTracking()
                .Where(f => f.UserId == userId && f.TargetType == FollowTargetType.Player && f.Player != null)
                .OrderBy(f => f.Player!.FullNameAr)
                .Select(f => new
                {
                    Id = f.PlayerId!.Value, f.Player!.FullNameAr, f.Player.PhotoPath, f.NotificationsEnabled,
                    Team = f.Player.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.Team.NameAr).FirstOrDefault()
                }).ToListAsync())
            .Select(p => new FavoriteItem
            {
                Type = FollowTargetType.Player, TargetId = p.Id, Name = p.FullNameAr, Image = p.PhotoPath, Subtitle = p.Team ?? "لاعب حر",
                Url = $"/players/{p.Id}", Follow = Follow(FollowTargetType.Player, p.Id, p.NotificationsEnabled)
            }).ToList();

        var competitions = (await db.UserFollows.AsNoTracking()
                .Where(f => f.UserId == userId && f.TargetType == FollowTargetType.Competition && f.Competition != null)
                .OrderBy(f => f.Competition!.NameAr)
                .Select(f => new { Id = f.CompetitionId!.Value, f.Competition!.NameAr, f.Competition.Slug, f.Competition.LogoPath, f.NotificationsEnabled })
                .ToListAsync())
            .Select(c => new FavoriteItem
            {
                Type = FollowTargetType.Competition, TargetId = c.Id, Name = c.NameAr, Image = c.LogoPath,
                Url = $"/competitions/{c.Slug}", Follow = Follow(FollowTargetType.Competition, c.Id, c.NotificationsEnabled)
            }).ToList();

        // Followed matches that are upcoming or recent (older ones drop off the list).
        var since = DateTime.UtcNow.AddDays(-3);
        var matches = await db.Matches.AsNoTracking().Visible()
            .Where(m => m.KickoffAt >= since && db.UserFollows.Any(f => f.UserId == userId && f.MatchId == m.Id))
            .OrderBy(m => m.KickoffAt).ToRows().ToListAsync();

        return View(new FavoritesViewModel { Teams = teams, Players = players, Competitions = competitions, Matches = matches });
    }
}
