namespace KeySpinner.UnitTests;

public class InMemoryKeyQueueTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesEmptyQueue_WhenNoKeysProvided()
    {
        // Arrange
        var apiKeys = new List<ApiKey>();

        // Act
        var queue = new InMemoryKeyQueue(apiKeys);

        // Assert
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Constructor_InitializesQueue_WithProvidedKeys()
    {
        // Arrange
        var apiKeys = new List<ApiKey>
            {
                new ApiKey { Key = "key1" },
                new ApiKey { Key = "key2" },
                new ApiKey { Key = "key3" }
            };

        // Act
        var queue = new InMemoryKeyQueue(apiKeys);

        // Assert
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public void Constructor_FiltersDuplicateKeys_ByKeyProperty()
    {
        // Arrange
        var apiKeys = new List<ApiKey>
            {
                new ApiKey { Key = "key1", RateLimitPerMinute = 10 },
                new ApiKey { Key = "key1", RateLimitPerMinute = 20 }, // Duplicate key
                new ApiKey { Key = "key2" }
            };

        // Act
        var queue = new InMemoryKeyQueue(apiKeys);

        // Assert
        Assert.Equal(2, queue.Count); // Should only have 2 unique keys
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_AddsApiKeyToQueue()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());
        var apiKey = new ApiKey { Key = "test-key" };

        // Act
        queue.Enqueue(apiKey);

        // Assert
        Assert.Equal(1, queue.Count);
        Assert.True(queue.Contains(apiKey));
    }

    [Fact]
    public void Enqueue_AllowsDuplicateKeys_WhenAddedManually()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());
        var apiKey1 = new ApiKey { Key = "key1" };
        var apiKey2 = new ApiKey { Key = "key1" }; // Same key

        // Act
        queue.Enqueue(apiKey1);
        queue.Enqueue(apiKey2);

        // Assert
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Enqueue_AddsMultipleKeysInOrder()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());
        var apiKey1 = new ApiKey { Key = "key1" };
        var apiKey2 = new ApiKey { Key = "key2" };
        var apiKey3 = new ApiKey { Key = "key3" };

        // Act
        queue.Enqueue(apiKey1);
        queue.Enqueue(apiKey2);
        queue.Enqueue(apiKey3);

        // Assert
        Assert.Equal(3, queue.Count);

        // Verify order by dequeueing
        var dequeuedKey1 = queue.Dequeue();
        var dequeuedKey2 = queue.Dequeue();
        var dequeuedKey3 = queue.Dequeue();

        Assert.Equal("key1", dequeuedKey1.Key);
        Assert.Equal("key2", dequeuedKey2.Key);
        Assert.Equal("key3", dequeuedKey3.Key);
    }

    #endregion

    #region Dequeue Tests

    [Fact]
    public void Dequeue_ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());

        // Act
        var result = queue.Dequeue();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Dequeue_ReturnsAndRemovesFirstKey_WhenQueueHasItems()
    {
        // Arrange
        var apiKeys = new List<ApiKey>
            {
                new ApiKey { Key = "key1" },
                new ApiKey { Key = "key2" }
            };
        var queue = new InMemoryKeyQueue(apiKeys);

        // Act
        var result = queue.Dequeue();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("key1", result.Key);
        Assert.Equal(1, queue.Count); // One item should be left
    }

    [Fact]
    public void Dequeue_ReturnsKeysInFIFOOrder()
    {
        // Arrange
        var apiKeys = new List<ApiKey>
            {
                new ApiKey { Key = "key1" },
                new ApiKey { Key = "key2" },
                new ApiKey { Key = "key3" }
            };
        var queue = new InMemoryKeyQueue(apiKeys);

        // Act & Assert - Dequeue all keys and verify order
        var firstKey = queue.Dequeue();
        Assert.Equal("key1", firstKey.Key);

        var secondKey = queue.Dequeue();
        Assert.Equal("key2", secondKey.Key);

        var thirdKey = queue.Dequeue();
        Assert.Equal("key3", thirdKey.Key);

        // Queue should now be empty
        Assert.Equal(0, queue.Count);
        Assert.Null(queue.Dequeue());
    }

    [Fact]
    public void Dequeue_ReturnsActualObject_NotACopy()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Key = "test-key",
            RateLimitPerMinute = 10,
            MinuteCounter = 0
        };
        var queue = new InMemoryKeyQueue(new List<ApiKey> { apiKey });

        // Act
        var dequeuedKey = queue.Dequeue();

        // Modify the dequeued object
        dequeuedKey.MinuteCounter = 5;

        // Assert - original object should be modified since it's the same reference
        Assert.Equal(5, apiKey.MinuteCounter);
    }

    #endregion

    #region Contains Tests

    [Fact]
    public void Contains_ReturnsFalse_WhenKeyIsNotInQueue()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());
        var apiKey = new ApiKey { Key = "test-key" };

        // Act
        var result = queue.Contains(apiKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Contains_ReturnsTrue_WhenKeyIsInQueue()
    {
        // Arrange
        var apiKey = new ApiKey { Key = "test-key" };
        var queue = new InMemoryKeyQueue(new List<ApiKey> { apiKey });

        // Act
        var result = queue.Contains(apiKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ChecksReferenceEquality_NotValueEquality()
    {
        // Arrange
        var apiKey1 = new ApiKey { Key = "test-key" };
        var apiKey2 = new ApiKey { Key = "test-key" }; // Same key value but different object
        var queue = new InMemoryKeyQueue(new List<ApiKey> { apiKey1 });

        // Act
        var containsOriginal = queue.Contains(apiKey1);
        var containsSameKey = queue.Contains(apiKey2);

        // Assert - ConcurrentQueue.Contains uses reference equality
        Assert.True(containsOriginal);
        Assert.False(containsSameKey);
    }

    #endregion

    #region Count Tests

    [Fact]
    public void Count_ReturnsZero_ForEmptyQueue()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());

        // Act & Assert
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Count_ReturnsCorrectNumber_AfterEnqueueAndDequeue()
    {
        // Arrange
        var queue = new InMemoryKeyQueue(new List<ApiKey>());

        // Act & Assert - Enqueue operations
        queue.Enqueue(new ApiKey { Key = "key1" });
        Assert.Equal(1, queue.Count);

        queue.Enqueue(new ApiKey { Key = "key2" });
        Assert.Equal(2, queue.Count);

        // Act & Assert - Dequeue operations
        queue.Dequeue();
        Assert.Equal(1, queue.Count);

        queue.Dequeue();
        Assert.Equal(0, queue.Count);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_QueueOperationsWorkAsExpected()
    {
        // Arrange
        var apiKey1 = new ApiKey { Key = "key1", RateLimitPerMinute = 10 };
        var apiKey2 = new ApiKey { Key = "key2", RateLimitPerMinute = 20 };

        var queue = new InMemoryKeyQueue(new List<ApiKey> { apiKey1 });

        // Act & Assert - Test full cycle of operations

        // Initial state
        Assert.Equal(1, queue.Count);
        Assert.True(queue.Contains(apiKey1));

        // Add another key
        queue.Enqueue(apiKey2);
        Assert.Equal(2, queue.Count);
        Assert.True(queue.Contains(apiKey2));

        // Dequeue in FIFO order
        var dequeuedKey1 = queue.Dequeue();
        Assert.Equal("key1", dequeuedKey1.Key);
        Assert.Equal(10, dequeuedKey1.RateLimitPerMinute);
        Assert.Equal(1, queue.Count);

        // Modify the dequeued key and re-enqueue it
        dequeuedKey1.MinuteCounter = 5;
        queue.Enqueue(dequeuedKey1);
        Assert.Equal(2, queue.Count);

        // Dequeue again to get the next key
        var dequeuedKey2 = queue.Dequeue();
        Assert.Equal("key2", dequeuedKey2.Key);
        Assert.Equal(20, dequeuedKey2.RateLimitPerMinute);
        Assert.Equal(1, queue.Count);

        // Dequeue again to get the modified key
        var dequeuedKey1Again = queue.Dequeue();
        Assert.Equal("key1", dequeuedKey1Again.Key);
        Assert.Equal(5, dequeuedKey1Again.MinuteCounter); // Should retain changes
        Assert.Equal(0, queue.Count);
    }

    #endregion
}