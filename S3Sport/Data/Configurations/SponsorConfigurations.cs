using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S3Sport.Models;

namespace S3Sport.Data.Configurations;

public class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> b)
    {
        b.HasOne(x => x.Governorate).WithMany().HasForeignKey(x => x.GovernorateId);
        b.HasOne(x => x.Wilayat).WithMany().HasForeignKey(x => x.WilayatId);
        b.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
        b.Property(x => x.StartsOn).HasColumnType("date");
        b.Property(x => x.EndsOn).HasColumnType("date");
        b.HasIndex(x => new { x.IsActive, x.Scope });
    }
}

public class MatchPhotoConfiguration : IEntityTypeConfiguration<MatchPhoto>
{
    public void Configure(EntityTypeBuilder<MatchPhoto> b)
    {
        b.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId);
        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId);
        b.HasIndex(x => new { x.MatchId, x.TeamId });
    }
}
