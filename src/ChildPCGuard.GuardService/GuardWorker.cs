using ChildPCGuard.GuardService.Core;
using ChildPCGuard.Shared.Blocklist;
using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.IPC;
using ChildPCGuard.Shared.Protection;
using ChildPCGuard.Shared.State;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.GuardService;

/// <summary>
/// GuardService 主 Worker：每 5 秒执行一次主循环
/// 负责协调所有子模块：计时、规则判断、锁屏、关机、IPC
/// </summary>
public class GuardWorker : BackgroundService
{
    private const int TickIntervalMs = 5000;
    private const double TickSeconds = 5.0;
    private const int NtpCheckIntervalTicks = 60; // 每 60 tick（5 分钟）校验一次 NTP

    private readonly ILogger<GuardWorker> _logger;
    private readonly ConfigManager _configManager;
    private readonly StateManager _stateManager;
    private readonly PipeServer _pipeServer;

    private AppConfig _config = new();
    private DailyState _state = new();

    private TimeTracker? _timeTracker;
    private RuleEngine? _ruleEngine;
    private ShutdownScheduler? _shutdownScheduler;
    private NotificationHelper? _notificationHelper;
    private NtpTimeValidator? _ntpValidator;
    private AppBlocklist? _appBlocklist;
    private ContinuousUsageMonitor? _continuousMonitor;

    private int _tickCount = 0;
    private bool _ntpTampered = false;
    private bool _isLocked = false;
    private System.Diagnostics.Process? _lockOverlayProcess;

    private readonly DateTime _startTime = DateTime.UtcNow;

    public GuardWorker(
        ILogger<GuardWorker> logger,
        ConfigManager configManager,
        StateManager stateManager,
        PipeServer pipeServer)
    {
        _logger = logger;
        _configManager = configManager;
        _stateManager = stateManager;
        _pipeServer = pipeServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChildPCGuard GuardService 启动");

        // 1. 安全模式检测（最高优先级）
        SafeModeDetector.CheckAndShutdownIfSafeMode(_logger);

        // 2. 加载配置和状态
        _config = _configManager.Load();
        _state = _stateManager.Load();

        // 3. 初始化子模块
        _timeTracker = new TimeTracker(_logger.CreateLogger<TimeTracker>(),
            _state.UsedMinutesToday, _state.ContinuousMinutes, _config.IdleThresholdMs);
        _ruleEngine = new RuleEngine(_logger.CreateLogger<RuleEngine>());
        _shutdownScheduler = new ShutdownScheduler(_logger.CreateLogger<ShutdownScheduler>());
        _notificationHelper = new NotificationHelper(_logger.CreateLogger<NotificationHelper>());
        _ntpValidator = new NtpTimeValidator(_config.NtpServers, _logger.CreateLogger<NtpTimeValidator>());

        // Phase 6: 初始化黑名单和连续使用监控
        _appBlocklist = new AppBlocklist(_logger.CreateLogger<AppBlocklist>());
        _continuousMonitor = new ContinuousUsageMonitor(_logger.CreateLogger<ContinuousUsageMonitor>());

        // 加载黑名单和连续使用配置
        if (_config.BlockedApps?.Count > 0)
        {
            _appBlocklist.AddProcesses(_config.BlockedApps);
            _logger.LogInformation("已加载 {Count} 个黑名单应用", _config.BlockedApps.Count);
        }

        if (_config.ContinuousLimitMinutes > 0 && _config.RestDurationMinutes > 0)
        {
            _continuousMonitor.UpdateConfig(_config.ContinuousLimitMinutes, _config.RestDurationMinutes);
            _continuousMonitor.StartSession();
            _logger.LogInformation("连续使用监控已启用: 限制={Limit}分钟, 休息={Rest}分钟",
                _config.ContinuousLimitMinutes, _config.RestDurationMinutes);
        }

        // 4. 启动 IPC 服务
        _pipeServer.MessageReceived += HandleIpcMessageAsync;
        _pipeServer.Start(stoppingToken);

        _logger.LogInformation("初始化完成，进入主循环（tick 间隔 {Interval}ms）", TickIntervalMs);

        // 5. 主循环
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "主循环 tick 异常");
            }

            await Task.Delay(TickIntervalMs, stoppingToken);
        }

        // 6. 退出前保存状态
        await SaveStateAsync();
        _logger.LogInformation("GuardService 已停止");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;

        // ── 暂停管控期间不计时也不锁屏 ──
        if (_state.PausedUntil.HasValue && DateTime.Now < _state.PausedUntil.Value)
        {
            _logger.LogDebug("管控已暂停，恢复时间: {Until}", _state.PausedUntil.Value);
            return;
        }
        else if (_state.PausedUntil.HasValue && DateTime.Now >= _state.PausedUntil.Value)
        {
            _state.PausedUntil = null;
            _logger.LogInformation("暂停时间已到，恢复管控");
        }

        // ── 1. NTP 校验（每 5 分钟一次）──
        if (_config.UseNtpValidation && _tickCount % NtpCheckIntervalTicks == 1)
        {
            var ntpResult = await _ntpValidator!.ValidateAsync(_config.NtpToleranceMinutes);
            _ntpTampered = ntpResult == false; // null = 不可达，不触发
        }

        // ── 2. 计时 ──
        bool active = _timeTracker!.Tick(TickSeconds);
        _state.UsedMinutesToday = _timeTracker.UsedMinutesToday;
        _state.ContinuousMinutes = _timeTracker.ContinuousMinutes;
        if (active) _state.LastActiveTime = DateTime.Now;

        // ── Phase 6: 黑名单扫描 ──
        if (!_isLocked && _appBlocklist != null && _appBlocklist.BlockedProcessNames.Count > 0)
        {
            int killedCount = _appBlocklist.ScanAndKillBlockedProcesses();
            if (killedCount > 0)
            {
                _logger.LogWarning("已终止 {Count} 个黑名单进程", killedCount);
            }
        }

        // ── Phase 6: 连续使用监控 ──
        if (_continuousMonitor != null && !_isLocked)
        {
            bool shouldRest = _continuousMonitor.ReportActivity();
            if (shouldRest && _continuousMonitor.IsResting)
            {
                // 触发强制休息锁屏
                _state.LockReason = "ContinuousLimitReached";
                await TriggerLockAsync("ContinuousLimitReached");
                return;
            }
        }

        // ── 3. 规则判断 ──
        if (!_isLocked)
        {
            var (shouldLock, reason) = _ruleEngine!.Evaluate(
                _config,
                _state.UsedMinutesToday,
                _state.ExtraMinutesToday,
                _state.ContinuousMinutes,
                _ntpTampered);

            if (shouldLock)
            {
                await TriggerLockAsync(reason.ToString());
            }
            else
            {
                // ── 4. 预警通知 ──
                var warnAt = _ruleEngine.CheckWarning(
                    _config, _state.UsedMinutesToday, _state.ExtraMinutesToday, _state.WarningSentMinutes);
                if (warnAt.HasValue)
                {
                    _notificationHelper!.SendTimeWarning(warnAt.Value);
                    _state.WarningSentMinutes.Add(warnAt.Value);
                }
            }
        }
        else
        {
            // 锁屏中：检查 LockOverlay 进程是否还活着
            EnsureLockOverlayAlive();
        }

        // ── 5. 关机调度 ──
        _shutdownScheduler!.Tick(_config.AutoShutdownTime);

        // ── 6. 持久化状态（每 tick 保存）──
        _state.IsLocked = _isLocked;
        await SaveStateAsync();
    }

    /// <summary>触发锁屏：记录原因，启动 LockOverlay 进程</summary>
    private async Task TriggerLockAsync(string reason)
    {
        if (_isLocked) return;

        _logger.LogInformation("触发锁屏，原因: {Reason}", reason);
        _state.LockReason = reason;
        _isLocked = true;

        _notificationHelper!.SendLockNotification(GetLockMessage(reason));

        await StartLockOverlayAsync();
    }

    private async Task StartLockOverlayAsync()
    {
        try
        {
            var lockOverlayPath = Path.Combine(
                AppContext.BaseDirectory, "..", "LockOverlay", "LockOverlay.exe");

            if (!File.Exists(lockOverlayPath))
            {
                _logger.LogWarning("LockOverlay.exe 未找到，使用系统原生锁屏（Phase 1 临时方案）");
                // Phase 1 临时方案：使用系统原生锁屏
                ChildPCGuard.Shared.Win32.NativeMethods.LockWorkStation();
                return;
            }

            // Phase 3: 传递锁屏原因参数
            var reasonArg = _state.LockReason ?? "ManualLock";
            _lockOverlayProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = lockOverlayPath,
                Arguments = $"--reason {reasonArg}",
                UseShellExecute = false,
                CreateNoWindow = false
            });

            _logger.LogInformation("LockOverlay 已启动（原因: {Reason}），PID: {Pid}",
                reasonArg, _lockOverlayProcess?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动 LockOverlay 失败，回退到系统锁屏");
            ChildPCGuard.Shared.Win32.NativeMethods.LockWorkStation();
        }

        await Task.CompletedTask;
    }

    /// <summary>确保锁屏进程存活，被杀死则立即重启</summary>
    private void EnsureLockOverlayAlive()
    {
        if (_lockOverlayProcess is null) return;

        try
        {
            if (_lockOverlayProcess.HasExited)
            {
                _logger.LogWarning("⚠️ LockOverlay 进程意外退出（exit code={Code}），立即重启",
                    _lockOverlayProcess.ExitCode);
                _ = StartLockOverlayAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查 LockOverlay 进程状态时出错");
        }
    }

    /// <summary>解除锁屏（由 IPC 消息或密码验证通过触发）</summary>
    private void Unlock()
    {
        if (!_isLocked) return;

        _logger.LogInformation("解除锁屏");
        _isLocked = false;
        _state.IsLocked = false;
        _state.LockReason = null;

        try
        {
            if (_lockOverlayProcess is not null && !_lockOverlayProcess.HasExited)
            {
                _lockOverlayProcess.Kill();
                _logger.LogInformation("LockOverlay 进程已终止");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "终止 LockOverlay 进程时出错");
        }

        _lockOverlayProcess = null;
    }

    /// <summary>处理来自 AdminPanel 的 IPC 消息</summary>
    private Task<IpcMessage?> HandleIpcMessageAsync(IpcMessage message)
    {
        _logger.LogInformation("处理 IPC 指令: {Command}", message.Command);

        return message.Command switch
        {
            IpcCommand.GetStatus => Task.FromResult<IpcMessage?>(
                IpcMessage.Create(IpcCommand.StatusResponse, BuildStatusPayload())),

            IpcCommand.Unlock => HandleUnlockCommand(),
            IpcCommand.LockNow => HandleLockNowCommand(),
            IpcCommand.AddTime => HandleAddTimeCommand(message),
            IpcCommand.Pause => HandlePauseCommand(message),
            IpcCommand.UpdateConfig => HandleUpdateConfigCommand(message),
            IpcCommand.ShutdownNow => HandleShutdownNowCommand(),

            _ => Task.FromResult<IpcMessage?>(
                IpcMessage.Create(IpcCommand.Error, new { message = "未知指令" }))
        };
    }

    private Task<IpcMessage?> HandleUnlockCommand()
    {
        Unlock();
        return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Ack));
    }

    private async Task<IpcMessage?> HandleLockNowCommand()
    {
        await TriggerLockAsync(nameof(Core.LockReason.ManualLock));
        return IpcMessage.Create(IpcCommand.Ack);
    }

    private Task<IpcMessage?> HandleAddTimeCommand(IpcMessage message)
    {
        var payload = message.GetPayload<IpcPayloads.AddTimePayload>();
        if (payload is null)
            return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Error, new { message = "无效参数" }));

        _state.ExtraMinutesToday += payload.Minutes;
        _logger.LogInformation("追加 {Minutes} 分钟，今日额外时间共 {Total} 分钟",
            payload.Minutes, _state.ExtraMinutesToday);

        // 追加时间后如果当前是因为时长超限锁屏，立即解锁
        if (_isLocked && _state.LockReason == nameof(Core.LockReason.DailyLimitReached))
        {
            Unlock();
        }

        return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Ack));
    }

    private Task<IpcMessage?> HandlePauseCommand(IpcMessage message)
    {
        var payload = message.GetPayload<IpcPayloads.PausePayload>();
        if (payload is null)
            return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Error, new { message = "无效参数" }));

        _state.PausedUntil = DateTime.Now.AddMinutes(payload.DurationMinutes);
        _logger.LogInformation("管控已暂停 {Minutes} 分钟，恢复时间: {Until}",
            payload.DurationMinutes, _state.PausedUntil);

        if (_isLocked) Unlock();

        return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Ack));
    }

    private Task<IpcMessage?> HandleUpdateConfigCommand(IpcMessage message)
    {
        var newConfig = message.GetPayload<AppConfig>();
        if (newConfig is null)
            return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Error, new { message = "无效配置" }));

        _config = newConfig;
        _configManager.Save(_config);

        // 重建依赖配置的子模块
        _ntpValidator = new NtpTimeValidator(_config.NtpServers, _logger.CreateLogger<NtpTimeValidator>());
        _state.WarningSentMinutes.Clear(); // 清除已发送预警记录（规则变了，重新计算）

        _logger.LogInformation("配置已热更新");
        return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Ack));
    }

    private Task<IpcMessage?> HandleShutdownNowCommand()
    {
        _shutdownScheduler!.ShutdownNow();
        return Task.FromResult<IpcMessage?>(IpcMessage.Create(IpcCommand.Ack));
    }

    private IpcPayloads.StatusPayload BuildStatusPayload()
    {
        var rule = RuleEngine.GetTodayRule(_config);
        var remaining = RuleEngine.GetRemainingMinutes(_config, _state.UsedMinutesToday, _state.ExtraMinutesToday);

        return new IpcPayloads.StatusPayload
        {
            UsedMinutesToday = _state.UsedMinutesToday,
            RemainingMinutes = remaining,
            DailyLimitMinutes = rule.DailyLimitMinutes + _state.ExtraMinutesToday,
            IsLocked = _isLocked,
            IsPaused = _state.PausedUntil.HasValue && DateTime.Now < _state.PausedUntil.Value,
            PausedUntil = _state.PausedUntil,
            ServiceUptime = DateTime.UtcNow - _startTime,
            LockReason = _state.LockReason
        };
    }

    private static string GetLockMessage(string reason) => reason switch
    {
        nameof(Core.LockReason.DailyLimitReached) => "今天的使用时间已到，好好休息～",
        nameof(Core.LockReason.OutsideAllowedWindow) => "现在不在允许的使用时段内",
        nameof(Core.LockReason.TimeTampered) => "检测到系统时间异常",
        nameof(Core.LockReason.ManualLock) => "屏幕已被家长锁定",
        nameof(Core.LockReason.ContinuousLimitReached) => "连续使用时间过长，休息一下吧～",
        _ => "屏幕已锁定"
    };

    private async Task SaveStateAsync()
    {
        if (_stateManager is not null)
            await _stateManager.SaveAsync(_state);
    }
}

/// <summary>本地 payload 类型别名（避免命名空间冲突）</summary>
file static class IpcPayloads
{
    public record AddTimePayload(int Minutes);
    public record PausePayload(int DurationMinutes);

    public class StatusPayload
    {
        public double UsedMinutesToday { get; set; }
        public double RemainingMinutes { get; set; }
        public double DailyLimitMinutes { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPaused { get; set; }
        public DateTime? PausedUntil { get; set; }
        public TimeSpan ServiceUptime { get; set; }
        public string? LockReason { get; set; }
    }
}
