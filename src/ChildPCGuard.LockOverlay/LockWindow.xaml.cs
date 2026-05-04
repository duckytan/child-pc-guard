using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.IPC;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ChildPCGuard.LockOverlay;

/// <summary>
/// 锁屏主窗口 - 虚拟桌面隔离 + 密码验证
/// </summary>
public partial class LockWindow : Window
{
    private readonly VirtualDesktopManager _desktopManager;
    private readonly KeyboardHook _keyboardHook;
    private readonly PasswordValidator _passwordValidator;
    private readonly PipeClient _pipeClient;
    private readonly DispatcherTimer _clockTimer;
    private readonly string _lockReason;

    private bool _isUnlocking = false;

    public LockWindow(AppConfig config, string passwordHash, string lockReason)
    {
        InitializeComponent();

        _desktopManager = new VirtualDesktopManager();
        _keyboardHook = new KeyboardHook();
        _passwordValidator = new PasswordValidator(passwordHash);
        _pipeClient = new PipeClient();
        _lockReason = lockReason;

        // 时钟定时器
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += UpdateClock;
        _clockTimer.Start();

        // 初始化界面
        InitializeUI(lockReason);
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            // 1. 创建并切换到虚拟桌面
            if (!_desktopManager.CreateLockDesktop() ||
                !_desktopManager.SwitchToLockDesktop())
            {
                throw new InvalidOperationException("虚拟桌面创建失败");
            }

            // 2. 安装全局键盘钩子
            if (!_keyboardHook.Install())
            {
                System.Diagnostics.Debug.WriteLine("[LockWindow] 键盘钩子安装失败");
            }

            System.Diagnostics.Debug.WriteLine("[LockWindow] 锁屏已激活");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LockWindow] 初始化失败: {ex.Message}");
            // 回退：仅全屏窗口，无虚拟桌面
        }
    }

    private void InitializeUI(string lockReason)
    {
        // 锁屏原因
        LockReasonText.Text = GetLockMessage(lockReason);

        // 初始时间
        UpdateClock(null, null);
    }

    private void UpdateClock(object? sender, EventArgs? e)
    {
        var now = DateTime.Now;
        CurrentTimeText.Text = now.ToString("HH:mm:ss");
        DateText.Text = now.ToString("yyyy年MM月dd日 dddd");
    }

    private string GetLockMessage(string reason) => reason switch
    {
        "DailyLimitReached" => "今日使用时间已到，好好休息～",
        "OutsideAllowedWindow" => "现在不在允许的使用时段内",
        "TimeTampered" => "检测到系统时间异常",
        "ManualLock" => "屏幕已被家长锁定",
        "ContinuousLimitReached" => "连续使用时间过长，休息一下吧～",
        _ => "屏幕已锁定"
    };

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        await ValidatePasswordAsync();
    }

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ValidatePasswordAsync();
        }
    }

    private async Task ValidatePasswordAsync()
    {
        if (_isUnlocking) return;
        _isUnlocking = true;

        try
        {
            var password = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowStatus("请输入密码");
                _isUnlocking = false;
                return;
            }

            // 验证密码
            var result = _passwordValidator.Validate(password);

            if (result.IsValid)
            {
                // 验证成功：发送解锁指令给 GuardService
                var response = await _pipeClient.SendMessageAsync(
                    IpcMessage.Create(IpcCommand.Unlock));

                if (response?.Command == IpcCommand.Ack)
                {
                    // 解锁成功，关闭窗口
                    System.Diagnostics.Debug.WriteLine("[LockWindow] 解锁成功");
                    _keyboardHook.Dispose();
                    _desktopManager.Dispose();
                    Close();
                }
                else
                {
                    ShowStatus("服务通信失败，请重试");
                }
            }
            else if (result.IsLocked)
            {
                // 被锁定
                var remaining = result.LockedUntil!.Value - DateTime.Now;
                ShowStatus($"密码错误次数过多，请等待 {remaining.Minutes + 1} 分钟后再试");
            }
            else
            {
                // 密码错误
                var msg = result.RemainingAttempts.HasValue
                    ? $"密码错误，还可尝试 {result.RemainingAttempts} 次"
                    : "密码错误";
                ShowStatus(msg);
                PasswordInput.Clear();
                PasswordInput.Focus();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LockWindow] 验证失败: {ex.Message}");
            ShowStatus("验证出错，请重试");
        }
        finally
        {
            _isUnlocking = false;
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage.Text = message;
        StatusMessage.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _clockTimer.Stop();
        _keyboardHook.Dispose();
        _desktopManager.Dispose();
    }

    // 防止窗口关闭（除解锁成功外）
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isUnlocking)
        {
            e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
