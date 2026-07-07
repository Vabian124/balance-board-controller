using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BalanceBoard.Core.Models;
using Microsoft.Win32;

namespace BalanceBoard.App.Services;

/// <summary>
/// Applies light/dark palettes and optional system-theme follow.
/// </summary>
public static class ThemeManager
{
    private const string AppAssembly = "BalanceBoardApp";
    private static readonly Uri LightColors = ThemeUri("Colors.Light.xaml");
    private static readonly Uri DarkColors = ThemeUri("Colors.Dark.xaml");

    private static Uri ThemeUri(string fileName) =>
        new($"pack://application:,,,/{AppAssembly};component/Themes/{fileName}", UriKind.Absolute);

    public static void Apply(ThemePreference preference)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var effective = ResolveEffectiveTheme(preference);
        var merged = app.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.ToString() ?? string.Empty;
            if (source.Contains("Colors.Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.Contains("Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase)
                || source.Contains("Themes/Colors.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Insert(0, new ResourceDictionary
        {
            Source = effective == ThemePreference.Dark ? DarkColors : LightColors,
        });

        foreach (Window window in app.Windows)
        {
            RefreshThemedElements(window);
        }
    }

    /// <summary>
    /// Re-applies dynamic theme bindings after the color dictionary is swapped.
    /// WPF does not always invalidate Style setter DynamicResources when merged dicts change.
    /// </summary>
    public static void RefreshThemedElements(DependencyObject root)
    {
        if (root is Window window)
        {
            window.SetResourceReference(Control.BackgroundProperty, "Brush.Background");
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            RefreshThemedElements(VisualTreeHelper.GetChild(root, i));
        }

        switch (root)
        {
            case TextBlock textBlock:
                RefreshTextBlockTheme(textBlock);
                break;
            case TabItem tabItem:
                tabItem.InvalidateProperty(Control.ForegroundProperty);
                break;
            case ComboBox comboBox:
                comboBox.InvalidateProperty(Control.ForegroundProperty);
                comboBox.InvalidateProperty(Control.BackgroundProperty);
                if (comboBox.Style is not null)
                {
                    ReapplyStyle(comboBox);
                }

                break;
            case Control control when control.Style is not null:
                ReapplyStyle(control);
                break;
        }
    }

    private static void RefreshTextBlockTheme(TextBlock textBlock)
    {
        if (textBlock.Style is not null)
        {
            ReapplyStyle(textBlock);
            return;
        }

        if (textBlock.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue)
        {
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
        }
    }

    private static void ReapplyStyle(FrameworkElement element)
    {
        var style = element.Style;
        element.ClearValue(FrameworkElement.StyleProperty);
        element.Style = style;
    }

    public static void WatchSystemTheme(ThemePreference preference, Action<ThemePreference> onSystemChanged)
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && preference == ThemePreference.System)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => onSystemChanged(preference));
            }
        };
    }

    public static ThemePreference ResolveEffectiveTheme(ThemePreference preference) =>
        preference switch
        {
            ThemePreference.Dark => ThemePreference.Dark,
            ThemePreference.Light => ThemePreference.Light,
            _ => IsSystemDark() ? ThemePreference.Dark : ThemePreference.Light,
        };

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}
