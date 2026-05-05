using ChildPCGuard.GuardService.Core;
using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.State;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// RuleEngine 单元测试
/// 对应需求：FR-001（时长限制）、FR-002（时段限制）
/// </summary>
public class RuleEngineTests
{
    private readonly RuleEngine _sut = new(NullLogger<RuleEngine>.Instance);

    private static AppConfig DefaultConfig(int weekdayLimit = 120, int weekendLimit = 240) => new()
    {
        IsEnabled = true,
        Rules = new TimeRules
        {
            Weekdays = new DayRule
            {
                DailyLimitMinutes = weekdayLimit,
                AllowedTimeWindows = [new TimeWindow { Start = "15:00", End = "20:00" }]
            },
            Weekends = new DayRule
            {
                DailyLimitMinutes = weekendLimit,
                AllowedTimeWindows = []
            }
        },
        ContinuousLimitMinutes = 0
    };

    // ── 时长判断 ──────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_UsedLessThanLimit_ShouldNotLock()
    {
        var config = DefaultConfig(weekdayLimit: 120);
        // 工作日，时段内，未超限
        // 注意：这个测试在非工作日运行可能会失败（时段判断），
        // 所以我们用周末规则（无时段限制）来测试纯时长逻辑
        var weekendConfig = new AppConfig
        {
            IsEnabled = true,
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 240, AllowedTimeWindows = [] }
            }
        };

        var (shouldLock, reason) = _sut.Evaluate(weekendConfig, 100, 0, 0, false);

        shouldLock.Should().BeFalse();
        reason.Should().Be(LockReason.None);
    }

    [Fact]
    public void Evaluate_UsedEqualsLimit_ShouldLock()
    {
        var config = new AppConfig
        {
            IsEnabled = true,
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 240, AllowedTimeWindows = [] }
            }
        };

        var (shouldLock, reason) = _sut.Evaluate(config, 120, 0, 0, false);

        shouldLock.Should().BeTrue();
        reason.Should().Be(LockReason.DailyLimitReached);
    }

    [Fact]
    public void Evaluate_UsedExceedsLimit_ShouldLock()
    {
        var config = new AppConfig
        {
            IsEnabled = true,
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 60, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] }
            }
        };

        var (shouldLock, reason) = _sut.Evaluate(config, 150, 0, 0, false);

        shouldLock.Should().BeTrue();
        reason.Should().Be(LockReason.DailyLimitReached);
    }

    [Fact]
    public void Evaluate_ExtraTimeExtendedLimit_ShouldNotLock()
    {
        var config = new AppConfig
        {
            IsEnabled = true,
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] }
            }
        };

        // 已用 130 分钟，但家长追加了 30 分钟，有效上限 = 150 分钟
        var (shouldLock, reason) = _sut.Evaluate(config, 130, 30, 0, false);

        shouldLock.Should().BeFalse();
        reason.Should().Be(LockReason.None);
    }

    // ── 时间篡改 ──────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_NtpTampered_ShouldLockWithTimeTamperedReason()
    {
        var config = new AppConfig { IsEnabled = true };

        var (shouldLock, reason) = _sut.Evaluate(config, 0, 0, 0, ntpTampered: true);

        shouldLock.Should().BeTrue();
        reason.Should().Be(LockReason.TimeTampered);
    }

    // ── 连续使用时长 ──────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ContinuousLimitExceeded_ShouldLock()
    {
        var config = new AppConfig
        {
            IsEnabled = true,
            ContinuousLimitMinutes = 45,
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 240, AllowedTimeWindows = [] }
            }
        };

        var (shouldLock, reason) = _sut.Evaluate(config, 60, 0, continuousMinutes: 45, false);

        shouldLock.Should().BeTrue();
        reason.Should().Be(LockReason.ContinuousLimitReached);
    }

    // ── 服务禁用 ──────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ServiceDisabled_ShouldNeverLock()
    {
        var config = new AppConfig { IsEnabled = false };

        var (shouldLock, _) = _sut.Evaluate(config, 9999, 0, 999, ntpTampered: true);

        shouldLock.Should().BeFalse();
    }

    // ── 剩余时间计算 ──────────────────────────────────────────────────────

    [Fact]
    public void GetRemainingMinutes_ReturnsCorrectValue()
    {
        var config = new AppConfig
        {
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120 },
                Weekends = new DayRule { DailyLimitMinutes = 240 }
            }
        };

        // 根据今天是工作日还是周末取正确规则
        var isWeekend = DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var limit = isWeekend ? 240 : 120;
        var remaining = RuleEngine.GetRemainingMinutes(config, 50, 10);

        remaining.Should().Be(limit - 50 + 10);
    }

    [Fact]
    public void GetRemainingMinutes_NeverNegative()
    {
        var config = new AppConfig
        {
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 60 },
                Weekends = new DayRule { DailyLimitMinutes = 60 }
            }
        };

        var remaining = RuleEngine.GetRemainingMinutes(config, 200, 0);

        remaining.Should().Be(0);
    }

    // ── 预警检查 ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckWarning_ReturnsCorrectWarningMinute()
    {
        var config = new AppConfig
        {
            WarningMinutes = [10, 5, 1],
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] }
            }
        };

        // 剩余 9.5 分钟，应触发 10 分钟预警
        var warnAt = _sut.CheckWarning(config, usedMinutesToday: 110.5, extraMinutesToday: 0, []);

        warnAt.Should().Be(10);
    }

    [Fact]
    public void CheckWarning_DoesNotRepeatSentWarning()
    {
        var config = new AppConfig
        {
            WarningMinutes = [10, 5, 1],
            Rules = new TimeRules
            {
                Weekdays = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] },
                Weekends = new DayRule { DailyLimitMinutes = 120, AllowedTimeWindows = [] }
            }
        };

        // 10 分钟预警已发送
        var warnAt = _sut.CheckWarning(config, usedMinutesToday: 110.5, extraMinutesToday: 0,
            alreadySentMinutes: [10]);

        warnAt.Should().BeNull();
    }
}
