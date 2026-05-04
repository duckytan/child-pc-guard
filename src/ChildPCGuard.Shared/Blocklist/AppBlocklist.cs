using System.Diagnostics;

namespace ChildPCGuard.Shared.Blocklist;

/// <summary>
/// 应用程序黑名单管理器（进程监控 + 终止）
/// </summary>
public sealed class AppBlocklist
{
    private readonly HashSet<string> _blockedProcessNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public AppBlocklist(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取已屏蔽的进程名列表
    /// </summary>
    public IReadOnlyCollection<string> BlockedProcessNames
    {
        get
        {
            lock (_lock)
            {
                return _blockedProcessNames.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// 添加进程到黑名单
    /// </summary>
    /// <param name="processName">进程名（如 "notepad.exe"，不含路径）</param>
    public void AddProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("进程名不能为空", nameof(processName));
        }

        // 确保扩展名为 .exe
        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = $"{processName}.exe";
        }

        lock (_lock)
        {
            if (_blockedProcessNames.Add(processName))
            {
                _logger.LogInformation("已添加黑名单进程: {ProcessName}", processName);
            }
        }
    }

    /// <summary>
    /// 从黑名单移除进程
    /// </summary>
    public void RemoveProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = $"{processName}.exe";
        }

        lock (_lock)
        {
            if (_blockedProcessNames.Remove(processName))
            {
                _logger.LogInformation("已从黑名单移除进程: {ProcessName}", processName);
            }
        }
    }

    /// <summary>
    /// 清空黑名单
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _blockedProcessNames.Clear();
            _logger.LogInformation("已清空黑名单");
        }
    }

    /// <summary>
    /// 检查进程是否在黑名单中
    /// </summary>
    public bool IsBlocked(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = $"{processName}.exe";
        }

        lock (_lock)
        {
            return _blockedProcessNames.Contains(processName);
        }
    }

    /// <summary>
    /// 扫描并终止黑名单中的进程
    /// </summary>
    /// <returns>终止的进程数量</returns>
    public int ScanAndKillBlockedProcesses()
    {
        lock (_lock)
        {
            if (_blockedProcessNames.Count == 0)
            {
                return 0;
            }
        }

        int killedCount = 0;

        foreach (var blockedName in BlockedProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(blockedName));

                foreach (var p in processes)
                {
                    try
                    {
                        p.Kill(true);
                        killedCount++;
                        _logger.LogWarning("已终止黑名单进程: {Name} (PID: {Pid})", blockedName, p.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "终止进程失败: {Name} (PID: {Pid})", blockedName, p.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描进程 {Name} 失败", blockedName);
            }
        }

        return killedCount;
    }

    /// <summary>
    /// 从列表批量添加进程
    /// </summary>
    public void AddProcesses(IEnumerable<string> processNames)
    {
        foreach (var name in processNames)
        {
            AddProcess(name);
        }
    }
}
