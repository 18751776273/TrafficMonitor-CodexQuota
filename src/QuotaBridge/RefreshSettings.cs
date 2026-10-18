using System.Text.Json;

namespace CodexQuota;

public static class RefreshSettings
{
    public const int DefaultSeconds = 120;
    public const int MinimumSeconds = 30;
    public const int MaximumSeconds = 300;

    public static int Read(string path, int previous = DefaultSeconds)
    {
        try
        {
            if (new FileInfo(path).Length > 4096) return previous;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("refreshSeconds", out var value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var seconds) &&
                seconds is >= MinimumSeconds and <= MaximumSeconds)
                return seconds;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        // A partial write or invalid edit keeps the last valid setting.
        return previous;
    }

    public static void EnsureExists(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(file);
            writer.WriteLine("{\n  \"refreshSeconds\": 120\n}");
        }
        catch (IOException) when (File.Exists(path)) { }
    }

    public static string Describe(int seconds) => seconds % 60 == 0 ? $"每 {seconds / 60} 分钟刷新" : $"每 {seconds} 秒刷新";
}
