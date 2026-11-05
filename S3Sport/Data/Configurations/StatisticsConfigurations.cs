using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class CompetitionStandingConfiguration : IEntityTypeConfiguration<CompetitionStanding>
{
    public void Configure(EntityTypeBuilder<CompetitionStanding> b)
    {
        b.HasOne(x => x.CompetitionSeason).WithMany(x => x.Standings).HasForeignKey(x => x.CompetitionSeasonId);
        b.Property(x => x.GoalDifference).HasComputedColumnSql("[GoalsFor] - [GoalsAgainst]", stored: true);
        b.Property(x => x.Form).HasMaxLength(10);
        // NULL StageId/GroupId are treated as equal by SQL Server unique indexes, so league tables stay unique too.
        b.HasIndex(x => new { x.CompetitionSeasonId, x.StageId, x.GroupId, x.TeamId }).IsUnique();
        b.HasIndex(x => new { x.CompetitionSeasonId, x.GroupId, x.Position });
    }
}

public class PlayerStatisticConfiguration : IEntityTypeConfiguration<PlayerStatistic>
{
    public void Configure(EntityTypeBuilder<PlayerStatistic> b)
    {
        b.HasOne(x => x.Player).WithMany(x => x.Statistics).HasForeignKey(x => x.PlayerId);
        b.HasIndex(x => new { x.PlayerId, x.CompetitionSeasonId, x.TeamId }).IsUnique();
        b.HasIndex(x => new { x.CompetitionSeasonId, x.Goals });
    }
}

public class TeamStatisticConfiguration : IEntityTypeConfiguration<TeamStatistic>
{
    public void Configure(EntityTypeBuilder<TeamStatistic> b)
    {
        b.HasIndex(x => new { x.TeamId, x.CompetitionSeasonId }).IsUnique();
        b.HasIndex(x => x.CompetitionSeasonId);
    }
}
