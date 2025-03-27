namespace KeySpinner.Options;

public class KeySpinnerOption
{
    public static readonly string ConfigSectionName = "KeySpinner";

    public IEnumerable<string> Keys { get; set; }

    public int RateLimitPerMinute { get; set; }
    public int RateLimitPerHour { get; set; }
    public int RateLimitPerDay { get; set; }
    public int RateLimitPerMonth { get; set; }
}