using ChildPCGuard.Shared.Blocklist;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ChildPCGuard.Tests.Unit;

public class AppBlocklistTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private readonly AppBlocklist _blocklist;

    public AppBlocklistTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger(output);
        _blocklist = new AppBlocklist(_logger);
    }

    public void Dispose()
    {
        _blocklist.Clear();
    }

    private class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output) => _output = output;

        public IDisposable? BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }

    [Fact]
    public void AddProcess_ShouldAddToList()
    {
        // Act
        _blocklist.AddProcess("notepad.exe");

        // Assert
        Assert.Contains("notepad.exe", _blocklist.BlockedProcessNames);
    }

    [Fact]
    public void AddProcess_WithoutExeExtension_ShouldAutoAdd()
    {
        // Act
        _blocklist.AddProcess("notepad");

        // Assert
        Assert.Contains("notepad.exe", _blocklist.BlockedProcessNames);
    }

    [Fact]
    public void RemoveProcess_ShouldRemoveFromList()
    {
        // Arrange
        _blocklist.AddProcess("notepad.exe");

        // Act
        _blocklist.RemoveProcess("notepad.exe");

        // Assert
        Assert.DoesNotContain("notepad.exe", _blocklist.BlockedProcessNames);
    }

    [Fact]
    public void IsBlocked_ShouldReturnTrue_ForBlockedProcess()
    {
        // Arrange
        _blocklist.AddProcess("notepad.exe");

        // Act
        var isBlocked = _blocklist.IsBlocked("notepad.exe");

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void IsBlocked_ShouldReturnFalse_ForUnblockedProcess()
    {
        // Act
        var isBlocked = _blocklist.IsBlocked("notepad.exe");

        // Assert
        Assert.False(isBlocked);
    }

    [Fact]
    public void Clear_ShouldRemoveAllProcesses()
    {
        // Arrange
        _blocklist.AddProcess("notepad.exe");
        _blocklist.AddProcess("calc.exe");

        // Act
        _blocklist.Clear();

        // Assert
        Assert.Empty(_blocklist.BlockedProcessNames);
    }

    [Fact]
    public void AddProcesses_ShouldAddMultiple()
    {
        // Act
        _blocklist.AddProcesses(new[] { "notepad.exe", "calc.exe" });

        // Assert
        Assert.Contains("notepad.exe", _blocklist.BlockedProcessNames);
        Assert.Contains("calc.exe", _blocklist.BlockedProcessNames);
    }
}
