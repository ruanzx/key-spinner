using KeySpinner.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KeySpinner.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddKeySpinnerRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
        services.AddSingleton<IKeyQueue>(sp =>
        {
            var keySpinnerOption = configuration.GetSection(KeySpinnerOption.ConfigSectionName).Get<KeySpinnerOption>()!;
            var apiKeys = keySpinnerOption.Keys.Select(x => new ApiKey
            {
                Key = x,
                ExpirationTimeUtc = DateTime.UtcNow.AddYears(1),
                RateLimitPerMinute = keySpinnerOption.RateLimitPerMinute,
                RateLimitPerHour = keySpinnerOption.RateLimitPerHour,
                RateLimitPerDay = keySpinnerOption.RateLimitPerDay,
                RateLimitPerMonth = keySpinnerOption.RateLimitPerMonth
            });

            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisKeyQueue(redis, apiKeys);
        });
        services.AddSingleton<IApiKeyService, ApiKeyService>();

        return services;
    }

    public static IServiceCollection AddKeySpinnerInMemory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IKeyQueue>(sp =>
        {
            var keySpinnerOption = configuration.GetSection(KeySpinnerOption.ConfigSectionName).Get<KeySpinnerOption>()!;
            var apiKeys = keySpinnerOption.Keys.Select(x => new ApiKey
            {
                Key = x,
                ExpirationTimeUtc = DateTime.UtcNow.AddYears(1),
                RateLimitPerMinute = keySpinnerOption.RateLimitPerMinute,
                RateLimitPerHour = keySpinnerOption.RateLimitPerHour,
                RateLimitPerDay = keySpinnerOption.RateLimitPerDay,
                RateLimitPerMonth = keySpinnerOption.RateLimitPerMonth
            });
            return new InMemoryKeyQueue(apiKeys);
        });
        services.AddSingleton<IApiKeyService, ApiKeyService>();

        return services;
    }
}