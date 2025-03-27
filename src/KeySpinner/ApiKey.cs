namespace KeySpinner;

public class ApiKey
{
    public string Key { get; set; }

    public DateTime ExpirationTimeUtc { get; set; }
    public DateTime LastAccessTimeUtc { get; set; }

    // Period-specific counters
    public int MinuteCounter { get; set; }
    public int HourCounter { get; set; }
    public int DayCounter { get; set; }
    public int MonthCounter { get; set; }

    // Track the last reset time for each period
    public DateTime LastMinuteResetUtc { get; set; } = DateTime.MinValue;
    public DateTime LastHourResetUtc { get; set; } = DateTime.MinValue;
    public DateTime LastDayResetUtc { get; set; } = DateTime.MinValue;
    public DateTime LastMonthResetUtc { get; set; } = DateTime.MinValue;


    public int RateLimitPerMonth { get; set; }
    public int RateLimitPerDay { get; set; }
    public int RateLimitPerHour { get; set; }
    public int RateLimitPerMinute { get; set; }

    public object Lock { get; } = new object();

    /// <summary>
    /// Resets counters for time periods that have elapsed
    /// </summary>
    public void ResetCounters(DateTime utcNow)
    {
        // Get current period start times
        var currentMinuteStart = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc);
        var currentHourStart = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);
        var currentDayStart = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Reset minute counter if we're in a new minute
        if (LastMinuteResetUtc < currentMinuteStart)
        {
            MinuteCounter = 0;
            LastMinuteResetUtc = currentMinuteStart;
        }

        // Reset hour counter if we're in a new hour
        if (LastHourResetUtc < currentHourStart)
        {
            HourCounter = 0;
            LastHourResetUtc = currentHourStart;
        }

        // Reset day counter if we're in a new day
        if (LastDayResetUtc < currentDayStart)
        {
            DayCounter = 0;
            LastDayResetUtc = currentDayStart;
        }

        // Reset month counter if we're in a new month
        if (LastMonthResetUtc < currentMonthStart)
        {
            MonthCounter = 0;
            LastMonthResetUtc = currentMonthStart;
        }
    }

    /// <summary>
    /// Increments usage counters for all time periods
    /// </summary>
    public void IncrementUsage()
    {
        MinuteCounter++;
        HourCounter++;
        DayCounter++;
        MonthCounter++;
    }

    /// <summary>
    /// Gets the current status of the API key including usage, limits, and expiration
    /// </summary>
    /// <returns>A KeyStatus object containing the API key's current status</returns>
    public KeyStatus GetStatus(DateTime utcNow)
    {
        // Make sure counters are up-to-date
        ResetCounters(utcNow);

        var isExpired = ExpirationTimeUtc < utcNow;
        var timeToExpiration = isExpired ? TimeSpan.Zero : ExpirationTimeUtc - utcNow;

        // Calculate remaining capacity for each period
        var minuteCapacityRemaining = RateLimitPerMinute > 0
            ? Math.Max(0, RateLimitPerMinute - MinuteCounter)
            : int.MaxValue;

        var hourCapacityRemaining = RateLimitPerHour > 0
            ? Math.Max(0, RateLimitPerHour - HourCounter)
            : int.MaxValue;

        var dayCapacityRemaining = RateLimitPerDay > 0
            ? Math.Max(0, RateLimitPerDay - DayCounter)
            : int.MaxValue;

        var monthCapacityRemaining = RateLimitPerMonth > 0
            ? Math.Max(0, RateLimitPerMonth - MonthCounter)
            : int.MaxValue;

        // Calculate when the next reset will occur for each period
        var nextMinuteReset = LastMinuteResetUtc.AddMinutes(1);
        var nextHourReset = LastHourResetUtc.AddHours(1);
        var nextDayReset = LastDayResetUtc.AddDays(1);
        var nextMonthReset = new DateTime(
            LastMonthResetUtc.Year + (LastMonthResetUtc.Month == 12 ? 1 : 0),
            LastMonthResetUtc.Month == 12 ? 1 : LastMonthResetUtc.Month + 1,
            1,
            0, 0, 0,
            DateTimeKind.Utc
        );

        return new KeyStatus
        {
            KeyId = Key,
            IsExpired = isExpired,
            TimeToExpiration = timeToExpiration,
            LastAccessTime = LastAccessTimeUtc,

            MinuteUsage = new RateLimitStatus
            {
                Current = MinuteCounter,
                Limit = RateLimitPerMinute,
                Remaining = minuteCapacityRemaining,
                ResetsAt = nextMinuteReset,
                TimeToReset = nextMinuteReset - utcNow
            },

            HourUsage = new RateLimitStatus
            {
                Current = HourCounter,
                Limit = RateLimitPerHour,
                Remaining = hourCapacityRemaining,
                ResetsAt = nextHourReset,
                TimeToReset = nextHourReset - utcNow
            },

            DayUsage = new RateLimitStatus
            {
                Current = DayCounter,
                Limit = RateLimitPerDay,
                Remaining = dayCapacityRemaining,
                ResetsAt = nextDayReset,
                TimeToReset = nextDayReset - utcNow
            },

            MonthUsage = new RateLimitStatus
            {
                Current = MonthCounter,
                Limit = RateLimitPerMonth,
                Remaining = monthCapacityRemaining,
                ResetsAt = nextMonthReset,
                TimeToReset = nextMonthReset - utcNow
            }
        };
    }
}
