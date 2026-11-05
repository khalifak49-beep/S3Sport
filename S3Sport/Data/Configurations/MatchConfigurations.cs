using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> b)
    {
        b.HasOne(x => x.HomeTeam).WithMany(x => x.HomeMatches).HasForeignKey(x => x.HomeTeamId);
        b.HasOne(x => x.AwayTeam).WithMany(x => x.AwayMatches).HasForeignKey(x => x.AwayTeamId);
        b.HasOne(x => x.WinnerTeam).WithMany().HasForeignKey(x => x.WinnerTeamId);
        b.HasOne(x => x.CompetitionSeason).WithMany(x => x.Matches).HasForeignKey(x => x.CompetitionSeasonId);
        b.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId);
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        b.HasOne(x => x.OperatorUser).WithMany().HasForeignKey(x => x.OperatorUserId);
        b.HasOne(x => x.OrganizerTeam).WithMany().HasForeignKey(x => x.OrganizerTeamId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.KickoffAt);
        b.HasIndex(x => new { x.Status, x.KickoffAt });
        b.HasIndex(x => new { x.CompetitionSeasonId, x.RoundNumber });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Matches_DifferentTeams", "[HomeTeamId] <> [AwayTeamId]");
            t.HasCheckConstraint("CK_Matches_Scores", "[HomeScore] >= 0 AND [AwayScore] >= 0");
            t.HasCheckConstraint("CK_Matches_Organizer", "[OrganizerTeamId] IS NULL OR ([CompetitionSeasonId] IS NULL AND [OrganizerTeamId] IN ([HomeTeamId], [AwayTeamId]))");
            t.HasCheckConstraint("CK_Matches_Winner", "[WinnerTeamId] IS NULL OR [WinnerTeamId] IN ([HomeTeamId], [AwayTeamId])");
        });
    }
}

public class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> b)
    {
        b.HasOne(x => x.Match).WithMany(x => x.Events).HasForeignKey(x => x.MatchId);
        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
        b.HasOne(x => x.RelatedPlayer).WithMany().HasForeignKey(x => x.RelatedPlayerId);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);

        // Idempotency: the same click (ClientRequestId) can never be stored twice for a match.
        b.HasIndex(x => new { x.MatchId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => new { x.MatchId, x.IsCancelled, x.Period, x.Minute });

        b.ToTable(t => t.HasCheckConstraint("CK_MatchEvents_Minute", "[Minute] BETWEEN 0 AND 200 AND ([ExtraMinute] IS NULL OR [ExtraMinute] BETWEEN 0 AND 30)"));
    }
}

public class MatchEventRevisionConfiguration : IEntityTypeConfiguration<MatchEventRevision>
{
    public void Configure(EntityTypeBuilder<MatchEventRevision> b)
    {
        b.HasOne(x => x.MatchEvent).WithMany(x => x.Revisions).HasForeignKey(x => x.MatchEventId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId);
    }
}

public class MatchLineupConfiguration : IEntityTypeConfiguration<MatchLineup>
{
    public void Configure(EntityTypeBuilder<MatchLineup> b)
    {
        b.HasOne(x => x.Match).WithMany(x => x.Lineups).HasForeignKey(x => x.MatchId);
        b.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();
        b.HasIndex(x => new { x.MatchId, x.TeamId });
    }
}

public class MatchOfficialConfiguration : IEntityTypeConfiguration<MatchOfficial>
{
    public void Configure(EntityTypeBuilder<MatchOfficial> b)
    {
        b.HasOne(x => x.Match).WithMany(x => x.Officials).HasForeignKey(x => x.MatchId);
        b.HasIndex(x => new { x.MatchId, x.Role }).IsUnique();
    }
}

public class MatchReportConfiguration : IEntityTypeConfiguration<MatchReport>
{
    public void Configure(EntityTypeBuilder<MatchReport> b)
    {
        b.HasOne(x => x.Match).WithOne(x => x.Report).HasForeignKey<MatchReport>(x => x.MatchId);
        b.HasIndex(x => x.MatchId).IsUnique();
        b.HasOne(x => x.BestPlayer).WithMany().HasForeignKey(x => x.BestPlayerId);
        b.HasOne(x => x.WrittenByUser).WithMany().HasForeignKey(x => x.WrittenByUserId);
    }
}
