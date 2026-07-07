using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class SetupWizardWindow : Window
{
    private int _step;
    private readonly BalanceBoardSession _session;

    public SetupWizardWindow(BalanceBoardSession session)
    {
        InitializeComponent();
        _session = session;
        ShowStep();
    }

    private void ShowStep()
    {
        StepContent.Children.Clear();
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == 2 ? "Finish" : "Next";
        StepProgress.Value = _step;

        switch (_step)
        {
            case 0:
                StepTitle.Text = "Step 1 of 3 — Prerequisites";
                BuildPrereqStep();
                break;
            case 1:
                StepTitle.Text = "Step 2 of 3 — Pair and connect";
                BuildPairStep();
                break;
            case 2:
                StepTitle.Text = "Step 3 of 3 — Calibrate";
                BuildCalibrateStep();
                break;
        }
    }

    private void BuildPrereqStep()
    {
        var diag = VJoyDiagnostics.Inspect(_session.Settings.VJoyDeviceId);
        AddBullet(diag.DriverEnabled ? "vJoy driver is enabled." : "vJoy driver is NOT enabled. Reboot after installing vJoy.");
        AddBullet(diag.HasAxisX && diag.HasAxisY ? "vJoy Device 1 has X/Y axes configured." : "Open vJoyConf and enable X/Y on Device 1.");
        if (!string.IsNullOrWhiteSpace(diag.Error)) AddBullet(diag.Error);

        var openVJoy = new Button { Content = "Open vJoyConf", Style = (Style)FindResource("SecondaryButton") };
        openVJoy.Click += (_, _) => TryLaunchVJoyConf();
        StepContent.Children.Add(openVJoy);

        var openBt = new Button { Content = "Open Bluetooth Settings", Style = (Style)FindResource("GhostButton") };
        openBt.Click += (_, _) => Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        StepContent.Children.Add(openBt);

        AddParagraph("Pair the Wii Balance Board in Windows Bluetooth (PIN 0000). Hold the red SYNC button in the battery bay while pairing.");
    }

    private void BuildPairStep()
    {
        AddParagraph("1. Remove old pairings if connection fails.");
        AddParagraph("2. Add the board in Windows Bluetooth while holding SYNC.");
        AddParagraph("3. Click Connect below and keep holding SYNC until connected.");

        var devices = _session.DiscoverDevices();
        AddBullet(devices.Count > 0
            ? $"Found {devices.Count} Wii HID device(s)."
            : "No Wii devices detected yet.");

        var connect = new Button { Content = "Connect Balance Board", Margin = new Thickness(0, 12, 0, 0) };
        connect.Click += (_, _) =>
        {
            if (_session.Connect())
            {
                MessageBox.Show(this, "Connected. Proceed to calibration.", "Connected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "Could not connect. Ensure the board is paired and hold SYNC while connecting.", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        StepContent.Children.Add(connect);
    }

    private void BuildCalibrateStep()
    {
        AddParagraph("Stand or sit on the board, then tare and set your resting posture as center.");

        var tare = new Button { Content = "Tare (zero sensors)" };
        tare.Click += (_, _) => _session.Tare();
        StepContent.Children.Add(tare);

        var center = new Button { Content = "Set Current Balance as Center", Style = (Style)FindResource("SecondaryButton") };
        center.Click += (_, _) => _session.SetCenter();
        StepContent.Children.Add(center);

        var preset = new Button { Content = "Apply Game Controller Preset", Margin = new Thickness(0, 12, 0, 0) };
        preset.Click += (_, _) => _session.ApplyControllerPreset();
        StepContent.Children.Add(preset);
    }

    private void AddParagraph(string text)
    {
        StepContent.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
    }

    private void AddBullet(string text)
    {
        StepContent.Children.Add(new TextBlock
        {
            Text = "• " + text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)FindResource("TextMutedBrush"),
        });
    }

    private static void TryLaunchVJoyConf()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyConf.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "vJoyConf.exe"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        MessageBox.Show("vJoyConf was not found. Install vJoy first.", "Not found", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) { _step--; ShowStep(); }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 2) { _step++; ShowStep(); return; }
        DialogResult = true;
        Close();
    }
}
