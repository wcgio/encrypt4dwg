using System.Reflection;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class WindowsTaskSchedulerService
{
    private const string TaskName = "ecrypt4DwgCheck";
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskTriggerDaily = 2;
    private const int TaskActionExecute = 0;

    public bool IsInstalled()
    {
        try
        {
            dynamic service = Connect();
            dynamic root = service.GetFolder("\\");
            _ = root.GetTask(TaskName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void InstallOrUpdate()
    {
        dynamic service = Connect();
        dynamic root = service.GetFolder("\\");
        dynamic definition = service.NewTask(0);
        definition.RegistrationInfo.Description = "检查 DWG 定时加密任务。";
        definition.Settings.Enabled = true;
        definition.Settings.StartWhenAvailable = true;
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Principal.LogonType = TaskLogonInteractiveToken;

        dynamic trigger = definition.Triggers.Create(TaskTriggerDaily);
        trigger.StartBoundary = DateTime.Now.AddMinutes(1).ToString("s");
        trigger.DaysInterval = 1;
        trigger.Repetition.Interval = "PT1M";
        trigger.Repetition.Duration = "P1D";

        var (executable, arguments) = GetScheduledCommand();
        dynamic action = definition.Actions.Create(TaskActionExecute);
        action.Path = executable;
        action.Arguments = arguments;
        action.WorkingDirectory = AppContext.BaseDirectory;

        root.RegisterTaskDefinition(TaskName, definition, TaskCreateOrUpdate, null, null, TaskLogonInteractiveToken, null);
    }

    private static dynamic Connect()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new PlatformNotSupportedException("未找到 Windows Task Scheduler 服务。");
        dynamic service = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法连接 Windows Task Scheduler 服务。");
        service.Connect();
        return service;
    }

    private static (string Executable, string Arguments) GetScheduledCommand()
    {
        var entryPath = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("无法确定应用程序路径。");
        if (entryPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return (Environment.ProcessPath ?? "dotnet", $"\"{entryPath}\" --check");
        }
        return (entryPath, "--check");
    }
}
