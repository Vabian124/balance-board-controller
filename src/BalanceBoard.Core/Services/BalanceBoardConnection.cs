using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using WiimoteLib;

namespace BalanceBoard.Core.Services;

public sealed class BalanceBoardConnection : IBalanceBoardConnection
{
    private Wiimote? _device;
    private WiimoteCollection? _collection;
    private readonly object _sync = new();

    public event Action<string>? StatusChanged;
    public event Action<string>? Error;
    public event Action<string>? ConnectLog;

    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    public IReadOnlyList<string> DiscoverDeviceIds()
    {
        try
        {
            return WiimoteCollectionHelper.DiscoverDeviceIds();
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return Array.Empty<string>();
        }
    }

    public bool Connect(int deviceIndex = 0)
    {
        Disconnect();

        try
        {
            _collection = new WiimoteCollection();
            _collection.FindAllWiimotes();
            var deviceIds = EnumerateDeviceIds(_collection);
            ConnectionFlowLogger.LogHidDiscovery(ConnectLog, deviceIds);

            if (_collection.Count == 0)
            {
                StatusChanged?.Invoke("No balance board found yet — automatic pairing will run when you connect.");
                return false;
            }

            if (deviceIndex >= 0 && deviceIndex < _collection.Count)
            {
                if (TryConnectDevice(_collection[deviceIndex], deviceIndex))
                {
                    return true;
                }

                Disconnect();
            }

            for (var i = 0; i < _collection.Count; i++)
            {
                if (i == deviceIndex)
                {
                    continue;
                }

                if (TryConnectDevice(_collection[i], i))
                {
                    return true;
                }

                Disconnect();
            }

            StatusChanged?.Invoke("No balance board found among visible Wii HID devices.");
            return false;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            Disconnect();
            return false;
        }
    }

    private bool TryConnectDevice(Wiimote device, int index)
    {
        var deviceId = ExtractDeviceId(device.HIDDevicePath);
        ConnectionFlowLogger.LogHidAttempt(ConnectLog, index, deviceId);

        try
        {
            _device = device;
            ConnectedDeviceId = deviceId;
            _device.Connect();
            _device.SetReportType(InputReport.IRAccel, false);
            _device.SetLEDs(true, false, false, false);

            var isBalanceBoard = _device.WiimoteState.ExtensionType == ExtensionType.BalanceBoard;
            ConnectionFlowLogger.LogHidSuccess(ConnectLog, ConnectedDeviceId, isBalanceBoard);

            if (!isBalanceBoard)
            {
                StatusChanged?.Invoke($"Connected device {ConnectedDeviceId} is not a balance board.");
                return false;
            }

            StatusChanged?.Invoke($"Connected to balance board {ConnectedDeviceId}.");
            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            ConnectionFlowLogger.LogHidFailure(ConnectLog, index, ex.Message);
            ReportError(ex);
            return false;
        }
    }

    public void Tare()
    {
        lock (_sync)
        {
            if (_device is null)
            {
                return;
            }

            _device.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
        }
    }

    public BalanceReading? GetCurrentReading()
    {
        lock (_sync)
        {
            if (_device is null || !IsConnected)
            {
                return null;
            }

            try
            {
                return ToReading(_device.WiimoteState);
            }
            catch (ObjectDisposedException)
            {
                IsConnected = false;
                _device = null;
                return null;
            }
            catch (Exception ex)
            {
                ReportError(ex);
                return null;
            }
        }
    }

    public void Disconnect()
    {
        WiimoteCollection? collection;
        lock (_sync)
        {
            collection = _collection;
            _device = null;
            _collection = null;
            IsConnected = false;
            ConnectedDeviceId = null;
        }

        WiimoteCollectionHelper.ReleaseAll(collection);
    }

    private void ReportError(Exception ex)
    {
        Error?.Invoke(ex.Message);
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            Error?.Invoke(ex.StackTrace);
        }
    }

    private static IReadOnlyList<string> EnumerateDeviceIds(WiimoteCollection collection) =>
        collection
            .Select(d => ExtractDeviceId(d.HIDDevicePath))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

    private static BalanceReading ToReading(WiimoteState state)
    {
        var bb = state.BalanceBoardState;
        return new BalanceReading
        {
            WeightKg = bb.WeightKg,
            TopLeftKg = bb.SensorValuesKg.TopLeft,
            TopRightKg = bb.SensorValuesKg.TopRight,
            BottomLeftKg = bb.SensorValuesKg.BottomLeft,
            BottomRightKg = bb.SensorValuesKg.BottomRight,
            ButtonA = state.ButtonState.A,
            IsBalanceBoard = state.ExtensionType == ExtensionType.BalanceBoard,
        };
    }

    private static string? ExtractDeviceId(string hidPath)
    {
        var match = System.Text.RegularExpressions.Regex.Match(hidPath, "e_pid&.*?&(.*?)&", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : hidPath;
    }

    public void Dispose() => Disconnect();
}
