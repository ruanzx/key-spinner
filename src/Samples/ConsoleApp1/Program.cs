using StackExchange.Redis;

using KeySpinner;

var apiKeys = new List<ApiKey>
        {
            new ApiKey { Key = "key1", ExpirationTimeUtc = DateTime.UtcNow.AddDays(30), RateLimitPerMinute = 1, RateLimitPerHour = 5, RateLimitPerDay = 10, RateLimitPerMonth = 100 },
            new ApiKey { Key = "key2", ExpirationTimeUtc = DateTime.UtcNow.AddDays(30), RateLimitPerMinute = 2, RateLimitPerHour = 5, RateLimitPerDay = 10, RateLimitPerMonth = 100 },
            new ApiKey { Key = "key3", ExpirationTimeUtc = DateTime.UtcNow.AddDays(30), RateLimitPerMinute = 3, RateLimitPerHour = 5, RateLimitPerDay = 10, RateLimitPerMonth = 100 }
        };

RedisSample();
InMemorySample();

void RedisSample()
{
    // docker run -it --rm -p 6379:6379 redis

    // Connect to Redis and create the service
    var redisConnectionString = "localhost:6379";

    var redis = ConnectionMultiplexer.Connect(redisConnectionString);

    var redisKeyQueue = new RedisKeyQueue(redis, apiKeys);

    IApiKeyService apiKeyService = new ApiKeyService(redisKeyQueue);
    try
    {
        for (int i = 0; i < 30; i++) // Reduced to 20 iterations for better readability
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            var apiKey = apiKeyService.GetAvailableKey();
            if (apiKey == null)
            {
                Console.WriteLine("All keys are rate limited");
                return;
            }

            Console.WriteLine($"Using API Key: {apiKey.Key}");

            // Get and print the key status
            Console.WriteLine(apiKeyService.PrintKeyStatus(apiKey));

            // Simulate API call (sleep for a short time)
            Thread.Sleep(100);

            // Release the key after use
            apiKeyService.ReleaseKey(apiKey);
        }

    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine(ex.Message);
    }
}
void InMemorySample()
{
    var keyQueue = new InMemoryKeyQueue(apiKeys);
    IApiKeyService apiKeyService = new ApiKeyService(keyQueue);

    try
    {
        for (int i = 0; i < 30; i++) // Reduced to 20 iterations for better readability
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            var apiKey = apiKeyService.GetAvailableKey();
            if (apiKey == null)
            {
                Console.WriteLine("All keys are rate limited");
                return;
            }

            Console.WriteLine($"Using API Key: {apiKey.Key}");

            // Get and print the key status
            Console.WriteLine(apiKeyService.PrintKeyStatus(apiKey));

            // Simulate API call (sleep for a short time)
            Thread.Sleep(100);

            // Release the key after use
            apiKeyService.ReleaseKey(apiKey);
        }

    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine(ex.Message);
    }


}
