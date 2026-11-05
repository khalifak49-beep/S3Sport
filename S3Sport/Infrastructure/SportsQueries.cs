using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

/// <summary>Read projections shared by admin and public pages (match rows, standings tables).</summary>
public static class SportsQueries
{
    public static IQueryable<MatchRow> ToRows(this IQueryable<Match> matches) =>
        matches.Select(m => new MatchRow
        {
            Id = m.Id,
            KickoffAt = m.KickoffAt,
            Status = m.Status,
            HomeName = m.HomeTeam.NameAr,
            HomeLogo = m.HomeTeam.LogoPath,
            AwayName = m.AwayTeam.NameAr,
            AwayLogo = m.AwayTeam.LogoPath,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            HomePenaltyScore = m.HomePenaltyScore,
            AwayPenaltyScore = m.AwayPenaltyScore,
            RoundName = m.RoundName,
            RoundNumber = m.RoundNumber,
            StageName = m.Stage != null ? m.Stage.NameAr : null,
            GroupName = m.Group != null ? m.Group.Name : null,
            CompetitionName = m.CompetitionSeason != null ? m.CompetitionSeason.Competition.NameAr : null,
            VenueName = m.Venue != null ? m.Venue.NameAr : null,
            OperatorName = m.OperatorUser != null ? m.OperatorUser.FullName : null,
            CurrentPeriod = m.CurrentPeriod,
            CurrentPeriodStartedAt = m.CurrentPeriodStartedAt,
            PeriodDurationMinutes = m.Sport.PeriodDurationMinutes,
            HomeSlug = m.HomeTeam.Status == TeamStatus.Guest ? null : m.HomeTeam.Slug,
            AwaySlug = m.AwayTeam.Status == TeamStatus.Guest ? null : m.AwayTeam.Slug,
            CompetitionId = m.CompetitionSeason != null ? m.CompetitionSeason.CompetitionId : null,
            CompetitionSlug = m.CompetitionSeason != null ? m.CompetitionSeason.Competition.Slug : null,
            CompetitionLogo = m.CompetitionSeason != null ? m.CompetitionSeason.Competition.LogoPath : null
        });

    /// <summary>Matches involving a team of the region (either side).</summary>
    public static IQueryable<Match> InRegion(this IQueryable<Match> matches, CurrentRegion? region)
    {
        if (region == null) return matches;
        if (region.AreaId.HasValue)
            return matches.Where(m => m.HomeTeam.TeamAreas.Any(ta => ta.AreaId == region.AreaId) || m.AwayTeam.TeamAreas.Any(ta => ta.AreaId == region.AreaId));
        if (region.WilayatId.HasValue)
            return matches.Where(m => m.HomeTeam.TeamAreas.Any(ta => ta.Area.WilayatId == region.WilayatId) || m.AwayTeam.TeamAreas.Any(ta => ta.Area.WilayatId == region.WilayatId));
        return matches.Where(m => m.HomeTeam.TeamAreas.Any(ta => ta.Area.Wilayat.GovernorateId == region.GovernorateId)
                                  || m.AwayTeam.TeamAreas.Any(ta => ta.Area.Wilayat.GovernorateId == region.GovernorateId));
    }

    /// <summary>
    /// Competitions relevant to a region: national ones, plus those scoped to the region or a level containing it
    /// (a wilayat league shows for every area of that wilayat).
    /// </summary>
    public static IQueryable<Competition> InRegion(this IQueryable<Competition> competitions, CurrentRegion? region)
    {
        if (region == null) return competitions;
        return competitions.Where(c => c.GovernorateId == null ||
            (c.GovernorateId == region.GovernorateId
             && (c.WilayatId == null || region.WilayatId == null || c.WilayatId == region.WilayatId)
             && (c.AreaId == null || region.AreaId == null || c.AreaId == region.AreaId)));
    }

    /// <summary>Public matches = not deleted, between visible teams (approved, or a guest opponent in a friendly).</summary>
    public static IQueryable<Match> Visible(this IQueryable<Match> matches) =>
        matches.Where(m => (m.HomeTeam.Status == TeamStatus.Approved || m.HomeTeam.Status == TeamStatus.Guest)
                           && (m.AwayTeam.Status == TeamStatus.Approved || m.AwayTeam.Status == TeamStatus.Guest)
                           && (m.HomeTeam.Status == TeamStatus.Approved || m.AwayTeam.Status == TeamStatus.Approved)
                           && (m.CompetitionSeason == null || m.CompetitionSeason.Competition.IsActive));

    /// <summary>Stored standings of an edition, one table per league stage or group, in stage/group order.</summary>
    public static async Task<List<StandingsTableView>> LoadStandingsAsync(ApplicationDbContext db, int editionId)
    {
        var rows = await db.CompetitionStandings.AsNoTracking()
            .Where(s => s.CompetitionSeasonId == editionId)
            .Select(s => new
            {
                StageOrder = s.Stage != null ? s.Stage.SortOrder : 0,
                StageName = s.Stage != null ? s.Stage.NameAr : null,
                StageType = s.Stage != null ? (StageType?)s.Stage.StageType : null,
                Qualifiers = s.Stage != null ? s.Stage.QualifiersPerGroup : null,
                GroupOrder = s.Group != null ? s.Group.SortOrder : 0,
                GroupName = s.Group != null ? s.Group.Name : null,
                Row = new StandingRow
                {
                    Position = s.Position, TeamId = s.TeamId, TeamName = s.Team.NameAr, TeamSlug = s.Team.Slug, TeamLogo = s.Team.LogoPath,
                    Played = s.Played, Won = s.Won, Drawn = s.Drawn, Lost = s.Lost, GoalsFor = s.GoalsFor, GoalsAgainst = s.GoalsAgainst,
                    GoalDifference = s.GoalDifference, Points = s.Points, PointsAdjustment = s.PointsAdjustment, Form = s.Form
                }
            })
            .ToListAsync();

        return rows
            .GroupBy(r => new { r.StageOrder, r.StageName, r.StageType, r.Qualifiers, r.GroupOrder, r.GroupName })
            .OrderBy(g => g.Key.StageOrder).ThenBy(g => g.Key.GroupOrder)
            .Select(g => new StandingsTableView
            {
                Title = g.Key.GroupName != null ? $"المجموعة {g.Key.GroupName}" : g.Key.StageName,
                Rows = g.Select(r =>
                {
                    r.Row.Qualifies = g.Key.StageType == StageType.Group && g.Key.Qualifiers is > 0 && r.Row.Position <= g.Key.Qualifiers;
                    return r.Row;
                }).OrderBy(r => r.Position).ToList()
            })
            .ToList();
    }

    public static IQueryable<CompetitionListItem> ToListItems(this IQueryable<Competition> competitions) =>
        competitions.OrderBy(c => c.NameAr).Select(c => new CompetitionListItem
        {
            Id = c.Id, NameAr = c.NameAr, Slug = c.Slug, LogoPath = c.LogoPath, SportName = c.Sport.NameAr, IsActive = c.IsActive,
            RegionText = c.Area != null ? c.Area.NameAr + "، " + c.Area.Wilayat.NameAr : c.Wilayat != null ? c.Wilayat.NameAr : c.Governorate != null ? c.Governorate.NameAr : "سلطنة عُمان",
            EditionsCount = c.Seasons.Count,
            CurrentEditionName = c.Seasons.OrderByDescending(s => s.Season.StartDate).Select(s => s.Season.Name).FirstOrDefault(),
            CurrentEditionStatus = c.Seasons.OrderByDescending(s => s.Season.StartDate).Select(s => (CompetitionSeasonStatus?)s.Status).FirstOrDefault()
        });
}
