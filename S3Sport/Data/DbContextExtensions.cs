using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using S3Sport.Models;

namespace S3Sport.Data;

public static class DbContextExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> inside a SQL transaction. Required because the SQL Server
    /// retrying execution strategy only allows user transactions through CreateExecutionStrategy().
    /// The delegate may be re-executed on a transient failure, so it must load what it changes.
    /// </summary>
    public static Task InTransactionAsync(this ApplicationDbContext db, Func<Task> work)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await work();
            await tx.CommitAsync();
        });
    }

    /// <summary>
    /// Applies a RowVersion posted back by a form as the original value, so SaveChanges fails with
    /// DbUpdateConcurrencyException if the row changed meanwhile. Malformed values are treated the same way.
    /// </summary>
    public static void SetOriginalRowVersion<T>(this ApplicationDbContext db, T entity, string? rowVersion,
        System.Linq.Expressions.Expression<Func<T, byte[]>> property) where T : class
    {
        if (string.IsNullOrEmpty(rowVersion)) return;
        byte[] value;
        try { value = Convert.FromBase64String(rowVersion); }
        catch (FormatException) { value = []; } // can never match → concurrency failure, not a 500
        db.Entry(entity).Property(property).OriginalValue = value;
    }

    /// <summary>Queues an audit entry; it is persisted by the caller's next SaveChanges.</summary>
    public static void AddAudit(this ApplicationDbContext db, HttpContext http, string action, string entityName, object? entityId, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = http.User.Identity?.Name,
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString(),
            Details = details,
            IpAddress = http.Connection.RemoteIpAddress?.ToString()
        });
    }

    public static void AddNotification(this ApplicationDbContext db, string userId, NotificationType type, string title,
        string? message = null, string? url = null, int? teamId = null, int? matchId = null, int? newsId = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId, Type = type, Title = title, Message = message, Url = url,
            TeamId = teamId, MatchId = matchId, NewsId = newsId
        });
    }
}
