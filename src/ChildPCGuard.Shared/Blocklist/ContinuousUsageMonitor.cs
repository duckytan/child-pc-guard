using Serilog;

namespace ChildPCGuard.Shared.Blocklist;

/// <summary>
/// 连续使用时长监控器（强制休息功能）
/// </summary>
public sealed class ContinuousUsageMonitor
{
    private readonly ILogger _logger;
    private readonly object _lock = new();

    // 配置
    private int _continuousLimitMinutes = 0;      // 连续使用时长限制（分钟），0=禁用
    private int _restDurationMinutes = 0;          // 休息时长（分钟）

    // 状态
    private DateTime _sessionStartTime = DateTime.MinValue;
    private DateTime _restStartTime = DateTime.MinValue;
    private bool _isResting = false;

    // 上次活动时间（用于检测空闲）
    private DateTime _lastActivityTime = DateTime.UtcNow;

    /// <summary>
    /// 当前是否处于强制休息状态
    /// </summary>
    public bool IsResting
    {
        get
        {
            lock (_lock)
            {
                return _isResting;
            }
        }
    }

    /// <summary>
    /// 剩余休息时间（秒），未休息时返回 0
    /// </summary>
    public int RemainingRestSeconds
    {
        get
        {
            lock (_lock)
            {
                if (!_isResting || _restStartTime == DateTime.MinValue)
                {
                    return 0;
                }

                var elapsed = DateTime.UtcNow - _restStartTime;
                var remaining = TimeSpan.FromMinutes(_restDurationMinutes) - elapsed;
                return Math.Max(0, (int)remaining.TotalSeconds);
            }
        }
    }

    /// <summary>
    /// 当前连续使用时长（分钟）
    /// </summary>
    public int CurrentUsageMinutes
    {
        get
        {
            lock (_lock)
            {
                if (_sessionStartTime == DateTime.MinValue)
                {
                    return 0;
                }

                return (int)(DateTime.UtcNow - _sessionStartTime).TotalMinutes;
            }
        }
    }

    public ContinuousUsageMonitor(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="continuousLimitMinutes">连续使用时长限制（分钟），0=禁用</param>
    /// <param name="restDurationMinutes">休息时长（分钟）</param>
    public void UpdateConfig(int continuousLimitMinutes, int restDurationMinutes)
    {
        lock (_lock)
        {
            _continuousLimitMinutes = continuousLimitMinutes;
            _restDurationMinutes = restDurationMinutes;

            _logger.Information(
                "连续使用监控已更新: 限制={Limit}分钟, 休息={Rest}分钟",
                continuousLimitMinutes, restDurationMinutes);
        }
    }

    /// <summary>
    /// 报告用户活动（每秒调用）
    /// </summary>
    /// <returns>是否触发强制休息</returns>
    public bool ReportActivity()
    {
        lock (_lock)
        {
            _lastActivityTime = DateTime.UtcNow;

            // 如果功能未启用，不检查
            if (_continuousLimitMinutes <= 0 || _restDurationMinutes <= 0)
            {
                return false;
            }

            // 检查是否在休息中
            if (_isResting)
            {
                return CheckRestComplete();
            }

            // 检查是否触发强制休息
            return CheckContinuousLimit();
        }
    }

    /// <summary>
    /// 开始新的使用会话
    /// </summary>
    public void StartSession()
    {
        lock (_lock)
        {
            _sessionStartTime = DateTime.UtcNow;
            _isResting = false;
            _restStartTime = DateTime.MinValue;

            _logger.Debug("连续使用会话已开始");
        }
    }

    /// <summary>
    /// 重置监控器（如用户解锁后）
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sessionStartTime = DateTime.UtcNow;
            _isResting = false;
            _restStartTime = DateTime.MinValue;

            _logger.Information("连续使用监控已重置");
        }
    }

    /// <summary>
    /// 手动结束休息（管理员操作）
    /// </summary>
    public void EndRest()
    {
        lock (_lock)
        {
            if (_isResting)
            {
                _isResting = false;
                _sessionStartTime = DateTime.UtcNow;  // 重新开始计时
                _logger.Information("强制休息已手动结束");
            }
        }
    }

    /// <summary>
    /// 检查连续使用时长是否超限
    /// </summary>
    private bool CheckContinuousLimit()
    {
        if (_sessionStartTime == DateTime.MinValue)
        {
            _sessionStartTime = DateTime.UtcNow;
            return false;
        }

        var elapsed = DateTime.UtcNow - _sessionStartTime;
        var elapsedMinutes = (int)elapsed.TotalMinutes;

        if (elapsedMinutes >= _continuousLimitMinutes)
        {
            // 触发强制休息
            _isResting = true;
            _restStartTime = DateTime.UtcNow;

            _logger.Warning(
                "连续使用时长已超限（{Elapsed}分钟），开始强制休息 {RestMinutes}分钟",
                elapsedMinutes, _restDurationMinutes);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查休息是否完成
    /// </summary>
    private bool CheckRestComplete()
    {
        var elapsed = DateTime.UtcNow - _restStartTime;
        var elapsedMinutes = (int)elapsed.TotalMinutes;

        if (elapsedMinutes >= _restDurationMinutes)
        {
            // 休息完成，结束休息状态
            _isResting = false;
            _sessionStartTime = DateTime.UtcNow;  // 重新开始计时
            _restStartTime = DateTime.MinValue;

            _logger.Information("强制休息已完成，重新开始计时");
            return false;
        }

        // 仍在休息中，返回 true（触发锁屏）
        return true;
    }
}
