using ChildPCGuard.AgentB;
using ChildPCGuard.Shared.Agent;
using ChildPCGuard.Shared.Protection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ChildPCGuard.AgentB;

public static class Program
{
    public static void Main(string[] args)
    {
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                @"C:\ProgramData\ChildPCGuard\logs\AgentB-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var agent = new AgentBWorker();
            agent.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "AgentB 发生未处理异常");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
