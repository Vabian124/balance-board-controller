using System.Windows;
using System.Windows.Input;

namespace BalanceBoard.App.Dialogs;

/// <summary>Modal single-line text prompt (used for naming custom profiles).</summary>
public partial class NamePromptDialog : Window
{
    private readonly Func<string, string?>? _validate;

    public NamePromptDialog(
        string title,
        string prompt,
        string initialValue = "",
        string confirmLabel = "Save",
        Func<string, string?>? validate = null)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        OkButton.Content = confirmLabel;
        InputBox.Text = initialValue;
        _validate = validate;
        Loaded += (_, _) =>
        {
            InputBox.SelectAll();
            _ = InputBox.Focus();
        };
    }

    public string ResponseText => InputBox.Text.Trim();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryAccept();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TryAccept()
    {
        var value = ResponseText;
        if (string.IsNullOrWhiteSpace(value))
        {
            ShowError("Enter a name.");
            return;
        }

        var error = _validate?.Invoke(value);
        if (error is not null)
        {
            ShowError(error);
            return;
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
