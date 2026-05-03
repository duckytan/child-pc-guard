using ChildPCGuard.Shared.Config;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.GuardService.Core;

/// <summary>锁屏触发原因</summary>
public enum LockReason
{
    None,
    DailyLimitReached,
    OutsideAllowedWindow,
    TimeTampered,
    ManualLock,
    AutoShutdown,
    ContinuousLimitReached
}

/// <summary>
/// 规则引擎：判断当前是否需要锁屏，并确定原因
/// </summary>
public class RuleEngine
{
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(ILogger<RuleEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 评估当前状态，返回是否需要锁屏以及原因
    /// </summary>
    public (bool ShouldLock, LockReason Reason) Evaluate(
        AppConfig config,
        double usedMinutesToday,
        double extraMinutesToday,
        double continuousMinutes,
        bool ntpTampered)
    {
        if (!config.IsEnabled)
            return (false, LockReason.None);

        // 1. NTP 时间篡改检测
        if (ntpTampered)
        {
            _logger.LogWarning("规则引擎: 检测到时间篡改 → 触发锁屏");
            return (true, LockReason.TimeTampered);
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var rule = GetTodayRule(config);

        // 2. 检查是否在允许时段内
        if (rule.AllowedTimeWindows.Count > 0)
        {
            bool inWindow = rule.AllowedTimeWindows.Any(w => w.Contains(now));
            if (!inWindow)
            {
                _logger.LogDebug("规则引擎: 当前时间 {Now} 不在允许时段内 → 触发锁屏", now);
                return (true, LockReason.OutsideAllowedWindow);
            }
        }

        // 3. 检查每日时长上限
        double effectiveLimit = rule.DailyLimitMinutes + extraMinutesToday;
        if (usedMinutesToday >= effectiveLimit)
        {
            _logger.LogInformation("规则引擎: 今日已用 {Used:F1}/{Limit:F1} 分钟 → 触发锁屏",
                usedMinutesToday, effectiveLimit);
            return (true, LockReason.DailyLimitReached);
        }

        // 4. 连续使用时长检测
        if (config.ContinuousLimitMinutes > 0 && continuousMinutes >= config.ContinuousLimitMinutes)
        {
            _logger.LogInformation("规则引擎: 连续使用 {Continuous:F1}/{Limit} 分钟 → 强制休息",
                continuousMinutes, config.ContinuousLimitMinutes);
            return (true, LockReason.ContinuousLimitReached);
        }

        return (false, LockReason.None);
    }

    /// <summary>检查是否需要发送剩余时间预警</summary>
    public int? CheckWarning(AppConfig config, double usedMinutesToday, double extraMinutesToday,
        IEnumerable<int> alreadySentMinutes)
    {
        var rule = GetTodayRule(config);
        double effectiveLimit = rule.DailyLimitMinutes + extraMinutesToday;
        double remainingMinutes = effectiveLimit - usedMinutesToday;

        foreach (var warnAt in config.WarningMinutes.OrderByDescending(m => m))
        {
            if (remainingMinutes <= warnAt && remainingMinutes > warnAt - 1.0
                && !alreadySentMinutes.Contains(warnAt))
            {
                return warnAt;
            }
        }

        return null;
    }

    /// <summary>检查是否到达关机时间（在关机时间前 60 秒内）</summary>
    public bool IsShutdownTime(string autoShutdownTime, out int secondsUntilShutdown)
    {
        secondsUntilShutdown = 0;
        if (!TimeOnly.TryParse(autoShutdownTime, out var shutdownTime))
            return false;

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var diff = shutdownTime.ToTimeSpan() - now.ToTimeSpan();

        // 处理跨午夜情况
        if (diff.TotalSeconds < -43200) diff = diff.Add(TimeSpan.FromDays(1));

        if (diff.TotalSeconds is >= 0 and <= 60)
        {
            secondsUntilShutdown = (int)diff.TotalSeconds;
            return true;
        }

        return false;
    }

    /// <summary>获取今天适用的规则（工作日/周末）</summary>
    public static DayRule GetTodayRule(AppConfig config)
    {
        var dayOfWeek = DateTime.Today.DayOfWeek;
        return dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? config.Rules.Weekends
            : config.Rules.Weekdays;
    }

    /// <summary>获取今日剩余可用分钟数</summary>
    public static double GetRemainingMinutes(AppConfig config, double usedMinutesToday, double extraMinutesToday)
    {
        var rule = GetTodayRule(config);
        return Math.Max(0, rule.DailyLimitMinutes + extraMinutesToday - usedMinutesToday);
    }
}
