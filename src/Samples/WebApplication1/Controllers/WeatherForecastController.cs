using KeySpinner;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(
            IApiKeyService apiKeyService,
            ILogger<WeatherForecastController> logger
            )
        {
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            for (int i = 0; i < 30; i++) // Reduced to 20 iterations for better readability
            {
                _logger.LogInformation($"\n--- Iteration {i + 1} ---");

                var apiKey = _apiKeyService.GetAvailableKey();
                if (apiKey == null)
                {
                    _logger.LogInformation("All keys are rate limited");
                    continue;
                }

                _logger.LogInformation($"Using API Key: {apiKey.Key}");

                // Get and print the key status
                _logger.LogInformation(_apiKeyService.PrintKeyStatus(apiKey));

                // Simulate API call (sleep for a short time)
                Thread.Sleep(100);

                // Release the key after use
                _apiKeyService.ReleaseKey(apiKey);
            }

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
