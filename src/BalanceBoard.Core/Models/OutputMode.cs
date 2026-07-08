namespace BalanceBoard.Core.Models;

/// <summary>
/// Primary movement output — vJoy and keyboard movement are mutually exclusive to avoid double input.
/// Board-button mappings (power/A) can still use keys or vJoy buttons independently.
/// </summary>
public enum OutputMode
{
    Keyboard = 0,
    VJoy = 1,
}
