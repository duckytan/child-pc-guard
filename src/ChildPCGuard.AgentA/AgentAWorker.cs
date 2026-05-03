using ChildPCGuard.Shared.Agent;
using ChildPCGuard.Shared.Protection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ChildPCGuard.AgentA;

/// <summary>
/// AgentA 主工作器（监控 AgentB 存活）
/// </summary>
public sealed class AgentAWorker
{
    private readonly ILogger _logger;
    private readonly ProcessManager _processManager;
    private HeartbeatProtocol? _heartbeat;
    private Timer? _heartbeatTimer;
    private Timer? _checkTimer;
    private Process? _agentBProcess;
    private string _agentBPath = string.Empty;

    // 进程伪装名称
    private const string AgentAName = "WinSecHelperA.exe";
    private const string AgentBName = "WinSecHelperB.exe";

    public AgentAWorker()
    {
        _logger = Serilog.Log.ForContext<AgentAWorker>();
        _processManager = new ProcessManager(_logger);
    }

    public void Run()
    {
        _logger.LogInformation("=== AgentA 启动 ===");
        _logger.LogInformation("伪装进程名: {Name}", AgentAName);

        // 1. 应用进程 DACL 保护（防任务管理器 Kill）
        bool protectedResult = ProcessSecurity.ProtectCurrentProcess();
        _logger.LogInformation("进程 DACL 保护: {Result}", protectedResult ? "成功" : "失败");

        // 2. 初始化心跳协议
        _heartbeat = HeartbeatProtocol.CreateAgentA(_logger);

        // 3. 确定 AgentB 路径
        var baseDir = AppContext.BaseDirectory;
        _agentBPath = Path.Combine(baseDir, AgentBName);
        _logger.LogInformation("AgentB 路径: {Path}", _agentBPath);

        // 4. 启动心跳定时器（每 10 秒发送心跳）
        _heartbeatTimer = new Timer(
            _ => SendHeartbeat(),
            null,
            TimeSpan.Zero,
            HeartbeatProtocol.HeartbeatInterval);

        // 5. 启动检查定时器（每 10 秒检查 AgentB 存活）
        _checkTimer = new Timer(
            _ => CheckPartner(),
            null,
            TimeSpan.Zero,
            HeartbeatProtocol.HeartbeatInterval);

        _logger.LogInformation("AgentA 已启动，心跳间隔: {Interval}ms",
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
            _logger.LogWarning("AgentB 心跳超时，尝试重启...");
            RestartAgentB();
        }
        else
        {
            _logger.LogDebug("AgentB 存活");
        }
    }

    private void RestartAgentB()
    {
        // 1. 先尝试终止旧进程（如果存在）
        _processManager.TerminateProcess(AgentBName);
        Thread.Sleep(500);

        // 2. 启动新进程
        _agentBProcess = _processManager.StartProcess(_agentBPath);

        if (_agentBProcess != null)
        {
            _logger.LogInformation("AgentB 已重启，PID: {Pid}", _agentBProcess.Id);
        }
        else
        {
            _logger.LogError("AgentB 重启失败，将在下次检查时重试");
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
