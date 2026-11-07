using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using S3Sport.Infrastructure;
using S3Sport.Models;

namespace S3Sport.Controllers;

/// <summary>
/// Match photo album. Only a manager (TeamMember role Manager) of one of the two teams uploads, one photo per request
/// (the browser shrinks each photo first). The uploading team's managers and admins can remove a photo.
/// </summary>
[Authorize]
public class MatchPhotosController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    public const int MaxPerTeam = 40;

    /// <summary>The match's teams that this user manages (empty for everyone else, admins included).</summary>
    public static async Task<List<int>> UploadTeamIdsAsync(ApplicationDbContext db, System.Security.Claims.ClaimsPrincipal user, int homeTeamId, int awayTeamId)
    {
        var uid = user.GetUserId();
        if (uid == null || !user.IsInRole(AppRoles.TeamManager)) return [];
        return await db.TeamMembers
            .Where(m => m.UserId == uid && m.IsActive && m.Role == TeamMemberRole.Manager && (m.TeamId == homeTeamId || m.TeamId == awayTeamId))
            .Select(m => m.TeamId).Distinct().ToListAsync();
    }

    [HttpPost("/matches/{id:int}/photos")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, int teamId, IFormFile? photo, string? caption)
    {
        var match = await db.Matches.AsNoTracking().Visible().Where(m => m.Id == id)
            .Select(m => new { m.HomeTeamId, m.AwayTeamId, m.Status }).FirstOrDefaultAsync();
        if (match == null) return NotFound();
        var teams = await UploadTeamIdsAsync(db, User, match.HomeTeamId, match.AwayTeamId);
        if (!teams.Contains(teamId)) return StatusCode(403, new { error = "رفع الصور متاح لمسؤولي الفريقين فقط." });
        if (match.Status == MatchStatus.Cancelled) return BadRequest(new { error = "المباراة ملغاة." });
        if (await db.MatchPhotos.CountAsync(p => p.MatchId == id && p.TeamId == teamId) >= MaxPerTeam)
            return BadRequest(new { error = $"وصل فريقك للحد الأقصى ({MaxPerTeam} صورة) لهذه المباراة." });
        if (FileUploadHelper.ValidateImage(photo) is { } error) return BadRequest(new { error });

        var entity = new MatchPhoto
        {
            MatchId = id, TeamId = teamId, UploadedByUserId = User.GetUserId(),
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 150)],
            Path = await FileUploadHelper.SaveImageAsync(photo!, env, $"matches/{id}")
        };
        db.MatchPhotos.Add(entity);
        await db.SaveChangesAsync();
        return Ok(new { ok = true, id = entity.Id, path = entity.Path });
    }

    [HttpPost("/matches/photos/{photoId:int}/delete")]
    public async Task<IActionResult> Delete(int photoId)
    {
        var photo = await db.MatchPhotos.FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo == null) return NotFound();
        var uid = User.GetUserId();
        var allowed = User.IsAdmin() || (User.IsInRole(AppRoles.TeamManager) && await db.TeamMembers
            .AnyAsync(m => m.UserId == uid && m.IsActive && m.Role == TeamMemberRole.Manager && m.TeamId == photo.TeamId));
        if (!allowed) return StatusCode(403, new { error = "لا تملك صلاحية حذف هذه الصورة." });

        db.MatchPhotos.Remove(photo);
        if (User.IsAdmin()) db.AddAudit(HttpContext, "حذف صورة مباراة", nameof(MatchPhoto), photo.Id, photo.Path);
        await db.SaveChangesAsync();
        FileUploadHelper.Delete(env, photo.Path);
        return Ok(new { ok = true });
    }
}
