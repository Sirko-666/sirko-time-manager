using System.Windows;

namespace StmInstaller.Windows;

public partial class UninstallConfirmWindow : Window
{
    public UninstallConfirmWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
