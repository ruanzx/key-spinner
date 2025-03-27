namespace KeySpinner.UnitTests;

public class ApiKeyTests
{
    private static readonly DateTime BaseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_InitializesProperties_WithDefaultValues()
    {
        // Act
        var apiKey = new ApiKey();

        // Assert
        Assert.Null(apiKey.Key);
        Assert.Equal(default, apiKey.ExpirationTimeUtc);
        Assert.Equal(default, apiKey.LastAccessTimeUtc);
        Assert.Equal(0, apiKey.MinuteCounter);
        Assert.Equal(0, apiKey.HourCounter);
        Assert.Equal(0, apiKey.DayCounter);
        Assert.Equal(0, apiKey.MonthCounter);
        Assert.Equal(DateTime.MinValue, apiKey.LastMinuteResetUtc);
        Assert.Equal(DateTime.MinValue, apiKey.LastHourResetUtc);
        Assert.Equal(DateTime.MinValue, apiKey.LastDayResetUtc);
        Assert.Equal(DateTime.MinValue, apiKey.LastMonthResetUtc);
        Assert.Equal(0, apiKey.RateLimitPerMinute);
        Assert.Equal(0, apiKey.RateLimitPerHour);
        Assert.Equal(0, apiKey.RateLimitPerDay);
        Assert.Equal(0, apiKey.RateLimitPerMonth);
        Assert.NotNull(apiKey.Lock);
    }

    [Fact]
    public void IncrementUsage_IncrementsAllCounters()
    {
        // Arrange
        var apiKey = new ApiKey();

        // Act
        apiKey.IncrementUsage();

        // Assert
        Assert.Equal(1, apiKey.MinuteCounter);
        Assert.Equal(1, apiKey.HourCounter);
        Assert.Equal(1, apiKey.DayCounter);
        Assert.Equal(1, apiKey.MonthCounter);

        // Act again
        apiKey.IncrementUsage();

        // Assert again
        Assert.Equal(2, apiKey.MinuteCounter);
        Assert.Equal(2, apiKey.HourCounter);
        Assert.Equal(2, apiKey.DayCounter);
        Assert.Equal(2, apiKey.MonthCounter);
    }

    [Fact]
    public void ResetCounters_InitializesResetTimesOnFirstCall()
    {
        // Arrange
        var apiKey = new ApiKey();
        var now = BaseTime;

        // Act
        apiKey.ResetCounters(now);

        // Assert
        Assert.Equal(new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc), apiKey.LastMinuteResetUtc);
        Assert.Equal(new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc), apiKey.LastHourResetUtc);
        Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), apiKey.LastDayResetUtc);
        Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), apiKey.LastMonthResetUtc);
    }

    [Fact]
    public void ResetCounters_ResetsMinuteCounter_WhenMinuteChanges()
    {
        // Arrange
        var apiKey = new ApiKey { MinuteCounter = 5 };
        var now = BaseTime;
        apiKey.ResetCounters(now); // Initialize reset times

        // Act - advance one minute
        var nextMinute = now.AddMinutes(1);
        apiKey.ResetCounters(nextMinute);

        // Assert
        Assert.Equal(0, apiKey.MinuteCounter);
        Assert.Equal(new DateTime(2023, 1, 1, 12, 1, 0, DateTimeKind.Utc), apiKey.LastMinuteResetUtc);
    }

    [Fact]
    public void ResetCounters_ResetsHourCounter_WhenHourChanges()
    {
        // Arrange
        var apiKey = new ApiKey { HourCounter = 50 };
        var now = BaseTime;
        apiKey.ResetCounters(now); // Initialize reset times

        // Act - advance one hour
        var nextHour = now.AddHours(1);
        apiKey.ResetCounters(nextHour);

        // Assert
        Assert.Equal(0, apiKey.HourCounter);
        Assert.Equal(new DateTime(2023, 1, 1, 13, 0, 0, DateTimeKind.Utc), apiKey.LastHourResetUtc);
    }

    [Fact]
    public void ResetCounters_ResetsDayCounter_WhenDayChanges()
    {
        // Arrange
        var apiKey = new ApiKey { DayCounter = 500 };
        var now = BaseTime;
        apiKey.ResetCounters(now); // Initialize reset times

        // Act - advance one day
        var nextDay = now.AddDays(1);
        apiKey.ResetCounters(nextDay);

        // Assert
        Assert.Equal(0, apiKey.DayCounter);
        Assert.Equal(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc), apiKey.LastDayResetUtc);
    }

    [Fact]
    public void ResetCounters_ResetsMonthCounter_WhenMonthChanges()
    {
        // Arrange
        var apiKey = new ApiKey { MonthCounter = 5000 };
        var now = BaseTime;
        apiKey.ResetCounters(now); // Initialize reset times

        // Act - advance one month
        var nextMonth = now.AddMonths(1);
        apiKey.ResetCounters(nextMonth);

        // Assert
        Assert.Equal(0, apiKey.MonthCounter);
        Assert.Equal(new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc), apiKey.LastMonthResetUtc);
    }

    [Fact]
    public void ResetCounters_DoesNotResetCounters_WhenTimePeriodsHaveNotChanged()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            MinuteCounter = 5,
            HourCounter = 50,
            DayCounter = 500,
            MonthCounter = 5000
        };
        var now = BaseTime;
        apiKey.ResetCounters(now); // Initialize reset times

        // Act - advance time but stay in same minute
        var sameMinute = now.AddSeconds(30);
        apiKey.ResetCounters(sameMinute);

        // Assert - counters should remain unchanged
        Assert.Equal(0, apiKey.MinuteCounter);
        Assert.Equal(0, apiKey.HourCounter);
        Assert.Equal(0, apiKey.DayCounter);
        Assert.Equal(0, apiKey.MonthCounter);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectKeyStatus_ForNonExpiredKey()
    {
        // Arrange
        var now = BaseTime;
        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = now.AddDays(30),
            RateLimitPerMinute = 10,
            RateLimitPerHour = 100,
            RateLimitPerDay = 1000,
            RateLimitPerMonth = 10000,
            MinuteCounter = 3,
            HourCounter = 30,
            DayCounter = 300,
            MonthCounter = 3000,
            LastAccessTimeUtc = now.AddMinutes(-5)
        };
        apiKey.ResetCounters(now); // Initialize reset times

        // Act
        var status = apiKey.GetStatus(now);

        // Assert
        Assert.Equal("test-key", status.KeyId);
        Assert.False(status.IsExpired);
        Assert.Equal(now.AddDays(30) - now, status.TimeToExpiration);
        Assert.Equal(now.AddMinutes(-5), status.LastAccessTime);

        // Check minute usage
        Assert.Equal(0, status.MinuteUsage.Current);
        Assert.Equal(10, status.MinuteUsage.Limit);
        Assert.Equal(10, status.MinuteUsage.Remaining);
        Assert.Equal(now.AddMinutes(1), status.MinuteUsage.ResetsAt);
        Assert.Equal(TimeSpan.FromMinutes(1), status.MinuteUsage.TimeToReset);

        // Check hour usage
        Assert.Equal(0, status.HourUsage.Current);
        Assert.Equal(100, status.HourUsage.Limit);
        Assert.Equal(100, status.HourUsage.Remaining);
        Assert.Equal(now.AddHours(1), status.HourUsage.ResetsAt);
        Assert.Equal(TimeSpan.FromHours(1), status.HourUsage.TimeToReset);

        // Check day usage
        Assert.Equal(0, status.DayUsage.Current);
        Assert.Equal(1000, status.DayUsage.Limit);
        Assert.Equal(1000, status.DayUsage.Remaining);
        Assert.Equal(now.Date.AddDays(1), status.DayUsage.ResetsAt);
        Assert.Equal(now.Date.AddDays(1) - now, status.DayUsage.TimeToReset);

        // Check month usage
        Assert.Equal(0, status.MonthUsage.Current);
        Assert.Equal(10000, status.MonthUsage.Limit);
        Assert.Equal(10000, status.MonthUsage.Remaining);
        Assert.Equal(new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc), status.MonthUsage.ResetsAt);
        Assert.Equal(new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc) - now, status.MonthUsage.TimeToReset);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectKeyStatus_ForExpiredKey()
    {
        // Arrange
        var now = BaseTime;
        var apiKey = new ApiKey
        {
            Key = "expired-key",
            ExpirationTimeUtc = now.AddDays(-1), // Expired 1 day ago
            LastAccessTimeUtc = now.AddDays(-2)
        };

        // Act
        var status = apiKey.GetStatus(now);

        // Assert
        Assert.Equal("expired-key", status.KeyId);
        Assert.True(status.IsExpired);
        Assert.Equal(TimeSpan.Zero, status.TimeToExpiration);
    }

    [Fact]
    public void GetStatus_HandlesRateLimits_WithZeroValues()
    {
        // Arrange
        var now = BaseTime;
        var apiKey = new ApiKey
        {
            Key = "unlimited-key",
            ExpirationTimeUtc = now.AddDays(30),
            RateLimitPerMinute = 0, // 0 means unlimited
            RateLimitPerHour = 0,
            RateLimitPerDay = 0,
            RateLimitPerMonth = 0,
            MinuteCounter = 100,
            HourCounter = 1000,
            DayCounter = 10000,
            MonthCounter = 100000
        };

        // Act
        var status = apiKey.GetStatus(now);

        // Assert
        Assert.Equal(int.MaxValue, status.MinuteUsage.Remaining);
        Assert.Equal(int.MaxValue, status.HourUsage.Remaining);
        Assert.Equal(int.MaxValue, status.DayUsage.Remaining);
        Assert.Equal(int.MaxValue, status.MonthUsage.Remaining);
    }

    [Fact]
    public void GetStatus_CalculatesCorrectResetTimes_WhenCrossingYearBoundary()
    {
        // Arrange
        var decemberDate = new DateTime(2023, 12, 31, 23, 59, 30, DateTimeKind.Utc);
        var apiKey = new ApiKey
        {
            Key = "year-end-key",
            ExpirationTimeUtc = decemberDate.AddDays(30)
        };
        apiKey.ResetCounters(decemberDate); // Initialize reset times

        // Act
        var status = apiKey.GetStatus(decemberDate);

        // Assert
        // Next minute is in the next year
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), status.MinuteUsage.ResetsAt);

        // Next month is in the next year
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), status.MonthUsage.ResetsAt);
    }
}
