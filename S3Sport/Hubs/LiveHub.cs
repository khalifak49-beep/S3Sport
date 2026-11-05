using Microsoft.AspNetCore.SignalR;

namespace S3Sport.Hubs;

/// <summary>
/// Real-time channel. Clients join "match-{id}" (match page), "live" (score tickers on lists).
/// Personal notifications go to Clients.User(userId) (Identity's NameIdentifier claim).
/// The hub only manages subscriptions; all updates are pushed from controllers after the database commit.
/// </summary>
public class LiveHub : Hub
{
    public const string LiveGroup = "live";
    public static string MatchGroup(int matchId) => $"match-{matchId}";

    public Task JoinMatch(int matchId) => Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(matchId));
    public Task LeaveMatch(int matchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroup(matchId));
    public Task JoinLive() => Groups.AddToGroupAsync(Context.ConnectionId, LiveGroup);
}
