namespace KeySpinner;

/// <summary>
/// Provides comprehensive status information for an API key
/// </summary>
public class KeyStatus
{
    /// <summary>
    /// The API key identifier
    /// </summary>
    public string KeyId { get; set; }

    /// <summary>
    /// Whether the key has expired
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// Time until the key expires
    /// </summary>
    public TimeSpan TimeToExpiration { get; set; }

    /// <summary>
    /// When the key was last used
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// Per-minute rate limit status
    /// </summary>
    public RateLimitStatus MinuteUsage { get; set; }

    /// <summary>
    /// Per-hour rate limit status
    /// </summary>
    public RateLimitStatus HourUsage { get; set; }

    /// <summary>
    /// Per-day rate limit status
    /// </summary>
    public RateLimitStatus DayUsage { get; set; }

    /// <summary>
    /// Per-month rate limit status
    /// </summary>
    public RateLimitStatus MonthUsage { get; set; }

    /// <summary>
    /// Determines if the key is currently rate limited for any period
    /// </summary>
    public bool IsRateLimited =>
        MinuteUsage.Remaining <= 0 ||
        HourUsage.Remaining <= 0 ||
        DayUsage.Remaining <= 0 ||
        MonthUsage.Remaining <= 0;
}