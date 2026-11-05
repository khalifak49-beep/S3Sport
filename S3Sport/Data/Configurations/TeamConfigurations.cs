using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.NameAr);
        b.HasIndex(x => new { x.Status, x.IsDeleted });
        b.ToTable(t => t.HasCheckConstraint("CK_Teams_FoundedYear", "[FoundedYear] IS NULL OR [FoundedYear] BETWEEN 1900 AND 2100"));
    }
}

public class TeamAreaConfiguration : IEntityTypeConfiguration<TeamArea>
{
    public void Configure(EntityTypeBuilder<TeamArea> b)
    {
        b.HasKey(x => new { x.TeamId, x.AreaId });
        b.HasOne(x => x.Team).WithMany(x => x.TeamAreas).HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.Area).WithMany(x => x.TeamAreas).HasForeignKey(x => x.AreaId);
        b.HasIndex(x => x.TeamId).IsUnique().HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_TeamAreas_TeamId_Primary");
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> b)
    {
        b.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        b.HasOne(x => x.Team).WithMany(x => x.Members).HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.User).WithMany(x => x.TeamMemberships).HasForeignKey(x => x.UserId);
    }
}

public class TeamApplicationConfiguration : IEntityTypeConfiguration<TeamApplication>
{
    public void Configure(EntityTypeBuilder<TeamApplication> b)
    {
        b.HasIndex(x => new { x.Status, x.SubmittedAt });
        b.HasOne(x => x.ApplicantUser).WithMany().HasForeignKey(x => x.ApplicantUserId);
        b.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId);
        b.HasOne(x => x.CreatedTeam).WithMany().HasForeignKey(x => x.CreatedTeamId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TeamApplications_FoundedYear", "[FoundedYear] IS NULL OR [FoundedYear] BETWEEN 1900 AND 2100");
            t.HasCheckConstraint("CK_TeamApplications_Area", "[AreaId] IS NOT NULL OR [ProposedAreaName] IS NOT NULL");
        });
    }
}

public class TeamApplicationAreaConfiguration : IEntityTypeConfiguration<TeamApplicationArea>
{
    public void Configure(EntityTypeBuilder<TeamApplicationArea> b)
    {
        b.HasKey(x => new { x.TeamApplicationId, x.AreaId });
        b.HasOne(x => x.TeamApplication).WithMany(x => x.AdditionalAreas).HasForeignKey(x => x.TeamApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TeamAchievementConfiguration : IEntityTypeConfiguration<TeamAchievement>
{
    public void Configure(EntityTypeBuilder<TeamAchievement> b)
    {
        b.HasOne(x => x.Team).WithMany(x => x.Achievements).HasForeignKey(x => x.TeamId);
        b.HasIndex(x => new { x.TeamId, x.Year });
    }
}
