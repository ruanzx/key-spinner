using System.Text;

namespace KeySpinner;

public class ApiKeyService : IApiKeyService
{
    private readonly IKeyQueue _keyQueue;
    private readonly ISystemClock _systemClock;

    public ApiKeyService(IKeyQueue keyQueue, ISystemClock systemClock)
    {
        _keyQueue = keyQueue;
        _systemClock = systemClock;
    }

    public ApiKeyService(IKeyQueue keyQueue) : this(keyQueue, new SystemClock())
    {
    }

    public ApiKey? GetAvailableKey()
    {
        ApiKey? apiKey = null;
        int attemptCount = 0;
        int maxAttempts = _keyQueue.Count;  // Prevent infinite loop if all keys are rate-limited

        // Try to find a key that is not rate limited
        do
        {
            apiKey = _keyQueue.Dequeue();

            if (apiKey == null)
            {
                throw new InvalidOperationException("No API keys available");
            }

            lock (apiKey.Lock)
            {
                // Reset counters if needed
                apiKey.ResetCounters(_systemClock.UtcNow);

                if (!IsKeyRateLimited(apiKey))
                {
                    // Update usage information
                    apiKey.LastAccessTimeUtc = _systemClock.UtcNow;
                    apiKey.IncrementUsage();

                    // Add key to queue again, ready for next request
                    _keyQueue.Enqueue(apiKey);

                    return apiKey;
                }
            }

            // Re-queue the rate-limited key for later use
            _keyQueue.Enqueue(apiKey);
            attemptCount++;

        } while (attemptCount < maxAttempts);

        // All API keys are currently rate-limited
        return null;
    }

    public void ReleaseKey(ApiKey apiKey)
    {
        _keyQueue.Enqueue(apiKey);
    }

    public bool IsKeyRateLimited(ApiKey apiKey)
    {
        DateTime now = _systemClock.UtcNow;
        if (apiKey.ExpirationTimeUtc < now)
        {
            return true;
        }

        // Check minute rate limit
        if (apiKey.RateLimitPerMinute > 0 && apiKey.MinuteCounter >= apiKey.RateLimitPerMinute)
        {
            return true;
        }

        // Check hour rate limit
        if (apiKey.RateLimitPerHour > 0 && apiKey.HourCounter >= apiKey.RateLimitPerHour)
        {
            return true;
        }

        // Check day rate limit
        if (apiKey.RateLimitPerDay > 0 && apiKey.DayCounter >= apiKey.RateLimitPerDay)
        {
            return true;
        }

        // Check month rate limit
        if (apiKey.RateLimitPerMonth > 0 && apiKey.MonthCounter >= apiKey.RateLimitPerMonth)
        {
            return true;
        }

        return false;
    }

    public void RotateKeys()
    {
        // Rotate keys in a round-robin fashion
        var apiKey = _keyQueue.Dequeue();
        if (apiKey != null)
        {
            _keyQueue.Enqueue(apiKey);
        }
    }

    /// <summary>
    /// Helper method to print key status in a readable format
    /// </summary>
    public string PrintKeyStatus(KeyStatus status)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Key Status: {status.KeyId}");
        sb.AppendLine($"  Expired: {status.IsExpired} (Expires in: {FormatTimeSpan(status.TimeToExpiration)})");
        sb.AppendLine($"  Last Access: {status.LastAccessTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Rate Limited: {status.IsRateLimited}");

        sb.AppendLine("  Rate Limits:");
        sb.AppendLine($"    Minute: {status.MinuteUsage.Current}/{status.MinuteUsage.Limit} (Resets in: {FormatTimeSpan(status.MinuteUsage.TimeToReset)})");
        sb.AppendLine($"    Hour: {status.HourUsage.Current}/{status.HourUsage.Limit} (Resets in: {FormatTimeSpan(status.HourUsage.TimeToReset)})");
        sb.AppendLine($"    Day: {status.DayUsage.Current}/{status.DayUsage.Limit} (Resets in: {FormatTimeSpan(status.DayUsage.TimeToReset)})");
        sb.AppendLine($"    Month: {status.MonthUsage.Current}/{status.MonthUsage.Limit} (Resets in: {FormatTimeSpan(status.MonthUsage.TimeToReset)})");

        return sb.ToString();
    }

    public string PrintKeyStatus(ApiKey? apiKey)
    {
        if (apiKey == null)
        {
            return "All API keys are currently rate-limited";
        }

        var status = apiKey.GetStatus(_systemClock.UtcNow);
        return PrintKeyStatus(status);
    }

    /// <summary>
    /// Helper method to format TimeSpan in a readable way
    /// </summary>
    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.TotalDays:F1} days";
        else if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.TotalHours:F1} hours";
        else if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.TotalMinutes:F1} minutes";
        else
            return $"{timeSpan.TotalSeconds:F1} seconds";
    }
}
