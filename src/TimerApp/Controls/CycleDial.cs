using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TimerApp.Controls;

/// <summary>
/// Two-wheel picker with configurable ranges, e.g. work/rest days "5 / 2".
/// Change with the mouse wheel over a wheel, or by holding the left mouse button
/// and moving vertically. Values wrap inside their range.
/// </summary>
public sealed class CycleDial : FrameworkElement
{
    public static readonly DependencyProperty LeftValueProperty = DependencyProperty.Register(
        nameof(LeftValue), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceLeft));

    public static readonly DependencyProperty RightValueProperty = DependencyProperty.Register(
        nameof(RightValue), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceRight));

    public static readonly DependencyProperty LeftMinProperty = DependencyProperty.Register(
        nameof(LeftMin), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftMaxProperty = DependencyProperty.Register(
        nameof(LeftMax), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(30, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightMinProperty = DependencyProperty.Register(
        nameof(RightMin), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightMaxProperty = DependencyProperty.Register(
        nameof(RightMax), typeof(int), typeof(CycleDial),
        new FrameworkPropertyMetadata(30, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeparatorProperty = DependencyProperty.Register(
        nameof(Separator), typeof(string), typeof(CycleDial),
        new FrameworkPropertyMetadata("/", FrameworkPropertyMetadataOptions.AffectsRender));

    private const double ColumnWidth = 64;
    private const double SeparatorWidth = 24;
    private const double RowSpacing = 30;
    private const int VisibleOffsets = 2;
    private const double CenterTextSize = 30;
    private const double MinTextSize = 14;

    private bool _dragging;
    private int _dragColumn;
    private double _dragAccumulator;
    private double _lastY;

    public int LeftValue
    {
        get => (int)GetValue(LeftValueProperty);
        set => SetValue(LeftValueProperty, value);
    }

    public int RightValue
    {
        get => (int)GetValue(RightValueProperty);
        set => SetValue(RightValueProperty, value);
    }

    public int LeftMin { get => (int)GetValue(LeftMinProperty); set => SetValue(LeftMinProperty, value); }
    public int LeftMax { get => (int)GetValue(LeftMaxProperty); set => SetValue(LeftMaxProperty, value); }
    public int RightMin { get => (int)GetValue(RightMinProperty); set => SetValue(RightMinProperty, value); }
    public int RightMax { get => (int)GetValue(RightMaxProperty); set => SetValue(RightMaxProperty, value); }
    public string Separator { get => (string)GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    public event Action? Changed;

    private static object CoerceLeft(DependencyObject d, object baseValue)
    {
        var dial = (CycleDial)d;
        return Math.Clamp((int)baseValue, dial.LeftMin, dial.LeftMax);
    }

    private static object CoerceRight(DependencyObject d, object baseValue)
    {
        var dial = (CycleDial)d;
        return Math.Clamp((int)baseValue, dial.RightMin, dial.RightMax);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = ColumnWidth * 2 + SeparatorWidth;
        double height = RowSpacing * (VisibleOffsets * 2 + 4) + 40;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var ink = GetBrush("InkBrush", "#F2EDED");
        var body = GetBrush("BodyTextBrush", "#B8B2B2");
        var muted = GetBrush("MutedTextBrush", "#7F7A7A");

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        double centerY = ActualHeight / 2;
        double leftCenterX = ColumnWidth / 2;
        double rightCenterX = leftCenterX + SeparatorWidth + ColumnWidth;
        double dpi = GetPixelsPerDip();

        DrawWheel(dc, leftCenterX, centerY, LeftValue, LeftMin, LeftMax, ink, body, muted, dpi);
        DrawWheel(dc, rightCenterX, centerY, RightValue, RightMin, RightMax, ink, body, muted, dpi);

        var sepStyle = new Typeface("Consolas");
        var sep = new FormattedText(Separator ?? "/", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, sepStyle, 30, ink, dpi);
        sep.SetFontWeight(FontWeights.Bold);
        double sepX = ColumnWidth + (SeparatorWidth - sep.WidthIncludingTrailingWhitespace) / 2;
        dc.DrawText(sep, new Point(sepX, centerY - sep.Height / 2));
    }

    private void DrawWheel(DrawingContext dc, double centerX, double centerY, int value, int min, int max,
        Brush ink, Brush body, Brush muted, double dpi)
    {
        var typeface = new Typeface("Consolas");

        for (int offset = -VisibleOffsets; offset <= VisibleOffsets; offset++)
        {
            int valueAt = Wrap(value + offset, min, max);
            double top = centerY + offset * RowSpacing;
            double absDistance = Math.Abs(offset);

            double fontSize = SizeForDistance(absDistance);
            double opacity = absDistance <= 1.0
                ? 1.0
                : Math.Max(0.0, 1.0 - (absDistance - 1.0) / (VisibleOffsets + 1.0));
            if (opacity <= 0.0) continue;

            double centerInfluence = 1.0 - SmoothStep(Math.Min(1.0, absDistance));
            double outerInfluence = SmoothStep(Math.Clamp(absDistance - 1.0, 0.0, 1.0));
            Brush textColor = BlendBrush(BlendBrush(body, muted, outerInfluence), ink, centerInfluence);

            var f = new FormattedText(valueAt.ToString("00"), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, textColor, dpi);
            f.SetFontWeight(offset == 0 ? FontWeights.Bold : FontWeights.Normal);
            dc.PushOpacity(opacity);
            dc.DrawText(f, new Point(centerX - f.WidthIncludingTrailingWhitespace / 2, top - f.Height / 2));
            dc.Pop();
        }
    }

    private static double SmoothStep(double value) => value * value * (3.0 - 2.0 * value);

    private static Brush BlendBrush(Brush from, Brush to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        if (from is not SolidColorBrush a || to is not SolidColorBrush b)
            return amount >= 0.5 ? to : from;

        Color x = a.Color;
        Color y = b.Color;
        return new SolidColorBrush(Color.FromArgb(
            (byte)(x.A + (y.A - x.A) * amount),
            (byte)(x.R + (y.R - x.R) * amount),
            (byte)(x.G + (y.G - x.G) * amount),
            (byte)(x.B + (y.B - x.B) * amount)));
    }

    private static double SizeForDistance(double distance) =>
        MinTextSize + (CenterTextSize - MinTextSize) * (1.0 - Math.Min(1.0, distance / (VisibleOffsets + 1.0)));

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        int column = ColumnAt(e.GetPosition(this).X);
        if (column < 0) return;
        Step(column, e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int column = ColumnAt(e.GetPosition(this).X);
        if (column < 0) return;
        _dragging = true;
        _dragColumn = column;
        _dragAccumulator = 0;
        _lastY = e.GetPosition(this).Y;
        Cursor = Cursors.SizeNS;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;

        double y = e.GetPosition(this).Y;
        _dragAccumulator += (_lastY - y) / RowSpacing; // drag down => value decreases
        _lastY = y;

        while (_dragAccumulator >= 1)
        {
            _dragAccumulator -= 1;
            Step(_dragColumn, 1);
        }
        while (_dragAccumulator <= -1)
        {
            _dragAccumulator += 1;
            Step(_dragColumn, -1);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _dragAccumulator = 0;
        Cursor = Cursors.Arrow;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private int ColumnAt(double x) =>
        x < ColumnWidth ? 0 : x > ColumnWidth + SeparatorWidth ? 1 : -1;

    private void Step(int column, int delta)
    {
        if (column == 0)
            LeftValue = Wrap(LeftValue + delta, LeftMin, LeftMax);
        else
            RightValue = Wrap(RightValue + delta, RightMin, RightMax);
        Changed?.Invoke();
    }

    private static int Wrap(int value, int min, int max)
    {
        int n = Math.Max(1, max - min + 1);
        int r = (value - min) % n;
        if (r < 0) r += n;
        return min + r;
    }

    private Brush GetBrush(string key, string fallbackHex)
    {
        if (TryFindResource(key) is Brush b) return b;
        return (Brush)new BrushConverter().ConvertFromString(fallbackHex)!;
    }

    private double GetPixelsPerDip()
    {
        CompositionTarget? target = PresentationSource.FromVisual(this)?.CompositionTarget;
        return target is null ? 1.0 : VisualTreeHelper.GetDpi(this).PixelsPerDip;
    }
}
