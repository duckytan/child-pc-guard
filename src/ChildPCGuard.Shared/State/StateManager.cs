using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.Shared.State;

/// <summary>
/// 状态文件管理器：负责 state.json 的读写（明文，依赖文件 ACL 保护）
/// </summary>
public class StateManager
{
    private const string StateFileName = "state.json";
    private const string DataDirectory = @"C:\ProgramData\ChildPCGuard";

    private readonly string _statePath;
    private readonly ILogger<StateManager>? _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public StateManager(ILogger<StateManager>? logger = null)
    {
        _statePath = Path.Combine(DataDirectory, StateFileName);
        _logger = logger;
    }

    /// <summary>
    /// 从 state.json 加载状态，自动处理跨天重置
    /// </summary>
    public DailyState Load()
    {
        if (!File.Exists(_statePath))
        {
            _logger?.LogInformation("状态文件不存在，初始化新状态");
            return new DailyState();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<DailyState>(json) ?? new DailyState();

            if (!state.IsToday())
            {
                _logger?.LogInformation("检测到新的一天 ({OldDate} → {Today})，重置今日状态",
                    state.Date, DateTime.Today.ToString("yyyy-MM-dd"));
                state.ResetForNewDay();
            }
            else
            {
                _logger?.LogInformation("恢复今日状态：已用 {Used:F1} 分钟", state.UsedMinutesToday);
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "读取状态文件失败，初始化新状态");
            return new DailyState();
        }
    }

    /// <summary>
    /// 异步保存状态到 state.json（带写锁，防止并发写入损坏）
    /// </summary>
    public async Task SaveAsync(DailyState state, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(DataDirectory);

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _statePath + ".tmp";

            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存状态文件失败");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>同步保存（在非 async 上下文使用）</summary>
    public void Save(DailyState state)
    {
        _writeLock.Wait();
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _statePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存状态文件失败");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
