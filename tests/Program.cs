using System.Runtime.InteropServices;
using System.Text.Json;
using CodexQuota;

var count = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); ++count; Console.WriteLine("PASS " + label); }
var now = DateTimeOffset.UtcNow;
string Payload(object? primary, object? secondary) => JsonSerializer.Serialize(new { rate_limit = new { primary_window = primary, secondary_window = secondary } });
object W(int seconds, double used, long? reset = null) => new { limit_window_seconds = seconds, used_percent = used, reset_at = reset ?? now.AddDays(7).ToUnixTimeSeconds() };
var q = QuotaParser.Parse(Payload(W(604800, 23), W(18000, 4.5)), now);
Check(q.FiveHour?.Remaining == "95%" && q.Weekly?.Remaining == "77%", "Swapped windows matched by duration; remaining rounded down");
var partial = QuotaParser.Parse(Payload(null, W(604800, 100)), now);
Check(partial.FiveHour is null && partial.Weekly?.Remaining == "0%", "Missing window stays unavailable, real 0% retained");
foreach (var bad in new[] { "{}", Payload(W(18000, -1), null), Payload(W(18000, 101), null), Payload(W(300, 2), null), Payload(W(18000, 3, now.ToUnixTimeSeconds()-1), null), Payload(new {limit_window_seconds=18000, reset_at=now.AddHours(5).ToUnixTimeSeconds()}, null) })
{
    bool rejected = false; try { QuotaParser.Parse(bad, now); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Reject unsupported / malformed / expired quota");
}
Check(Snapshot.Format(q, false, "offline", now).Contains("five=95%*"), "Network failure marks cached values");
Check(Snapshot.Format(q, false, "offline", now.AddMinutes(11)).Contains("five=--"), "Old cache is unavailable");
Check(Snapshot.Format(null, false, "no login", now).Contains("five=--"), "No login never becomes 0%");

var fiveReset = now.AddHours(3).ToUnixTimeSeconds();
var weekReset = now.AddDays(4).ToUnixTimeSeconds();
string ResetText(long reset) => DateTimeOffset.FromUnixTimeSeconds(reset).ToLocalTime().ToString("MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
var fiveResetText = ResetText(fiveReset);
var weekResetText = ResetText(weekReset);
var exhaustedFive = new Quota(new Window(100, fiveReset), new Window(23, weekReset), now);
var exhaustedWeek = new Quota(new Window(5, fiveReset), new Window(100, weekReset), now);
var exhaustedBoth = new Quota(new Window(100, fiveReset), new Window(100, weekReset), now);
var fiveSnapshot = Snapshot.Format(exhaustedFive, true, "ok", now);
Check(fiveSnapshot.Contains($"five={fiveResetText}\n") && fiveSnapshot.Contains("weekly=77%\n"), "Only exhausted 5h switches to its local reset time");
var weekSnapshot = Snapshot.Format(exhaustedWeek, true, "ok", now);
Check(weekSnapshot.Contains("five=95%\n") && weekSnapshot.Contains($"weekly={weekResetText}\n"), "Only exhausted 7d switches to its own reset time");
var bothSnapshot = Snapshot.Format(exhaustedBoth, false, "offline", now);
Check(bothSnapshot.Contains($"five={fiveResetText}*\n") && bothSnapshot.Contains($"weekly={weekResetText}*\n"), "Both exhausted windows retain their own reset times and stale markers");
foreach (var decimals in new[] { 0, 1 })
{
    var precision = new PluginSettings(DecimalPlaces: decimals);
    var almostEmpty = new Quota(new Window(99.95, fiveReset), null, now);
    Check(Snapshot.Format(almostEmpty, true, "ok", now, precision).Contains($"five={fiveResetText}\n"), "Remaining quota rounded to zero displays reset time");
    Check(Snapshot.Format(exhaustedBoth, true, "ok", now, precision with { ShowUsed = true }).Contains($"weekly={weekResetText}\n"), "Exhausted quota displays reset time in used mode too");
    var unused = new Quota(new Window(0, fiveReset), null, now);
    Check(Snapshot.Format(unused, true, "ok", now, precision with { ShowUsed = true }).Contains(decimals == 0 ? "five=0%\n" : "five=0.0%\n"), "Zero used quota still displays a percentage");
}
var fractional = new Quota(new Window(99.5, fiveReset), null, now);
Check(Snapshot.Format(fractional, true, "ok", now).Contains($"five={fiveResetText}\n"), "Integer remaining zero switches to reset time");
Check(Snapshot.Format(fractional, true, "ok", now, new PluginSettings(DecimalPlaces: 1)).Contains("five=0.5%\n"), "Nonzero fractional remaining quota stays a percentage");
Check(Snapshot.Format(exhaustedBoth, false, "offline", now.AddMinutes(11)).Contains("five=--\n"), "Expired cache never displays a reset time");
var justReset = new Quota(new Window(100, now.AddSeconds(1).ToUnixTimeSeconds()), new Window(100, weekReset), now);
var afterReset = Snapshot.Format(justReset, false, "offline", now.AddSeconds(2));
Check(afterReset.Contains("five=--\n") && afterReset.Contains($"weekly={weekResetText}*\n"), "Reset invalidates only its own exhausted window");

var settingsDir = Path.Combine(Path.GetTempPath(), "CodexQuota-settings-" + Guid.NewGuid().ToString("N"));
var settingsPath = Path.Combine(settingsDir, "settings.json");
try
{
    RefreshSettings.EnsureExists(settingsPath);
    Check(RefreshSettings.Read(settingsPath) == 120, "Default interval is two minutes");
    File.WriteAllText(settingsPath, "{\"refreshSeconds\":60}");
    RefreshSettings.EnsureExists(settingsPath);
    Check(RefreshSettings.Read(settingsPath) == 60, "Existing user interval survives startup");
    File.WriteAllText(settingsPath, "{\"refreshSeconds\":30}");
    Check(RefreshSettings.Read(settingsPath, 60) == 30, "Live edit shortens interval");
    File.WriteAllText(settingsPath, "{\"refreshSeconds\":300}");
    Check(RefreshSettings.Read(settingsPath, 30) == 300, "Live edit lengthens interval");
    foreach (var invalid in new[] { "{", "null", "{}", "{\"refreshSeconds\":0}", "{\"refreshSeconds\":301}", "{\"refreshSeconds\":1.5}", "{\"refreshSeconds\":\"60\"}" })
    {
        File.WriteAllText(settingsPath, invalid);
        Check(RefreshSettings.Read(settingsPath, 90) == 90, "Invalid or partial edit retains last valid interval");
    }
}
finally { Directory.Delete(settingsDir, true); }

var optionDir = Path.Combine(Path.GetTempPath(), "CodexQuota-options-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(optionDir);
try {
    var file = Path.Combine(optionDir, "settings.json");
    File.WriteAllText(file, "{\"refreshSeconds\":60,\"futureField\":42}");
    Check(PluginSettings.Read(file) == new PluginSettings(60), "Legacy interval-only settings migrated without resetting interval");
    var options = new PluginSettings(90, true, 1, 30); options.Save(file);
    Check(PluginSettings.Read(file) == options, "UI settings round-trip");
    Check(File.ReadAllText(file).Contains("futureField"), "Unknown settings survive save");
    File.WriteAllText(file, "{\"refreshSeconds\":0,\"showUsed\":\"bad\",\"decimalPlaces\":5,\"cacheMinutes\":999}");
    Check(PluginSettings.Read(file, options) == options, "Invalid fields retain previous settings");
    File.WriteAllText(file, "{");
    Check(PluginSettings.Read(file, options) == options, "Partial settings write keeps previous values");
    Check(Snapshot.Format(q, true, "ok", now, options).Contains("five=4.5%"), "Used percentage and one decimal applied");
    var tooltip = Snapshot.Format(q, true, "ok", now, options).Split('\n').Single(l => l.StartsWith("tooltip="));
    Check(tooltip.StartsWith("tooltip=5h: ") && tooltip.Contains(" | Weekly: ") && !tooltip.Contains('%') && !tooltip.Contains("ok"), "Tooltip contains reset times only");
    Check(Snapshot.Format(q, false, "offline", now.AddMinutes(15), options).Contains("five=4.5%*"), "Configured cache lifetime respected");
    Check(Snapshot.Format(q, false, "offline", now.AddMinutes(31), options).Contains("five=--"), "Configured cache expires");
    var resetQuota = new Quota(new Window(5, now.AddMinutes(1).ToUnixTimeSeconds()), null, now);
    Check(Snapshot.Format(resetQuota, false, "offline", now.AddMinutes(2), options).Contains("five=--"), "Reset invalidates even extended cache");
} finally { Directory.Delete(optionDir, true); }
Check(RetryPolicy.Delay(0, 120, null, now) == TimeSpan.Zero, "Successful query resets backoff");
Check(RetryPolicy.Delay(1, 30, null, now) == TimeSpan.FromSeconds(120), "Failure has minimum 2-minute retry");
Check(RetryPolicy.Delay(3, 120, null, now) == TimeSpan.FromSeconds(480), "Failures back off exponentially");
Check(RetryPolicy.Delay(10, 300, null, now) == TimeSpan.FromMinutes(30), "Automatic backoff capped at 30 minutes");
Check(RetryPolicy.Delay(1, 30, new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1)), now) == TimeSpan.FromHours(1), "Server Retry-After duration overrides local cap");
Check(RetryPolicy.Delay(1, 30, new System.Net.Http.Headers.RetryConditionHeaderValue(now.AddMinutes(10)), now) == TimeSpan.FromMinutes(10), "HTTP-date Retry-After honored");

if (args.Length > 0)
{
    var folder = Path.Combine(Path.GetTempPath(), "CodexQuota-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    Environment.SetEnvironmentVariable("CODEX_QUOTA_STATE_DIR", folder);
    Environment.SetEnvironmentVariable("CODEX_QUOTA_NO_LAUNCH", "1");
    var dll = NativeLibrary.Load(Path.GetFullPath(args[0]));
    try
    {
        T V<T>(nint obj, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size));
        string S(nint obj, int slot) => Marshal.PtrToStringUni(V<Native.Text>(obj, slot)(obj))!;
        var instance = Marshal.GetDelegateForFunctionPointer<Native.Instance>(NativeLibrary.GetExport(dll, "TMPluginGetInstance"))();
        Check(V<Native.Version>(instance, 0)(instance) == 8, "Native plugin API v8");
        var item = V<Native.Item>(instance, 1);
        var five = item(instance, 0); var week = item(instance, 1);
        Check(item(instance, -1) == 0 && item(instance, 2) == 0, "Native item bounds");
        Check(S(five, 1) == "CodexQuota.5h" && S(week, 1) == "CodexQuota.7d", "MSVC vtable labels and IDs");
        var refresh = V<Native.Refresh>(instance, 2);
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), Snapshot.Format(q, true, "test", now));
        refresh(instance);
        Check(S(five, 3) == "95%" && S(week, 3) == "77%", "Native plugin consumes atomic Unicode snapshot");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), Snapshot.Format(q, false, "offline", now));
        refresh(instance);
        Check(S(five, 3) == "95%*", "Native plugin displays stale marker");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), fiveSnapshot);
        refresh(instance);
        Check(S(five, 3) == fiveResetText && S(week, 3) == "77%", "Native plugin displays the exhausted 5h reset time");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), weekSnapshot);
        refresh(instance);
        Check(S(five, 3) == "95%" && S(week, 3) == weekResetText, "Native plugin displays the exhausted 7d reset time");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), bothSnapshot);
        refresh(instance);
        Check(S(five, 3) == fiveResetText + "*" && S(week, 3) == weekResetText + "*", "Native buffers preserve both full reset times and stale markers");
        Check(S(five, 4) == "88/88 88:88*" && S(week, 4) == "88/88 88:88*", "Taskbar reserves width for full reset times");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), Snapshot.Format(q, true, "recovered", now));
        refresh(instance);
        Check(S(five, 3) == "95%" && S(week, 3) == "77%", "Native plugin returns to percentages after quota recovers");
        Snapshot.Write(Path.Combine(folder, "snapshot.ini"), "[quota]\nfive=99%\nfive_until=1\nweekly=55%\nweekly_until=1\n");
        refresh(instance);
        Check(S(five, 3) == "--" && S(week, 3) == "--", "Native plugin rejects expired cached values independently of worker");
    }
    finally { NativeLibrary.Free(dll); Directory.Delete(folder, true); }
}
Console.WriteLine($"{count} checks passed.");

static class Native
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate nint Instance();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate int Version(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate nint Item(nint self, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate nint Text(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void Refresh(nint self);
}
