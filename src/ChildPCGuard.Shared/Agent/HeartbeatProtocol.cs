using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChildPCGuard.Shared.Agent;

/// <summary>
/// 双进程互保心跳协议（共享内存或 Named Pipe）
/// </summary>
public sealed class HeartbeatProtocol
{
    private const string SharedMemoryName = @"Global\ChildPCGuard_Heartbeat";
    private const int HeartbeatIntervalMs = 10000;  // 10 秒心跳
    private const int HeartbeatTimeoutMs = 30000;   // 30 秒超时（3 次心跳）

    private readonly string _agentId;
    private readonly ILogger _logger;
    private readonly System.IO.MemoryMappedFiles.MemoryMappedFile? _mmf;
    private readonly System.IO.MemoryMappedFiles.MemoryMappedViewAccessor? _accessor;

    /// <summary>
    /// 心跳数据结构（64 字节，固定大小）
    /// </summary>
    private struct HeartbeatData
    {
        public long LastHeartbeatTimestamp;  // 8 字节
        public int AgentId;                  // 4 字节（1=AgentA, 2=AgentB）
        public int Reserved1;                // 4 字节
        public int Reserved2;                // 4 字节
        public int Reserved3;                // 4 字节
        public long Reserved4;               // 8 字节
        public long Reserved5;               // 8 字节
        public long Reserved6;               // 8 字节
        public long Reserved7;               // 8 字节
        public long Reserved8;               // 8 字节
        public long Reserved9;               // 8 字节
    }

    private HeartbeatProtocol(string agentId, ILogger logger)
    {
        _agentId = agentId;
        _logger = logger;
        _mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen(
            SharedMemoryName,
            64,
            System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite);
        _accessor = _mmf.CreateViewAccessor(0, 64, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite);
    }

    /// <summary>
    /// 创建 AgentA 实例
    /// </summary>
    public static HeartbeatProtocol CreateAgentA(ILogger logger) => new("AgentA", logger);

    /// <summary>
    /// 创建 AgentB 实例
    /// </summary>
    public static HeartbeatProtocol CreateAgentB(ILogger logger) => new("AgentB", logger);

    /// <summary>
    /// 发送心跳（更新共享内存中的时间戳）
    /// </summary>
    public void SendHeartbeat()
    {
        if (_accessor == null) return;

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var agentIdValue = _agentId == "AgentA" ? 1 : 2;

            _accessor.Write(0, timestamp);
            _accessor.Write(8, agentIdValue);

            _logger.LogDebug("{AgentId} 心跳已发送", _agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{AgentId} 发送心跳失败", _agentId);
        }
    }

    /// <summary>
    /// 检查对方进程是否存活（通过心跳超时判断）
    /// </summary>
    /// <returns>true=对方存活, false=对方已超时</returns>
    public bool IsPartnerAlive()
    {
        if (_accessor == null) return true;  // 共享内存异常时保守处理

        try
        {
            var partnerId = _agentId == "AgentA" ? 2 : 1;
            var timestamp = _accessor.ReadInt64(0);
            var agentIdValue = _accessor.ReadInt32(8);

            // 验证心跳来自对方进程
            if (agentIdValue != partnerId)
            {
                _logger.LogDebug("{AgentId} 未检测到对方进程心跳", _agentId);
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var elapsed = now - timestamp;

            var isAlive = elapsed < HeartbeatTimeoutMs;

            if (!isAlive)
            {
                _logger.LogWarning(
                    "{AgentId} 检测到对方进程心跳超时（上次心跳: {ElapsedMs}ms 前）",
                    _agentId, elapsed);
            }

            return isAlive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{AgentId} 检查对方心跳失败", _agentId);
            return false;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _logger.LogDebug("{AgentId} 心跳协议已释放", _agentId);
    }

    /// <summary>
    /// 获取心跳间隔（用于 Timer）
    /// </summary>
    public static TimeSpan HeartbeatInterval => TimeSpan.FromMilliseconds(HeartbeatIntervalMs);
}

/// <summary>
/// 进程重启命令
/// </summary>
public sealed class ProcessRestartCommand
{
    public required string ProcessName { get; init; }  // 进程名（如 WinSecHelperA.exe）
    public required string ExecutablePath { get; init; } // 完整路径
}
