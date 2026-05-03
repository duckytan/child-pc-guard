using Microsoft.Extensions.Logging;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ChildPCGuard.GuardService.Core;

/// <summary>
/// Toast 通知辅助器：发送 Windows 10/11 原生 Toast 通知
/// </summary>
public class NotificationHelper
{
    private const string AppId = "ChildPCGuard.GuardService";
    private readonly ILogger<NotificationHelper> _logger;

    public NotificationHelper(ILogger<NotificationHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>发送剩余时间预警通知</summary>
    public void SendTimeWarning(int remainingMinutes)
    {
        var (title, content) = remainingMinutes switch
        {
            10 => ("使用时间提醒", "还有 10 分钟，记得保存进度哦 😊"),
            5 => ("使用时间提醒", "还有 5 分钟！"),
            1 => ("使用时间提醒", "⚠️ 即将锁屏，最后 1 分钟！"),
            _ => ("使用时间提醒", $"还有 {remainingMinutes} 分钟")
        };

        Send(title, content);
        _logger.LogInformation("已发送 {Minutes} 分钟预警通知", remainingMinutes);
    }

    /// <summary>发送关机预警通知</summary>
    public void SendShutdownWarning(int secondsRemaining)
    {
        Send("自动关机提醒", $"⏰ 电脑将在 {secondsRemaining} 秒后自动关机，请保存好你的工作！");
        _logger.LogInformation("已发送关机预警通知（{Seconds} 秒后）", secondsRemaining);
    }

    /// <summary>发送锁屏通知</summary>
    public void SendLockNotification(string reason)
    {
        Send("屏幕已锁定", reason);
    }

    private void Send(string title, string content)
    {
        try
        {
            var xml = $@"<toast>
                <visual>
                    <binding template='ToastGeneric'>
                        <text>{EscapeXml(title)}</text>
                        <text>{EscapeXml(content)}</text>
                    </binding>
                </visual>
            </toast>";

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var toast = new ToastNotification(xmlDoc);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送 Toast 通知失败（非关键错误）");
        }
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
