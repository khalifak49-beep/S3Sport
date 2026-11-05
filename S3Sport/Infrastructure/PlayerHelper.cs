using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

/// <summary>Player create/update, squad membership and transfers — shared by admins and team managers.</summary>
public static class PlayerHelper
{
    public static async Task FillListsAsync(ApplicationDbContext db, PlayerFormViewModel model, bool includeTeams)
    {
        model.Positions = await db.SportPositions.AsNoTracking().Where(p => p.SportId == SeedData.FootballSportId)
            .OrderBy(p => p.SortOrder).Select(p => new SelectListItem(p.NameAr, p.Id.ToString())).ToListAsync();
        if (includeTeams)
            model.Teams = await db.Teams.AsNoTracking().Where(t => t.Status == TeamStatus.Approved).OrderBy(t => t.NameAr)
                .Select(t => new SelectListItem(t.NameAr, t.Id.ToString())).ToListAsync();
    }

    public static async Task ValidateAsync(ApplicationDbContext db, PlayerFormViewModel model, ModelStateDictionary modelState)
    {
        if (model.DateOfBirth is { } dob && (dob > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-5) || dob.Year < 1940))
            modelState.AddModelError(nameof(model.DateOfBirth), "تاريخ الميلاد غير منطقي.");
        if (model.PositionId is > 0 && !await db.SportPositions.AnyAsync(p => p.Id == model.PositionId))
            modelState.AddModelError(nameof(model.PositionId), "المركز غير صالح.");
        if (model.Photo is { Length: > 0 } && FileUploadHelper.ValidateImage(model.Photo) is { } err)
            modelState.AddModelError(nameof(model.Photo), err);

        if (model.TeamId is > 0)
        {
            if (!await db.Teams.AnyAsync(t => t.Id == model.TeamId && t.Status == TeamStatus.Approved))
                modelState.AddModelError(nameof(model.TeamId), "الفريق غير صالح.");
            else if (model.ShirtNumber != null && await db.TeamPlayers.AnyAsync(tp =>
                         tp.TeamId == model.TeamId && tp.IsActive && tp.ShirtNumber == model.ShirtNumber && tp.PlayerId != model.Id))
                modelState.AddModelError(nameof(model.ShirtNumber), "رقم القميص مستخدم للاعب آخر في الفريق.");
        }
    }

    /// <summary>
    /// Creates or updates the player. On creation with a team, the squad membership is created too;
    /// on update the active membership's shirt number / captaincy is updated (team changes go through transfers).
    /// </summary>
    public static async Task<Player> SaveAsync(ApplicationDbContext db, IWebHostEnvironment env, PlayerFormViewModel model)
    {
        Player player;
        if (model.Id == 0)
        {
            player = new Player { SportId = SeedData.FootballSportId };
            db.Players.Add(player);
        }
        else
        {
            player = await db.Players.Include(p => p.TeamPlayers.Where(tp => tp.IsActive)).FirstAsync(p => p.Id == model.Id);
            db.SetOriginalRowVersion(player, model.RowVersion, p => p.RowVersion);
        }

        player.FullNameAr = model.FullNameAr.Trim();
        player.FullNameEn = model.FullNameEn?.Trim();
        player.DateOfBirth = model.DateOfBirth;
        player.Nationality = model.Nationality?.Trim();
        player.PositionId = model.PositionId is > 0 ? model.PositionId : null;
        player.Bio = model.Bio?.Trim();

        if (model.Photo is { Length: > 0 })
        {
            FileUploadHelper.Delete(env, player.PhotoPath);
            player.PhotoPath = await FileUploadHelper.SaveImageAsync(model.Photo, env, "players");
        }
        else if (model.RemovePhoto)
        {
            FileUploadHelper.Delete(env, player.PhotoPath);
            player.PhotoPath = null;
        }

        var active = player.TeamPlayers.FirstOrDefault(tp => tp.IsActive);
        if (active == null && model.TeamId is > 0)
        {
            player.TeamPlayers.Add(new TeamPlayer
            {
                TeamId = model.TeamId.Value, ShirtNumber = model.ShirtNumber, PositionId = player.PositionId,
                IsCaptain = model.IsCaptain, JoinedOn = model.JoinedOn ?? DateOnly.FromDateTime(DateTime.UtcNow)
            });
        }
        else if (active != null)
        {
            active.ShirtNumber = model.ShirtNumber;
            active.IsCaptain = model.IsCaptain;
            active.PositionId = player.PositionId;
        }
        return player;
    }

    /// <summary>
    /// Ends the active membership and starts a new one (toTeamId null = released), recording the transfer.
    /// Must run inside a transaction. Returns an Arabic error or null.
    /// </summary>
    public static async Task<string?> TransferAsync(ApplicationDbContext db, int playerId, int? toTeamId, DateOnly date,
        int? shirtNumber, string? notes, string? userId)
    {
        var player = await db.Players.Include(p => p.TeamPlayers.Where(tp => tp.IsActive)).FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null) return "اللاعب غير موجود.";
        var active = player.TeamPlayers.FirstOrDefault();

        if (toTeamId == null && active == null) return "اللاعب ليس في أي فريق حالياً.";
        if (toTeamId != null && active?.TeamId == toTeamId) return "اللاعب في هذا الفريق بالفعل.";
        if (active != null && date < active.JoinedOn) return "تاريخ الانتقال قبل تاريخ انضمامه للفريق الحالي.";
        if (toTeamId != null)
        {
            if (!await db.Teams.AnyAsync(t => t.Id == toTeamId && t.Status == TeamStatus.Approved)) return "الفريق الجديد غير صالح.";
            if (shirtNumber != null && await db.TeamPlayers.AnyAsync(tp => tp.TeamId == toTeamId && tp.IsActive && tp.ShirtNumber == shirtNumber))
                return "رقم القميص مستخدم في الفريق الجديد.";
        }

        if (active != null)
        {
            active.IsActive = false;
            active.LeftOn = date;
            active.IsCaptain = false;
            // Persist the end of the old membership before inserting the new one
            // (filtered unique index: one active membership per player).
            await db.SaveChangesAsync();
        }
        if (toTeamId != null)
        {
            db.TeamPlayers.Add(new TeamPlayer
            {
                PlayerId = playerId, TeamId = toTeamId.Value, ShirtNumber = shirtNumber, PositionId = player.PositionId, JoinedOn = date
            });
        }
        db.PlayerTransfers.Add(new PlayerTransfer
        {
            PlayerId = playerId, FromTeamId = active?.TeamId, ToTeamId = toTeamId, TransferDate = date,
            Notes = notes?.Trim(), RecordedByUserId = userId
        });
        await db.SaveChangesAsync();
        return null;
    }

    public static IQueryable<PlayerListItem> ToListItems(this IQueryable<Player> players, ApplicationDbContext db) =>
        players.Select(p => new PlayerListItem
        {
            Id = p.Id,
            FullNameAr = p.FullNameAr,
            PhotoPath = p.PhotoPath,
            DateOfBirth = p.DateOfBirth,
            Position = p.Position != null ? p.Position.NameAr : null,
            TeamId = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => (int?)tp.TeamId).FirstOrDefault(),
            TeamName = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.Team.NameAr).FirstOrDefault(),
            ShirtNumber = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.ShirtNumber).FirstOrDefault(),
            Goals = db.PlayerStatistics.Where(s => s.PlayerId == p.Id).Sum(s => (int?)s.Goals) ?? 0,
            Appearances = db.PlayerStatistics.Where(s => s.PlayerId == p.Id).Sum(s => (int?)s.Appearances) ?? 0
        });

    public static Task<PlayerDetailsViewModel?> LoadDetailsAsync(ApplicationDbContext db, int id) =>
        db.Players.AsNoTracking().Where(p => p.Id == id).Select(p => new PlayerDetailsViewModel
        {
            Id = p.Id,
            FullNameAr = p.FullNameAr,
            FullNameEn = p.FullNameEn,
            PhotoPath = p.PhotoPath,
            DateOfBirth = p.DateOfBirth,
            Nationality = p.Nationality,
            Position = p.Position != null ? p.Position.NameAr : null,
            Bio = p.Bio,
            CurrentTeamId = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => (int?)tp.TeamId).FirstOrDefault(),
            CurrentTeamName = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.Team.NameAr).FirstOrDefault(),
            CurrentTeamSlug = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.Team.Slug).FirstOrDefault(),
            CurrentTeamLogo = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.Team.LogoPath).FirstOrDefault(),
            ShirtNumber = p.TeamPlayers.Where(tp => tp.IsActive).Select(tp => tp.ShirtNumber).FirstOrDefault(),
            History = p.TeamPlayers.OrderByDescending(tp => tp.JoinedOn).Select(tp => new PlayerHistoryRow
            {
                TeamName = tp.Team.NameAr, TeamSlug = tp.Team.Slug, TeamLogo = tp.Team.LogoPath, ShirtNumber = tp.ShirtNumber,
                JoinedOn = tp.JoinedOn, LeftOn = tp.LeftOn, IsActive = tp.IsActive
            }).ToList(),
            Transfers = p.Transfers.OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.Id).Select(t => new PlayerTransferRow
            {
                TransferDate = t.TransferDate,
                FromTeam = t.FromTeam != null ? t.FromTeam.NameAr : null,
                ToTeam = t.ToTeam != null ? t.ToTeam.NameAr : null,
                Notes = t.Notes
            }).ToList(),
            Stats = p.Statistics.OrderByDescending(s => s.CompetitionSeason.Season.StartDate).Select(s => new PlayerSeasonStatRow
            {
                CompetitionName = s.CompetitionSeason.Competition.NameAr,
                CompetitionSlug = s.CompetitionSeason.Competition.Slug,
                SeasonName = s.CompetitionSeason.Season.Name,
                TeamName = s.Team.NameAr,
                Appearances = s.Appearances, Starts = s.Starts, Goals = s.Goals, Assists = s.Assists,
                YellowCards = s.YellowCards, RedCards = s.RedCards, BestPlayerAwards = s.BestPlayerAwards
            }).ToList()
        }).FirstOrDefaultAsync();
}
