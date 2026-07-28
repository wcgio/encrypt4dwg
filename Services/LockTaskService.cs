using DwgTimedEncryptor.Windows.Models;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class LockTaskService(TaskRegistryService registry, NetworkTimeService timeService, FileCryptographyService cryptography)
{
    public IReadOnlyList<LockTask> List() => registry.Load().Tasks.OrderBy(task => task.DueAt).ToList();

    public async Task<LockTask> CreateAsync(string targetPath, DateTime dueAt, string privateKeyPath)
    {
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("请选择一个有效的文件。", targetPath);
        }

        var (now, _) = await timeService.GetCurrentTimeAsync();
        if (dueAt <= now)
        {
            throw new ArgumentException("锁定时间必须晚于当前时间。");
        }

        targetPath = Path.GetFullPath(targetPath);
        privateKeyPath = Path.GetFullPath(privateKeyPath);
        var taskRegistry = registry.Load();
        if (taskRegistry.Tasks.Any(task => !task.IsLocked && string.Equals(task.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("该文件已有等待执行的定时加密任务。请先取消原任务。");
        }
        if (File.Exists(privateKeyPath))
        {
            throw new IOException($"私钥文件已存在，拒绝覆盖：{privateKeyPath}");
        }

        var (publicKey, privateKey) = cryptography.CreateKeyPair();
        var task = new LockTask
        {
            Id = Guid.NewGuid().ToString("N"),
            TargetPath = targetPath,
            DueAt = dueAt,
            CreatedAt = now,
            PublicKeyPem = publicKey,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(privateKeyPath)!);
        try
        {
            await File.WriteAllTextAsync(privateKeyPath, privateKey);
            taskRegistry.Tasks.Add(task);
            registry.Save(taskRegistry);
        }
        catch
        {
            if (File.Exists(privateKeyPath))
            {
                File.Delete(privateKeyPath);
            }
            throw;
        }

        return task;
    }

    public void Cancel(string taskId)
    {
        var taskRegistry = registry.Load();
        var task = taskRegistry.Tasks.SingleOrDefault(item => item.Id == taskId)
            ?? throw new InvalidOperationException("未找到任务，可能已被其他进程修改。");
        if (task.IsLocked)
        {
            throw new InvalidOperationException("文件已经锁定，不能取消；请使用对应私钥解锁。");
        }

        taskRegistry.Tasks.Remove(task);
        registry.Save(taskRegistry);
    }
}
