using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TimerApp.Controls;

/// <summary>
/// iOS-style 24h dial: two rolling wheels (Hours 0-23 / Minutes 0-59).
/// Scrolling: mouse wheel over a wheel, or hold left mouse button and move
/// vertically. Digits glide smoothly between rows; the edges fade out.
///
/// Public API (Hours / Minutes / IsEditable / LargeMode / Changed) is unchanged.
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

    // Layout (depends on LargeMode).
    private double ColumnWidth => LargeMode ? 74 : 60;
    private double ColonWidth => LargeMode ? 26 : 20;
    private double RowSpacing => LargeMode ? 30 : 24;
    private int VisibleOffsets => LargeMode ? 2 : 1;

    // Visual rule, independent of VisibleOffsets.
    private const double CenterTextSize = 30.0;
    private const double MinTextSize = 14.0;

    // Drag speed and start inertia (per Dial v0.1.9).
    private const double DragSpeedFactor = 0.075;
    private const double BasePixelsPerSecondPerRow = 260.0;
    private const double StartRampFloor = 0.03;
    private const double StartRampMid = 0.30;
    private const double StartRampCeiling = 1.0;
    private const double StartRampStage1Seconds = 0.18;
    private const double StartRampStage2Seconds = 1.0;

    // Settle after release.
    private const double SettleDurationPerRow = 0.50;
    private const double MaxWheelPendingRows = 4.0;

    // Wheel chase: exponential ease-out toward an integer target.
    private const double WheelEaseRate = 14.0;
    private const double WheelSnap = 0.0005;

    // Fractional visual positions, one per wheel (continuous, wrap around).
    private double _hoursPosition;
    private double _minutesPosition;

    // Drag state.
    private bool _dragging;
    private int _dragColumn;
    private Point _lastMovePoint;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private DateTime _dragStartTime = DateTime.MinValue;
    private double _smoothSpeed;
    private double _dragAccumulator;

    // Settle state (one at a time; the active wheel is _dragColumn).
    private double _settleTarget;
    private double _settleStartPosition;
    private double _settleDistance;
    private double _settleElapsed;
    private double _settleDuration;
    private bool _settling;
    private DateTime _lastSettleTime = DateTime.MinValue;

    // Independent wheel chase per column, so scrolling one column never
    // interrupts the other one.
    private sealed class WheelChase
    {
        public bool Active;
        public double Target;
        public DateTime Last;
    }

    private readonly WheelChase _hoursChase = new();
    private readonly WheelChase _minutesChase = new();
    private bool _wheelLoopActive;

    public bool LargeMode
    {
        get => (bool)GetValue(LargeModeProperty);
        set => SetValue(LargeModeProperty, value);
    }

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

    /// <summary>Raised after the user changes hours or minutes.</summary>
    public event Action? Changed;

    private static object CoerceHours(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, 23);

    private static object CoerceMinutes(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, 59);

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = ColumnWidth * 2 + ColonWidth;
        // Extra rows so edge digits fade fully inside the element.
        double height = RowSpacing * (VisibleOffsets * 2 + 4) + (LargeMode ? 44 : 40);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        // Hours/Minutes can be assigned by the host application. Keep the
        // fractional drawing positions aligned when no interaction animation
        // is active; otherwise a newly assigned value can render off-center.
        if (!_dragging && !_settling && !_hoursChase.Active && !_minutesChase.Active)
        {
            _hoursPosition = Hours;
            _minutesPosition = Minutes;
        }

        var ink = GetBrush("InkBrush", "#F2EDED");
        var body = GetBrush("BodyTextBrush", "#B8B2B2");
        var muted = GetBrush("MutedTextBrush", "#7F7A7A");

        // Make the complete dial bounds hit-testable, including empty space
        // between and around the digits.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        double centerY = ActualHeight / 2;
        double hourCenterX = ColumnWidth / 2;
        double minuteCenterX = hourCenterX + ColonWidth + ColumnWidth;
        double dpi = GetPixelsPerDip();
        double baseSize = LargeMode ? CenterTextSize : CenterTextSize - 4;

        DrawWheel(dc, hourCenterX, centerY, _hoursPosition, 24, ink, body, muted, baseSize, dpi);
        DrawWheel(dc, minuteCenterX, centerY, _minutesPosition, 60, ink, body, muted, baseSize, dpi);

        var colonStyle = new Typeface("Consolas");
        var colon = new FormattedText(":", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, colonStyle, LargeMode ? 32 : 26, ink, dpi);
        colon.SetFontWeight(FontWeights.Bold);
        double colonX = ColumnWidth + (ColonWidth - colon.WidthIncludingTrailingWhitespace) / 2;
        dc.DrawText(colon, new Point(colonX, centerY - colon.Height / 2));
    }

    private void DrawWheel(DrawingContext dc, double centerX, double centerY, double position, int modulus,
        Brush ink, Brush body, Brush muted, double centerSize, double dpi)
    {
        Brush centerText = ink;

        var typeface = new Typeface("Consolas");

        int baseValue = (int)Math.Floor(position);
        double fraction = position - baseValue;

        int drawOffsets = VisibleOffsets + 2;
        for (int offset = -drawOffsets; offset <= drawOffsets; offset++)
        {
            double rowDistance = offset - fraction;
            double top = centerY + rowDistance * RowSpacing;
            double absDistance = Math.Abs(rowDistance);

            int valueAt = Mod(baseValue + offset, modulus);

            double fontSize = SizeForDistance(absDistance, centerSize);
            double opacity = absDistance <= 1.0
                ? 1.0
                : Math.Max(0.0, 1.0 - (absDistance - 1.0) / (VisibleOffsets + 1.0));
            if (opacity <= 0.0) continue;

            // Blend the center color continuously instead of switching it at
            // a distance threshold. This prevents a visual jump near center.
            double centerInfluence = 1.0 - SmoothStep(Math.Min(1.0, absDistance));
            double outerInfluence = SmoothStep(Math.Clamp(absDistance - 1.0, 0.0, 1.0));
            Brush outerColor = BlendBrush(body, muted, outerInfluence);
            Brush textColor = BlendBrush(outerColor, centerText, centerInfluence);

            var f = new FormattedText(valueAt.ToString("00"), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, textColor, dpi);
            f.SetFontWeight(FontWeights.Normal);
            dc.PushOpacity(opacity);
            dc.DrawText(f, new Point(centerX - f.WidthIncludingTrailingWhitespace / 2, top - f.Height / 2));

            // Fade the bold face in continuously as the value approaches the
            // center, rather than switching FontWeight abruptly.
            if (centerInfluence > 0.001)
            {
                var bold = new FormattedText(valueAt.ToString("00"), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, textColor, dpi);
                bold.SetFontWeight(FontWeights.Bold);
                dc.PushOpacity(opacity * centerInfluence);
                dc.DrawText(bold, new Point(centerX - bold.WidthIncludingTrailingWhitespace / 2, top - bold.Height / 2));
                dc.Pop();
            }
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

    /// <summary>
    /// Font size by distance from the center, independent of VisibleOffsets.
    /// Linear from centerSize (d = 0) down to MinTextSize at the outer edge.
    /// </summary>
    private double SizeForDistance(double distance, double centerSize)
    {
        double span = VisibleOffsets + 1.0;
        double t = Math.Min(1.0, distance / span);
        return MinTextSize + (centerSize - MinTextSize) * (1.0 - t);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsEditable) return;

        Point p = e.GetPosition(this);

        if (_dragging)
        {
            ContinueDrag(p);
            return;
        }

    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsEditable) return;

        int column = ColumnAt(e.GetPosition(this).X);
        if (column < 0) return;

        // Wheel up (Delta > 0) => value decreases, like a normal scroll.
        WheelStepToCenter(column, e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEditable) return;

        Point p = e.GetPosition(this);
        int column = ColumnAt(p.X);
        if (column < 0) return;

        StartDrag(column, p);
        e.Handled = true;
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

    private int ColumnAt(double x) =>
        x < ColumnWidth ? 0 : x > ColumnWidth + ColonWidth ? 1 : -1;

    private double PositionOf(int column) => column == 0 ? _hoursPosition : _minutesPosition;

    private void SetPosition(int column, double position)
    {
        if (column == 0)
        {
            _hoursPosition = Normalize(position, 24);
        }
        else
        {
            _minutesPosition = Normalize(position, 60);
        }
    }

    private static double Normalize(double position, int modulus)
    {
        double m = modulus;
        position %= m;
        if (position < 0) position += m;
        return position;
    }

    private void WheelStepToCenter(int column, int delta)
    {
        // Take over from an LMB settle on the same column.
        if (_settling && _dragColumn == column)
        {
            ChaseOf(column).Target = _settleTarget;
            StopSettle();
        }

        WheelChase chase = ChaseOf(column);
        double current = PositionOf(column);
        int modulus = column == 0 ? 24 : 60;

        if (!chase.Active)
            chase.Target = Math.Round(current);

        double proposed = chase.Target + delta;
        if (Math.Abs(ShortestDelta(current, proposed, modulus)) > MaxWheelPendingRows)
            return;

        chase.Target = Normalize(proposed, modulus);
        CommitSettledValue(column, chase.Target);

        if (!chase.Active)
        {
            chase.Active = true;
            chase.Last = DateTime.UtcNow;
        }

        StartWheelLoop();
    }

    private WheelChase ChaseOf(int column) => column == 0 ? _hoursChase : _minutesChase;

    /// <summary>Shortest signed distance from one position to another (wrap-aware).</summary>
    private static double ShortestDelta(double from, double to, int modulus)
    {
        double delta = to - from;
        delta %= modulus;
        if (delta > modulus / 2.0) delta -= modulus;
        else if (delta < -modulus / 2.0) delta += modulus;
        return delta;
    }

    private void StartWheelLoop()
    {
        if (_wheelLoopActive) return;
        _wheelLoopActive = true;
        CompositionTarget.Rendering += OnWheelRendering;
    }

    private void OnWheelRendering(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        bool any = UpdateChase(0, _hoursChase, now, 24);
        any |= UpdateChase(1, _minutesChase, now, 60);

        InvalidateVisual();

        if (!any)
        {
            _wheelLoopActive = false;
            CompositionTarget.Rendering -= OnWheelRendering;
        }
    }

    private bool UpdateChase(int column, WheelChase chase, DateTime now, int modulus)
    {
        if (!chase.Active) return false;

        double dt = (now - chase.Last).TotalSeconds;
        chase.Last = now;
        if (dt <= 0) return true;
        if (dt > 0.1) dt = 0.1;

        double current = PositionOf(column);
        double remaining = ShortestDelta(current, chase.Target, modulus);

        if (Math.Abs(remaining) <= WheelSnap)
        {
            SetPosition(column, chase.Target);
            chase.Active = false;
            return false;
        }

        // One continuous motion: extend the target without restarting, so a
        // burst of notches keeps the same smooth chase (no per-notch stutter).
        double ease = 1.0 - Math.Exp(-WheelEaseRate * dt);
        SetPosition(column, current + remaining * ease);
        return true;
    }

    // ---- Drag ----

    private void StartDrag(int column, Point start)
    {
        StopSettle();
        // A drag on this column takes over from its wheel chase; the other
        // column's chase keeps running independently.
        ChaseOf(column).Active = false;
        _dragging = true;
        _dragColumn = column;
        _dragAccumulator = 0;
        _smoothSpeed = 0;
        _dragStartTime = DateTime.UtcNow;
        _lastMovePoint = start;
        _lastMoveTime = DateTime.UtcNow;
        Cursor = Cursors.SizeNS;
        CaptureMouse();
    }

    private void ContinueDrag(Point p)
    {
        DateTime now = DateTime.UtcNow;
        double dy = p.Y - _lastMovePoint.Y;
        double dt = (now - _lastMoveTime).TotalSeconds;

        _lastMovePoint = p;
        _lastMoveTime = now;

        if (dt <= 0) return;

        // Only the vertical movement speed matters.
        double speed = Math.Abs(dy) / dt;
        double direction = dy > 0 ? -1 : 1;

        // Smooth per-event jitter.
        _smoothSpeed = _smoothSpeed * 0.7 + speed * 0.3;
        speed = _smoothSpeed;

        // Start inertia: two-stage ramp (3% -> 30% over 0.18 s, then -> 100% over 1 s).
        double elapsed = (now - _dragStartTime).TotalSeconds;
        double rampFactor;
        if (elapsed < StartRampStage1Seconds)
        {
            double t1 = elapsed / StartRampStage1Seconds;
            rampFactor = StartRampFloor + (StartRampMid - StartRampFloor) * t1;
        }
        else
        {
            double t2 = Math.Min(1.0, (elapsed - StartRampStage1Seconds) / StartRampStage2Seconds);
            rampFactor = StartRampMid + (StartRampCeiling - StartRampMid) * t2;
        }

        double rowsDelta = direction * speed * rampFactor / (BasePixelsPerSecondPerRow / DragSpeedFactor);

        double position = PositionOf(_dragColumn) + rowsDelta;
        SetPosition(_dragColumn, position);

        _dragAccumulator += rowsDelta;
        int whole = (int)_dragAccumulator;
        _dragAccumulator -= whole;
        if (whole != 0)
        {
            if (_dragColumn == 0)
                Hours = Mod(Hours + whole, 24);
            else
                Minutes = Mod(Minutes + whole, 60);
            Changed?.Invoke();
        }

        InvalidateVisual();
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _dragAccumulator = 0;
        Cursor = Cursors.Arrow;
        if (IsMouseCaptured) ReleaseMouseCapture();
        StartSettle();
    }

    // ---- Settle (ease the nearest value into the center) ----

    private void StartSettle()
    {
        StartSettleTo(_dragColumn, Math.Round(PositionOf(_dragColumn)));
    }

    private void StartSettleTo(int column, double target)
    {
        _dragColumn = column;
        _settleStartPosition = PositionOf(column);
        _settleTarget = target;
        _settleDistance = _settleTarget - _settleStartPosition;
        if (Math.Abs(_settleDistance) <= 0.001)
        {
            SetPosition(column, _settleTarget);
            CommitSettledValue(column, _settleTarget);
            InvalidateVisual();
            return;
        }

        if (!_settling)
        {
            _settling = true;
            CompositionTarget.Rendering += OnSettleRendering;
        }
        _settleElapsed = 0;
        _settleDuration = Math.Max(0.16, Math.Abs(_settleDistance) * SettleDurationPerRow);
        _lastSettleTime = DateTime.UtcNow;
    }

    private void OnSettleRendering(object? sender, EventArgs e)
    {
        if (_dragging)
        {
            StopSettle();
            return;
        }

        DateTime now = DateTime.UtcNow;
        double dt = (now - _lastSettleTime).TotalSeconds;
        _lastSettleTime = now;
        if (dt <= 0) return;
        if (dt > 0.1) dt = 0.1;

        _settleElapsed += dt;
        double t = Math.Min(1.0, _settleElapsed / _settleDuration);
        double eased = t * t * (3.0 - 2.0 * t);
        SetPosition(_dragColumn, _settleStartPosition + _settleDistance * eased);

        if (t >= 1.0)
        {
            SetPosition(_dragColumn, _settleTarget);
            CommitSettledValue(_dragColumn, _settleTarget);
            StopSettle();
            InvalidateVisual();
            return;
        }

        InvalidateVisual();
    }

    private void CommitSettledValue(int column, double position)
    {
        int modulus = column == 0 ? 24 : 60;
        int value = Mod((int)Math.Round(position), modulus);
        bool changed = column == 0 ? Hours != value : Minutes != value;

        if (column == 0)
            Hours = value;
        else
            Minutes = value;

        if (changed)
            Changed?.Invoke();
    }

    private void StopSettle()
    {
        if (!_settling) return;
        _settling = false;
        CompositionTarget.Rendering -= OnSettleRendering;
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
