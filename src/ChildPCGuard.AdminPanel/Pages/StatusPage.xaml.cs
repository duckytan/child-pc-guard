using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows;
using ChildPCGuard.Shared.IPC;

namespace ChildPCGuard.AdminPanel.Pages;

public partial class StatusPage : Page
{
    public StatusPage()
    {
        InitializeComponent();
        Loaded += StatusPage_Loaded;
    }

    private async void StatusPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var response = await PipeClient.SendAsync(
                IpcMessage.Create(IpcCommand.GetStatus));

            if (response?.Command == IpcCommand.StatusResponse)
            {
                var payload = response.GetPayload<IpcPayloads.StatusPayload>();
                if (payload != null)
                {
                    UsedTimeText.Text = $"{(int)payload.UsedMinutesToday} 分钟";
                    RemainingTimeText.Text = $"{(int)payload.RemainingMinutes} 分钟";
                    ServiceStatusText.Text = payload.IsLocked ? "已锁定" : "运行中";
                    LastUnlockTimeText.Text = payload.LockReason ?? "无";
                }
            }
            else
            {
                System.Windows.MessageBox.Show("获取状态失败", "错误");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"刷新状态异常：{ex.Message}", "错误");
        }
    }
}

file static class IpcPayloads
{
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
