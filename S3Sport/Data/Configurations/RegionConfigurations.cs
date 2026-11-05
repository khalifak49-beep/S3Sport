using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> b)
    {
        b.HasIndex(x => x.NameAr).IsUnique();
        b.HasIndex(x => x.NameEn).IsUnique();
    }
}

public class WilayatConfiguration : IEntityTypeConfiguration<Wilayat>
{
    public void Configure(EntityTypeBuilder<Wilayat> b)
    {
        b.HasIndex(x => new { x.GovernorateId, x.NameAr }).IsUnique();
        b.HasOne(x => x.Governorate).WithMany(x => x.Wilayats).HasForeignKey(x => x.GovernorateId);
    }
}

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> b)
    {
        b.HasIndex(x => new { x.WilayatId, x.NameAr }).IsUnique();
        b.HasOne(x => x.Wilayat).WithMany(x => x.Areas).HasForeignKey(x => x.WilayatId);
    }
}

public class VillageConfiguration : IEntityTypeConfiguration<Village>
{
    public void Configure(EntityTypeBuilder<Village> b)
    {
        b.HasIndex(x => new { x.AreaId, x.NameAr }).IsUnique();
        b.HasOne(x => x.Area).WithMany(x => x.Villages).HasForeignKey(x => x.AreaId);
    }
}

public class UserAreaConfiguration : IEntityTypeConfiguration<UserArea>
{
    public void Configure(EntityTypeBuilder<UserArea> b)
    {
        b.HasOne(x => x.User).WithMany(x => x.UserAreas).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsDefault] = 1")
            .HasDatabaseName("UX_UserAreas_UserId_Default");
        b.HasIndex(x => new { x.UserId, x.GovernorateId, x.WilayatId, x.AreaId, x.VillageId }).IsUnique()
            .HasDatabaseName("UX_UserAreas_UserId_Region");
    }
}
