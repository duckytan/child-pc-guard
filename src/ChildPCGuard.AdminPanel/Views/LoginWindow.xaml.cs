using ChildPCGuard.AdminPanel.Services;
using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.IPC;
using System.Windows;
using System.Windows.Input;

namespace ChildPCGuard.AdminPanel.Views;

public partial class LoginWindow : Window
{
    private readonly PasswordValidator _passwordValidator;
    private readonly ConfigManager _configManager;
    private DispatcherTimer? _lockTimer;

    public bool LoginSuccess { get; private set; } = false;

    public LoginWindow()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        var config = _configManager.Load();
        _passwordValidator = new PasswordValidator(config.AdminPasswordHash);
        _lockTimer = null;

        // 检查是否锁定
        CheckLockStatus();
    }

    private void CheckLockStatus()
    {
        var (isLocked, remainingSeconds) = _passwordValidator.CheckLockStatus();

        if (isLocked)
        {
            // 已锁定，禁用登录按钮并显示倒计时
            LoginButton.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            StatusText.Text = $"登录失败次数过多，请等待 {remainingSeconds / 60} 分 {remainingSeconds % 60} 秒";
            StatusText.Visibility = Visibility.Visible;

            // 启动倒计时
            _lockTimer = new DispatcherTimer();
            _lockTimer.Interval = TimeSpan.FromSeconds(1);
            _lockTimer.Tick += (s, e) =>
            {
                var (locked, remaining) = _passwordValidator.CheckLockStatus();
                if (locked)
                {
                    StatusText.Text = $"登录失败次数过多，请等待 {remaining / 60} 分 {remaining % 60} 秒";
                }
                else
                {
                    // 解除锁定
                    _lockTimer.Stop();
                    LoginButton.IsEnabled = true;
                    PasswordBox.IsEnabled = true;
                    StatusText.Visibility = Visibility.Collapsed;
                }
            };
            _lockTimer.Start();
        }
        else
        {
            // 未锁定
            LoginButton.IsEnabled = false;
            PasswordBox.IsEnabled = true;
            StatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // 只有输入密码后才启用登录按钮
        LoginButton.IsEnabled = !string.IsNullOrWhiteSpace(PasswordBox.Password);
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Login_Click(sender, e);
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("请输入密码");
            return;
        }

        // 验证密码
        var (isValid, remaining) = await _passwordValidator.VerifyPasswordAsync(password);

        if (isValid)
        {
            // 登录成功
            LoginSuccess = true;
            DialogResult = true;
            Close();
        }
        else
        {
            // 登录失败
            if (remaining > 0)
            {
                // 触发锁定
                ShowError($"密码错误，已锁定 {remaining} 秒");
                CheckLockStatus();
            }
            else
            {
                ShowError($"密码错误，剩余尝试次数: {_passwordValidator.GetRemainingAttempts()}");
            }
            PasswordBox.Password = string.Empty;
            PasswordBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
