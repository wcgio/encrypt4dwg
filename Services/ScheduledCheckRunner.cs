using System.Text;
using DwgTimedEncryptor.Windows.Models;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class ScheduledCheckRunner(
    TaskRegistryService registry,
    NetworkTimeService timeService,
    FileCryptographyService cryptography)
{
    public async Task RunAsync()
    {
        var (now, timeSource) = await timeService.GetCurrentTimeAsync();
        var log = new StringBuilder($"{now:O} [{timeSource}]");
        TaskRegistry taskRegistry;

        try
        {
            taskRegistry = registry.Load();
        }
        catch (Exception exception)
        {
            AppendLog($"{log} 无法读取注册表：{exception.Message}");
            return;
        }

        var changed = false;
        foreach (var task in taskRegistry.Tasks.Where(item => !item.IsLocked && item.DueAt <= now))
        {
            if (!File.Exists(task.TargetPath))
            {
                AppendLog($"{log} 未找到目标文件：{task.TargetPath}");
                continue;
            }

            string? lockedPath = null;
            try
            {
                lockedPath = cryptography.EncryptFile(task.TargetPath, task.PublicKeyPem);
                File.Delete(task.TargetPath);
                task.IsLocked = true;
                task.LockedAt = now;
                task.LockedFilePath = lockedPath;
                changed = true;
                AppendLog($"{log} 已锁定：{task.TargetPath} -> {lockedPath}");
            }
            catch (Exception exception)
            {
                if (lockedPath is not null && File.Exists(lockedPath))
                {
                    File.Delete(lockedPath);
                }
                AppendLog($"{log} 锁定失败：{task.TargetPath}；{exception.Message}");
            }
        }

        if (changed)
        {
            try
            {
                registry.Save(taskRegistry);
            }
            catch (Exception exception)
            {
                AppendLog($"{log} 无法保存注册表：{exception.Message}");
            }
        }
    }

    private void AppendLog(string message) => File.AppendAllText(registry.LogPath, $"{message}{Environment.NewLine}");
}
