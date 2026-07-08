using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BalanceBoard.Core.Models;

namespace BalanceBoard.App.Controls;

public partial class ActionBindingRow : UserControl
{
    private static readonly (ActionKind Kind, string Label)[] KindOptions =
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
    /// <summary>
    /// Last committed key name — distinct from the button's display text so an aborted capture
    /// (Escape, or clicking away without pressing a key) cannot silently persist an empty
    /// <see cref="ActionBinding.KeyName"/> and erase the user's existing binding on next save.
    /// </summary>
    private string _committedKeyName = string.Empty;

    public event EventHandler? BindingChanged;

    public ActionBindingRow()
    {
        InitializeComponent();
        KindCombo.ItemsSource = KindOptions.Select(o => o.Label).ToList();
        MouseButtonCombo.ItemsSource = MouseButtons;
    }

    public string SlotDisplayName
    {
        get => SlotLabel.Text;
        set => SlotLabel.Text = value;
    }

    public void LoadBinding(ActionBinding binding)
    {
        _suppressEvents = true;
        _capturingKey = false;
        _committedKeyName = binding.KeyName ?? string.Empty;
        KindCombo.SelectedItem = KindOptions.First(o => o.Kind == binding.Kind).Label;
        KeyCaptureButton.Content = FormatKeyLabel(_committedKeyName);
        if (!string.IsNullOrEmpty(binding.MouseButton))
        {
            MouseButtonCombo.SelectedItem = binding.MouseButton;
        }
        else
        {
            MouseButtonCombo.SelectedIndex = 0;
        }

        MoveAmountSlider.Value = binding.Amount;
        MoveAmountLabel.Text = $"{binding.Amount:0}";
        UpdateValuePanel(binding.Kind);
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
            _ => new ActionBinding { Kind = ActionKind.None },
        };
    }

    private ActionKind GetSelectedKind()
    {
        var label = KindCombo.SelectedItem?.ToString();
        return KindOptions.FirstOrDefault(o => o.Label == label).Kind;
    }

    private void KindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        UpdateValuePanel(GetSelectedKind());
        RaiseChanged();
    }

    private void UpdateValuePanel(ActionKind kind)
    {
        KeyCaptureButton.Visibility = kind == ActionKind.Key ? Visibility.Visible : Visibility.Collapsed;
        MouseButtonCombo.Visibility = kind == ActionKind.MouseButton ? Visibility.Visible : Visibility.Collapsed;
        MovePanel.Visibility = kind is ActionKind.MouseMoveX or ActionKind.MouseMoveY
            ? Visibility.Visible
            : Visibility.Collapsed;
        NoneLabel.Visibility = kind == ActionKind.None ? Visibility.Visible : Visibility.Collapsed;
        _capturingKey = false;
    }

    private void KeyCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingKey = true;
        KeyCaptureButton.Content = "Press any key…";
        KeyCaptureButton.Focus();
    }

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

        // Clicked/tabbed away mid-capture without pressing a key or Escape — restore the
        // previous label instead of leaving "Press any key…" as the (would-be-persisted) value.
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
