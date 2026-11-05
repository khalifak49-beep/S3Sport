using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using S3Sport.Data;

namespace S3Sport.Infrastructure;

/// <summary>
/// VAPID key pair identifying this server to the browsers' push services (P-256; public key = 65-byte uncompressed
/// point, private key = 32-byte scalar, both base64url). Taken from configuration (WebPush:PublicKey / WebPush:PrivateKey)
/// when set; otherwise generated once and kept in App_Data/vapid.json. The keys must stay stable: browsers subscribed
/// with the public key, and a new key pair invalidates every existing subscription (devices re-subscribe on their next visit).
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

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        PublicKey = Base64Url.Encode([0x04, .. p.Q.X!, .. p.Q.Y!]);
        PrivateKey = Base64Url.Encode(p.D!);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new StoredKeys(PublicKey, PrivateKey)));
    }

    private record StoredKeys(string PublicKey, string PrivateKey);
}

/// <summary>
/// Web Push message encryption and VAPID signing on the .NET crypto primitives:
/// payload encryption per RFC 8291 with the "aes128gcm" content coding (RFC 8188) — the only coding Apple's push
/// service (iPhone/iPad/Safari) accepts, and supported by Chrome, Edge, Firefox and Android — and the VAPID
/// "vapid t=…, k=…" authorization header per RFC 8292 (ES256 JWT).
/// </summary>
public static class WebPushCrypto
{
    private const int RecordSize = 4096;

    /// <summary>Encrypted request body: salt(16) | rs(4) | idlen(1) | server public key(65) | AES-GCM ciphertext+tag.</summary>
    public static byte[] Encrypt(string p256dh, string auth, byte[] payload)
    {
        var userPublic = Base64Url.Decode(p256dh);
        var authSecret = Base64Url.Decode(auth);
        if (userPublic.Length != 65 || userPublic[0] != 0x04) throw new CryptographicException("Invalid p256dh key.");

        using var server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var sp = server.ExportParameters(false);
        byte[] serverPublic = [0x04, .. sp.Q.X!, .. sp.Q.Y!];
        using var user = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = userPublic[1..33], Y = userPublic[33..65] }
        });
        var sharedSecret = server.DeriveRawSecretAgreement(user.PublicKey);

        // RFC 8291 §3.4: IKM from the ECDH secret, keyed by the subscription's auth secret.
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. userPublic, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

        // RFC 8188 §2.2: content-encryption key and nonce from a random salt.
        var salt = RandomNumberGenerator.GetBytes(16);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        // A single record: payload followed by the 0x02 "last record" delimiter (no padding).
        byte[] plain = [.. payload, 0x02];
        if (plain.Length + 16 > RecordSize) throw new ArgumentException("Payload too large for one push record.");
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(cek, 16)) gcm.Encrypt(nonce, plain, cipher, tag);

        var body = new byte[16 + 4 + 1 + serverPublic.Length + cipher.Length + tag.Length];
        var o = 0;
        salt.CopyTo(body, o); o += 16;
        body[o++] = (byte)(RecordSize >> 24); body[o++] = (byte)(RecordSize >> 16); body[o++] = (byte)(RecordSize >> 8); body[o++] = RecordSize & 0xFF;
        body[o++] = (byte)serverPublic.Length;
        serverPublic.CopyTo(body, o); o += serverPublic.Length;
        cipher.CopyTo(body, o); o += cipher.Length;
        tag.CopyTo(body, o);
        return body;
    }

    /// <summary>VAPID Authorization header value for the push service that hosts <paramref name="endpoint"/>.</summary>
    public static string VapidAuthorization(string endpoint, PushKeys keys)
    {
        var uri = new Uri(endpoint);
        var audience = $"{uri.Scheme}://{uri.Authority}";
        var header = Base64Url.Encode("""{"typ":"JWT","alg":"ES256"}"""u8.ToArray());
        var claims = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = audience,
            exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds(),
            sub = keys.Subject
        }));
        var publicKey = Base64Url.Decode(keys.PublicKey);
        using var signer = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.Decode(keys.PrivateKey),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] }
        });
        // JWS ES256 = raw r||s (IEEE P1363), not DER.
        var signature = signer.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"vapid t={header}.{claims}.{Base64Url.Encode(signature)}, k={keys.PublicKey}";
    }
}

public static class Base64Url
{
    public static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string s)
    {
        s = s.Trim().Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

/// <summary>
/// Delivers stored notifications to subscribed devices (outbox pattern): every Notification row starts with
/// PushPending = true; this worker sends it to the recipient's devices and clears the flag. The operator's request
/// never waits on push services, and notifications created anywhere (helpers, admin screens) are covered.
/// Subscriptions the push service reports as gone (404/410) are deleted; other refusals are logged with the
/// service's answer so they can be diagnosed (a signing bug must not silently erase everyone's devices).
/// Designed for a single app instance; with several instances only one should run the worker.
/// </summary>
public class PushWorker(IServiceScopeFactory scopes, PushKeys keys, IHttpClientFactory httpFactory, ILogger<PushWorker> logger) : BackgroundService
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Signal.WaitAsync(PollInterval, stoppingToken);
                while (await SendBatchAsync(stoppingToken) > 0) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push delivery batch failed");
            }
        }
    }

    /// <summary>Sends up to 200 pending notifications; returns how many were processed.</summary>
    private async Task<int> SendBatchAsync(CancellationToken ct)
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

        var http = httpFactory.CreateClient(nameof(PushWorker));
        var dead = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
        await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (job, token) =>
        {
            if (dead.ContainsKey(job.Subscription.Id)) return;
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { title = job.Notification.Title, message = job.Notification.Message, url = job.Notification.Url ?? "/" });
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, job.Subscription.Endpoint)
                {
                    Content = new ByteArrayContent(WebPushCrypto.Encrypt(job.Subscription.P256dh, job.Subscription.Auth, payload))
                };
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                request.Content.Headers.ContentEncoding.Add("aes128gcm");
                request.Headers.TryAddWithoutValidation("Authorization", WebPushCrypto.VapidAuthorization(job.Subscription.Endpoint, keys));
                request.Headers.TryAddWithoutValidation("TTL", "3600");      // a goal an hour later is no longer news
                request.Headers.TryAddWithoutValidation("Urgency", "high");  // live events: deliver now, even on battery saver

                using var response = await http.SendAsync(request, token);
                if (response.IsSuccessStatusCode) return;
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    dead.TryAdd(job.Subscription.Id, true);
                    return;
                }
                var answer = await response.Content.ReadAsStringAsync(token);
                logger.LogWarning("Push to subscription {SubscriptionId} ({Host}) refused: {Status} {Answer}",
                    job.Subscription.Id, new Uri(job.Subscription.Endpoint).Host, (int)response.StatusCode, answer.Length > 300 ? answer[..300] : answer);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                // A slow or failing push service must not hold back the other devices; the notification stays in the app.
                logger.LogWarning(ex, "Push to subscription {SubscriptionId} failed", job.Subscription.Id);
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
