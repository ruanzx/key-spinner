namespace KeySpinner;

/// <summary>
/// Abstraction for system clock to facilitate testing of time-dependent code
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Gets the current UTC date and time
    /// </summary>
    DateTime UtcNow { get; }
}
