using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

/// <summary>
/// Live-match rules. The match state (status, period, score, winner) is always *derived* from the
/// non-cancelled events, so adding, correcting or cancelling any event keeps everything consistent.
/// </summary>
public static class MatchLiveHelper
{
    public static readonly MatchEventType[] StateEvents =
        [MatchEventType.MatchStarted, MatchEventType.FirstHalfEnded, MatchEventType.SecondHalfStarted, MatchEventType.MatchEnded];

    public static readonly MatchEventType[] GoalEvents = [MatchEventType.Goal, MatchEventType.PenaltyGoal, MatchEventType.OwnGoal];

    public static bool IsStateEvent(MatchEventType t) => StateEvents.Contains(t);

    /// <summary>The state event allowed next, or null when the match is over.</summary>
    public static MatchEventType? NextStateEvent(MatchStatus status, int period) => status switch
    {
        MatchStatus.Scheduled or MatchStatus.Postponed => MatchEventType.MatchStarted,
        MatchStatus.Live when period == 1 => MatchEventType.FirstHalfEnded,
        MatchStatus.HalfTime => MatchEventType.SecondHalfStarted,
        MatchStatus.Live => MatchEventType.MatchEnded,
        _ => null
    };

    /// <summary>Validates a new event against the current state. Returns an Arabic error or null.</summary>
    public static string? Validate(Match match, MatchEventType type, int? teamId, int? playerId, int? relatedPlayerId,
        string? playerName = null, string? relatedPlayerName = null, bool hasRoster = true)
    {
        if (match.Status == MatchStatus.Cancelled) return "المباراة ملغاة.";
        if (IsStateEvent(type))
        {
            var expected = NextStateEvent(match.Status, match.CurrentPeriod);
            return expected == type ? null : expected == null
                ? "المباراة منتهية."
                : $"الإجراء التالي المتاح هو «{expected.Value.GetDisplayName()}».";
        }
        if (match.Status is not (MatchStatus.Live or MatchStatus.HalfTime) && match.Status != MatchStatus.Finished)
            return "ابدأ المباراة أولاً.";
        if (match.Status == MatchStatus.Finished) return "المباراة منتهية؛ استخدم تصحيح الأحداث.";
        if (match.Status == MatchStatus.HalfTime && type != MatchEventType.Substitution) return "الاستراحة: يمكن تسجيل التبديلات فقط.";
        if (teamId != match.HomeTeamId && teamId != match.AwayTeamId) return "اختر الفريق.";
        // A player is an id from the team's squad or a typed shirt number / name.
        var playerOut = playerId?.ToString() ?? playerName;
        var playerIn = relatedPlayerId?.ToString() ?? relatedPlayerName;
        // A team without registered players may record a substitution without naming anyone.
        if (type == MatchEventType.Substitution && hasRoster && (playerOut == null || playerIn == null))
            return "التبديل يحتاج اللاعب الخارج واللاعب الداخل.";
        if (playerIn != null && playerIn == playerOut) return "لا يمكن أن يكون اللاعب نفسه.";
        return null;
    }

    /// <summary>Re-derives status, period, timestamps, score and winner from the non-cancelled events.</summary>
    public static void Recompute(Match match, IReadOnlyCollection<MatchEvent> allEvents)
    {
        var events = allEvents.Where(e => !e.IsCancelled).ToList();
        DateTime? At(MatchEventType t) => events.Where(e => e.EventType == t).Select(e => (DateTime?)e.CreatedAt).FirstOrDefault();

        var started = At(MatchEventType.MatchStarted);
        var halfEnded = At(MatchEventType.FirstHalfEnded);
        var secondStarted = At(MatchEventType.SecondHalfStarted);
        var ended = At(MatchEventType.MatchEnded);

        if (ended != null) { match.Status = MatchStatus.Finished; match.CurrentPeriod = 2; match.FinishedAt = ended; }
        else if (secondStarted != null) { match.Status = MatchStatus.Live; match.CurrentPeriod = 2; match.CurrentPeriodStartedAt = secondStarted; match.FinishedAt = null; }
        else if (halfEnded != null) { match.Status = MatchStatus.HalfTime; match.CurrentPeriod = 1; match.FinishedAt = null; }
        else if (started != null) { match.Status = MatchStatus.Live; match.CurrentPeriod = 1; match.CurrentPeriodStartedAt = started; match.FinishedAt = null; }
        else
        {
            if (match.Status is MatchStatus.Live or MatchStatus.HalfTime or MatchStatus.Finished) match.Status = MatchStatus.Scheduled;
            match.CurrentPeriod = 0;
            match.CurrentPeriodStartedAt = null;
            match.FinishedAt = null;
        }
        match.StartedAt = started;

        match.HomeScore = events.Count(e => Scores(e, match.HomeTeamId, match.AwayTeamId));
        match.AwayScore = events.Count(e => Scores(e, match.AwayTeamId, match.HomeTeamId));
        if (match.Status != MatchStatus.Finished) { match.HomePenaltyScore = match.AwayPenaltyScore = null; }

        match.WinnerTeamId = match.Status != MatchStatus.Finished ? null
            : match.HomeScore != match.AwayScore ? (match.HomeScore > match.AwayScore ? match.HomeTeamId : match.AwayTeamId)
            : match.HomePenaltyScore != null && match.AwayPenaltyScore != null && match.HomePenaltyScore != match.AwayPenaltyScore
                ? (match.HomePenaltyScore > match.AwayPenaltyScore ? match.HomeTeamId : match.AwayTeamId)
                : null;
    }

    /// <summary>Goal for <paramref name="team"/>: its own goals, or an own goal by the opponent's player.</summary>
    private static bool Scores(MatchEvent e, int team, int opponent) =>
        (e.EventType is MatchEventType.Goal or MatchEventType.PenaltyGoal && e.TeamId == team) ||
        (e.EventType == MatchEventType.OwnGoal && e.TeamId == opponent);

    public static async Task<MatchLiveDto?> BuildDtoAsync(ApplicationDbContext db, int matchId, bool includeCancelled = false)
    {
        var m = await db.Matches.AsNoTracking().Where(x => x.Id == matchId)
            .Select(x => new
            {
                x.Id, x.Status, x.HomeTeamId, x.AwayTeamId, x.HomeScore, x.AwayScore, x.HomePenaltyScore, x.AwayPenaltyScore,
                x.CurrentPeriod, x.CurrentPeriodStartedAt, x.KickoffAt, x.Sport.PeriodDurationMinutes
            }).FirstOrDefaultAsync();
        if (m == null) return null;

        var events = await db.MatchEvents.AsNoTracking()
            .Where(e => e.MatchId == matchId && (includeCancelled || !e.IsCancelled))
            .OrderBy(e => e.Period).ThenBy(e => e.Minute).ThenBy(e => e.ExtraMinute).ThenBy(e => e.Id)
            .Select(e => new LiveEventDto
            {
                Id = e.Id,
                Type = e.EventType.ToString(),
                Side = e.TeamId == null ? null : e.TeamId == m.HomeTeamId ? "home" : "away",
                Minute = e.Minute, ExtraMinute = e.ExtraMinute, Period = e.Period,
                PlayerId = e.PlayerId, PlayerName = e.Player != null ? e.Player.FullNameAr : e.PlayerNameText,
                RelatedPlayerId = e.RelatedPlayerId, RelatedPlayerName = e.RelatedPlayer != null ? e.RelatedPlayer.FullNameAr : e.RelatedPlayerNameText,
                Notes = e.Notes, IsCancelled = e.IsCancelled, RevisionCount = e.Revisions.Count
            }).ToListAsync();
        foreach (var e in events) e.TypeText = Enum.Parse<MatchEventType>(e.Type).GetDisplayName();

        return new MatchLiveDto
        {
            MatchId = m.Id,
            Status = m.Status.ToString(),
            StatusText = m.Status.GetDisplayName(),
            HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId,
            HomeScore = m.HomeScore, AwayScore = m.AwayScore,
            HomePenaltyScore = m.HomePenaltyScore, AwayPenaltyScore = m.AwayPenaltyScore,
            Period = m.CurrentPeriod,
            PeriodDurationMinutes = m.PeriodDurationMinutes,
            PeriodStartedAt = m.CurrentPeriodStartedAt is { } p ? DateTime.SpecifyKind(p, DateTimeKind.Utc).ToString("O") : null,
            KickoffAt = DateTime.SpecifyKind(m.KickoffAt, DateTimeKind.Utc).ToString("O"),
            Events = events
        };
    }
}
