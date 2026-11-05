using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

/// <summary>Follow-state queries shared by the team pages, favorites and the follow endpoints.</summary>
public static class FollowHelper
{
    /// <summary>Follows of one target (by its type-specific FK).</summary>
    public static IQueryable<UserFollow> ForTarget(this IQueryable<UserFollow> follows, FollowTargetType type, int targetId) =>
        type switch
        {
            FollowTargetType.Team => follows.Where(f => f.TargetType == type && f.TeamId == targetId),
            FollowTargetType.Player => follows.Where(f => f.TargetType == type && f.PlayerId == targetId),
            FollowTargetType.Competition => follows.Where(f => f.TargetType == type && f.CompetitionId == targetId),
            _ => follows.Where(f => f.TargetType == type && f.MatchId == targetId)
        };

    public static void SetTarget(this UserFollow follow, FollowTargetType type, int targetId)
    {
        follow.TargetType = type;
        switch (type)
        {
            case FollowTargetType.Team: follow.TeamId = targetId; break;
            case FollowTargetType.Player: follow.PlayerId = targetId; break;
            case FollowTargetType.Competition: follow.CompetitionId = targetId; break;
            default: follow.MatchId = targetId; break;
        }
    }

    public static async Task<FollowButtonViewModel> BuildAsync(ApplicationDbContext db, string? userId, FollowTargetType type, int targetId, bool compact)
    {
        var follows = db.UserFollows.AsNoTracking().ForTarget(type, targetId);
        var mine = userId == null ? null : await follows.Where(f => f.UserId == userId)
            .Select(f => new { f.NotificationsEnabled }).FirstOrDefaultAsync();
        return new FollowButtonViewModel
        {
            TargetType = type,
            TargetId = targetId,
            IsAuthenticated = userId != null,
            IsFollowing = mine != null,
            NotificationsEnabled = mine?.NotificationsEnabled ?? false,
            FollowersCount = compact ? 0 : await follows.CountAsync(),
            Compact = compact
        };
    }

    /// <summary>Team card projection (logo, primary area, followers, whether the current user follows it).</summary>
    public static IQueryable<TeamCardViewModel> ToCards(this IQueryable<Team> teams, ApplicationDbContext db, string? userId) =>
        teams.Select(t => new TeamCardViewModel
        {
            Id = t.Id,
            Slug = t.Slug,
            NameAr = t.NameAr,
            LogoPath = t.LogoPath,
            FoundedYear = t.FoundedYear,
            AreaText = t.TeamAreas.Where(ta => ta.IsPrimary).Select(ta => ta.Area.NameAr + "، " + ta.Area.Wilayat.NameAr).FirstOrDefault(),
            FollowersCount = db.UserFollows.Count(f => f.TeamId == t.Id),
            IsFollowed = userId != null && db.UserFollows.Any(f => f.TeamId == t.Id && f.UserId == userId)
        });
}
