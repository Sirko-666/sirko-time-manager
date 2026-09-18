using System.Windows;

namespace TimerApp.Windows;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
