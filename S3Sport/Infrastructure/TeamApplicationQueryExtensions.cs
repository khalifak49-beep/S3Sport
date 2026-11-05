using S3Sport.Models;
using S3Sport.ViewModels;

namespace S3Sport.Infrastructure;

public static class TeamApplicationQueryExtensions
{
    /// <summary>Single projection used by both the user and admin details pages.</summary>
    public static IQueryable<TeamApplicationDetailsViewModel> ToDetailsViewModel(this IQueryable<TeamApplication> query) =>
        query.Select(a => new TeamApplicationDetailsViewModel
        {
            Id = a.Id, NameAr = a.NameAr, NameEn = a.NameEn, LogoPath = a.LogoPath, SportName = a.Sport.NameAr,
            GovernorateName = a.Governorate.NameAr, WilayatName = a.Wilayat.NameAr,
            AreaName = a.Area != null ? a.Area.NameAr : a.ProposedAreaName!, IsProposedArea = a.AreaId == null,
            VillageName = a.Village != null ? a.Village.NameAr : null,
            AdditionalAreas = a.AdditionalAreas.Select(x => x.Area.NameAr + " (" + x.Area.Wilayat.NameAr + ")").ToList(),
            FoundedYear = a.FoundedYear, Description = a.Description, ContactPhone = a.ContactPhone, ContactEmail = a.ContactEmail,
            Status = a.Status, SubmittedAt = a.SubmittedAt,
            ApplicantName = a.ApplicantUser.FullName, ApplicantEmail = a.ApplicantUser.Email, ApplicantPhone = a.ApplicantUser.PhoneNumber,
            ReviewedByName = a.ReviewedByUser != null ? a.ReviewedByUser.FullName : null,
            ReviewedAt = a.ReviewedAt, ReviewNotes = a.ReviewNotes, CreatedTeamId = a.CreatedTeamId,
            RowVersion = Convert.ToBase64String(a.RowVersion)
        });
}
