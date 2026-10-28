using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexQuota;

var stateDir = Environment.GetEnvironmentVariable("CODEX_QUOTA_STATE_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuota");
var snapshotPath = Path.Combine(stateDir, "snapshot.ini");
var settingsPath = Path.Combine(stateDir, "settings.json");
if (args.Contains("--settings")) { SettingsForm.Open(settingsPath); return; }
if (args.Contains("--state-path")) { Console.WriteLine(snapshotPath); return; }
var once = args.Contains("--once");
var parentArg = Array.IndexOf(args, "--parent");
Process? parent = null;
if (parentArg >= 0)
{
    if (parentArg + 1 >= args.Length || !int.TryParse(args[parentArg + 1], out var pid)) return;
    try { parent = Process.GetProcessById(pid); _ = parent.Handle; } catch { return; }
}
// A named semaphore (rather than a thread-owned mutex) remains valid across awaits.
var stateKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(stateDir).ToUpperInvariant())))[..16];
using var gate = new Semaphore(1, 1, "Local\\CodexQuotaBridge-" + stateKey);
if (!gate.WaitOne(0)) return;
using var lifetime = new CancellationTokenSource();
if (parent != null)
{
    parent.EnableRaisingEvents = true;
    parent.Exited += (_, _) => lifetime.Cancel();
    if (parent.HasExited) return;
}
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
Quota? last = null;
string? lastIdentity = null;
try
{
    RefreshSettings.EnsureExists(settingsPath);
    var settings = PluginSettings.Read(settingsPath);
    var failures = 0;
    do
    {
        var fresh = false;
        var status = "连接失败，* 表示上次结果";
        System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter = null;
        try
        {
            var home = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var authFile = Path.Combine(home, "auth.json");
            if (new FileInfo(authFile).Length > 1024 * 1024) throw new InvalidDataException();
            using var auth = JsonDocument.Parse(await File.ReadAllTextAsync(authFile, lifetime.Token));
            var tokens = auth.RootElement.GetProperty("tokens");
            var token = tokens.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidDataException();
            var account = tokens.TryGetProperty("account_id", out var a) ? a.GetString() : null;
            // Keep identity in memory only. Clear cached values on credential/account changes.
            var identity = account ?? token;
            if (identity != lastIdentity) last = null;
            lastIdentity = identity;
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrEmpty(account)) request.Headers.Add("ChatGPT-Account-Id", account);
            using var response = await http.SendAsync(request, lifetime.Token);
            retryAfter = response.Headers.RetryAfter;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                last = null;
                status = "登录已过期或无权限，请在 Codex 中重新登录";
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests) status = "请求暂时受限，稍后重试";
            else
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(lifetime.Token);
                if (body.Length > 1024 * 1024) throw new InvalidDataException();
                last = QuotaParser.Parse(body, DateTimeOffset.UtcNow);
                fresh = true;
                status = RefreshSettings.Describe(settings.RefreshSeconds);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
        catch (FileNotFoundException) { last = null; lastIdentity = null; status = "未找到 Codex 登录，请先登录"; }
        catch (DirectoryNotFoundException) { last = null; lastIdentity = null; status = "未找到 Codex 登录，请先登录"; }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException)
        { last = null; status = "额度或登录格式不可用"; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        { /* Do not persist exception messages, response bodies, or credentials. */ }
        failures = fresh ? 0 : Math.Min(failures + 1, 10);
        var retryDelay = RetryPolicy.Delay(failures, settings.RefreshSeconds, retryAfter, DateTimeOffset.UtcNow);
        var retryAt = DateTimeOffset.UtcNow + retryDelay;
        string Status() => fresh ? RefreshSettings.Describe(settings.RefreshSeconds) : $"{status}；下次尝试 {retryAt.ToLocalTime():MM-dd HH:mm:ss}";
        Snapshot.Write(snapshotPath, Snapshot.Format(last, fresh, Status(), DateTimeOffset.UtcNow, settings));
        if (once && !fresh) Environment.ExitCode = 1;
        if (!once)
        {
            var elapsed = Stopwatch.StartNew();
            var displayMinute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            while (!lifetime.IsCancellationRequested)
            {
                var nextSettings = PluginSettings.Read(settingsPath, settings);
                var currentMinute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
                if (nextSettings != settings || currentMinute != displayMinute)
                {
                    settings = nextSettings;
                    displayMinute = currentMinute;
                    Snapshot.Write(snapshotPath, Snapshot.Format(last, fresh, Status(), DateTimeOffset.UtcNow, settings));
                }
                // Config edits cannot shorten the server's retry delay or failure backoff.
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(settings.RefreshSeconds) && elapsed.Elapsed >= retryDelay) break;
                await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token);
            }
        }
    } while (!once && !lifetime.IsCancellationRequested);
}
catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
catch (IOException) { Environment.ExitCode = 1; }
finally { gate.Release(); parent?.Dispose(); }
