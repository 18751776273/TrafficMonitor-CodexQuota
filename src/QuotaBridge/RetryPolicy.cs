using System.Net.Http.Headers;

namespace CodexQuota;

public static class RetryPolicy
{
    public static TimeSpan Delay(int failures, int refreshSeconds, RetryConditionHeaderValue? retryAfter, DateTimeOffset now)
    {
        if (failures <= 0) return TimeSpan.Zero;
        var seconds = Math.Min(1800, Math.Max(120, refreshSeconds) * Math.Pow(2, Math.Min(failures - 1, 5)));
        var server = retryAfter?.Delta ?? (retryAfter?.Date - now) ?? TimeSpan.Zero;
        return server > TimeSpan.FromSeconds(seconds) ? server : TimeSpan.FromSeconds(seconds);
    }
}
