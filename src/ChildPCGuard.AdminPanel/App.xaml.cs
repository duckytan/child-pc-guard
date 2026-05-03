using System.Windows;
using ChildPCGuard.AdminPanel.Views;

namespace ChildPCGuard.AdminPanel;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 显示登录窗口
        var loginWindow = new LoginWindow();
        var result = loginWindow.ShowDialog();

        if (result == true && loginWindow.LoginSuccess)
        {
            // 登录成功，显示主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        else
        {
            // 登录失败或取消，退出程序
            Shutdown();
        }
    }
}
