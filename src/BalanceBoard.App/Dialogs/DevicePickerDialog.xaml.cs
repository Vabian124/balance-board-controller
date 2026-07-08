using System.Windows;
using System.Windows.Input;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Dialogs;

/// <summary>Modal list picker when more than one Wii Balance Board HID device is visible.</summary>
public partial class DevicePickerDialog : Window
{
    private readonly IReadOnlyList<string> _deviceIds;

    public DevicePickerDialog(IReadOnlyList<string> deviceIds, string? preferredDeviceId = null)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        if (deviceIds.Count == 0)
        {
            throw new ArgumentException("At least one device id is required.", nameof(deviceIds));
        }

        _deviceIds = deviceIds;
        InitializeComponent();
        DeviceList.ItemsSource = deviceIds.Select((id, index) => FormatItem(index, id)).ToList();

        var preferredIndex = DeviceSelection.IndexOfPreferred(deviceIds, preferredDeviceId);
        DeviceList.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
        Loaded += (_, _) => _ = DeviceList.Focus();
    }

    public int SelectedIndex => DeviceList.SelectedIndex;

    public string? SelectedDeviceId =>
        SelectedIndex >= 0 && SelectedIndex < _deviceIds.Count
            ? _deviceIds[SelectedIndex]
            : null;

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIndex < 0)
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void DeviceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedIndex >= 0)
        {
            DialogResult = true;
        }
    }

    private static string FormatItem(int index, string id) => $"{index + 1}. {id}";
}
