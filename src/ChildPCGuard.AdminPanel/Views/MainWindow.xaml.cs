using System.Windows;

namespace ChildPCGuard.AdminPanel.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 默认显示状态页
        NavigateToStatus(null, null);
    }

    private void NavigateToStatus(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.StatusPage());
    }

    private void NavigateToRules(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.RulesPage());
    }

    private void NavigateToActions(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.ActionsPage());
    }

    private void NavigateToLogs(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.LogsPage());
    }

    private void NavigateToPassword(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.PasswordPage());
    }

    private void NavigateToAbout(object? sender, RoutedEventArgs? e)
    {
        ContentFrame.Navigate(new Pages.AboutPage());
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
