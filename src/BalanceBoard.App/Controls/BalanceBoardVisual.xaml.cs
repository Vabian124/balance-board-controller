using System.Windows;
using System.Windows.Controls;
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
        Loaded += (_, _) => LayoutBoard();
        SizeChanged += (_, _) => LayoutBoard();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not BalanceBoardVisual visual)
        {
            return;
        }

        if (e.NewValue is ProcessedBalance data)
        {
            visual.UpdateVisual(data);
        }
        else
        {
            visual.ResetVisual();
        }
    }

    private void LayoutBoard()
    {
        var w = BoardCanvas.ActualWidth;
        var h = BoardCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        const double padSize = 52;
        const double padInset = 4;
        PlacePad(TopLeftPad, padInset, padInset);
        PlacePad(TopRightPad, w - padSize - padInset, padInset);
        PlacePad(BottomLeftPad, padInset, h - padSize - padInset);
        PlacePad(BottomRightPad, w - padSize - padInset, h - padSize - padInset);

        Canvas.SetLeft(TopLeftDot, padInset + 20);
        Canvas.SetTop(TopLeftDot, padInset + 20);
        Canvas.SetLeft(TopRightDot, w - padInset - 32);
        Canvas.SetTop(TopRightDot, padInset + 20);
        Canvas.SetLeft(BottomLeftDot, padInset + 20);
        Canvas.SetTop(BottomLeftDot, h - padInset - 32);
        Canvas.SetLeft(BottomRightDot, w - padInset - 32);
        Canvas.SetTop(BottomRightDot, h - padInset - 32);
    }

    private static void PlacePad(Border pad, double left, double top)
    {
        Canvas.SetLeft(pad, left);
        Canvas.SetTop(pad, top);
    }

    private void UpdateVisual(ProcessedBalance data)
    {
        LayoutBoard();
        var w = BoardCanvas.ActualWidth;
        var h = BoardCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var onBoard = data.WeightKg > BalanceConstants.WeightOnBoardThresholdKg;
        UpdateCorner(TopLeftPad, TopLeftDot, onBoard ? data.TopLeftKg : 0);
        UpdateCorner(TopRightPad, TopRightDot, onBoard ? data.TopRightKg : 0);
        UpdateCorner(BottomLeftPad, BottomLeftDot, onBoard ? data.BottomLeftKg : 0);
        UpdateCorner(BottomRightPad, BottomRightDot, onBoard ? data.BottomRightKg : 0);

        PlaceCenterDot(data.BalanceX, data.BalanceY, w, h);
        CenterDot.Opacity = onBoard ? 1.0 : 0.9;
    }

    private void PlaceCenterDot(float balanceX, float balanceY, double w, double h)
    {
        var xRatio = Math.Clamp(balanceX / BalanceConstants.PercentScale, 0, 1);
        var yRatio = Math.Clamp(balanceY / BalanceConstants.PercentScale, 0, 1);
        Canvas.SetLeft(CenterDot, xRatio * (w - CenterDot.Width));
        Canvas.SetTop(CenterDot, yRatio * (h - CenterDot.Height));
    }

    private static void UpdateCorner(Border pad, Ellipse dot, float weight)
    {
        var intensity = Math.Clamp(weight / 35f, 0.2, 1);
        pad.Opacity = 0.25 + intensity * 0.45;
        dot.Opacity = 0.5 + intensity * 0.5;
    }

    private void ResetVisual()
    {
        LayoutBoard();
        UpdateCorner(TopLeftPad, TopLeftDot, 0);
        UpdateCorner(TopRightPad, TopRightDot, 0);
        UpdateCorner(BottomLeftPad, BottomLeftDot, 0);
        UpdateCorner(BottomRightPad, BottomRightDot, 0);
        var w = BoardCanvas.ActualWidth;
        var h = BoardCanvas.ActualHeight;
        if (w > 0 && h > 0)
        {
            PlaceCenterDot(BalanceConstants.BalanceCenterPercent, BalanceConstants.BalanceCenterPercent, w, h);
        }

        CenterDot.Opacity = 0.9;
    }
}
