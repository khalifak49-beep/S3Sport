namespace S3Sport.Infrastructure;

/// <summary>Pure fixture algorithms (no database access), so they are easy to reason about and test.</summary>
public static class FixtureGenerator
{
    public record Pairing(int HomeTeamId, int AwayTeamId);

    /// <summary>
    /// Round-robin by the circle method: every team meets every other team once per leg.
    /// With an odd number of teams one team rests each round. Home/away alternates so no team
    /// plays every match at home; the second leg mirrors the first with venues swapped.
    /// </summary>
    public static List<List<Pairing>> RoundRobin(IReadOnlyList<int> teamIds, int legs = 1)
    {
        var slots = teamIds.Select(id => (int?)id).ToList();
        if (slots.Count < 2) return [];
        if (slots.Count % 2 == 1) slots.Add(null); // "bye"

        var n = slots.Count;
        var firstLeg = new List<List<Pairing>>();
        for (var round = 0; round < n - 1; round++)
        {
            var pairs = new List<Pairing>();
            for (var i = 0; i < n / 2; i++)
            {
                var a = slots[i];
                var b = slots[n - 1 - i];
                if (a == null || b == null) continue;
                // Alternate the fixed team's venue each round, and the others by position, for balance.
                var swap = i == 0 ? round % 2 == 1 : i % 2 == 1;
                pairs.Add(swap ? new Pairing(b.Value, a.Value) : new Pairing(a.Value, b.Value));
            }
            firstLeg.Add(pairs);

            // Rotate every slot except the first.
            var last = slots[n - 1];
            slots.RemoveAt(n - 1);
            slots.Insert(1, last);
        }

        var rounds = new List<List<Pairing>>(firstLeg);
        if (legs == 2)
            rounds.AddRange(firstLeg.Select(r => r.Select(p => new Pairing(p.AwayTeamId, p.HomeTeamId)).ToList()));
        return rounds;
    }

    /// <summary>
    /// First knockout round for a seeded list (best first). The field is padded to the next power of two:
    /// the top seeds receive byes, the rest play top-vs-bottom. Returns the matches and the teams with byes.
    /// </summary>
    public static (List<Pairing> Matches, List<int> Byes) KnockoutFirstRound(IReadOnlyList<int> seededTeamIds)
    {
        var n = seededTeamIds.Count;
        if (n < 2) return ([], seededTeamIds.ToList());
        var size = 1;
        while (size < n) size *= 2;
        var byeCount = size - n;

        var byes = seededTeamIds.Take(byeCount).ToList();
        var playing = seededTeamIds.Skip(byeCount).ToList();
        var matches = new List<Pairing>();
        for (int i = 0, j = playing.Count - 1; i < j; i++, j--)
            matches.Add(new Pairing(playing[i], playing[j]));
        return (matches, byes);
    }

    /// <summary>
    /// Bracket order for the round after the first: each team that had a bye meets a first-round winner
    /// from the opposite end of the bracket (bye #1 vs the last match's winner), so top seeds do not meet early.
    /// </summary>
    public static List<int> NextRoundPool(IReadOnlyList<int> byes, IReadOnlyList<int> winnersInMatchOrder)
    {
        var pool = new List<int>();
        var paired = Math.Min(byes.Count, winnersInMatchOrder.Count);
        for (var i = 0; i < paired; i++)
        {
            pool.Add(byes[i]);
            pool.Add(winnersInMatchOrder[winnersInMatchOrder.Count - 1 - i]);
        }
        pool.AddRange(winnersInMatchOrder.Take(winnersInMatchOrder.Count - paired));
        pool.AddRange(byes.Skip(paired));
        return pool;
    }

    /// <summary>Next knockout round: advancing teams in bracket order are paired 1v2, 3v4, ...</summary>
    public static (List<Pairing> Matches, List<int> Byes) KnockoutNextRound(IReadOnlyList<int> advancingTeamIds)
    {
        var matches = new List<Pairing>();
        var byes = new List<int>();
        for (var i = 0; i < advancingTeamIds.Count; i += 2)
        {
            if (i + 1 < advancingTeamIds.Count) matches.Add(new Pairing(advancingTeamIds[i], advancingTeamIds[i + 1]));
            else byes.Add(advancingTeamIds[i]);
        }
        return (matches, byes);
    }

    /// <summary>
    /// Seeds group qualifiers by finishing place: A1, B1, C1, ..., A2, B2, C2, ...
    /// KnockoutFirstRound pairs first-vs-last, so winners meet runners-up of another group
    /// (2 groups: A1–B2, B1–A2; 4 groups: A1–D2, B1–C2, C1–B2, D1–A2) and group-mates can only meet later.
    /// </summary>
    public static List<int> SeedGroupQualifiers(IReadOnlyList<IReadOnlyList<int>> rankedByGroup, int qualifiersPerGroup)
    {
        var seeded = new List<int>();
        for (var place = 0; place < qualifiersPerGroup; place++)
            seeded.AddRange(rankedByGroup.Where(g => g.Count > place).Select(g => g[place]));
        return seeded;
    }

    public static string KnockoutRoundName(int teamsInRound) => teamsInRound switch
    {
        2 => "النهائي",
        <= 4 => "نصف النهائي",
        <= 8 => "ربع النهائي",
        <= 16 => "دور الـ16",
        <= 32 => "دور الـ32",
        _ => $"دور الـ{teamsInRound}"
    };
}
