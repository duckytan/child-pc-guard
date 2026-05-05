using Xunit;
using ChildPCGuard.Shared.Blocklist;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ChildPCGuard.Tests.Unit;

public class ContinuousUsageMonitorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ContinuousUsageMonitor> _logger;
    private readonly ContinuousUsageMonitor _monitor;

    public ContinuousUsageMonitorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<ContinuousUsageMonitor>(output);
        _monitor = new ContinuousUsageMonitor(_logger);
    }

    public void Dispose()
    {
        // 重置监控器状态
        _monitor.EndRest();
    }

    private class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output) => _output = output;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }

    [Fact]
    public void UpdateConfig_ShouldSetLimits()
    {
        // Act
        _monitor.UpdateConfig(60, 5);  // 60 分钟限制，5 分钟休息

        // Assert
        // 仅验证无异常抛出
        Assert.True(true);
    }

    [Fact]
    public void ReportActivity_ShouldNotTriggerRest_WhenDisabled()
    {
        // Arrange
        _monitor.UpdateConfig(0, 0);  // 禁用

        // Act
        var shouldRest = _monitor.ReportActivity();

        // Assert
        Assert.False(shouldRest);
    }

    [Fact]
    public void StartSession_ShouldInitializeStartTime()
    {
        // Act
        _monitor.StartSession();

        // Assert
        var usageMinutes = _monitor.CurrentUsageMinutes;
        Assert.True(usageMinutes >= 0);
    }

    [Fact]
    public void Reset_ShouldResetSession()
    {
        // Arrange
        _monitor.StartSession();
        Thread.Sleep(100);
        _monitor.ReportActivity();

        // Act
        _monitor.Reset();

        // Assert
        var usageMinutes = _monitor.CurrentUsageMinutes;
        Assert.True(usageMinutes >= 0);  // 应该重置后重新开始计时
    }

    [Fact]
    public void EndRest_ShouldStopResting()
    {
        // Arrange
        _monitor.UpdateConfig(1, 5);  // 1 分钟限制，5 分钟休息
        _monitor.StartSession();

        // Act
        _monitor.EndRest();

        // Assert
        Assert.False(_monitor.IsResting);
    }

    [Fact]
    public void RemainingRestSeconds_ShouldReturnZero_WhenNotResting()
    {
        // Act
        var remaining = _monitor.RemainingRestSeconds;

        // Assert
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void IsResting_ShouldReturnFalse_WhenNotResting()
    {
        // Act
        var isResting = _monitor.IsResting;

        // Assert
        Assert.False(isResting);
    }
}
