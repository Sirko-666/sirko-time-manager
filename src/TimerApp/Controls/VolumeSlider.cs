using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TimerApp.Controls;

/// <summary>
/// Volume slider: click/drag anywhere on the track with the left mouse button,
/// and mouse wheel steps by <see cref="WheelStep"/> (2% by default).
/// </summary>
public sealed class VolumeSlider : Slider
{
    /// <summary>Thumb diameter used by VolumeSliderStyle — needed for accurate tracking.</summary>
    private const double ThumbSize = 12;

    public static readonly DependencyProperty WheelStepProperty = DependencyProperty.Register(
        nameof(WheelStep), typeof(double), typeof(VolumeSlider),
        new PropertyMetadata(0.02));

    public double WheelStep
    {
        get => (double)GetValue(WheelStepProperty);
        set => SetValue(WheelStepProperty, value);
    }

    private bool _dragging;
    private double _dragStartX;
    private double _dragStartValue;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Relative drag: the thumb does NOT jump to the click point; it follows
        // the mouse movement from wherever it currently is.
        Mouse.Capture(this);
        _dragging = true;
        _dragStartX = e.GetPosition(this).X;
        _dragStartValue = Value;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging || !IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;

        double dx = e.GetPosition(this).X - _dragStartX;
        double travel = Math.Max(1.0, ActualWidth - ThumbSize);
        double range = Maximum - Minimum;
        SetCurrentValue(ValueProperty,
            Math.Clamp(_dragStartValue + dx / travel * range, Minimum, Maximum));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        double step = e.Delta > 0 ? WheelStep : -WheelStep;
        SetCurrentValue(ValueProperty, Math.Clamp(Value + step, Minimum, Maximum));
        e.Handled = true;
    }
}
