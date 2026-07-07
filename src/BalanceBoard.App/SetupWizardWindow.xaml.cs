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
    private TextBlock? _statusText;

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
                StepTitle.Text = "Step 1 of 3 — Ready";
                BuildReadyStep();
                break;
            case 1:
                StepTitle.Text = "Step 2 of 3 — Pair automatically";
                BuildPairStep();
                break;
            case 2:
                StepTitle.Text = "Step 3 of 3 — Calibrate";
                BuildCalibrateStep();
                break;
        }
    }

    private void BuildReadyStep()
    {
        var diag = VJoyDiagnostics.Inspect(_session.Settings.VJoyDeviceId);
        AddBullet(diag.DriverEnabled ? "vJoy driver is enabled." : "vJoy driver is NOT enabled. Reboot after installing vJoy.");
        AddBullet(diag.HasAxisX && diag.HasAxisY ? "vJoy Device 1 has X/Y axes configured." : "Open vJoyConf and enable X/Y on Device 1.");
        if (!string.IsNullOrWhiteSpace(diag.Error)) AddBullet(diag.Error);

        AddParagraph("No Windows Bluetooth menus. The app pairs automatically using your PC's Bluetooth address as the Wii PIN (same as WiiBalanceWalker).");

        var openVJoy = new Button { Content = "Open vJoyConf", Style = (Style)FindResource("SecondaryButton") };
        openVJoy.Click += (_, _) => TryLaunchVJoyConf();
        StepContent.Children.Add(openVJoy);
    }

    private void BuildPairStep()
    {
        AddParagraph("1. Flip open the battery cover on the balance board.");
        AddParagraph("2. Press the red SYNC button once.");
        AddParagraph("3. Click Find & Pair below — the app handles Bluetooth for you.");

        _statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = (Brush)FindResource("TextMutedBrush"),
        };
        StepContent.Children.Add(_statusText);

        var pair = new Button { Content = "Find & Pair Balance Board", Margin = new Thickness(0, 12, 0, 0) };
        pair.Click += (_, _) =>
        {
            pair.IsEnabled = false;
            SetStatus("Searching and pairing… press SYNC if you have not already.");
            var ok = _session.ConnectOrPair(discoveryRounds: 4);
            pair.IsEnabled = true;
            if (ok)
            {
                SetStatus("Connected! Click Next to calibrate.");
            }
            else
            {
                SetStatus("Not found yet. Press SYNC on the board and try again.");
            }
        };
        StepContent.Children.Add(pair);
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

    private void SetStatus(string text)
    {
        if (_statusText is not null)
        {
            _statusText.Text = text;
        }
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
