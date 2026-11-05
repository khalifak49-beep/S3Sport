using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class DashboardController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var model = new DashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            NewUsersThisWeek = await db.Users.CountAsync(u => u.CreatedAt >= now.AddDays(-7)),
            ApprovedTeams = await db.Teams.CountAsync(t => t.Status == TeamStatus.Approved),
            PendingApplications = await db.TeamApplications.CountAsync(a => a.Status == TeamStatus.Pending),
            ActiveCompetitions = await db.CompetitionSeasons.CountAsync(c => c.Status == CompetitionSeasonStatus.InProgress),
            UpcomingMatches = await db.Matches.CountAsync(m => m.Status == MatchStatus.Scheduled && m.KickoffAt >= now),
            LiveMatches = await db.Matches.CountAsync(m => m.Status == MatchStatus.Live || m.Status == MatchStatus.HalfTime),
            TotalPlayers = await db.Players.CountAsync(),

            LatestApplications = await db.TeamApplications.AsNoTracking()
                .Where(a => a.Status == TeamStatus.Pending)
                .OrderBy(a => a.SubmittedAt).Take(5)
                .Select(a => new TeamApplicationListItem
                {
                    Id = a.Id, NameAr = a.NameAr, LogoPath = a.LogoPath, ApplicantName = a.ApplicantUser.FullName,
                    RegionText = (a.Area != null ? a.Area.NameAr : a.ProposedAreaName) + "، " + a.Wilayat.NameAr, SubmittedAt = a.SubmittedAt, Status = a.Status
                }).ToListAsync(),

            RecentActivity = await db.AuditLogs.AsNoTracking()
                .OrderByDescending(l => l.Id).Take(8)
                .Select(l => new AuditLogRow
                {
                    Id = l.Id, CreatedAt = l.CreatedAt, UserName = l.UserName, Action = l.Action,
                    EntityName = l.EntityName, EntityId = l.EntityId, Details = l.Details
                }).ToListAsync(),

            TeamsPerGovernorate = await db.Governorates.AsNoTracking()
                .OrderBy(g => g.SortOrder)
                .Select(g => new ChartPoint(g.NameAr, db.Teams.Count(t => t.Status == TeamStatus.Approved
                    && t.TeamAreas.Any(ta => ta.IsPrimary && ta.Area.Wilayat.GovernorateId == g.Id))))
                .ToListAsync()
        };

        var roleCounts = await db.UserRoles.AsNoTracking()
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .GroupBy(n => n).Select(g => new { Role = g.Key, Count = g.Count() }).ToListAsync();
        model.UsersPerRole = AppRoles.All
            .Select(r => new ChartPoint(AppRoles.DisplayNames[r], roleCounts.FirstOrDefault(x => x.Role == r)?.Count ?? 0))
            .ToList();

        return View(model);
    }
}
