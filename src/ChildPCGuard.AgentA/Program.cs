using ChildPCGuard.Shared.Agent;
using ChildPCGuard.Shared.Protection;
using Serilog;

namespace ChildPCGuard.AgentA;

public static class Program
{
    public static void Main(string[] args)
    {
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                @"C:\ProgramData\ChildPCGuard\logs\AgentA-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var agent = new AgentAWorker();
            agent.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "AgentA 发生未处理异常");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
