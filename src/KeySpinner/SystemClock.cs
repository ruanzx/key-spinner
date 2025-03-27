namespace KeySpinner;

/// <summary>
/// Default implementation of ISystemClock that uses the system clock
/// </summary>
public class SystemClock : ISystemClock
{
    /// <summary>
    /// Gets the current UTC date and time from the system clock
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;
}
