using System.IO;
using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using WiimoteLib;

namespace BalanceBoard.Core.Services;

public sealed class BalanceBoardConnection : IBalanceBoardConnection
{
    private Wiimote? _device;
    private WiimoteCollection? _collection;
    private readonly object _sync = new();
    private volatile bool _disconnecting;

    public event Action<string>? StatusChanged;
    public event Action<string>? Error;
    public event Action<string>? ConnectLog;
    public event Action? ReadingAvailable;

    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    public IReadOnlyList<string> DiscoverDeviceIds()
    {
        lock (_sync)
        {
            if (IsConnected && !string.IsNullOrWhiteSpace(ConnectedDeviceId))
            {
                return [ConnectedDeviceId];
            }
        }

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
        var deviceId = DeviceIdRules.ExtractFromHidPath(device.HIDDevicePath);
        ConnectionFlowLogger.LogHidAttempt(ConnectLog, index, deviceId);

        try
        {
            _device = device;
            ConnectedDeviceId = deviceId;
            _device.WiimoteChanged += OnWiimoteChanged;
            _device.Connect();
            // Continuous reports keep the dashboard and vJoy stream alive between lean changes.
            _device.SetReportType(InputReport.IRAccel, true);
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

    private void OnWiimoteChanged(object? sender, WiimoteChangedEventArgs e)
    {
        try
        {
            if (_disconnecting || !IsConnected || e.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                return;
            }

            SafeCallbacks.Raise(ReadingAvailable);
        }
        catch (ObjectDisposedException)
        {
            // Late HID callback after disconnect — ignore.
        }
        catch (IOException)
        {
            // Device unplugged during callback — ignore.
        }
        catch (Exception ex)
        {
            ReportError(ex);
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

            try
            {
                _device.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }
    }

    public BalanceReading? GetCurrentReading()
    {
        WiimoteCollection? release = null;
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
            catch (IOException ex)
            {
                release = DetachLocked();
                ReportError(ex);
            }
            catch (ObjectDisposedException)
            {
                release = DetachLocked();
            }
            catch (Exception ex)
            {
                ReportError(ex);
                return null;
            }
        }

        if (release is not null)
        {
            ReleaseCollection(release, notify: true);
        }

        return null;
    }

    public void Disconnect()
    {
        WiimoteCollection? release;
        lock (_sync)
        {
            _disconnecting = true;
            release = DetachLocked();
        }

        try
        {
            ConnectLog?.Invoke("[DISCONNECT] Releasing HID collection.");
            ReleaseCollection(release, notify: false);
        }
        finally
        {
            _disconnecting = false;
        }
    }

    private WiimoteCollection? DetachLocked()
    {
        if (_device is not null)
        {
            try
            {
                _device.WiimoteChanged -= OnWiimoteChanged;
            }
            catch
            {
                // Best-effort unsubscribe before HID teardown.
            }
        }

        var collection = _collection;
        _device = null;
        _collection = null;
        IsConnected = false;
        ConnectedDeviceId = null;
        return collection;
    }

    private void ReleaseCollection(WiimoteCollection? collection, bool notify)
    {
        if (collection is null)
        {
            return;
        }

        try
        {
            WiimoteCollectionHelper.ReleaseAll(collection);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }

        if (notify)
        {
            try
            {
                StatusChanged?.Invoke("Balance board disconnected.");
            }
            catch
            {
                // Status subscribers must not block teardown.
            }
        }
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
            .Select(d => DeviceIdRules.ExtractFromHidPath(d.HIDDevicePath))
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

    public void Dispose() => Disconnect();
}
