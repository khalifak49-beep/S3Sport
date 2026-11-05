using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Hubs;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>
/// Fan-out of follower notifications: stores one Notification row per recipient and pushes it in real time.
/// Call after the triggering change has been committed, so nobody is notified about something that rolled back.
/// The stored rows are also the Web Push outbox: PushWorker delivers them to the recipients' devices.
/// </summary>
public static class NotificationHelper
{
    /// <summary>
    /// Followers of a match or of either team. An explicit match follow decides on its own
    /// (muting a match silences it even if the team is followed); otherwise any team follow with notifications on counts.
    /// </summary>
    public static async Task<int> NotifyMatchFollowersAsync(ApplicationDbContext db, IHubContext<LiveHub> hub,
        int matchId, NotificationType type, string title, string? message)
    {
        var teams = await db.Matches.AsNoTracking().IgnoreQueryFilters().Where(m => m.Id == matchId)
            .Select(m => new { m.HomeTeamId, m.AwayTeamId }).FirstOrDefaultAsync();
        if (teams == null) return 0;

        var follows = await db.UserFollows.AsNoTracking()
            .Where(f => f.MatchId == matchId || f.TeamId == teams.HomeTeamId || f.TeamId == teams.AwayTeamId)
            .Select(f => new { f.UserId, f.MatchId, f.NotificationsEnabled })
            .ToListAsync();
        var recipients = follows.GroupBy(f => f.UserId)
            .Where(g => g.Any(f => f.MatchId != null) ? g.First(f => f.MatchId != null).NotificationsEnabled : g.Any(f => f.NotificationsEnabled))
            .Select(g => g.Key).ToList();

        return await SendAsync(db, hub, recipients, type, title, message, $"/matches/{matchId}", matchId: matchId);
    }

    /// <summary>Followers (with notifications on) of any of the given teams, players or competition.</summary>
    public static async Task<int> NotifyFollowersAsync(ApplicationDbContext db, IHubContext<LiveHub> hub,
        IReadOnlyCollection<int> teamIds, IReadOnlyCollection<int> playerIds, int? competitionId,
        NotificationType type, string title, string? message, string url, int? newsId = null)
    {
        var teamArr = teamIds.ToArray();
        var playerArr = playerIds.ToArray();
        var recipients = await db.UserFollows.AsNoTracking()
            .Where(f => f.NotificationsEnabled && (
                (f.TeamId != null && teamArr.Contains(f.TeamId.Value)) ||
                (f.PlayerId != null && playerArr.Contains(f.PlayerId.Value)) ||
                (competitionId != null && f.CompetitionId == competitionId)))
            .Select(f => f.UserId).Distinct().ToListAsync();
        return await SendAsync(db, hub, recipients, type, title, message, url, newsId: newsId,
            teamId: teamIds.Count == 1 ? teamIds.First() : null);
    }

    private static async Task<int> SendAsync(ApplicationDbContext db, IHubContext<LiveHub> hub, List<string> userIds,
        NotificationType type, string title, string? message, string url, int? matchId = null, int? newsId = null, int? teamId = null)
    {
        if (userIds.Count == 0) return 0;
        foreach (var userId in userIds)
            db.Notifications.Add(new Notification
            {
                UserId = userId, Type = type, Title = title, Message = message, Url = url,
                MatchId = matchId, NewsId = newsId, TeamId = teamId
            });
        await db.SaveChangesAsync();
        PushWorker.Wake();

        await hub.Clients.Users(userIds).SendAsync("notification", new { type = type.ToString(), title, message, url });
        return userIds.Count;
    }
}
