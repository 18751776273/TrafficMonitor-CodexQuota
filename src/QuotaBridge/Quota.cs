using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CodexQuota;

public record Window(double Used, long ResetAt)
{
    public string Remaining => Math.Floor(100 - Used).ToString(CultureInfo.InvariantCulture) + "%";
}
public record Quota(Window? FiveHour, Window? Weekly, DateTimeOffset FetchedAt);

public static class QuotaParser
{
    public static Quota Parse(string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("rate_limit", out var limits) || limits.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Missing rate_limit");
        Window? five = null, weekly = null;
        foreach (var name in new[] { "primary_window", "secondary_window" })
        {
            if (!limits.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object) continue;
            if (!w.TryGetProperty("limit_window_seconds", out var d) || !d.TryGetInt64(out var duration)) continue;
            if (!w.TryGetProperty("used_percent", out var u) || !u.TryGetDouble(out var used) || !double.IsFinite(used) || used < 0 || used > 100) continue;
            if (!w.TryGetProperty("reset_at", out var r) || !r.TryGetInt64(out var reset) || reset <= now.ToUnixTimeSeconds() || reset > 253402300799) continue;
            if (duration == 18000) five = new(used, reset);
            if (duration == 604800) weekly = new(used, reset);
        }
        if (five is null && weekly is null) throw new InvalidDataException("No supported quota windows");
        return new(five, weekly, now);
    }
}

public static class Snapshot
{
    public static string Format(Quota? quota, bool fresh, string status, DateTimeOffset now, PluginSettings? settings = null)
    {
        settings ??= new();
        var usable = quota != null && now >= quota.FetchedAt.AddMinutes(-1) && now - quota.FetchedAt < TimeSpan.FromMinutes(settings.CacheMinutes);
        string Value(Window? w) {
            if (!usable || w is null || w.ResetAt <= now.ToUnixTimeSeconds()) return "--";
            var value = settings.ShowUsed ? w.Used : 100 - w.Used;
            var factor = settings.DecimalPlaces == 1 ? 10.0 : 1.0;
            value = (settings.ShowUsed ? Math.Ceiling(value * factor) : Math.Floor(value * factor)) / factor;
            return value.ToString(settings.DecimalPlaces == 1 ? "F1" : "F0", CultureInfo.InvariantCulture) + "%" + (fresh ? "" : "*");
        }
        string Reset(Window? w) => w is null ? "unavailable" : DateTimeOffset.FromUnixTimeSeconds(w.ResetAt).ToLocalTime().ToString("MM-dd HH:mm");
        var deadline = usable ? quota!.FetchedAt.AddMinutes(settings.CacheMinutes).ToUnixTimeSeconds() : 0;
        // Each window has its own deadline; a reset must never leave a misleading old value.
        var fiveDeadline = quota?.FiveHour is {} a ? Math.Min(deadline, a.ResetAt) : 0;
        var weekDeadline = quota?.Weekly is {} b ? Math.Min(deadline, b.ResetAt) : 0;
        var tooltip = $"Codex {(settings.ShowUsed ? "已用" : "剩余")}额度 | {status} | 5h 重置: {Reset(quota?.FiveHour)} | 7d 重置: {Reset(quota?.Weekly)} | 更新: {quota?.FetchedAt.ToLocalTime().ToString("HH:mm:ss") ?? "尚未成功"}";
        return $"[quota]\nfive={Value(quota?.FiveHour)}\nweekly={Value(quota?.Weekly)}\nfive_until={fiveDeadline}\nweekly_until={weekDeadline}\ntooltip={tooltip}\n";
    }

    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.Unicode);
        File.Move(tmp, path, true);
    }
}
