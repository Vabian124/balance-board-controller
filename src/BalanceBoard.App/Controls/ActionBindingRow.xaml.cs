using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BalanceBoard.Core.Models;

namespace BalanceBoard.App.Controls;

public partial class ActionBindingRow : UserControl
{
    private static readonly (ActionKind Kind, string Label)[] BaseKindOptions =
    [
        (ActionKind.None, "None"),
        (ActionKind.Key, "Key"),
        (ActionKind.MouseButton, "Mouse button"),
        (ActionKind.MouseMoveX, "Mouse move X"),
        (ActionKind.MouseMoveY, "Mouse move Y"),
    ];

    private static readonly string[] MouseButtons = ["Left", "Right", "Middle", "X1", "X2"];

    private bool _suppressEvents;
    private bool _capturingKey;
    private string _committedKeyName = string.Empty;

    public event EventHandler? BindingChanged;

    public ActionBindingRow()
    {
        InitializeComponent();
        RefreshKindOptions();
        MouseButtonCombo.ItemsSource = MouseButtons;
        VJoyButtonCombo.ItemsSource = Enumerable.Range(1, 32).ToList();
    }

    public string SlotDisplayName
    {
        get => SlotLabel.Text;
        set => SlotLabel.Text = value;
    }

    public bool IncludeVJoyButton { get; set; }

    public void LoadBinding(ActionBinding binding)
    {
        _suppressEvents = true;
        _capturingKey = false;
        _committedKeyName = binding.KeyName ?? string.Empty;
        RefreshKindOptions();
        KindCombo.SelectedItem = GetKindOptions().First(o => o.Kind == binding.Kind).Label;
        KeyCaptureButton.Content = FormatKeyLabel(_committedKeyName);
        if (!string.IsNullOrEmpty(binding.MouseButton))
        {
            MouseButtonCombo.SelectedItem = binding.MouseButton;
        }
        else
        {
            MouseButtonCombo.SelectedIndex = 0;
        }

        VJoyButtonCombo.SelectedItem = Math.Clamp(binding.VJoyButtonNumber, 1, 32);
        MoveAmountSlider.Value = binding.Amount;
        MoveAmountLabel.Text = $"{binding.Amount:0}";
        UpdateValuePanel(binding.Kind, beginKeyCaptureIfKey: false);
        _suppressEvents = false;
    }

    public ActionBinding GetBinding()
    {
        var kind = GetSelectedKind();
        return kind switch
        {
            ActionKind.Key => new ActionBinding
            {
                Kind = ActionKind.Key,
                KeyName = _committedKeyName,
            },
            ActionKind.MouseButton => new ActionBinding
            {
                Kind = ActionKind.MouseButton,
                MouseButton = MouseButtonCombo.SelectedItem?.ToString() ?? "Left",
            },
            ActionKind.MouseMoveX => new ActionBinding
            {
                Kind = ActionKind.MouseMoveX,
                Amount = (int)MoveAmountSlider.Value,
            },
            ActionKind.MouseMoveY => new ActionBinding
            {
                Kind = ActionKind.MouseMoveY,
                Amount = (int)MoveAmountSlider.Value,
            },
            ActionKind.VJoyButton => new ActionBinding
            {
                Kind = ActionKind.VJoyButton,
                VJoyButtonNumber = VJoyButtonCombo.SelectedItem is int number ? number : 1,
            },
            _ => new ActionBinding { Kind = ActionKind.None },
        };
    }

    private IEnumerable<(ActionKind Kind, string Label)> GetKindOptions()
    {
        if (IncludeVJoyButton)
        {
            return BaseKindOptions.Append((ActionKind.VJoyButton, "vJoy button"));
        }

        return BaseKindOptions;
    }

    private void RefreshKindOptions()
    {
        KindCombo.ItemsSource = GetKindOptions().Select(o => o.Label).ToList();
    }

    private ActionKind GetSelectedKind()
    {
        var label = KindCombo.SelectedItem?.ToString();
        return GetKindOptions().FirstOrDefault(o => o.Label == label).Kind;
    }

    private void KindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        var kind = GetSelectedKind();
        UpdateValuePanel(kind, beginKeyCaptureIfKey: true);
        if (kind != ActionKind.Key || !string.IsNullOrEmpty(_committedKeyName))
        {
            RaiseChanged();
        }
    }

    private void UpdateValuePanel(ActionKind kind, bool beginKeyCaptureIfKey)
    {
        KeyCaptureButton.Visibility = kind == ActionKind.Key ? Visibility.Visible : Visibility.Collapsed;
        MouseButtonCombo.Visibility = kind == ActionKind.MouseButton ? Visibility.Visible : Visibility.Collapsed;
        VJoyButtonPanel.Visibility = kind == ActionKind.VJoyButton ? Visibility.Visible : Visibility.Collapsed;
        MovePanel.Visibility = kind is ActionKind.MouseMoveX or ActionKind.MouseMoveY
            ? Visibility.Visible
            : Visibility.Collapsed;
        NoneLabel.Visibility = kind == ActionKind.None ? Visibility.Visible : Visibility.Collapsed;
        if (kind == ActionKind.Key && beginKeyCaptureIfKey)
        {
            BeginKeyCapture();
        }
        else
        {
            _capturingKey = false;
        }
    }

    private void BeginKeyCapture()
    {
        _capturingKey = true;
        KeyCaptureButton.Content = "Press any key…";
        Dispatcher.BeginInvoke(() => KeyCaptureButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void KeyCaptureButton_Click(object sender, RoutedEventArgs e) => BeginKeyCapture();

    private void KeyCaptureButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingKey)
        {
            return;
        }

        e.Handled = true;
        _capturingKey = false;

        if (e.Key == Key.Escape)
        {
            KeyCaptureButton.Content = FormatKeyLabel(_committedKeyName);
            return;
        }

        var keyName = e.Key == Key.System ? e.SystemKey.ToString() : e.Key.ToString();
        _committedKeyName = keyName;
        KeyCaptureButton.Content = FormatKeyLabel(keyName);
        RaiseChanged();
    }

    private void KeyCaptureButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_capturingKey)
        {
            return;
        }

        _capturingKey = false;
        KeyCaptureButton.Content = FormatKeyLabel(_committedKeyName);
    }

    private void Value_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressEvents)
        {
            RaiseChanged();
        }
    }

    private void MoveAmount_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents)
        {
            return;
        }

        MoveAmountLabel.Text = $"{e.NewValue:0}";
        RaiseChanged();
    }

    private void RaiseChanged() => BindingChanged?.Invoke(this, EventArgs.Empty);

    private static string FormatKeyLabel(string keyName) =>
        string.IsNullOrWhiteSpace(keyName) ? "Press a key…" : keyName;
}
