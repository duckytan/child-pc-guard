using Xunit;
using ChildPCGuard.Shared.Agent;
using Serilog;
using Serilog.Parsing;
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

    private class TestLogger : Serilog.ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly Serilog.Parsing.MessageTemplateParser _parser = new();

        public TestLogger(ITestOutputHelper output) => _output = output;

        public void Write(Serilog.Events.LogEvent logEvent)
        {
            _output.WriteLine($"[{logEvent.Level}] {logEvent.RenderMessage()}");
        }

        public bool IsEnabled(Serilog.Events.LogEventLevel level) => true;

        public Serilog.ILogger ForContext(Serilog.Events.LogEventLevel level) => this;

        public Serilog.ILogger ForContext(string propertyName, object? value, bool destructureObjects = false) => this;

        public Serilog.ILogger ForContext<TSource>() => this;

        public Serilog.ILogger ForContext(System.Type sourceContext) => this;

        private Serilog.Events.LogEvent CreateLogEvent(Serilog.Events.LogEventLevel level, Exception? exception, string messageTemplate)
        {
            var parsedTemplate = _parser.Parse(messageTemplate);
            return new Serilog.Events.LogEvent(
                DateTimeOffset.UtcNow,
                level,
                exception,
                parsedTemplate,
                Enumerable.Empty<Serilog.Events.LogEventProperty>());
        }

        public void Debug(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Debug, null, messageTemplate));

        public void Debug<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Debug, null, messageTemplate));

        public void Debug(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Debug, null, messageTemplate));

        public void Debug(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Debug, exception, messageTemplate));

        public void Debug(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Debug, exception, messageTemplate));

        public void Information(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Information, null, messageTemplate));

        public void Information<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Information, null, messageTemplate));

        public void Information(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Information, null, messageTemplate));

        public void Information(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Information, exception, messageTemplate));

        public void Information(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Information, exception, messageTemplate));

        public void Warning(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Warning, null, messageTemplate));

        public void Warning<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Warning, null, messageTemplate));

        public void Warning(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Warning, null, messageTemplate));

        public void Warning(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Warning, exception, messageTemplate));

        public void Warning(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Warning, exception, messageTemplate));

        public void Error(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Error, null, messageTemplate));

        public void Error<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Error, null, messageTemplate));

        public void Error(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Error, null, messageTemplate));

        public void Error(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Error, exception, messageTemplate));

        public void Error(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Error, exception, messageTemplate));

        public void Fatal(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Fatal, null, messageTemplate));

        public void Fatal<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Fatal, null, messageTemplate));

        public void Fatal(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Fatal, null, messageTemplate));

        public void Fatal(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Fatal, exception, messageTemplate));

        public void Fatal(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Fatal, exception, messageTemplate));

        public void Verbose(string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Verbose, null, messageTemplate));

        public void Verbose<T>(string messageTemplate, T propertyValue) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Verbose, null, messageTemplate));

        public void Verbose(string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Verbose, null, messageTemplate));

        public void Verbose(Exception? exception, string messageTemplate) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Verbose, exception, messageTemplate));

        public void Verbose(Exception? exception, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(Serilog.Events.LogEventLevel.Verbose, exception, messageTemplate));

        public void Write(Serilog.Events.LogEventLevel level, string messageTemplate) => Write(CreateLogEvent(level, null, messageTemplate));

        public void Write<T>(Serilog.Events.LogEventLevel level, string messageTemplate, T propertyValue) => Write(CreateLogEvent(level, null, messageTemplate));

        public void Write(Serilog.Events.LogEventLevel level, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(level, null, messageTemplate));

        public void Write(Exception? exception, Serilog.Events.LogEventLevel level, string messageTemplate) => Write(CreateLogEvent(level, exception, messageTemplate));

        public void Write(Exception? exception, Serilog.Events.LogEventLevel level, string messageTemplate, params object?[]? propertyValues) => Write(CreateLogEvent(level, exception, messageTemplate));
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
