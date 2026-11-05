using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> b)
    {
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.NameAr).IsUnique();
    }
}

public class SportPositionConfiguration : IEntityTypeConfiguration<SportPosition>
{
    public void Configure(EntityTypeBuilder<SportPosition> b)
    {
        b.HasOne(x => x.Sport).WithMany(x => x.Positions).HasForeignKey(x => x.SportId);
        b.HasIndex(x => new { x.SportId, x.Code }).IsUnique();
    }
}

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> b)
    {
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.IsCurrent).IsUnique().HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("UX_Seasons_Current");
        b.ToTable(t => t.HasCheckConstraint("CK_Seasons_Dates", "[EndDate] > [StartDate]"));
    }
}

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> b)
    {
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.HasIndex(x => x.NameAr);
    }
}

public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.SportId, x.IsActive });
    }
}

public class CompetitionManagerConfiguration : IEntityTypeConfiguration<CompetitionManager>
{
    public void Configure(EntityTypeBuilder<CompetitionManager> b)
    {
        b.HasKey(x => new { x.CompetitionId, x.UserId });
        b.HasOne(x => x.Competition).WithMany(x => x.Managers).HasForeignKey(x => x.CompetitionId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.Property(x => x.AssignedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class CompetitionSeasonConfiguration : IEntityTypeConfiguration<CompetitionSeason>
{
    public void Configure(EntityTypeBuilder<CompetitionSeason> b)
    {
        b.HasIndex(x => new { x.CompetitionId, x.SeasonId }).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasOne(x => x.Competition).WithMany(x => x.Seasons).HasForeignKey(x => x.CompetitionId);
        b.HasOne(x => x.ChampionTeam).WithMany().HasForeignKey(x => x.ChampionTeamId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CompetitionSeasons_Points", "[PointsForWin] >= [PointsForDraw] AND [PointsForDraw] >= [PointsForLoss]");
            t.HasCheckConstraint("CK_CompetitionSeasons_Dates", "[EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]");
        });
    }
}

public class CompetitionStageConfiguration : IEntityTypeConfiguration<CompetitionStage>
{
    public void Configure(EntityTypeBuilder<CompetitionStage> b)
    {
        b.HasOne(x => x.CompetitionSeason).WithMany(x => x.Stages).HasForeignKey(x => x.CompetitionSeasonId);
        b.HasIndex(x => new { x.CompetitionSeasonId, x.SortOrder });
        b.ToTable(t => t.HasCheckConstraint("CK_CompetitionStages_Legs", "[Legs] IN (1, 2)"));
    }
}

public class CompetitionGroupConfiguration : IEntityTypeConfiguration<CompetitionGroup>
{
    public void Configure(EntityTypeBuilder<CompetitionGroup> b)
    {
        b.HasOne(x => x.Stage).WithMany(x => x.Groups).HasForeignKey(x => x.StageId);
        b.HasIndex(x => new { x.StageId, x.Name }).IsUnique();
    }
}

public class CompetitionTeamConfiguration : IEntityTypeConfiguration<CompetitionTeam>
{
    public void Configure(EntityTypeBuilder<CompetitionTeam> b)
    {
        b.HasOne(x => x.CompetitionSeason).WithMany(x => x.Teams).HasForeignKey(x => x.CompetitionSeasonId);
        b.HasOne(x => x.Team).WithMany(x => x.Competitions).HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.Group).WithMany(x => x.Teams).HasForeignKey(x => x.GroupId);
        b.HasIndex(x => new { x.CompetitionSeasonId, x.TeamId }).IsUnique();
    }
}
