using System.Windows;

namespace TimerApp.Windows;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message, bool dangerButton = false, string? confirmText = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        if (!string.IsNullOrEmpty(confirmText))
            ConfirmButton.Content = confirmText;
        if (dangerButton)
            ConfirmButton.Style = (System.Windows.Style)this.FindResource("PrimaryDangerButtonStyle");
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
