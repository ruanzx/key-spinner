using Moq;
using StackExchange.Redis;
using System.Text.Json;

namespace KeySpinner.UnitTests;

public class RedisKeyQueueTests
{
    private const string QueueKey = "api:keys:queue";
    private const string KeysHashKey = "api:keys:hash";
    private const string CounterKey = "api:keys:count";

    #region Constructor Tests

    [Fact]
    public void Constructor_DoesNotInitializeRedis_WhenQueueExists()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);

        var apiKeys = new List<ApiKey>
            {
                new ApiKey { Key = "key1" },
                new ApiKey { Key = "key2" }
            };

        // Act
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        // Assert
        mockDb.Verify(db => db.KeyExists(QueueKey, CommandFlags.None), Times.Once);
        mockDb.Verify(db => db.CreateTransaction(It.IsAny<object>()), Times.Never);
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_UpdatesHashAndAddsToConcurrentQueue()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
            RateLimitPerMinute = 10
        };

        // Act
        queue.Enqueue(apiKey);

        // Assert
        var expectedJson = JsonSerializer.Serialize(apiKey);
        mockDb.Verify(db => db.HashSet(
            KeysHashKey,
            apiKey.Key,
            It.Is<RedisValue>(rv => rv.ToString() == expectedJson),
            When.Always,
            CommandFlags.None), Times.Once);

        mockDb.Verify(db => db.ListRightPush(
            QueueKey,
            apiKey.Key,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    #endregion

    #region Dequeue Tests

    [Fact]
    public void Dequeue_ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.ListLeftPop(QueueKey, CommandFlags.None)).Returns(RedisValue.Null);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        // Act
        var result = queue.Dequeue();

        // Assert
        Assert.Null(result);
        mockDb.Verify(db => db.ListLeftPop(QueueKey, CommandFlags.None), Times.Once);
    }

    [Fact]
    public void Dequeue_ReturnsNull_WhenKeyNotFoundInHash()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.ListLeftPop(QueueKey, CommandFlags.None)).Returns("key1");
        mockDb.Setup(db => db.HashGet(KeysHashKey, "key1", CommandFlags.None)).Returns(RedisValue.Null);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        // Act
        var result = queue.Dequeue();

        // Assert
        Assert.Null(result);
        mockDb.Verify(db => db.ListLeftPop(QueueKey, CommandFlags.None), Times.Once);
        mockDb.Verify(db => db.HashGet(KeysHashKey, "key1", CommandFlags.None), Times.Once);
    }

    [Fact]
    public void Dequeue_ReturnsApiKey_WhenKeyFoundInHash()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        var apiKey = new ApiKey
        {
            Key = "key1",
            ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
            RateLimitPerMinute = 10
        };
        var json = JsonSerializer.Serialize(apiKey);

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.ListLeftPop(QueueKey, CommandFlags.None)).Returns("key1");
        mockDb.Setup(db => db.HashGet(KeysHashKey, "key1", CommandFlags.None)).Returns(json);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        // Act
        var result = queue.Dequeue();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("key1", result.Key);
        Assert.Equal(apiKey.ExpirationTimeUtc, result.ExpirationTimeUtc);
        Assert.Equal(apiKey.RateLimitPerMinute, result.RateLimitPerMinute);

        mockDb.Verify(db => db.ListLeftPop(QueueKey, CommandFlags.None), Times.Once);
        mockDb.Verify(db => db.HashGet(KeysHashKey, "key1", CommandFlags.None), Times.Once);
    }

    #endregion

    #region UpdateKey Tests

    [Fact]
    public void UpdateKey_UpdatesHashEntryOnly()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        var apiKey = new ApiKey
        {
            Key = "test-key",
            ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
            RateLimitPerMinute = 10,
            MinuteCounter = 5 // Updated counter
        };

        // Act
        queue.UpdateKey(apiKey);

        // Assert
        var expectedJson = JsonSerializer.Serialize(apiKey);
        mockDb.Verify(db => db.HashSet(
            KeysHashKey,
            apiKey.Key,
            It.Is<RedisValue>(rv => rv.ToString() == expectedJson),
            When.Always,
            CommandFlags.None), Times.Once);

        // Should NOT add to queue
        mockDb.Verify(db => db.ListRightPush(
            QueueKey,
            It.IsAny<RedisValue>(),
            When.Always,
            CommandFlags.None), Times.Never);
    }

    #endregion

    #region Contains Tests

    [Fact]
    public void Contains_ChecksIfKeyExistsInHash()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.HashExists(KeysHashKey, "test-key", CommandFlags.None)).Returns(true);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        var apiKey = new ApiKey { Key = "test-key" };

        // Act
        var result = queue.Contains(apiKey);

        // Assert
        Assert.True(result);
        mockDb.Verify(db => db.HashExists(KeysHashKey, "test-key", CommandFlags.None), Times.Once);
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenKeyDoesNotExistInHash()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.HashExists(KeysHashKey, "test-key", CommandFlags.None)).Returns(false);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        var apiKey = new ApiKey { Key = "test-key" };

        // Act
        var result = queue.Contains(apiKey);

        // Assert
        Assert.False(result);
        mockDb.Verify(db => db.HashExists(KeysHashKey, "test-key", CommandFlags.None), Times.Once);
    }

    #endregion

    #region Count Tests

    [Fact]
    public void Count_ReturnsListLength()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();

        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(db => db.KeyExists(QueueKey, CommandFlags.None)).Returns(true);
        mockDb.Setup(db => db.ListLength(QueueKey, CommandFlags.None)).Returns(5);

        var apiKeys = new List<ApiKey>();
        var queue = new RedisKeyQueue(mockRedis.Object, apiKeys);

        // Act
        var result = queue.Count;

        // Assert
        Assert.Equal(5, result);
        mockDb.Verify(db => db.ListLength(QueueKey, CommandFlags.None), Times.Once);
    }

    #endregion

    #region Integration Tests

    // Note: These tests require a running Redis instance
    // Comment them out if you're not running tests with Redis available

    /*
    [Fact]
    public void IntegrationTest_EnqueueAndDequeue()
    {
        // Arrange
        var connectionString = "localhost:6379";
        var apiKeys = new List<ApiKey>
        {
            new ApiKey 
            { 
                Key = "integration-test-key", 
                ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
                RateLimitPerMinute = 10
            }
        };

        var queue = new RedisKeyQueue(connectionString, apiKeys);

        // Act & Assert - test full cycle
        var retrievedKey = queue.Dequeue();
        Assert.NotNull(retrievedKey);
        Assert.Equal("integration-test-key", retrievedKey.Key);

        retrievedKey.MinuteCounter = 5; // Update counter
        queue.UpdateKey(retrievedKey); // Update in Redis

        queue.Enqueue(retrievedKey); // Re-queue

        var retrievedAgain = queue.Dequeue();
        Assert.NotNull(retrievedAgain);
        Assert.Equal("integration-test-key", retrievedAgain.Key);
        Assert.Equal(5, retrievedAgain.MinuteCounter); // Counter should be preserved

        // Clean up
        var redis = ConnectionMultiplexer.Connect(connectionString);
        var db = redis.GetDatabase();
        db.KeyDelete(new RedisKey[] { QueueKey, KeysHashKey, CounterKey });
    }
    */

    #endregion
}
