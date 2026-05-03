using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.Shared.Protection;

/// <summary>
/// NTP 时间校验器：通过 UDP 123 端口查询网络时间，防止孩子修改系统时间绕过计时
/// </summary>
public class NtpTimeValidator
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private const int NtpPort = 123;
    private const int NtpPacketSize = 48;
    private const int UdpTimeoutMs = 3000;

    private readonly IReadOnlyList<string> _ntpServers;
    private readonly ILogger<NtpTimeValidator>? _logger;

    private DateTime? _cachedNtpTime;
    private DateTime _cacheTimestamp = DateTime.MinValue;

    public NtpTimeValidator(IReadOnlyList<string> ntpServers, ILogger<NtpTimeValidator>? logger = null)
    {
        _ntpServers = ntpServers;
        _logger = logger;
    }

    /// <summary>
    /// 校验当前系统时间是否被篡改
    /// </summary>
    /// <param name="toleranceMinutes">允许偏差（分钟）</param>
    /// <returns>true = 时间正常；false = 检测到篡改；null = NTP 不可达（不触发锁屏）</returns>
    public async Task<bool?> ValidateAsync(int toleranceMinutes = 5)
    {
        var ntpTime = await GetNtpTimeAsync();
        if (ntpTime is null)
        {
            _logger?.LogWarning("NTP 校验失败：所有服务器不可达，跳过校验");
            return null; // 网络不可达时不触发锁屏，避免误判
        }

        var drift = Math.Abs((DateTime.UtcNow - ntpTime.Value).TotalMinutes);
        if (drift > toleranceMinutes)
        {
            _logger?.LogWarning("⚠️ 检测到时间篡改！系统时间与 NTP 偏差 {Drift:F1} 分钟（阈值 {Threshold} 分钟）",
                drift, toleranceMinutes);
            return false;
        }

        return true;
    }

    /// <summary>获取 NTP 时间（带缓存，5 分钟内不重复请求）</summary>
    public async Task<DateTime?> GetNtpTimeAsync()
    {
        // 使用缓存
        if (_cachedNtpTime.HasValue && DateTime.UtcNow - _cacheTimestamp < CacheExpiry)
            return _cachedNtpTime;

        foreach (var server in _ntpServers)
        {
            var time = await QueryNtpServerAsync(server);
            if (time.HasValue)
            {
                _cachedNtpTime = time;
                _cacheTimestamp = DateTime.UtcNow;
                return time;
            }
        }

        return null;
    }

    private async Task<DateTime?> QueryNtpServerAsync(string server)
    {
        try
        {
            var ntpData = new byte[NtpPacketSize];
            ntpData[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = UdpTimeoutMs;

            await udpClient.SendAsync(ntpData, NtpPacketSize, server, NtpPort);

            var result = await udpClient.ReceiveAsync();
            if (result.Buffer.Length < NtpPacketSize) return null;

            // 提取 Transmit Timestamp（字节 40-47）
            ulong intPart = ((ulong)result.Buffer[40] << 24) |
                            ((ulong)result.Buffer[41] << 16) |
                            ((ulong)result.Buffer[42] << 8) |
                            result.Buffer[43];
            ulong fracPart = ((ulong)result.Buffer[44] << 24) |
                             ((ulong)result.Buffer[45] << 16) |
                             ((ulong)result.Buffer[46] << 8) |
                             result.Buffer[47];

            var milliseconds = intPart * 1000 + fracPart * 1000 / 0x100000000L;
            // NTP epoch 从 1900-01-01 开始
            var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return ntpEpoch.AddMilliseconds(milliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("NTP 服务器 {Server} 请求失败: {Msg}", server, ex.Message);
            return null;
        }
    }
}
