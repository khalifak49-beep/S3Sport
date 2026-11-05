using System.Linq.Expressions;
using S3Sport.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace S3Sport.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Regions
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<Wilayat> Wilayats => Set<Wilayat>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Village> Villages => Set<Village>();
    public DbSet<UserArea> UserAreas => Set<UserArea>();

    // Teams
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamArea> TeamAreas => Set<TeamArea>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamApplication> TeamApplications => Set<TeamApplication>();
    public DbSet<TeamApplicationArea> TeamApplicationAreas => Set<TeamApplicationArea>();
    public DbSet<TeamAchievement> TeamAchievements => Set<TeamAchievement>();

    // Players
    public DbSet<Player> Players => Set<Player>();
    public DbSet<TeamPlayer> TeamPlayers => Set<TeamPlayer>();
    public DbSet<PlayerTransfer> PlayerTransfers => Set<PlayerTransfer>();

    // Sports & competitions
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<SportPosition> SportPositions => Set<SportPosition>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionManager> CompetitionManagers => Set<CompetitionManager>();
    public DbSet<CompetitionSeason> CompetitionSeasons => Set<CompetitionSeason>();
    public DbSet<CompetitionStage> CompetitionStages => Set<CompetitionStage>();
    public DbSet<CompetitionGroup> CompetitionGroups => Set<CompetitionGroup>();
    public DbSet<CompetitionTeam> CompetitionTeams => Set<CompetitionTeam>();

    // Matches
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<MatchEventRevision> MatchEventRevisions => Set<MatchEventRevision>();
    public DbSet<MatchLineup> MatchLineups => Set<MatchLineup>();
    public DbSet<MatchOfficial> MatchOfficials => Set<MatchOfficial>();
    public DbSet<MatchReport> MatchReports => Set<MatchReport>();

    // Statistics
    public DbSet<CompetitionStanding> CompetitionStandings => Set<CompetitionStanding>();
    public DbSet<PlayerStatistic> PlayerStatistics => Set<PlayerStatistic>();
    public DbSet<TeamStatistic> TeamStatistics => Set<TeamStatistic>();

    // Content & engagement
    public DbSet<News> News => Set<News>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
                entityType.SetQueryFilter(BuildSoftDeleteFilter(clrType));

            if (typeof(BaseEntity).IsAssignableFrom(clrType))
                builder.Entity(clrType).Property(nameof(BaseEntity.CreatedAt)).HasDefaultValueSql("SYSUTCDATETIME()");
        }

        // SQL Server rejects multiple cascade paths, and deleting sports data should be explicit.
        // Every FK we did not configure explicitly becomes RESTRICT (Identity's own tables keep their cascades).
        foreach (var fk in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            var isIdentityTable = fk.DeclaringEntityType.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true;
            var isExplicit = ((IConventionForeignKey)fk).GetDeleteBehaviorConfigurationSource() == ConfigurationSource.Explicit;
            if (!fk.IsOwnership && !isIdentityTable && !isExplicit)
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        SeedData.Apply(builder);
    }

    private static LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var parameter = Expression.Parameter(type, "e");
        var body = Expression.Equal(
            Expression.Property(parameter, nameof(ISoftDelete.IsDeleted)),
            Expression.Constant(false));
        return Expression.Lambda(body, parameter);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Stamps timestamps and converts deletes of soft-delete entities into updates.</summary>
    private void ApplyAuditRules()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity entity)
            {
                if (entry.State == EntityState.Added && entity.CreatedAt == default)
                    entity.CreatedAt = now;
                else if (entry.State == EntityState.Modified)
                    entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = now;
            }

            if (entry.State == EntityState.Added && entry.Entity is ApplicationUser user && user.CreatedAt == default)
                user.CreatedAt = now;

            if (entry.State == EntityState.Added && entry.Entity is AuditLog log && log.CreatedAt == default)
                log.CreatedAt = now;
        }
    }
}
