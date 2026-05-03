using ChildPCGuard.Shared.State;
using FluentAssertions;
using Xunit;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// DailyState 单元测试
/// 对应需求：FR-008（重启恢复）、跨天自动重置
/// </summary>
public class DailyStateTests
{
    [Fact]
    public void IsToday_WhenDateIsToday_ReturnsTrue()
    {
        var state = new DailyState { Date = DateTime.Today.ToString("yyyy-MM-dd") };
        state.IsToday().Should().BeTrue();
    }

    [Fact]
    public void IsToday_WhenDateIsYesterday_ReturnsFalse()
    {
        var state = new DailyState { Date = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd") };
        state.IsToday().Should().BeFalse();
    }

    [Fact]
    public void ResetForNewDay_ClearsAllDailyFields()
    {
        var state = new DailyState
        {
            Date = "2026-01-01",
            UsedMinutesToday = 100,
            IsLocked = true,
            LockReason = "DailyLimitReached",
            ExtraMinutesToday = 30,
            ContinuousMinutes = 45,
            WarningSentMinutes = [10, 5],
            ShutdownWarningSent = true
        };

        state.ResetForNewDay();

        state.Date.Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
        state.UsedMinutesToday.Should().Be(0);
        state.IsLocked.Should().BeFalse();
        state.LockReason.Should().BeNull();
        state.ExtraMinutesToday.Should().Be(0);
        state.ContinuousMinutes.Should().Be(0);
        state.WarningSentMinutes.Should().BeEmpty();
        state.ShutdownWarningSent.Should().BeFalse();
    }

    [Fact]
    public void EffectiveDailyLimit_ReturnsBasePlusExtra()
    {
        var state = new DailyState { ExtraMinutesToday = 30 };
        state.EffectiveDailyLimit(120).Should().Be(150);
    }

    [Fact]
    public void EffectiveDailyLimit_WithNoExtra_ReturnsBase()
    {
        var state = new DailyState { ExtraMinutesToday = 0 };
        state.EffectiveDailyLimit(120).Should().Be(120);
    }
}
