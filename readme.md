## Key Spinner

KeySpinner is a robust API key rotation and rate limit management library for .NET applications. It enables efficient management of multiple API keys with complex rate limiting rules, automatically rotating between keys to maximize throughput while respecting rate limits.

## Features

-	Automatically cycles through available API keys to maximize throughput
-	Enforces rate limits per minute, hour, day, and month
-	Counters automatically reset when their time period elapses
-	Automatically detects and avoids using expired API keys
-	Choose between in-memory storage or Redis for distributed scenarios
-	All operations are thread-safe for use in multi-threaded applications
-	Testable design for easy testing
-	Detailed status reporting for monitoring key usage and limits

## Requirements

- .NET 8.0 or higher
- Redis (optional, for distributed scenarios)

## Usage

### Basic Setup with In-Memory Queue

```csharp
// Define your API keys
var apiKeys = new List<ApiKey>
{
    new ApiKey 
    { 
        Key = "key1", 
        ExpirationTimeUtc = DateTime.UtcNow.AddDays(30),
        RateLimitPerMinute = 60,
        RateLimitPerHour = 1000,
        RateLimitPerDay = 10000
    },
    new ApiKey 
    { 
        Key = "key2", 
        ExpirationTimeUtc = DateTime.UtcNow.AddDays(30),
        RateLimitPerMinute = 60,
        RateLimitPerHour = 1000,
        RateLimitPerDay = 10000
    }
};

// Create an in-memory key queue
var keyQueue = new InMemoryKeyQueue(apiKeys);

// Create the API key service
var apiKeyService = new ApiKeyService(keyQueue);
```

### Setup with Redis for Distributed Scenarios

```csharp
// Define your API keys
var apiKeys = new List<ApiKey>
{
    // ... same as above
};

// Connect to Redis and create a Redis-backed queue
var redisConnectionString = "localhost:6379,password=your_password";
var redisKeyQueue = new RedisKeyQueue(redisConnectionString, apiKeys);

// Create the API key service with Redis queue
var apiKeyService = new ApiKeyService(redisKeyQueue);
```

### Using KeySpinner in API Calls

```csharp
try
{
    // Get an available API key
    var apiKey = apiKeyService.GetAvailableKey();
    if (apiKey == null)
    {
        Console.WriteLine("All API keys are currently rate-limited");
        return;
    }

    // Use the API key for your API request
    var apiResponse = await httpClient.GetAsync($"https://api.example.com/data?api_key={apiKey.Key}");
    
    // Release the key after use (automatically updates usage counters)
    apiKeyService.ReleaseKey(apiKey);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

### Checking Key Status

```csharp
var apiKey = apiKeyService.GetAvailableKey();
var statusText = apiKeyService.PrintKeyStatus(apiKey);
Console.WriteLine(statusText);
```

### ASP.NET Core Integration

Register KeySpinner in your `Program.cs` or `Startup.cs`:

```
// Add KeySpinner services
builder.Services.AddKeySpinnerRedis(builder.Configuration);
builder.Services.AddKeySpinnerInMemory(builder.Configuration);
```

Then inject and use `IApiKeyService` in your controllers or services:

```csharp
public class MyService
{
    private readonly IApiKeyService _apiKeyService;
    
    public MyService(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }
    
    public async Task CallExternalApiAsync()
    {
        var apiKey = _apiKeyService.GetAvailableKey();
        if (apiKey != null)
        {
            // Use apiKey.Key in your API call
            _apiKeyService.ReleaseKey(apiKey);
        }
    }
}

```

## License

This project is licensed under the MIT License - see the LICENSE file for details.