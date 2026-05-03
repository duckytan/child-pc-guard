using System.Text.Json.Serialization;

namespace ChildPCGuard.Shared.State;

/// <summary>
/// 今日运行状态，持久化到 state.json，重启后恢复计时
/// </summary>
public class DailyState
{
    /// <summary>记录日期，格式 yyyy-MM-dd，用于判断是否需要重置</summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    /// <summary>今日已累计使用分钟数（不含空闲时间）</summary>
    [JsonPropertyName("usedMinutesToday")]
    public double UsedMinutesToday { get; set; } = 0;

    /// <summary>最后一次活跃时间</summary>
    [JsonPropertyName("lastActiveTime")]
    public DateTime? LastActiveTime { get; set; }

    /// <summary>当前是否处于锁屏状态</summary>
    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; } = false;

    /// <summary>锁屏原因</summary>
    [JsonPropertyName("lockReason")]
    public string? LockReason { get; set; }

    /// <summary>暂停管控到此时间，null 表示未暂停</summary>
    [JsonPropertyName("pausedUntil")]
    public DateTime? PausedUntil { get; set; }

    /// <summary>今日家长追加的额外时间（分钟）</summary>
    [JsonPropertyName("extraMinutesToday")]
    public int ExtraMinutesToday { get; set; } = 0;

    /// <summary>当前连续使用时长（分钟），用于强制休息功能</summary>
    [JsonPropertyName("continuousMinutes")]
    public double ContinuousMinutes { get; set; } = 0;

    /// <summary>最后一次 NTP 校验时间</summary>
    [JsonPropertyName("lastNtpCheckTime")]
    public DateTime? LastNtpCheckTime { get; set; }

    /// <summary>最后一次成功获取的 NTP 时间</summary>
    [JsonPropertyName("lastNtpTime")]
    public DateTime? LastNtpTime { get; set; }

    /// <summary>已发送预警的分钟数集合（避免重复发送）</summary>
    [JsonPropertyName("warningSentMinutes")]
    public List<int> WarningSentMinutes { get; set; } = [];

    /// <summary>是否已发送关机预警</summary>
    [JsonPropertyName("shutdownWarningSent")]
    public bool ShutdownWarningSent { get; set; } = false;

    /// <summary>判断记录的日期是否为今天</summary>
    public bool IsToday() => Date == DateTime.Today.ToString("yyyy-MM-dd");

    /// <summary>重置为新的一天</summary>
    public void ResetForNewDay()
    {
        Date = DateTime.Today.ToString("yyyy-MM-dd");
        UsedMinutesToday = 0;
        IsLocked = false;
        LockReason = null;
        PausedUntil = null;
        ExtraMinutesToday = 0;
        ContinuousMinutes = 0;
        WarningSentMinutes.Clear();
        ShutdownWarningSent = false;
    }

    /// <summary>今日有效时长上限（原始上限 + 家长追加）</summary>
    public double EffectiveDailyLimit(int baseLimitMinutes) =>
        baseLimitMinutes + ExtraMinutesToday;
}
