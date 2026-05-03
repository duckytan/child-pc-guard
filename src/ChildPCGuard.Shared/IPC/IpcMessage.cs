using System.Text.Json.Serialization;

namespace ChildPCGuard.Shared.IPC;

/// <summary>
/// IPC 消息类型枚举（AdminPanel ↔ GuardService）
/// </summary>
public enum IpcCommand
{
    GetStatus,
    StatusResponse,
    UpdateConfig,
    AddTime,
    Unlock,
    LockNow,
    Pause,
    ShutdownNow,
    Ack,
    Error
}

/// <summary>
/// IPC 消息封装，通过 Named Pipe 以 JSON 传输
/// </summary>
public class IpcMessage
{
    [JsonPropertyName("command")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IpcCommand Command { get; set; }

    /// <summary>JSON 序列化的 payload 字符串</summary>
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static IpcMessage Create(IpcCommand command, object? payload = null) => new()
    {
        Command = command,
        Payload = payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload),
        Timestamp = DateTime.UtcNow
    };

    public T? GetPayload<T>() =>
        Payload is null ? default : System.Text.Json.JsonSerializer.Deserialize<T>(Payload);
}

/// <summary>GET_STATUS 响应 payload</summary>
public class StatusPayload
{
    [JsonPropertyName("usedMinutesToday")]
    public double UsedMinutesToday { get; set; }

    [JsonPropertyName("remainingMinutes")]
    public double RemainingMinutes { get; set; }

    [JsonPropertyName("dailyLimitMinutes")]
    public double DailyLimitMinutes { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("pausedUntil")]
    public DateTime? PausedUntil { get; set; }

    [JsonPropertyName("serviceUptime")]
    public TimeSpan ServiceUptime { get; set; }

    [JsonPropertyName("lockReason")]
    public string? LockReason { get; set; }
}

/// <summary>ADD_TIME 请求 payload</summary>
public class AddTimePayload
{
    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }
}

/// <summary>PAUSE 请求 payload</summary>
public class PausePayload
{
    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; set; }
}
