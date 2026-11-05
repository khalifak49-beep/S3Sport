using S3Sport.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace S3Sport.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.HasIndex(x => x.FullNameAr);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
    }
}

public class TeamPlayerConfiguration : IEntityTypeConfiguration<TeamPlayer>
{
    public void Configure(EntityTypeBuilder<TeamPlayer> b)
    {
        b.HasOne(x => x.Team).WithMany(x => x.TeamPlayers).HasForeignKey(x => x.TeamId);
        b.HasOne(x => x.Player).WithMany(x => x.TeamPlayers).HasForeignKey(x => x.PlayerId);

        // A player has at most one active team, and a shirt number is unique within a team's active squad.
        b.HasIndex(x => x.PlayerId).IsUnique().HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_TeamPlayers_PlayerId_Active");
        b.HasIndex(x => new { x.TeamId, x.ShirtNumber }).IsUnique()
            .HasFilter("[IsActive] = 1 AND [ShirtNumber] IS NOT NULL")
            .HasDatabaseName("UX_TeamPlayers_TeamId_ShirtNumber_Active");
        b.HasIndex(x => new { x.TeamId, x.IsActive });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TeamPlayers_ShirtNumber", "[ShirtNumber] IS NULL OR [ShirtNumber] BETWEEN 1 AND 99");
            t.HasCheckConstraint("CK_TeamPlayers_Dates", "[LeftOn] IS NULL OR [LeftOn] >= [JoinedOn]");
        });
    }
}

public class PlayerTransferConfiguration : IEntityTypeConfiguration<PlayerTransfer>
{
    public void Configure(EntityTypeBuilder<PlayerTransfer> b)
    {
        b.HasOne(x => x.Player).WithMany(x => x.Transfers).HasForeignKey(x => x.PlayerId);
        b.HasOne(x => x.FromTeam).WithMany().HasForeignKey(x => x.FromTeamId);
        b.HasOne(x => x.ToTeam).WithMany().HasForeignKey(x => x.ToTeamId);
        b.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedByUserId);
        b.HasIndex(x => new { x.PlayerId, x.TransferDate });
    }
}
