using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>
/// Rebuilds the materialised statistics of one competition season (standings, player and team statistics)
/// from its finished matches and non-cancelled events. A full rebuild keeps corrections trivially correct.
/// Callers wrap it in a transaction (InTransactionAsync) together with the change that triggered it.
/// </summary>
public static class StandingsCalculator
{
    public record MatchResult(int Id, int? StageId, int? GroupId, int HomeTeamId, int AwayTeamId, int HomeScore, int AwayScore, DateTime KickoffAt);

    public class TableRow
    {
        public int TeamId { get; init; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Adjustment { get; set; }
        public int Points { get; set; }
        public int FairPlayPoints { get; set; }
        public string Form { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    /// <summary>Card weights for the fair-play tie-breaker (fewer points ranks higher).</summary>
    private static int FairPlayWeight(MatchEventType type) => type switch
    {
        MatchEventType.YellowCard => 1,
        MatchEventType.SecondYellowCard => 3,
        MatchEventType.RedCard => 4,
        _ => 0
    };

    /// <summary>Ranks teams: points first, then the season's tie-break criteria in order, then name.</summary>
    public static List<TableRow> Rank(
        IReadOnlyCollection<int> teamIds, IReadOnlyList<MatchResult> matches,
        int pointsForWin, int pointsForDraw, int pointsForLoss,
        IReadOnlyList<TieBreakerCriterion> criteria,
        IReadOnlyDictionary<int, int> adjustments, IReadOnlyDictionary<int, int> fairPlay,
        IReadOnlyDictionary<int, string> names)
    {
        var rows = teamIds.Distinct().ToDictionary(id => id, id => new TableRow
        {
            TeamId = id,
            Adjustment = adjustments.GetValueOrDefault(id),
            FairPlayPoints = fairPlay.GetValueOrDefault(id)
        });

        foreach (var m in matches.OrderBy(m => m.KickoffAt))
        {
            if (!rows.TryGetValue(m.HomeTeamId, out var home) || !rows.TryGetValue(m.AwayTeamId, out var away)) continue;
            Apply(home, m.HomeScore, m.AwayScore);
            Apply(away, m.AwayScore, m.HomeScore);
        }

        foreach (var r in rows.Values)
        {
            r.Points = r.Won * pointsForWin + r.Drawn * pointsForDraw + r.Lost * pointsForLoss + r.Adjustment;
            if (r.Form.Length > 5) r.Form = r.Form[^5..];
        }

        var ranked = new List<TableRow>();
        foreach (var block in rows.Values.GroupBy(r => r.Points).OrderByDescending(g => g.Key))
        {
            var tied = block.ToList();
            if (tied.Count > 1) tied.Sort(BuildComparison(tied, matches, pointsForWin, pointsForDraw, pointsForLoss, criteria, names));
            ranked.AddRange(tied);
        }
        for (var i = 0; i < ranked.Count; i++) ranked[i].Position = i + 1;
        return ranked;

        static void Apply(TableRow row, int scored, int conceded)
        {
            row.Played++;
            row.GoalsFor += scored;
            row.GoalsAgainst += conceded;
            if (scored > conceded) { row.Won++; row.Form += "W"; }
            else if (scored == conceded) { row.Drawn++; row.Form += "D"; }
            else { row.Lost++; row.Form += "L"; }
        }
    }

    private static Comparison<TableRow> BuildComparison(
        List<TableRow> tied, IReadOnlyList<MatchResult> matches, int pw, int pd, int pl,
        IReadOnlyList<TieBreakerCriterion> criteria, IReadOnlyDictionary<int, string> names)
    {
        // Head-to-head mini table among the tied teams only.
        var ids = tied.Select(r => r.TeamId).ToHashSet();
        var h2h = ids.ToDictionary(id => id, _ => (Points: 0, GoalsFor: 0, GoalsAgainst: 0));
        foreach (var m in matches.Where(m => ids.Contains(m.HomeTeamId) && ids.Contains(m.AwayTeamId)))
        {
            var (hp, ap) = m.HomeScore > m.AwayScore ? (pw, pl) : m.HomeScore < m.AwayScore ? (pl, pw) : (pd, pd);
            var h = h2h[m.HomeTeamId]; h2h[m.HomeTeamId] = (h.Points + hp, h.GoalsFor + m.HomeScore, h.GoalsAgainst + m.AwayScore);
            var a = h2h[m.AwayTeamId]; h2h[m.AwayTeamId] = (a.Points + ap, a.GoalsFor + m.AwayScore, a.GoalsAgainst + m.HomeScore);
        }

        return (a, b) =>
        {
            foreach (var c in criteria)
            {
                var cmp = c switch
                {
                    TieBreakerCriterion.GoalDifference => b.GoalDifference.CompareTo(a.GoalDifference),
                    TieBreakerCriterion.GoalsFor => b.GoalsFor.CompareTo(a.GoalsFor),
                    TieBreakerCriterion.Wins => b.Won.CompareTo(a.Won),
                    TieBreakerCriterion.FairPlay => a.FairPlayPoints.CompareTo(b.FairPlayPoints),
                    TieBreakerCriterion.HeadToHeadPoints => h2h[b.TeamId].Points.CompareTo(h2h[a.TeamId].Points),
                    TieBreakerCriterion.HeadToHeadGoalDifference =>
                        (h2h[b.TeamId].GoalsFor - h2h[b.TeamId].GoalsAgainst).CompareTo(h2h[a.TeamId].GoalsFor - h2h[a.TeamId].GoalsAgainst),
                    TieBreakerCriterion.HeadToHeadGoalsFor => h2h[b.TeamId].GoalsFor.CompareTo(h2h[a.TeamId].GoalsFor),
                    _ => 0
                };
                if (cmp != 0) return cmp;
            }
            var byName = string.CompareOrdinal(names.GetValueOrDefault(a.TeamId), names.GetValueOrDefault(b.TeamId));
            return byName != 0 ? byName : a.TeamId.CompareTo(b.TeamId);
        };
    }

    public static async Task RecalculateAsync(ApplicationDbContext db, int editionId)
    {
        var edition = await db.CompetitionSeasons.AsNoTracking()
            .Include(s => s.Stages).ThenInclude(st => st.Groups)
            .FirstOrDefaultAsync(s => s.Id == editionId);
        if (edition == null) return;

        var participants = await db.CompetitionTeams.AsNoTracking()
            .Where(ct => ct.CompetitionSeasonId == editionId && !ct.IsWithdrawn)
            .Select(ct => new { ct.TeamId, ct.GroupId, ct.Team.NameAr })
            .ToListAsync();
        var names = participants.ToDictionary(p => p.TeamId, p => p.NameAr);

        var finished = await db.Matches.AsNoTracking()
            .Where(m => m.CompetitionSeasonId == editionId && m.Status == MatchStatus.Finished)
            .Select(m => new MatchResult(m.Id, m.StageId, m.GroupId, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore, m.KickoffAt))
            .ToListAsync();

        var events = await db.MatchEvents.AsNoTracking()
            .Where(e => !e.IsCancelled && e.Match.CompetitionSeasonId == editionId && e.Match.Status == MatchStatus.Finished)
            .Select(e => new { e.MatchId, e.EventType, e.TeamId, e.PlayerId, e.RelatedPlayerId })
            .ToListAsync();

        var fairPlay = events.Where(e => e.TeamId != null)
            .GroupBy(e => e.TeamId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => FairPlayWeight(e.EventType)));

        // Manual point adjustments survive a rebuild.
        var oldStandings = await db.CompetitionStandings.AsNoTracking()
            .Where(s => s.CompetitionSeasonId == editionId)
            .Select(s => new { s.StageId, s.GroupId, s.TeamId, s.PointsAdjustment })
            .ToListAsync();

        // ----- standings: one table per league stage / per group -----
        var tables = new List<(int? StageId, int? GroupId, List<int> TeamIds, List<MatchResult> Matches)>();
        var tableStages = edition.Stages.Where(s => s.StageType != StageType.Knockout).OrderBy(s => s.SortOrder).ToList();
        if (tableStages.Count == 0 && edition.Format != CompetitionFormat.Knockout)
            tables.Add((null, null, participants.Select(p => p.TeamId).ToList(), finished));
        foreach (var stage in tableStages)
        {
            if (stage.StageType == StageType.League)
                // Matches added without a stage (e.g. a rescheduled replay) still belong to the league table.
                tables.Add((stage.Id, null, participants.Select(p => p.TeamId).ToList(),
                    finished.Where(m => m.StageId == stage.Id || m.StageId == null).ToList()));
            else
                foreach (var group in stage.Groups)
                    tables.Add((stage.Id, group.Id,
                        participants.Where(p => p.GroupId == group.Id).Select(p => p.TeamId).ToList(),
                        finished.Where(m => m.GroupId == group.Id).ToList()));
        }

        var criteria = edition.GetTieBreakers();
        var newStandings = new List<CompetitionStanding>();
        foreach (var (stageId, groupId, teamIds, matches) in tables)
        {
            var adjustments = oldStandings.Where(s => s.StageId == stageId && s.GroupId == groupId)
                .ToDictionary(s => s.TeamId, s => s.PointsAdjustment);
            var rows = Rank(teamIds, matches, edition.PointsForWin, edition.PointsForDraw, edition.PointsForLoss,
                criteria, adjustments, fairPlay, names);
            newStandings.AddRange(rows.Select(r => new CompetitionStanding
            {
                CompetitionSeasonId = editionId, StageId = stageId, GroupId = groupId, TeamId = r.TeamId,
                Position = r.Position, Played = r.Played, Won = r.Won, Drawn = r.Drawn, Lost = r.Lost,
                GoalsFor = r.GoalsFor, GoalsAgainst = r.GoalsAgainst, Points = r.Points,
                PointsAdjustment = r.Adjustment, Form = r.Form
            }));
        }

        // ----- player statistics -----
        var lineups = await db.MatchLineups.AsNoTracking()
            .Where(l => l.Match.CompetitionSeasonId == editionId && l.Match.Status == MatchStatus.Finished)
            .Select(l => new { l.MatchId, l.PlayerId, l.TeamId, l.IsStarter })
            .ToListAsync();
        var bestPlayers = await db.MatchReports.AsNoTracking()
            .Where(r => r.BestPlayerId != null && r.Match.CompetitionSeasonId == editionId && r.Match.Status == MatchStatus.Finished)
            .Select(r => new { r.MatchId, PlayerId = r.BestPlayerId!.Value })
            .ToListAsync();

        var playerStats = new Dictionary<(int PlayerId, int TeamId), PlayerStatistic>();
        PlayerStatistic Stat(int playerId, int teamId)
        {
            if (!playerStats.TryGetValue((playerId, teamId), out var s))
                playerStats[(playerId, teamId)] = s = new PlayerStatistic { PlayerId = playerId, TeamId = teamId, CompetitionSeasonId = editionId };
            return s;
        }

        // Appearances: starters, substitutes who came on, and (when no lineup was recorded) anyone with an event.
        var appeared = new HashSet<(int MatchId, int PlayerId, int TeamId)>();
        foreach (var l in lineups.Where(l => l.IsStarter))
        {
            appeared.Add((l.MatchId, l.PlayerId, l.TeamId));
            Stat(l.PlayerId, l.TeamId).Starts++;
        }
        foreach (var e in events.Where(e => e.TeamId != null))
        {
            if (e.EventType == MatchEventType.Substitution && e.RelatedPlayerId != null)
                appeared.Add((e.MatchId, e.RelatedPlayerId.Value, e.TeamId!.Value));
            if (e.PlayerId != null)
                appeared.Add((e.MatchId, e.PlayerId.Value, e.TeamId!.Value));
        }
        foreach (var a in appeared) Stat(a.PlayerId, a.TeamId).Appearances++;

        foreach (var e in events.Where(e => e.TeamId != null && e.PlayerId != null))
        {
            var s = Stat(e.PlayerId!.Value, e.TeamId!.Value);
            switch (e.EventType)
            {
                case MatchEventType.Goal:
                case MatchEventType.PenaltyGoal:
                    s.Goals++;
                    if (e.EventType == MatchEventType.PenaltyGoal) s.PenaltyGoals++;
                    if (e.RelatedPlayerId != null) Stat(e.RelatedPlayerId.Value, e.TeamId.Value).Assists++;
                    break;
                case MatchEventType.OwnGoal: s.OwnGoals++; break;
                case MatchEventType.YellowCard: s.YellowCards++; break;
                case MatchEventType.SecondYellowCard: s.YellowCards++; s.RedCards++; break;
                case MatchEventType.RedCard: s.RedCards++; break;
            }
        }
        foreach (var b in bestPlayers)
        {
            var team = appeared.Where(a => a.MatchId == b.MatchId && a.PlayerId == b.PlayerId).Select(a => (int?)a.TeamId).FirstOrDefault()
                       ?? lineups.Where(l => l.MatchId == b.MatchId && l.PlayerId == b.PlayerId).Select(l => (int?)l.TeamId).FirstOrDefault();
            if (team != null) Stat(b.PlayerId, team.Value).BestPlayerAwards++;
        }

        // ----- team statistics -----
        var teamStats = participants.Select(p => p.TeamId)
            .Union(finished.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }))
            .Distinct()
            .ToDictionary(id => id, id => new TeamStatistic { TeamId = id, CompetitionSeasonId = editionId });
        foreach (var m in finished)
        {
            foreach (var (team, scored, conceded) in new[] { (m.HomeTeamId, m.HomeScore, m.AwayScore), (m.AwayTeamId, m.AwayScore, m.HomeScore) })
            {
                var t = teamStats[team];
                t.Played++;
                t.GoalsFor += scored;
                t.GoalsAgainst += conceded;
                if (scored > conceded) t.Won++; else if (scored == conceded) t.Drawn++; else t.Lost++;
                if (conceded == 0) t.CleanSheets++;
            }
        }
        foreach (var e in events.Where(e => e.TeamId != null && teamStats.ContainsKey(e.TeamId.Value)))
        {
            var t = teamStats[e.TeamId!.Value];
            if (e.EventType == MatchEventType.YellowCard) t.YellowCards++;
            if (e.EventType is MatchEventType.RedCard or MatchEventType.SecondYellowCard) t.RedCards++;
        }

        // ----- replace -----
        await db.CompetitionStandings.Where(s => s.CompetitionSeasonId == editionId).ExecuteDeleteAsync();
        await db.PlayerStatistics.Where(s => s.CompetitionSeasonId == editionId).ExecuteDeleteAsync();
        await db.TeamStatistics.Where(s => s.CompetitionSeasonId == editionId).ExecuteDeleteAsync();
        db.CompetitionStandings.AddRange(newStandings);
        db.PlayerStatistics.AddRange(playerStats.Values);
        db.TeamStatistics.AddRange(teamStats.Values);
        await db.SaveChangesAsync();
    }
}
