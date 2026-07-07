using System.Text.RegularExpressions;
using BalanceBoard.Core.Models;
using WiimoteLib;

namespace BalanceBoard.Core.Services;

public sealed class BalanceBoardConnection : IDisposable
{
    private Wiimote? _device;
    private readonly object _sync = new();

    public event Action<BalanceReading>? ReadingReceived;
    public event Action<string>? StatusChanged;
    public event Action<string>? Error;

    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    public IReadOnlyList<string> DiscoverDeviceIds()
    {
        try
        {
            var collection = new WiimoteCollection();
            collection.FindAllWiimotes();
            return collection
                .Select(d => ExtractDeviceId(d.HIDDevicePath))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToList();
        }
        catch (WiimoteNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
            return Array.Empty<string>();
        }
    }

    public bool Connect(int deviceIndex = 0)
    {
        Disconnect();

        try
        {
            var collection = new WiimoteCollection();
            collection.FindAllWiimotes();

            if (collection.Count == 0)
            {
                StatusChanged?.Invoke("No balance board found yet — automatic pairing will run when you connect.");
                return false;
            }

            if (deviceIndex < 0 || deviceIndex >= collection.Count)
            {
                deviceIndex = 0;
            }

            _device = collection[deviceIndex];
            ConnectedDeviceId = ExtractDeviceId(_device.HIDDevicePath);

            _device.WiimoteChanged += OnWiimoteChanged;
            _device.Connect();
            _device.SetReportType(InputReport.IRAccel, false);
            _device.SetLEDs(true, false, false, false);

            if (_device.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                StatusChanged?.Invoke("Connected device is not a balance board.");
            }
            else
            {
                StatusChanged?.Invoke($"Connected to balance board {ConnectedDeviceId}.");
            }

            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                Error?.Invoke(ex.StackTrace);
            }

            Disconnect();
            return false;
        }
    }

    public void Tare()
    {
        lock (_sync)
        {
            if (_device is null) return;
            _device.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
        }
    }

    public BalanceReading? GetCurrentReading()
    {
        lock (_sync)
        {
            if (_device is null || !IsConnected) return null;
            return ToReading(_device.WiimoteState);
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            if (_device is not null)
            {
                try
                {
                    _device.WiimoteChanged -= OnWiimoteChanged;
                    _device.Disconnect();
                }
                catch
                {
                    // ignored
                }
            }

            _device = null;
            IsConnected = false;
            ConnectedDeviceId = null;
        }
    }

    private void OnWiimoteChanged(object? sender, WiimoteChangedEventArgs e)
    {
        var reading = ToReading(e.WiimoteState);
        ReadingReceived?.Invoke(reading);
    }

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
        var match = Regex.Match(hidPath, "e_pid&.*?&(.*?)&", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : hidPath;
    }

    public void Dispose() => Disconnect();
}
