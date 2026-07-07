using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BalanceBoard.Core.Models;

namespace BalanceBoard.App.Controls;

public partial class BalanceBoardVisual : UserControl
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(ProcessedBalance), typeof(BalanceBoardVisual),
            new PropertyMetadata(null, OnDataChanged));

    public ProcessedBalance? Data
    {
        get => (ProcessedBalance?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public BalanceBoardVisual()
    {
        InitializeComponent();
        Loaded += (_, _) => LayoutDots();
        SizeChanged += (_, _) => LayoutDots();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BalanceBoardVisual visual && e.NewValue is ProcessedBalance data)
        {
            visual.UpdateVisual(data);
        }
    }

    private void LayoutDots()
    {
        var w = BoardCanvas.ActualWidth;
        var h = BoardCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        Canvas.SetLeft(TopLeftDot, 0);
        Canvas.SetTop(TopLeftDot, 0);
        Canvas.SetLeft(TopRightDot, w - TopRightDot.Width);
        Canvas.SetTop(TopRightDot, 0);
        Canvas.SetLeft(BottomLeftDot, 0);
        Canvas.SetTop(BottomLeftDot, h - BottomLeftDot.Height);
        Canvas.SetLeft(BottomRightDot, w - BottomRightDot.Width);
        Canvas.SetTop(BottomRightDot, h - BottomRightDot.Height);
    }

    private void UpdateVisual(ProcessedBalance data)
    {
        LayoutDots();
        var w = BoardCanvas.ActualWidth;
        var h = BoardCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        UpdateCorner(TopLeftDot, data.TopLeftKg);
        UpdateCorner(TopRightDot, data.TopRightKg);
        UpdateCorner(BottomLeftDot, data.BottomLeftKg);
        UpdateCorner(BottomRightDot, data.BottomRightKg);

        var x = Math.Clamp(data.BalanceX / 100.0, 0, 1);
        var y = Math.Clamp(data.BalanceY / 100.0, 0, 1);
        Canvas.SetLeft(CenterDot, x * (w - CenterDot.Width));
        Canvas.SetTop(CenterDot, y * (h - CenterDot.Height));
    }

    private static void UpdateCorner(Ellipse ellipse, float weight)
    {
        ellipse.Opacity = Math.Clamp(weight / 40f, 0.15, 1);
    }
}
