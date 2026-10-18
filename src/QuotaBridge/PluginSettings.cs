using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexQuota;

public sealed record PluginSettings(int RefreshSeconds = 120, bool ShowUsed = false, int DecimalPlaces = 0, int CacheMinutes = 10)
{
    public static PluginSettings Read(string path, PluginSettings? previous = null)
    {
        var result = previous ?? new();
        try
        {
            if (new FileInfo(path).Length > 16384) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return result;
            int Integer(string name, int fallback, int min, int max) => root.TryGetProperty(name, out var v) &&
                v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= min && n <= max ? n : fallback;
            var used = root.TryGetProperty("showUsed", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False ? b.GetBoolean() : result.ShowUsed;
            return new(Integer("refreshSeconds", result.RefreshSeconds, 30, 300), used,
                Integer("decimalPlaces", result.DecimalPlaces, 0, 1), Integer("cacheMinutes", result.CacheMinutes, 10, 60));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return result; }
    }

    public void Save(string path)
    {
        if (RefreshSeconds is < 30 or > 300 || DecimalPlaces is < 0 or > 1 || CacheMinutes is < 10 or > 60)
            throw new InvalidDataException("设置超出支持范围。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject root = new();
        // Preserve unknown fields for compatibility with future versions and scripts.
        if (File.Exists(path))
        {
            try { root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new(); }
            catch (JsonException) { }
        }
        root["refreshSeconds"] = RefreshSeconds; root["showUsed"] = ShowUsed;
        root["decimalPlaces"] = DecimalPlaces; root["cacheMinutes"] = CacheMinutes;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
