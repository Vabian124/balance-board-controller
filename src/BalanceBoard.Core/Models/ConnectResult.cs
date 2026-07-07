namespace BalanceBoard.Core.Models;

public enum ConnectStatus
{
    Success,
    Cancelled,
    AlreadyInProgress,
    BluetoothUnavailable,
    NoDevices,
    PairingFailed,
    HidFailed,
    NotBalanceBoard,
    TimedOut,
    Error,
}

public sealed record ConnectResult(ConnectStatus Status, string? Message = null)
{
    public bool IsSuccess => Status == ConnectStatus.Success;

    public static ConnectResult Ok(string? message = null) => new(ConnectStatus.Success, message);

    public static ConnectResult Fail(ConnectStatus status, string? message = null) => new(status, message);
}
