using Moq;

namespace KeySpinner.UnitTests;

public class ApiKeyServiceTests
{
    private static readonly DateTime BaseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    #region Constructor Tests

    [Fact]
    public void Constructor_WithKeyQueueAndSystemClock_InitializesProperties()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();

        // Act
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Assert - no exception means success
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithKeyQueueOnly_UsesDefaultSystemClock()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();

        // Act
        var service = new ApiKeyService(mockKeyQueue.Object);

        // Assert - no exception means success
        Assert.NotNull(service);
    }

    #endregion

    #region GetAvailableKey Tests

    [Fact]
    public void GetAvailableKey_WhenNoKeysInQueue_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockKeyQueue.Setup(q => q.Dequeue()).Returns((ApiKey)null);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.GetAvailableKey());
    }

    [Fact]
    public void GetAvailableKey_WhenKeyIsAvailable_ReturnsKeyAndRequeuesIt()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);

        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = 10,
            MinuteCounter = 0
        };

        mockKeyQueue.Setup(q => q.Dequeue()).Returns(apiKey);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.GetAvailableKey();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-key", result.Key);
        Assert.Equal(1, result.MinuteCounter); // Counter should be incremented
        Assert.Equal(BaseTime, result.LastAccessTimeUtc); // Last access time should be updated
        mockKeyQueue.Verify(q => q.Enqueue(apiKey), Times.Once); // Key should be requeued
    }

    [Fact]
    public void GetAvailableKey_WhenKeyIsRateLimited_TriesNextKey()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockKeyQueue.Setup(q => q.Count).Returns(2);
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);

        var rateLimitedKey = new ApiKey
        {
            Key = "limited-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = 1,
            MinuteCounter = 1, // Already at limit
            LastMinuteResetUtc = BaseTime.AddMinutes(5)
        };

        var availableKey = new ApiKey
        {
            Key = "available-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = 10,
            MinuteCounter = 0
        };

        // Setup queue to return limited key first, then available key
        var callCount = 0;
        mockKeyQueue.Setup(q => q.Dequeue()).Returns(() =>
        {
            callCount++;
            return callCount == 1 ? rateLimitedKey : availableKey;
        });

        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.GetAvailableKey();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("available-key", result.Key);
        mockKeyQueue.Verify(q => q.Enqueue(rateLimitedKey), Times.Once); // Rate-limited key should be requeued
        mockKeyQueue.Verify(q => q.Enqueue(availableKey), Times.Once); // Available key should be requeued
    }

    [Fact]
    public void GetAvailableKey_WhenAllKeysRateLimited_ReturnsNull()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockKeyQueue.Setup(q => q.Count).Returns(1);
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);

        var rateLimitedKey = new ApiKey
        {
            Key = "limited-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = 1,
            MinuteCounter = 2, // Clearly over the limit to ensure it's rate limited
            LastMinuteResetUtc = BaseTime.AddMinutes(5)
        };

        mockKeyQueue.Setup(q => q.Dequeue()).Returns(rateLimitedKey);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.GetAvailableKey();

        // Assert
        Assert.Null(result);
        mockKeyQueue.Verify(q => q.Enqueue(rateLimitedKey), Times.Once); // Rate-limited key should be requeued
    }

    [Fact]
    public void GetAvailableKey_ResetsCounters_BeforeCheckingRateLimits()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockKeyQueue.Setup(q => q.Count).Returns(1);

        // Key was rate limited in the previous minute
        var previousMinute = BaseTime.AddMinutes(-1);
        var currentMinute = BaseTime;

        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = 1,
            MinuteCounter = 1, // At limit for previous minute
            LastMinuteResetUtc = previousMinute
        };

        mockKeyQueue.Setup(q => q.Dequeue()).Returns(apiKey);
        mockClock.Setup(c => c.UtcNow).Returns(currentMinute);

        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.GetAvailableKey();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-key", result.Key);
        Assert.Equal(1, result.MinuteCounter); // Should be reset to 0 and then incremented to 1
        Assert.Equal(currentMinute, result.LastMinuteResetUtc); // Reset time should be updated
    }

    [Fact]
    public void GetAvailableKey_WhenKeyExpired_TreatsAsRateLimited()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockKeyQueue.Setup(q => q.Count).Returns(1);
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);

        var expiredKey = new ApiKey
        {
            Key = "expired-key",
            ExpirationTimeUtc = BaseTime.AddDays(-1), // Expired yesterday
            RateLimitPerMinute = 10,
            MinuteCounter = 0
        };

        mockKeyQueue.Setup(q => q.Dequeue()).Returns(expiredKey);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.GetAvailableKey();

        // Assert
        Assert.Null(result);
        mockKeyQueue.Verify(q => q.Enqueue(expiredKey), Times.Once); // Expired key should be requeued
    }

    #endregion

    #region ReleaseKey Tests

    [Fact]
    public void ReleaseKey_EnqueuesKeyBackToQueue()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);
        var apiKey = new ApiKey { Key = "test-key" };

        // Act
        service.ReleaseKey(apiKey);

        // Assert
        mockKeyQueue.Verify(q => q.Enqueue(apiKey), Times.Once);
    }

    #endregion

    #region IsKeyRateLimited Tests

    [Fact]
    public void IsKeyRateLimited_WhenKeyExpired_ReturnsTrue()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        var apiKey = new ApiKey
        {
            Key = "expired-key",
            ExpirationTimeUtc = BaseTime.AddDays(-1) // Expired yesterday
        };

        // Act
        var result = service.IsKeyRateLimited(apiKey);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(1, 1, 0, 0, 0, 0, 0, 0, true)]  // Minute limit reached
    [InlineData(0, 0, 10, 10, 0, 0, 0, 0, true)] // Hour limit reached
    [InlineData(0, 0, 0, 0, 5, 5, 0, 0, true)] // Day limit reached
    [InlineData(0, 0, 0, 0, 0, 0, 100, 100, true)] // Month limit reached
    [InlineData(10, 9, 100, 90, 1000, 900, 10000, 9000, false)] // No limits reached
    [InlineData(0, 100, 0, 100, 0, 100, 0, 100, false)] // No limits set (unlimited)
    public void IsKeyRateLimited_ChecksAllRateLimits(
        int minuteLimit, int minuteCount,
        int hourLimit, int hourCount,
        int dayLimit, int dayCount,
        int monthLimit, int monthCount,
        bool expectedResult)
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.UtcNow).Returns(BaseTime);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = BaseTime.AddDays(1),
            RateLimitPerMinute = minuteLimit,
            MinuteCounter = minuteCount,
            RateLimitPerHour = hourLimit,
            HourCounter = hourCount,
            RateLimitPerDay = dayLimit,
            DayCounter = dayCount,
            RateLimitPerMonth = monthLimit,
            MonthCounter = monthCount
        };

        // Act
        var result = service.IsKeyRateLimited(apiKey);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    #endregion

    #region RotateKeys Tests

    [Fact]
    public void RotateKeys_DequeuesAndRequeuesKey()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        var apiKey = new ApiKey { Key = "test-key" };

        mockKeyQueue.Setup(q => q.Dequeue()).Returns(apiKey);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        service.RotateKeys();

        // Assert
        mockKeyQueue.Verify(q => q.Dequeue(), Times.Once);
        mockKeyQueue.Verify(q => q.Enqueue(apiKey), Times.Once);
    }

    [Fact]
    public void RotateKeys_WhenQueueEmpty_DoesNothing()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();

        mockKeyQueue.Setup(q => q.Dequeue()).Returns((ApiKey)null);
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        service.RotateKeys();

        // Assert
        mockKeyQueue.Verify(q => q.Dequeue(), Times.Once);
        mockKeyQueue.Verify(q => q.Enqueue(It.IsAny<ApiKey>()), Times.Never);
    }

    #endregion

    #region PrintKeyStatus Tests

    [Fact]
    public void PrintKeyStatus_WithNullApiKey_ReturnsRateLimitedMessage()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        // Act
        var result = service.PrintKeyStatus((ApiKey)null);

        // Assert
        Assert.Equal("All API keys are currently rate-limited", result);
    }

    [Fact]
    public void PrintKeyStatus_WithKeyStatus_ReturnsFormattedStatus()
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);

        var status = new KeyStatus
        {
            KeyId = "test-key",
            IsExpired = false,
            TimeToExpiration = TimeSpan.FromDays(1),
            LastAccessTime = BaseTime.AddMinutes(-5),
            MinuteUsage = new RateLimitStatus
            {
                Current = 5,
                Limit = 10,
                Remaining = 5,
                ResetsAt = BaseTime.AddMinutes(1),
                TimeToReset = TimeSpan.FromMinutes(1)
            },
            HourUsage = new RateLimitStatus
            {
                Current = 50,
                Limit = 100,
                Remaining = 50,
                ResetsAt = BaseTime.AddHours(1),
                TimeToReset = TimeSpan.FromHours(1)
            },
            DayUsage = new RateLimitStatus
            {
                Current = 500,
                Limit = 1000,
                Remaining = 500,
                ResetsAt = BaseTime.AddDays(1),
                TimeToReset = TimeSpan.FromDays(1)
            },
            MonthUsage = new RateLimitStatus
            {
                Current = 5000,
                Limit = 10000,
                Remaining = 5000,
                ResetsAt = BaseTime.AddMonths(1),
                TimeToReset = TimeSpan.FromDays(31)
            }
        };

        // Act
        var result = service.PrintKeyStatus(status);

        // Assert
        Assert.Contains("Key Status: test-key", result);
        Assert.Contains("Expired: False", result);
        Assert.Contains("1.0 days", result); // Time to expiration
        Assert.Contains("Minute: 5/10", result); // Minute usage
        Assert.Contains("Hour: 50/100", result); // Hour usage
        Assert.Contains("Day: 500/1000", result); // Day usage
        Assert.Contains("Month: 5000/10000", result); // Month usage
    }

    #endregion

    #region FormatTimeSpan Tests

    [Theory]
    [InlineData(3 * 24 * 60 * 60, "3.0 days")]
    [InlineData(1 * 24 * 60 * 60, "1.0 days")]
    [InlineData(12 * 60 * 60, "12.0 hours")]
    [InlineData(1 * 60 * 60, "1.0 hours")]
    [InlineData(30 * 60, "30.0 minutes")]
    [InlineData(1 * 60, "1.0 minutes")]
    [InlineData(30, "30.0 seconds")]
    [InlineData(1, "1.0 seconds")]
    public void FormatTimeSpan_ReturnsCorrectFormat(int seconds, string expected)
    {
        // Arrange
        var mockKeyQueue = new Mock<IKeyQueue>();
        var mockClock = new Mock<ISystemClock>();
        var service = new ApiKeyService(mockKeyQueue.Object, mockClock.Object);
        var timeSpan = TimeSpan.FromSeconds(seconds);

        // Act - using reflection to access private method
        var methodInfo = typeof(ApiKeyService).GetMethod("FormatTimeSpan",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = methodInfo.Invoke(service, new object[] { timeSpan }) as string;

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
