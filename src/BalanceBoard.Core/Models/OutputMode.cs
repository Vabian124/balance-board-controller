namespace BalanceBoard.Core.Models;

/// <summary>
/// Primary movement output — virtual controller and keyboard movement are mutually exclusive to avoid double input.
/// Board-button mappings (power/A) can still use keys or controller buttons independently.
/// </summary>
public enum OutputMode
{
    Keyboard = 0,
    VirtualController = 1,
    VJoy = VirtualController,
}
