using System.Text.Json.Serialization;

namespace ChildPCGuard.Shared.Config;

/// <summary>
/// 应用配置根模型，对应 config.bin 解密后的 JSON 结构
/// </summary>
public class AppConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>BCrypt 哈希后的管理员密码</summary>
    [JsonPropertyName("adminPasswordHash")]
    public string AdminPasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("rules")]
    public TimeRules Rules { get; set; } = new();

    /// <summary>每日自动关机时间，格式 "HH:mm"，默认 22:00</summary>
    [JsonPropertyName("autoShutdownTime")]
    public string AutoShutdownTime { get; set; } = "22:00";

    /// <summary>提前预警分钟数列表，默认 10/5/1 分钟</summary>
    [JsonPropertyName("warningMinutes")]
    public List<int> WarningMinutes { get; set; } = [10, 5, 1];

    /// <summary>空闲判定阈值（毫秒），超过此值视为空闲，默认 5 秒</summary>
    [JsonPropertyName("idleThresholdMs")]
    public int IdleThresholdMs { get; set; } = 5000;

    /// <summary>连续使用时长上限（分钟），0 表示禁用</summary>
    [JsonPropertyName("continuousLimitMinutes")]
    public int ContinuousLimitMinutes { get; set; } = 0;

    /// <summary>强制休息时长（分钟），仅 continuousLimitMinutes > 0 时有效</summary>
    [JsonPropertyName("restDurationMinutes")]
    public int RestDurationMinutes { get; set; } = 0;

    [JsonPropertyName("blockedApps")]
    public List<string> BlockedApps { get; set; } = [];

    [JsonPropertyName("blockedSites")]
    public List<string> BlockedSites { get; set; } = [];

    [JsonPropertyName("useNtpValidation")]
    public bool UseNtpValidation { get; set; } = true;

    [JsonPropertyName("ntpServers")]
    public List<string> NtpServers { get; set; } =
    [
        "cn.pool.ntp.org",
        "pool.ntp.org",
        "time.windows.com",
        "time.google.com"
    ];

    /// <summary>NTP 时间偏差容忍阈值（分钟），超过则视为篡改</summary>
    [JsonPropertyName("ntpToleranceMinutes")]
    public int NtpToleranceMinutes { get; set; } = 5;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = "WinSecSvc";

    [JsonPropertyName("serviceDisplayName")]
    public string ServiceDisplayName { get; set; } = "Windows Security Update Service";

    [JsonPropertyName("lockScreenMessage")]
    public string LockScreenMessage { get; set; } = "今天的使用时间已到，好好休息～";

    [JsonPropertyName("emergencyUnlockShortcut")]
    public string EmergencyUnlockShortcut { get; set; } = "Ctrl+Alt+Shift+F12";
}

/// <summary>工作日和周末的时间规则</summary>
public class TimeRules
{
    [JsonPropertyName("weekdays")]
    public DayRule Weekdays { get; set; } = new()
    {
        DailyLimitMinutes = 120,
        AllowedTimeWindows = [new TimeWindow { Start = "15:00", End = "20:00" }]
    };

    [JsonPropertyName("weekends")]
    public DayRule Weekends { get; set; } = new()
    {
        DailyLimitMinutes = 240,
        AllowedTimeWindows = [new TimeWindow { Start = "09:00", End = "21:00" }]
    };
}

/// <summary>某类日（工作日/周末）的时间规则</summary>
public class DayRule
{
    /// <summary>每日时长上限（分钟）</summary>
    [JsonPropertyName("dailyLimitMinutes")]
    public int DailyLimitMinutes { get; set; } = 120;

    /// <summary>允许使用的时段列表，为空表示不限时段</summary>
    [JsonPropertyName("allowedTimeWindows")]
    public List<TimeWindow> AllowedTimeWindows { get; set; } = [];
}

/// <summary>允许使用的时间窗口，格式 "HH:mm"</summary>
public class TimeWindow
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = "00:00";

    [JsonPropertyName("end")]
    public string End { get; set; } = "23:59";

    /// <summary>判断指定时刻是否在此时间窗口内</summary>
    public bool Contains(TimeOnly time)
    {
        var start = TimeOnly.Parse(Start);
        var end = TimeOnly.Parse(End);
        return start <= end
            ? time >= start && time <= end
            : time >= start || time <= end; // 跨午夜时间段
    }
}
