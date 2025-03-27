using StackExchange.Redis;
using System.Text.Json;

namespace KeySpinner;

public class RedisKeyQueue : IKeyQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private const string QueueKey = "api:keys:queue";
    private const string KeysHashKey = "api:keys:hash";
    private const string CounterKey = "api:keys:count";

    /// <summary>
    /// Samples of Redis connection string:
    ///     localhost:6379,password=your_strong_password
    ///     localhost:6379,password=your_strong_password,abortConnect=false,ssl=false
    ///     localhost:6379,password=your_strong_password,defaultDatabase=0
    ///     localhost:6379,password=your_strong_password,connectTimeout=5000,syncTimeout=10000
    ///     server1:6379,server2:6379,password=your_strong_password
    ///     your-cache-name.redis.cache.windows.net:6380,password=your_access_key,ssl=true
    /// 
    /// </summary>
    public RedisKeyQueue(string connectionString, IEnumerable<ApiKey> apiKeys)
        : this(ConnectionMultiplexer.Connect(connectionString), apiKeys)
    {
    }

    public RedisKeyQueue(IConnectionMultiplexer redis, IEnumerable<ApiKey> apiKeys)
    {
        _redis = redis;
        _db = redis.GetDatabase();

        // Initialize Redis with the API keys if the queue doesn't exist
        if (!_db.KeyExists(QueueKey))
        {
            var transaction = _db.CreateTransaction();
            foreach (var apiKey in apiKeys.DistinctBy(k => k.Key))
            {
                // Store the serialized key in a hash
                var json = JsonSerializer.Serialize(apiKey);
                transaction.HashSetAsync(KeysHashKey, apiKey.Key, json);

                // Add the key to the queue
                transaction.ListRightPushAsync(QueueKey, apiKey.Key);
            }

            // Set the count
            transaction.StringSetAsync(CounterKey, apiKeys.Count());

            transaction.Execute();
        }
    }

    public void Enqueue(ApiKey apiKey)
    {
        // Update the key data in the hash
        var json = JsonSerializer.Serialize(apiKey);
        _db.HashSet(KeysHashKey, apiKey.Key, json);

        // Add the key to the queue
        _db.ListRightPush(QueueKey, apiKey.Key);
    }

    public ApiKey? Dequeue()
    {
        // Pop a key from the left end of the list (FIFO)
        var keyValue = _db.ListLeftPop(QueueKey);

        if (!keyValue.HasValue)
        {
            return null;
        }

        string key = keyValue.ToString();

        // Get the serialized key data from the hash
        var json = _db.HashGet(KeysHashKey, key);

        if (!json.HasValue)
        {
            return null;
        }

        // Deserialize and return the key
        return JsonSerializer.Deserialize<ApiKey>(json.ToString());
    }

    public void UpdateKey(ApiKey apiKey)
    {
        // Just update the hash entry with the latest key state
        var json = JsonSerializer.Serialize(apiKey);
        _db.HashSet(KeysHashKey, apiKey.Key, json);
    }

    public bool Contains(ApiKey apiKey)
    {
        // Check if the key exists in the hash
        return _db.HashExists(KeysHashKey, apiKey.Key);
    }

    public int Count
    {
        get
        {
            // Get the length of the list
            return (int)_db.ListLength(QueueKey);
        }
    }
}