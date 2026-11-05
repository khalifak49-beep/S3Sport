using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>
/// Resource-level authorization on top of the role policies:
/// a TeamManager only manages teams they belong to, a TournamentManager only competitions assigned to them,
/// and a MatchOperator only matches assigned to them. Admins can manage everything.
/// </summary>
public static class OwnershipHelper
{
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.SuperAdmin) || user.IsInRole(AppRoles.Admin);

    public static string? GetUserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static async Task<bool> CanManageTeamAsync(this ApplicationDbContext db, ClaimsPrincipal user, int teamId)
    {
        if (user.IsAdmin()) return true;
        var uid = user.GetUserId();
        return uid != null && user.IsInRole(AppRoles.TeamManager)
            && await db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == uid && m.IsActive);
    }

    public static IQueryable<Competition> ManageableCompetitions(this ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (user.IsAdmin()) return db.Competitions;
        var uid = user.GetUserId();
        return db.Competitions.Where(c => c.Managers.Any(m => m.UserId == uid));
    }

    public static async Task<bool> CanManageCompetitionAsync(this ApplicationDbContext db, ClaimsPrincipal user, int competitionId) =>
        user.IsAdmin() || await db.ManageableCompetitions(user).AnyAsync(c => c.Id == competitionId);

    public static async Task<bool> CanManageEditionAsync(this ApplicationDbContext db, ClaimsPrincipal user, int editionId)
    {
        if (user.IsAdmin()) return true;
        var uid = user.GetUserId();
        return await db.CompetitionSeasons.AnyAsync(cs => cs.Id == editionId && cs.Competition.Managers.Any(m => m.UserId == uid));
    }

    /// <summary>Matches a user may schedule/edit (admins: all; tournament managers: their competitions' matches).</summary>
    public static IQueryable<Match> ManageableMatches(this ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (user.IsAdmin()) return db.Matches;
        var uid = user.GetUserId();
        return db.Matches.Where(m => m.CompetitionSeason != null && m.CompetitionSeason.Competition.Managers.Any(x => x.UserId == uid));
    }

    /// <summary>Friendlies a team manager created for one of their teams (they schedule and edit them).</summary>
    public static IQueryable<Match> OrganizedMatches(this ApplicationDbContext db, ClaimsPrincipal user, int teamId)
    {
        if (user.IsAdmin()) return db.Matches.Where(m => m.OrganizerTeamId == teamId);
        var uid = user.GetUserId();
        var isTeamManager = user.IsInRole(AppRoles.TeamManager);
        return db.Matches.Where(m => isTeamManager && m.OrganizerTeamId == teamId
            && m.OrganizerTeam!.Members.Any(x => x.UserId == uid && x.IsActive));
    }

    /// <summary>
    /// Matches a user may record live events for: manageable matches, those assigned to them as operator,
    /// and friendlies organised by a team they manage.
    /// </summary>
    public static IQueryable<Match> OperableMatches(this ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (user.IsAdmin()) return db.Matches;
        var uid = user.GetUserId();
        var isTeamManager = user.IsInRole(AppRoles.TeamManager);
        return db.Matches.Where(m => m.OperatorUserId == uid
            || (m.CompetitionSeason != null && m.CompetitionSeason.Competition.Managers.Any(x => x.UserId == uid))
            || (isTeamManager && m.OrganizerTeamId != null && m.OrganizerTeam!.Members.Any(x => x.UserId == uid && x.IsActive)));
    }

    public static Task<bool> CanOperateMatchAsync(this ApplicationDbContext db, ClaimsPrincipal user, int matchId) =>
        db.OperableMatches(user).AnyAsync(m => m.Id == matchId);
}
