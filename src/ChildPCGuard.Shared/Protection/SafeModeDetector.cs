using ChildPCGuard.Shared.Win32;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.Shared.Protection;

/// <summary>
/// 安全模式检测器：检测到安全模式启动时立即强制关机
/// </summary>
public static class SafeModeDetector
{
    /// <summary>
    /// 检查是否在安全模式下运行
    /// </summary>
    /// <returns>0=正常，1=安全模式，2=带网络安全模式</returns>
    public static int GetBootMode()
    {
        return NativeMethods.GetSystemMetrics(NativeMethods.SM_CLEANBOOT);
    }

    public static bool IsInSafeMode() => GetBootMode() != NativeMethods.CLEAN_BOOT_NORMAL;

    /// <summary>
    /// 如果检测到安全模式，立即强制关机（给孩子30秒看到警告）
    /// </summary>
    public static void CheckAndShutdownIfSafeMode(ILogger? logger = null)
    {
        if (!IsInSafeMode()) return;

        logger?.LogCritical("🚨 检测到安全模式启动（模式={Mode}），立即强制关机", GetBootMode());

        try
        {
            // 先显示警告消息（如果有 UI 能力）
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/s /f /t 10 /c \"系统检测到异常启动模式，正在关机...\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "执行关机命令失败");
        }
    }
}
