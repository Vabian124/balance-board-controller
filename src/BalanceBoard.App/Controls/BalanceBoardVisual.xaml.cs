using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;

namespace BalanceBoard.App.Controls;

public partial class BalanceBoardVisual : UserControl
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(ProcessedBalance), typeof(BalanceBoardVisual),
            new PropertyMetadata(null, OnDataChanged));

    private float _centerBalanceX = BalanceConstants.BalanceCenterPercent;
    private float _centerBalanceY = BalanceConstants.BalanceCenterPercent;

    public ProcessedBalance? Data
    {
        get => (ProcessedBalance?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public BalanceBoardVisual()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (_, _) => LayoutBoard();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LayoutBoard();
        if (Data is not null)
        {
            UpdateVisual(Data);
        }
        else
        {
            ResetVisual();
        }
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

    private void EnsureCanvasSize()
    {
        var hostW = BoardHost.ActualWidth;
        var hostH = BoardHost.ActualHeight;
        if (hostW <= 0 || hostH <= 0)
        {
            hostW = ActualWidth;
            hostH = ActualHeight;
        }

        if (hostW <= 0 || hostH <= 0)
        {
            return;
        }

        var margin = BoardCanvas.Margin;
        var canvasW = hostW - margin.Left - margin.Right;
        var canvasH = hostH - margin.Top - margin.Bottom;
        if (canvasW > 0 && canvasH > 0)
        {
            BoardCanvas.Width = canvasW;
            BoardCanvas.Height = canvasH;
        }
    }

    private (double w, double h) GetCanvasSize()
    {
        EnsureCanvasSize();
        var w = BoardCanvas.Width > 0 ? BoardCanvas.Width : BoardCanvas.ActualWidth;
        var h = BoardCanvas.Height > 0 ? BoardCanvas.Height : BoardCanvas.ActualHeight;
        return (w, h);
    }

    private void LayoutBoard()
    {
        var (w, h) = GetCanvasSize();
        if (w <= 0 || h <= 0)
        {
            return;
        }

        const double padSize = 52;
        const double padInset = 4;
        const double bannerW = 148;
        PlacePad(TopLeftPad, padInset, padInset);
        PlacePad(TopRightPad, w - padSize - padInset, padInset);
        PlacePad(BottomLeftPad, padInset, h - padSize - padInset);
        PlacePad(BottomRightPad, w - padSize - padInset, h - padSize - padInset);

        Canvas.SetLeft(JumpBanner, (w - bannerW) / 2);
        Canvas.SetTop(JumpBanner, 6);

        Canvas.SetLeft(TopLeftDot, padInset + 20);
        Canvas.SetTop(TopLeftDot, padInset + 20);
        Canvas.SetLeft(TopRightDot, w - padInset - 32);
        Canvas.SetTop(TopRightDot, padInset + 20);
        Canvas.SetLeft(BottomLeftDot, padInset + 20);
        Canvas.SetTop(BottomLeftDot, h - padInset - 32);
        Canvas.SetLeft(BottomRightDot, w - padInset - 32);
        Canvas.SetTop(BottomRightDot, h - padInset - 32);

        RepositionCenterDot();
    }

    private static void PlacePad(Border pad, double left, double top)
    {
        Canvas.SetLeft(pad, left);
        Canvas.SetTop(pad, top);
    }

    private void UpdateVisual(ProcessedBalance data)
    {
        var onBoard = data.WeightKg > BalanceConstants.WeightOnBoardThresholdKg;
        UpdateCorner(TopLeftPad, TopLeftDot, onBoard ? data.TopLeftKg : 0);
        UpdateCorner(TopRightPad, TopRightDot, onBoard ? data.TopRightKg : 0);
        UpdateCorner(BottomLeftPad, BottomLeftDot, onBoard ? data.BottomLeftKg : 0);
        UpdateCorner(BottomRightPad, BottomRightDot, onBoard ? data.BottomRightKg : 0);
        UpdateJumpIndicator(data.Jump);

        (_centerBalanceX, _centerBalanceY) = BalanceDisplay.GetCenterDotPercent(data);
        LayoutBoard();
        CenterDot.Opacity = onBoard ? 1.0 : 0.9;
    }

    private void RepositionCenterDot()
    {
        var (w, h) = GetCanvasSize();
        if (w <= 0 || h <= 0)
        {
            return;
        }

        PlaceCenterDot(_centerBalanceX, _centerBalanceY, w, h);
    }

    private void PlaceCenterDot(float balanceX, float balanceY, double w, double h)
    {
        var dotW = CenterDot.Width > 0 ? CenterDot.Width : CenterDot.ActualWidth;
        var dotH = CenterDot.Height > 0 ? CenterDot.Height : CenterDot.ActualHeight;
        var (left, top) = BalanceDisplay.CenterDotCanvasPosition(balanceX, balanceY, w, h, dotW, dotH);
        Canvas.SetLeft(CenterDot, left);
        Canvas.SetTop(CenterDot, top);
    }

    private void UpdateJumpIndicator(bool jumping)
    {
        JumpBanner.Opacity = jumping ? 0.95 : 0;
    }

    private static void UpdateCorner(Border pad, Ellipse dot, float weight)
    {
        var intensity = Math.Clamp(weight / 35f, 0.2, 1);
        pad.Opacity = 0.25 + intensity * 0.45;
        dot.Opacity = 0.5 + intensity * 0.5;
    }

    private void ResetVisual()
    {
        _centerBalanceX = BalanceConstants.BalanceCenterPercent;
        _centerBalanceY = BalanceConstants.BalanceCenterPercent;
        LayoutBoard();
        UpdateCorner(TopLeftPad, TopLeftDot, 0);
        UpdateCorner(TopRightPad, TopRightDot, 0);
        UpdateCorner(BottomLeftPad, BottomLeftDot, 0);
        UpdateCorner(BottomRightPad, BottomRightDot, 0);
        UpdateJumpIndicator(false);
        RepositionCenterDot();
        CenterDot.Opacity = 0.9;
    }
}
