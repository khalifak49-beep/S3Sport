using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S3Sport.Models;

namespace S3Sport.Data.Configurations;

public class FriendlyRequestConfiguration : IEntityTypeConfiguration<FriendlyRequest>
{
    public void Configure(EntityTypeBuilder<FriendlyRequest> b)
    {
        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId);
        b.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        b.Property(x => x.PreferredDate).HasColumnType("date");
        b.HasIndex(x => new { x.Status, x.PreferredDate });
        b.HasIndex(x => x.TeamId);
    }
}

public class FriendlyOfferConfiguration : IEntityTypeConfiguration<FriendlyOffer>
{
    public void Configure(EntityTypeBuilder<FriendlyOffer> b)
    {
        b.HasOne(x => x.FriendlyRequest).WithMany(x => x.Offers).HasForeignKey(x => x.FriendlyRequestId);
        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        // One offer per team and request (a withdrawn offer is re-opened instead of duplicated).
        b.HasIndex(x => new { x.FriendlyRequestId, x.TeamId }).IsUnique();
    }
}
