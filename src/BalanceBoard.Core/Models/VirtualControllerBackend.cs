namespace BalanceBoard.Core.Models;

/// <summary>
/// Selected virtual-controller implementation when movement output targets a controller instead of keyboard input.
/// </summary>
public enum VirtualControllerBackend
{
    VJoy = 0,
    Xbox360 = 1,
}
