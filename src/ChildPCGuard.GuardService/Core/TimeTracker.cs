using ChildPCGuard.Shared.Win32;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.GuardService.Core;

/// <summary>
/// 时间追踪器：使用 GetLastInputInfo 统计用户实际活跃使用时长
/// 空闲超过阈值时暂停计时，比"登录即计时"更精准
/// </summary>
public class TimeTracker
{
    private readonly ILogger<TimeTracker> _logger;
    private readonly int _idleThresholdMs;

    /// <summary>今日已累计使用时长（分钟），含今日家长追加时间</summary>
    public double UsedMinutesToday { get; private set; }

    /// <summary>当前连续使用时长（分钟），中途空闲会重置</summary>
    public double ContinuousMinutes { get; private set; }

    private DateTime _currentDate = DateTime.Today;

    public TimeTracker(ILogger<TimeTracker> logger, double initialUsedMinutes = 0,
        double initialContinuousMinutes = 0, int idleThresholdMs = 5000)
    {
        _logger = logger;
        _idleThresholdMs = idleThresholdMs;
        UsedMinutesToday = initialUsedMinutes;
        ContinuousMinutes = initialContinuousMinutes;
    }

    /// <summary>
    /// 每次 tick（5 秒）调用一次，传入本次 tick 经过的秒数
    /// </summary>
    /// <param name="tickSeconds">本次 tick 间隔（秒）</param>
    /// <returns>true = 用户活跃（已计时）；false = 用户空闲（未计时）</returns>
    public bool Tick(double tickSeconds = 5.0)
    {
        // 跨天自动重置
        if (DateTime.Today != _currentDate)
        {
            _logger.LogInformation("跨天重置计时器: {OldDate} → {NewDate}",
                _currentDate.ToString("yyyy-MM-dd"), DateTime.Today.ToString("yyyy-MM-dd"));
            UsedMinutesToday = 0;
            ContinuousMinutes = 0;
            _currentDate = DateTime.Today;
        }

        if (!IsUserActive())
        {
            // 空闲时连续使用计时重置
            if (ContinuousMinutes > 0)
            {
                _logger.LogDebug("用户空闲，连续使用计时重置（原 {Continuous:F1} 分钟）", ContinuousMinutes);
                ContinuousMinutes = 0;
            }
            return false;
        }

        var addedMinutes = tickSeconds / 60.0;
        UsedMinutesToday += addedMinutes;
        ContinuousMinutes += addedMinutes;

        return true;
    }

    /// <summary>家长追加时间时调用（不修改 UsedMinutesToday，而是在 RuleEngine 侧调整上限）</summary>
    public void AddExtraTime(double minutes)
    {
        // 追加时间通过 DailyState.ExtraMinutesToday 实现，TimeTracker 只负责计时
        _logger.LogInformation("追加使用时间 {Minutes} 分钟（由 RuleEngine 处理上限调整）", minutes);
    }

    /// <summary>从恢复的状态同步使用时长</summary>
    public void RestoreState(double usedMinutes, double continuousMinutes)
    {
        UsedMinutesToday = usedMinutes;
        ContinuousMinutes = continuousMinutes;
        _logger.LogInformation("恢复计时状态: 已用 {Used:F1} 分钟，连续 {Continuous:F1} 分钟",
            usedMinutes, continuousMinutes);
    }

    /// <summary>判断用户当前是否处于活跃状态（最近一次输入距现在 < 阈值）</summary>
    public bool IsUserActive()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info)) return false;

        var idleMs = (int)(NativeMethods.GetTickCount() - info.dwTime);
        return idleMs < _idleThresholdMs;
    }
}
