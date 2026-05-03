using ChildPCGuard.Shared.Agent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Diagnostics;

namespace ChildPCGuard.Tests.Unit;

public class ProcessManagerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private readonly ProcessManager _processManager;
    private readonly List<Process> _processes = new();

    public ProcessManagerTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger(output);
        _processManager = new ProcessManager(_logger);
    }

    public void Dispose()
    {
        foreach (var p in _processes)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(1000);
                }
            }
            catch { }
        }
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
    public void IsProcessRunning_ShouldReturnFalse_WhenProcessDoesNotExist()
    {
        // Act
        var isRunning = _processManager.IsProcessRunning("NonExistentProcess.exe");

        // Assert
        Assert.False(isRunning);
    }

    [Fact]
    public void StartProcess_ShouldFail_WhenFileDoesNotExist()
    {
        // Act
        var process = _processManager.StartProcess("NonExistent.exe");

        // Assert
        Assert.Null(process);
    }

    [Fact]
    public void TerminateProcess_ShouldReturnFalse_WhenProcessDoesNotExist()
    {
        // Act
        var success = _processManager.TerminateProcess("NonExistent.exe");

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void GetProcessId_ShouldReturnNull_WhenProcessDoesNotExist()
    {
        // Act
        var pid = _processManager.GetProcessId("NonExistent.exe");

        // Assert
        Assert.Null(pid);
    }

    [Fact]
    public void StartAndTerminateProcess_ShouldWork()
    {
        // Arrange - 使用 notepad.exe 作为测试进程
        var notepadPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

        if (!File.Exists(notepadPath))
        {
            _output.WriteLine("notepad.exe 不存在，跳过测试");
            return;
        }

        // Act
        var process = _processManager.StartProcess(notepadPath);

        Assert.NotNull(process);
        Assert.NotNull(process?.Id);
        _processes.Add(process!);

        var isRunning = _processManager.IsProcessRunning("notepad.exe");
        Assert.True(isRunning);

        var pid = _processManager.GetProcessId("notepad.exe");
        Assert.NotNull(pid);
        Assert.Equal(process!.Id, pid);

        var terminated = _processManager.TerminateProcess("notepad.exe");
        Assert.True(terminated);

        Thread.Sleep(500);
        isRunning = _processManager.IsProcessRunning("notepad.exe");
        Assert.False(isRunning);
    }
}
