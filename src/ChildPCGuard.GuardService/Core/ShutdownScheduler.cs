using Microsoft.Extensions.Logging;

namespace ChildPCGuard.GuardService.Core;

/// <summary>
/// 关机调度器（双重保障机制之一）：
/// GuardService 内部定时检查到达关机时间时执行关机
/// 另一重保障为安装时创建的 Windows 任务计划程序任务
/// </summary>
public class ShutdownScheduler
{
    private readonly ILogger<ShutdownScheduler> _logger;
    private bool _warningIssued = false;
    private bool _shutdownInitiated = false;

    public ShutdownScheduler(ILogger<ShutdownScheduler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 每次 tick 调用，检查是否到达关机时间
    /// </summary>
    /// <param name="autoShutdownTime">关机时间，格式 "HH:mm"</param>
    public void Tick(string autoShutdownTime)
    {
        if (_shutdownInitiated) return;

        if (!TimeOnly.TryParse(autoShutdownTime, out var shutdownTime)) return;

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var diff = shutdownTime.ToTimeSpan() - now.ToTimeSpan();

        // 处理跨午夜情况
        if (diff.TotalSeconds < -43200)
            diff = diff.Add(TimeSpan.FromDays(1));

        double secondsUntil = diff.TotalSeconds;

        // 到点：执行关机（给 60 秒缓冲）
        if (secondsUntil is >= -5 and <= 5)
        {
            ExecuteShutdown(60);
            return;
        }

        // 提前 60 秒发出警告
        if (!_warningIssued && secondsUntil is > 0 and <= 60)
        {
            _warningIssued = true;
            _logger.LogInformation("关机倒计时：{Seconds:F0} 秒后自动关机", secondsUntil);
            SendShutdownWarning((int)secondsUntil);
        }
    }

    private void ExecuteShutdown(int delaySeconds)
    {
        if (_shutdownInitiated) return;
        _shutdownInitiated = true;

        _logger.LogInformation("执行自动关机，延迟 {Delay} 秒", delaySeconds);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = $"/s /f /t {delaySeconds} /c \"每日使用时间已到，电脑将自动关机，晚安～\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行关机命令失败");
        }
    }

    private void SendShutdownWarning(int secondsRemaining)
    {
        // Toast 通知由 NotificationHelper 处理，这里只记录日志
        _logger.LogInformation("⚠️ 关机预警：电脑将在 {Seconds} 秒后自动关机", secondsRemaining);
    }

    /// <summary>取消已发起的关机（家长操作时使用）</summary>
    public void CancelShutdown()
    {
        if (!_shutdownInitiated) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/a",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            _shutdownInitiated = false;
            _warningIssued = false;
            _logger.LogInformation("关机已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消关机失败");
        }
    }

    /// <summary>立即关机（家长指令或紧急情况）</summary>
    public void ShutdownNow()
    {
        _shutdownInitiated = true;
        _logger.LogInformation("立即关机（家长指令）");
        ExecuteShutdown(0);
    }
}
