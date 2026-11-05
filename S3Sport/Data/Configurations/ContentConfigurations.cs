using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class NewsConfiguration : IEntityTypeConfiguration<News>
{
    public void Configure(EntityTypeBuilder<News> b)
    {
        b.ToTable("News");
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.IsPublished, x.PublishedAt });
        b.HasOne(x => x.AuthorUser).WithMany().HasForeignKey(x => x.AuthorUserId);
    }
}

public class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> b)
    {
        b.HasOne(x => x.News).WithMany(x => x.MediaFiles).HasForeignKey(x => x.NewsId);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId);
    }
}

public class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> b)
    {
        b.HasOne(x => x.User).WithMany(x => x.Follows).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.TeamId }).IsUnique().HasFilter("[TeamId] IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.PlayerId }).IsUnique().HasFilter("[PlayerId] IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.CompetitionId }).IsUnique().HasFilter("[CompetitionId] IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.MatchId }).IsUnique().HasFilter("[MatchId] IS NOT NULL");

        b.ToTable(t => t.HasCheckConstraint("CK_UserFollows_SingleTarget",
            "([TargetType] = 0 AND [TeamId] IS NOT NULL AND [PlayerId] IS NULL AND [CompetitionId] IS NULL AND [MatchId] IS NULL) OR " +
            "([TargetType] = 1 AND [PlayerId] IS NOT NULL AND [TeamId] IS NULL AND [CompetitionId] IS NULL AND [MatchId] IS NULL) OR " +
            "([TargetType] = 2 AND [CompetitionId] IS NOT NULL AND [TeamId] IS NULL AND [PlayerId] IS NULL AND [MatchId] IS NULL) OR " +
            "([TargetType] = 3 AND [MatchId] IS NOT NULL AND [TeamId] IS NULL AND [PlayerId] IS NULL AND [CompetitionId] IS NULL)"));
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        // The push outbox only ever scans pending rows.
        b.HasIndex(x => x.PushPending).HasFilter("[PushPending] = 1");
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Endpoint).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.HasIndex(x => x.UserId);
    }
}
