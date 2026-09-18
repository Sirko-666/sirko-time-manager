using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TimerApp.Controls;

/// <summary>
/// iOS-style 24h dial: two rolling wheels (Hours 0-23 / Minutes 0-59).
/// Hover a wheel and scroll the mouse wheel; values wrap around.
/// </summary>
public sealed class TimeDial : FrameworkElement
{
    public static readonly DependencyProperty HoursProperty = DependencyProperty.Register(
        nameof(Hours), typeof(int), typeof(TimeDial),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceHours));

    public static readonly DependencyProperty MinutesProperty = DependencyProperty.Register(
        nameof(Minutes), typeof(int), typeof(TimeDial),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceMinutes));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(TimeDial),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LargeModeProperty = DependencyProperty.Register(
        nameof(LargeMode), typeof(bool), typeof(TimeDial),
        new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private int? _hoverColumn;

    /// <summary>
    /// True — the larger "Dial 1" look: two extra value rows above and below.
    /// False — the compact dial used inside app cards.
    /// </summary>
    public bool LargeMode
    {
        get => (bool)GetValue(LargeModeProperty);
        set => SetValue(LargeModeProperty, value);
    }

    private double ColumnWidth => LargeMode ? 74 : 60;
    private double ColonWidth => LargeMode ? 26 : 20;
    private double RowSpacing => LargeMode ? 30 : 24;
    private int VisibleOffsets => LargeMode ? 2 : 1;
    private double PillHeight => LargeMode ? 30 : 26;

    public int Hours
    {
        get => (int)GetValue(HoursProperty);
        set => SetValue(HoursProperty, value);
    }

    public int Minutes
    {
        get => (int)GetValue(MinutesProperty);
        set => SetValue(MinutesProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>Raised after the user changes the value with the mouse wheel.</summary>
    public event Action? Changed;

    private static object CoerceHours(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, 23);

    private static object CoerceMinutes(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, 59);

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = ColumnWidth * 2 + ColonWidth;
        double height = RowSpacing * (VisibleOffsets * 2) + (LargeMode ? 44 : 40);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        var ink = GetBrush("InkBrush", "#201D1D");
        var body = GetBrush("BodyTextBrush", "#646262");
        var muted = GetBrush("MutedTextBrush", "#9A9898");
        var accent = GetBrush("AccentBrush", "#F8FAC7");

        double size = ActualWidth;
        if (size <= 0 || ActualHeight <= 0) return;

        double centerY = ActualHeight / 2;
        double hourCenterX = ColumnWidth / 2;
        double minuteCenterX = hourCenterX + ColonWidth + ColumnWidth;
        double dpi = GetPixelsPerDip();

        DrawWheel(dc, hourCenterX, centerY, Hours, 23, ink, body, muted, accent,
                  _hoverColumn == 0, dpi);
        DrawWheel(dc, minuteCenterX, centerY, Minutes, 59, ink, body, muted, accent,
                  _hoverColumn == 1, dpi);

        var colonStyle = new Typeface("Consolas");
        var colon = new FormattedText(":", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, colonStyle, LargeMode ? 32 : 26, ink, dpi);
        colon.SetFontWeight(FontWeights.Bold);
        double colonX = ColumnWidth + (ColonWidth - colon.WidthIncludingTrailingWhitespace) / 2;
        dc.DrawText(colon, new Point(colonX, centerY - colon.Height / 2));
    }

    private void DrawWheel(DrawingContext dc, double centerX, double centerY, int value, int max,
        Brush ink, Brush body, Brush muted, Brush accent, bool active, double dpi)
    {
        if (active)
        {
            double pillW = ColumnWidth - 6;
            var geo = new RectangleGeometry(
                new Rect(centerX - pillW / 2, centerY - PillHeight / 2, pillW, PillHeight),
                PillHeight / 2, PillHeight / 2);
            dc.DrawGeometry(accent, null, geo);
        }

        Brush centerText = active ? GetBrush("AccentTextBrush", "#201D1D") : ink;

        for (int offset = -VisibleOffsets; offset <= VisibleOffsets; offset++)
        {
            int valueAt = Mod(value + offset, max + 1);
            double top = centerY + offset * RowSpacing;
            var font = new Typeface("Consolas");
            double size = offset == 0 ? (LargeMode ? 34 : 26)
                : offset is -1 or 1 ? (LargeMode ? 22 : 16)
                : 18;
            Brush textColor = offset == 0
                ? centerText
                : offset is -1 or 1 ? body : muted;
            var f = new FormattedText(valueAt.ToString("00"), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, font, size, textColor, dpi);
            f.SetFontWeight(offset == 0 ? FontWeights.Bold : FontWeights.Normal);
            dc.DrawText(f, new Point(centerX - f.WidthIncludingTrailingWhitespace / 2, top - f.Height / 2));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsEditable) return;
        double x = e.GetPosition(this).X;
        int column = x < ColumnWidth ? 0 : x > ColumnWidth + ColonWidth ? 1 : -1;
        if (column != _hoverColumn)
        {
            _hoverColumn = column;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverColumn != null)
        {
            _hoverColumn = null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsEditable) return;

        double x = e.GetPosition(this).X;
        int column = x < ColumnWidth ? 0 : x > ColumnWidth + ColonWidth ? 1 : -1;
        if (column < 0) return;

        int delta = e.Delta > 0 ? 1 : -1;
        if (column == 0)
            Hours = Mod(Hours + delta, 24);
        else
            Minutes = Mod(Minutes + delta, 60);

        Changed?.Invoke();
        e.Handled = true;
    }

    private static int Mod(int value, int modulus)
    {
        int r = value % modulus;
        return r < 0 ? r + modulus : r;
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