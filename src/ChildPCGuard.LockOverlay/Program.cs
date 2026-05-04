using ChildPCGuard.Shared.Config;
using System.Windows;

namespace ChildPCGuard.LockOverlay;

/// <summary>
/// LockOverlay 程序入口
/// 运行参数：LockOverlay.exe --reason <lockReason>
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 解析命令行参数
        string lockReason = "ManualLock";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--reason" && i + 1 < args.Length)
            {
                lockReason = args[i + 1];
                break;
            }
        }

        try
        {
            // 加载配置
            var configManager = new ConfigManager();
            var config = configManager.Load();

            if (string.IsNullOrEmpty(config.AdminPasswordHash))
            {
                // 首次运行或配置异常：生成临时密码
                config.AdminPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");
                configManager.Save(config);
            }

            // 创建并显示锁屏窗口
            var app = new Application();
            var window = new LockWindow(config, config.AdminPasswordHash, lockReason);
            app.Run(window);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LockOverlay] 启动失败: {ex.Message}");
            // 回退到系统锁屏
            ChildPCGuard.Shared.Win32.NativeMethods.LockWorkStation();
        }
    }
}
