using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;
using WebPush;

namespace S3Sport.Infrastructure;

/// <summary>
/// VAPID key pair identifying this server to the browsers' push services.
/// Taken from configuration (WebPush:PublicKey / WebPush:PrivateKey) when set; otherwise generated once and kept in
/// App_Data/vapid.json. The keys must stay stable: browsers subscribed with the public key, and a new key pair
/// invalidates every existing subscription (devices then re-subscribe on their next visit).
/// </summary>
public class PushKeys
{
    public string PublicKey { get; }
    public string PrivateKey { get; }
    public string Subject { get; }

    public PushKeys(IConfiguration config, IWebHostEnvironment env)
    {
        Subject = config["WebPush:Subject"] is { Length: > 0 } s ? s : "mailto:admin@s3sport.om";
        if (config["WebPush:PublicKey"] is { Length: > 0 } pub && config["WebPush:PrivateKey"] is { Length: > 0 } priv)
        {
            PublicKey = pub;
            PrivateKey = priv;
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "App_Data", "vapid.json");
        if (File.Exists(path) && JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(path)) is { PublicKey.Length: > 0, PrivateKey.Length: > 0 } stored)
        {
            PublicKey = stored.PublicKey;
            PrivateKey = stored.PrivateKey;
            return;
        }

        var generated = VapidHelper.GenerateVapidKeys();
        PublicKey = generated.PublicKey;
        PrivateKey = generated.PrivateKey;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new StoredKeys(PublicKey, PrivateKey)));
    }

    private record StoredKeys(string PublicKey, string PrivateKey);
}

/// <summary>
/// Delivers stored notifications to subscribed devices (outbox pattern): every Notification row starts with
/// PushPending = true; this worker sends it to the recipient's devices and clears the flag. The operator's request
/// never waits on push services, and notifications created anywhere (helpers, admin screens) are covered.
/// Subscriptions the push service reports as gone (404/410) or unauthorised (401/403, e.g. after a key change) are deleted.
/// Designed for a single app instance; with several instances only one should run the worker.
/// </summary>
public class PushWorker(IServiceScopeFactory scopes, PushKeys keys, ILogger<PushWorker> logger) : BackgroundService
{
    private static readonly SemaphoreSlim Signal = new(0);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    /// <summary>Notifications older than this are not pushed (e.g. created while the worker was down).</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    /// <summary>Wakes the worker right away instead of at the next poll (called after notifications are saved).</summary>
    public static void Wake()
    {
        if (Signal.CurrentCount == 0) Signal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var vapid = new VapidDetails(keys.Subject, keys.PublicKey, keys.PrivateKey);
        using var client = new WebPushClient();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Signal.WaitAsync(PollInterval, stoppingToken);
                while (await SendBatchAsync(client, vapid, stoppingToken) > 0) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push delivery batch failed");
            }
        }
    }

    /// <summary>Sends up to 200 pending notifications; returns how many were processed.</summary>
    private async Task<int> SendBatchAsync(WebPushClient client, VapidDetails vapid, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var batch = await db.Notifications.AsNoTracking().Where(n => n.PushPending).OrderBy(n => n.Id).Take(200)
            .Select(n => new { n.Id, n.UserId, n.Title, n.Message, n.Url, n.CreatedAt }).ToListAsync(ct);
        if (batch.Count == 0) return 0;

        var userIds = batch.Select(n => n.UserId).Distinct().ToList();
        var subscriptions = (await db.PushSubscriptions.AsNoTracking().Where(s => userIds.Contains(s.UserId))
                .Select(s => new { s.Id, s.UserId, s.Endpoint, s.P256dh, s.Auth }).ToListAsync(ct))
            .ToLookup(s => s.UserId);

        var fresh = DateTime.UtcNow - MaxAge;
        var jobs = batch.Where(n => n.CreatedAt >= fresh)
            .SelectMany(n => subscriptions[n.UserId].Select(s => (Notification: n, Subscription: s))).ToList();

        var dead = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
        await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (job, token) =>
        {
            if (dead.ContainsKey(job.Subscription.Id)) return;
            var payload = JsonSerializer.Serialize(new { title = job.Notification.Title, message = job.Notification.Message, url = job.Notification.Url ?? "/" });
            try
            {
                await client.SendNotificationAsync(new WebPush.PushSubscription(job.Subscription.Endpoint, job.Subscription.P256dh, job.Subscription.Auth),
                    payload, vapid, token);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
                                                  or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                dead.TryAdd(job.Subscription.Id, true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A slow or failing push service must not hold back the other devices; the notification stays in the app.
                logger.LogInformation(ex, "Push to subscription {SubscriptionId} failed", job.Subscription.Id);
            }
        });

        var ids = batch.Select(n => n.Id).ToList();
        await db.Notifications.Where(n => ids.Contains(n.Id)).ExecuteUpdateAsync(u => u.SetProperty(n => n.PushPending, false), ct);
        if (!dead.IsEmpty)
        {
            var deadIds = dead.Keys.ToList();
            await db.PushSubscriptions.Where(s => deadIds.Contains(s.Id)).ExecuteDeleteAsync(ct);
        }
        return batch.Count;
    }
}
