namespace KeySpinner;

/// <summary>
/// Represents the status of a rate limit for a specific time period
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// Current usage count for this period
    /// </summary>
    public int Current { get; set; }

    /// <summary>
    /// Maximum allowed usage for this period (0 = unlimited)
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Remaining capacity for this period
    /// </summary>
    public int Remaining { get; set; }

    /// <summary>
    /// When the counter will reset
    /// </summary>
    public DateTime ResetsAt { get; set; }

    /// <summary>
    /// Time until the counter resets
    /// </summary>
    public TimeSpan TimeToReset { get; set; }
}
