using ChildPCGuard.Shared.Agent;
using ChildPCGuard.Shared.Protection;
using Serilog;
using System.Diagnostics;

namespace ChildPCGuard.AgentB;

/// <summary>
/// AgentB 主工作器（监控 AgentA 存活）
/// </summary>
public sealed class AgentBWorker
{
    private readonly ILogger _logger;
    private readonly ProcessManager _processManager;
    private HeartbeatProtocol? _heartbeat;
    private Timer? _heartbeatTimer;
    private Timer? _checkTimer;
    private Process? _agentAProcess;
    private string _agentAPath = string.Empty;

    // 进程伪装名称
    private const string AgentAName = "WinSecHelperA.exe";
    private const string AgentBName = "WinSecHelperB.exe";

    public AgentBWorker()
    {
        _logger = Serilog.Log.ForContext<AgentBWorker>();
        _processManager = new ProcessManager(_logger);
    }

    public void Run()
    {
        _logger.LogInformation("=== AgentB 启动 ===");
        _logger.LogInformation("伪装进程名: {Name}", AgentBName);

        // 1. 应用进程 DACL 保护（防任务管理器 Kill）
        bool protectedResult = ProcessSecurity.ProtectCurrentProcess();
        _logger.LogInformation("进程 DACL 保护: {Result}", protectedResult ? "成功" : "失败");

        // 2. 初始化心跳协议
        _heartbeat = HeartbeatProtocol.CreateAgentB(_logger);

        // 3. 确定 AgentA 路径
        var baseDir = AppContext.BaseDirectory;
        _agentAPath = Path.Combine(baseDir, AgentAName);
        _logger.Information("AgentA 路径: {Path}", _agentAPath);

        // 4. 启动心跳定时器（每 10 秒发送心跳）
        _heartbeatTimer = new Timer(
            _ => SendHeartbeat(),
            null,
            TimeSpan.Zero,
            HeartbeatProtocol.HeartbeatInterval);

        // 5. 启动检查定时器（每 10 秒检查 AgentA 存活）
        _checkTimer = new Timer(
            _ => CheckPartner(),
            null,
            TimeSpan.Zero,
            HeartbeatProtocol.HeartbeatInterval);

        _logger.Information("AgentB 已启动，心跳间隔: {Interval}ms",
            HeartbeatProtocol.HeartbeatInterval.TotalMilliseconds);

        // 6. 主循环（保持进程运行）
        RunMainLoop();
    }

    private void SendHeartbeat()
    {
        _heartbeat?.SendHeartbeat();
    }

    private void CheckPartner()
    {
        // 1. 检查心跳超时
        bool isAlive = _heartbeat?.IsPartnerAlive() ?? true;

        if (!isAlive)
        {
            _logger.LogWarning("AgentA 心跳超时，尝试重启...");
            RestartAgentA();
        }
        else
        {
            _logger.LogDebug("AgentA 存活");
        }
    }

    private void RestartAgentA()
    {
        // 1. 先尝试终止旧进程（如果存在）
        _processManager.TerminateProcess(AgentAName);
        Thread.Sleep(500);

        // 2. 启动新进程
        _agentAProcess = _processManager.StartProcess(_agentAPath);

        if (_agentAProcess != null)
        {
            _logger.LogInformation("AgentA 已重启，PID: {Pid}", _agentAProcess.Id);
        }
        else
        {
            _logger.LogError("AgentA 重启失败，将在下次检查时重试");
        }
    }

    private void RunMainLoop()
    {
        while (true)
        {
            Thread.Sleep(1000);
        }
    }
}
