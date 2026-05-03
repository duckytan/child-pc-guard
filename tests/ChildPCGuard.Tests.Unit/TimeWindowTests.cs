using ChildPCGuard.Shared.Config;
using FluentAssertions;
using Xunit;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// TimeWindow 和 AppConfig 模型单元测试
/// </summary>
public class TimeWindowTests
{
    [Theory]
    [InlineData("15:00", "20:00", "17:30", true)]
    [InlineData("15:00", "20:00", "15:00", true)]  // 边界：开始时刻
    [InlineData("15:00", "20:00", "20:00", true)]  // 边界：结束时刻
    [InlineData("15:00", "20:00", "14:59", false)]
    [InlineData("15:00", "20:00", "20:01", false)]
    public void Contains_NormalWindow_ReturnsExpected(string start, string end, string time, bool expected)
    {
        var window = new TimeWindow { Start = start, End = end };
        var t = TimeOnly.Parse(time);

        window.Contains(t).Should().Be(expected);
    }

    [Theory]
    [InlineData("22:00", "06:00", "23:30", true)]  // 跨午夜
    [InlineData("22:00", "06:00", "03:00", true)]  // 跨午夜，深夜
    [InlineData("22:00", "06:00", "12:00", false)] // 跨午夜，正午不在范围
    public void Contains_CrossMidnightWindow_ReturnsExpected(string start, string end, string time, bool expected)
    {
        var window = new TimeWindow { Start = start, End = end };
        var t = TimeOnly.Parse(time);

        window.Contains(t).Should().Be(expected);
    }

    [Fact]
    public void AppConfig_DefaultValues_AreReasonable()
    {
        var config = new AppConfig();

        config.IsEnabled.Should().BeTrue();
        config.IdleThresholdMs.Should().Be(5000);
        config.NtpToleranceMinutes.Should().Be(5);
        config.WarningMinutes.Should().ContainInOrder(10, 5, 1);
        config.UseNtpValidation.Should().BeTrue();
        config.NtpServers.Should().NotBeEmpty();
    }

    [Fact]
    public void DayRule_DefaultWeekdayLimit_Is120Minutes()
    {
        var config = new AppConfig();
        config.Rules.Weekdays.DailyLimitMinutes.Should().Be(120);
    }

    [Fact]
    public void DayRule_DefaultWeekendLimit_Is240Minutes()
    {
        var config = new AppConfig();
        config.Rules.Weekends.DailyLimitMinutes.Should().Be(240);
    }
}
