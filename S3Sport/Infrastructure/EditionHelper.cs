using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;

namespace S3Sport.Infrastructure;

/// <summary>Competition-edition structure: stages, groups, fixtures and knockout progression.</summary>
public static class EditionHelper
{
    public const string LeagueStageName = "الدوري";
    public const string GroupStageName = "دور المجموعات";
    public const string KnockoutStageName = "الأدوار الإقصائية";

    public static async Task<CompetitionStage> GetOrCreateStageAsync(ApplicationDbContext db, int editionId, StageType type, int legs = 1)
    {
        var stage = await db.CompetitionStages.Include(s => s.Groups)
            .FirstOrDefaultAsync(s => s.CompetitionSeasonId == editionId && s.StageType == type);
        if (stage != null) return stage;

        var order = type switch { StageType.League => 1, StageType.Group => 1, _ => 2 };
        stage = new CompetitionStage
        {
            CompetitionSeasonId = editionId, StageType = type, SortOrder = order, Legs = type == StageType.Knockout ? 1 : legs,
            NameAr = type switch { StageType.League => LeagueStageName, StageType.Group => GroupStageName, _ => KnockoutStageName }
        };
        db.CompetitionStages.Add(stage);
        await db.SaveChangesAsync();
        return stage;
    }

    /// <summary>Inserts the matches of a round-robin for the given teams, one date per round.</summary>
    public static int AddRoundRobinMatches(ApplicationDbContext db, int editionId, int sportId, CompetitionStage stage, int? groupId,
        IReadOnlyList<int> teamIds, int legs, DateTime firstKickoffUtc, int daysBetweenRounds, int? venueId,
        IReadOnlyDictionary<int, int?> homeVenues, int roundOffset = 0)
    {
        var rounds = FixtureGenerator.RoundRobin(teamIds, legs);
        var count = 0;
        for (var r = 0; r < rounds.Count; r++)
        {
            foreach (var p in rounds[r])
            {
                db.Matches.Add(new Match
                {
                    SportId = sportId, CompetitionSeasonId = editionId, StageId = stage.Id, GroupId = groupId,
                    RoundNumber = roundOffset + r + 1, RoundName = $"الجولة {roundOffset + r + 1}",
                    LegNumber = legs == 2 ? (r < rounds.Count / 2 ? 1 : 2) : null,
                    HomeTeamId = p.HomeTeamId, AwayTeamId = p.AwayTeamId,
                    KickoffAt = firstKickoffUtc.AddDays(r * daysBetweenRounds),
                    VenueId = venueId ?? homeVenues.GetValueOrDefault(p.HomeTeamId),
                    Status = MatchStatus.Scheduled
                });
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Teams entering the knockout stage, seeded: all participants (pure knockout, by seed then name),
    /// or the group qualifiers (mixed format, once every group match is finished). Returns an Arabic error or the pool.
    /// </summary>
    public static async Task<(List<int> Pool, string? Error)> InitialKnockoutPoolAsync(ApplicationDbContext db, CompetitionSeason edition)
    {
        if (edition.Format == CompetitionFormat.Knockout)
        {
            var teams = await db.CompetitionTeams.AsNoTracking()
                .Where(ct => ct.CompetitionSeasonId == edition.Id && !ct.IsWithdrawn)
                .OrderBy(ct => ct.Seed == null).ThenBy(ct => ct.Seed).ThenBy(ct => ct.Team.NameAr)
                .Select(ct => ct.TeamId).ToListAsync();
            return teams.Count < 2 ? ([], "أضف فريقين على الأقل.") : (teams, null);
        }

        var groupStage = await db.CompetitionStages.AsNoTracking().Include(s => s.Groups)
            .FirstOrDefaultAsync(s => s.CompetitionSeasonId == edition.Id && s.StageType == StageType.Group);
        if (groupStage == null || groupStage.Groups.Count == 0) return ([], "لا يوجد دور مجموعات.");
        if (await db.Matches.AnyAsync(m => m.StageId == groupStage.Id && m.Status != MatchStatus.Finished && m.Status != MatchStatus.Cancelled))
            return ([], "يجب انتهاء جميع مباريات دور المجموعات أولاً.");
        if (!await db.Matches.AnyAsync(m => m.StageId == groupStage.Id))
            return ([], "لم تُجدول مباريات دور المجموعات بعد.");

        var qualifiers = groupStage.QualifiersPerGroup ?? 2;
        var ranked = new List<IReadOnlyList<int>>();
        foreach (var g in groupStage.Groups.OrderBy(g => g.SortOrder))
            ranked.Add(await db.CompetitionStandings.AsNoTracking()
                .Where(s => s.GroupId == g.Id).OrderBy(s => s.Position).Select(s => s.TeamId).ToListAsync());
        var pool = FixtureGenerator.SeedGroupQualifiers(ranked, qualifiers);
        return pool.Count < 2 ? ([], "عدد المتأهلين غير كافٍ.") : (pool, null);
    }

    /// <summary>
    /// Creates the next knockout round (or the first). Byes and bracket order are re-derived from the
    /// initial pool and the finished rounds, so nothing extra needs to be stored.
    /// </summary>
    /// <summary>
    /// Replays the bracket: starting from the initial pool, applies every finished knockout round
    /// (winners + byes) and returns the teams still in, and the number of the next round.
    /// Error when a round is still being played or the pool cannot be built yet.
    /// </summary>
    private static async Task<(List<int> Pool, int NextRound, string? Error)> AdvanceBracketAsync(
        ApplicationDbContext db, CompetitionSeason edition, int? knockoutStageId)
    {
        var (pool, error) = await InitialKnockoutPoolAsync(db, edition);
        if (error != null) return ([], 1, error);
        if (knockoutStageId == null) return (pool, 1, null);

        var rounds = await db.Matches.AsNoTracking()
            .Where(m => m.StageId == knockoutStageId && m.Status != MatchStatus.Cancelled)
            .OrderBy(m => m.RoundNumber).ThenBy(m => m.Id)
            .Select(m => new { m.RoundNumber, m.HomeTeamId, m.AwayTeamId, m.Status, m.WinnerTeamId })
            .ToListAsync();

        var nextRound = 1;
        foreach (var round in rounds.GroupBy(m => m.RoundNumber ?? 1).OrderBy(g => g.Key))
        {
            if (round.Any(m => m.Status != MatchStatus.Finished || m.WinnerTeamId == null))
                return (pool, round.Key, $"يجب انتهاء جميع مباريات {FixtureGenerator.KnockoutRoundName(pool.Count)} وتحديد الفائز فيها أولاً.");
            var playing = round.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).ToHashSet();
            var byes = pool.Where(t => !playing.Contains(t)).ToList();
            var winners = round.Select(m => m.WinnerTeamId!.Value).ToList();
            pool = round.Key == 1 ? FixtureGenerator.NextRoundPool(byes, winners) : winners.Concat(byes).ToList();
            nextRound = round.Key + 1;
        }
        return (pool, nextRound, null);
    }

    public static async Task<(int Created, string? Message)> GenerateNextKnockoutRoundAsync(ApplicationDbContext db,
        CompetitionSeason edition, DateTime kickoffUtc, int? venueId)
    {
        var (_, initialError) = await InitialKnockoutPoolAsync(db, edition);
        if (initialError != null) return (0, initialError);

        var stage = await GetOrCreateStageAsync(db, edition.Id, StageType.Knockout);
        var (pool, roundNumber, error) = await AdvanceBracketAsync(db, edition, stage.Id);
        if (error != null) return (0, error);

        if (pool.Count < 2)
        {
            if (pool.Count == 1) await SetChampionAsync(db, edition.Id, pool[0]);
            return (0, "انتهت الأدوار الإقصائية وتم تحديد البطل.");
        }

        var (matches, _) = roundNumber == 1 ? FixtureGenerator.KnockoutFirstRound(pool) : FixtureGenerator.KnockoutNextRound(pool);
        var name = FixtureGenerator.KnockoutRoundName(roundNumber == 1 ? NextPowerOfTwo(pool.Count) : pool.Count + pool.Count % 2);
        foreach (var p in matches)
        {
            db.Matches.Add(new Match
            {
                SportId = edition.Competition.SportId, CompetitionSeasonId = edition.Id, StageId = stage.Id,
                RoundNumber = roundNumber, RoundName = name, HomeTeamId = p.HomeTeamId, AwayTeamId = p.AwayTeamId,
                KickoffAt = kickoffUtc, VenueId = venueId, Status = MatchStatus.Scheduled
            });
        }
        await db.SaveChangesAsync();
        return (matches.Count, null);
    }

    private static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p *= 2;
        return p;
    }

    /// <summary>
    /// Called after a knockout match finishes. The champion is decided structurally (not by the round's
    /// editable name): when every round is complete and only one team remains in the bracket.
    /// </summary>
    public static async Task CheckKnockoutChampionAsync(ApplicationDbContext db, int matchId)
    {
        var m = await db.Matches.AsNoTracking().Where(x => x.Id == matchId && x.Stage != null && x.Stage.StageType == StageType.Knockout)
            .Select(x => new { x.CompetitionSeasonId, x.StageId, x.Status }).FirstOrDefaultAsync();
        if (m?.CompetitionSeasonId == null || m.Status != MatchStatus.Finished) return;

        var edition = await db.CompetitionSeasons.AsNoTracking().FirstAsync(e => e.Id == m.CompetitionSeasonId);
        var (pool, _, error) = await AdvanceBracketAsync(db, edition, m.StageId);
        if (error == null && pool.Count == 1) await SetChampionAsync(db, edition.Id, pool[0]);
    }

    public static async Task SetChampionAsync(ApplicationDbContext db, int editionId, int teamId)
    {
        await db.CompetitionSeasons.Where(s => s.Id == editionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ChampionTeamId, teamId).SetProperty(x => x.Status, CompetitionSeasonStatus.Completed));
    }
}
