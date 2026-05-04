using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows;
using ChildPCGuard.Shared.IPC;

namespace ChildPCGuard.AdminPanel.Pages;

public partial class StatusPage : Page
{
    private readonly PipeClient _pipeClient;

    public StatusPage()
    {
        InitializeComponent();
        _pipeClient = new PipeClient();
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
            var response = await _pipeClient.SendCommandAsync(new IpcMessage
            {
                Command = "GET_STATUS"
            });

            if (response?.Success == true && response?.Data != null)
            {
                var data = response.Data;
                UsedTimeText.Text = $"{data.GetValue("usedMinutes")} 分钟";
                RemainingTimeText.Text = $"{data.GetValue("remainingMinutes")} 分钟";
                ServiceStatusText.Text = data.GetValue("serviceStatus") ?? "未知";
                LastUnlockTimeText.Text = data.GetValue("lastUnlockTime") ?? "无";
            }
            else
            {
                System.Windows.MessageBox.Show($"获取状态失败：{response?.Message}", "错误");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"刷新状态异常：{ex.Message}", "错误");
        }
    }
}
