using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TimerApp.Windows;

/// <summary>
/// A small topmost "toast" anchored to the bottom-right of the primary screen
/// (above the taskbar). Auto-closes after a few seconds; a click runs the action.
/// Used instead of NotifyIcon balloons, which Windows 10/11 often suppresses.
/// </summary>
public partial class ToastWindow : Window
{
    private const double EdgeMargin = 12;
    private readonly DispatcherTimer _autoClose;
    private readonly Action? _onClick;

    public ToastWindow(string title, string message, Action? onClick)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        _onClick = onClick;

        _autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _autoClose.Tick += (_, _) => Close();

        Loaded += OnLoaded;
        Closed += (_, _) => _autoClose.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rect work = SystemParameters.WorkArea;
        Left = work.Right - ActualWidth - EdgeMargin;
        Top = work.Bottom - ActualHeight - EdgeMargin;

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        _autoClose.Start();
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        Action? callback = _onClick;
        Close();
        callback?.Invoke();
    }
}
