using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using DwgTimedEncryptor.Windows.Models;
using DwgTimedEncryptor.Windows.Services;
using Microsoft.Win32;

namespace DwgTimedEncryptor.Windows;

public partial class MainWindow : Window
{
    private readonly LockTaskService _taskService;
    private readonly ScheduledCheckRunner _checkRunner;
    private readonly WindowsTaskSchedulerService _scheduler = new();
    private readonly ObservableCollection<TaskRow> _tasks = [];

    public MainWindow(TaskRegistryService registry, ScheduledCheckRunner checkRunner)
    {
        InitializeComponent();
        _taskService = new LockTaskService(registry, new NetworkTimeService(), new FileCryptographyService());
        _checkRunner = checkRunner;
        TasksGrid.ItemsSource = _tasks;
        DueAtTextBox.Text = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd 00:00:00");
        RefreshTasks();
        StatusTextBlock.Text = "到期检查优先使用 HTTPS 服务器时间；网络不可用时回退到本机时间。"
            + " 本工具不阻止到期前复制明文 DWG。";
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DWG 文件 (*.dwg)|*.dwg|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true)
        {
            TargetPathTextBox.Text = dialog.FileName;
        }
    }

    private async void CreateTask_Click(object sender, RoutedEventArgs e)
    {
        var targetPath = TargetPathTextBox.Text.Trim();
        if (!DateTime.TryParseExact(
                DueAtTextBox.Text.Trim(),
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dueAt))
        {
            MessageBox.Show(this, "锁定时间格式不正确，请使用 YYYY-MM-DD HH:MM:SS。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var defaultKeyPath = Path.Combine(
            Path.GetDirectoryName(targetPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(targetPath)}.privatekey.pem");
        var keyDialog = new SaveFileDialog
        {
            Title = "保存唯一的解锁私钥",
            FileName = defaultKeyPath,
            Filter = "PEM 私钥 (*.pem)|*.pem|所有文件 (*.*)|*.*",
            OverwritePrompt = false,
        };
        if (keyDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var task = await _taskService.CreateAsync(targetPath, dueAt, keyDialog.FileName);
            EnsureScheduledCheck();
            RefreshTasks();
            MessageBox.Show(
                this,
                $"任务已设置，将于 {task.DueAt:yyyy-MM-dd HH:mm:ss} 加密。\n\n"
                + $"私钥已写入：\n{keyDialog.FileName}\n\n"
                + "这是解锁的唯一凭证，请立即转移到项目目录以外的安全位置。",
                "请保存私钥",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _checkRunner.RunAsync();
            RefreshTasks();
            MessageBox.Show(this, "检查已完成。", "DWG 定时加密工具", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "检查失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTasks();

    private void CancelTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is not TaskRow task)
        {
            MessageBox.Show(this, "请先选择一个等待中的任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (task.IsLocked)
        {
            MessageBox.Show(this, "已锁定任务不能取消；请使用私钥解锁。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"确定取消以下任务吗？\n\n{task.TargetPath}\n\n不会删除原文件或私钥。", "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _taskService.Cancel(task.Id);
            RefreshTasks();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "取消失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var lockedFileDialog = new OpenFileDialog
        {
            Title = "选择要解锁的文件",
            Filter = "锁定文件 (*.locked)|*.locked|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (lockedFileDialog.ShowDialog(this) != true)
        {
            return;
        }

        var keyDialog = new OpenFileDialog
        {
            Title = "选择解锁私钥",
            Filter = "PEM 私钥 (*.pem)|*.pem|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (keyDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var privateKey = File.ReadAllText(keyDialog.FileName);
            var outputPath = new FileCryptographyService().DecryptFile(lockedFileDialog.FileName, privateKey);
            MessageBox.Show(this, $"已解锁：\n{outputPath}", "解锁成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "解锁失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshTasks()
    {
        _tasks.Clear();
        foreach (var task in _taskService.List())
        {
            _tasks.Add(new TaskRow(task));
        }
    }

    private void EnsureScheduledCheck()
    {
        if (_scheduler.IsInstalled())
        {
            return;
        }
        if (MessageBox.Show(
                this,
                "尚未配置每分钟自动检查。现在配置 Windows 计划任务吗？\n\n电脑关机、休眠或任务被禁用时，会在下次成功检查时加密。",
                "配置自动检查",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _scheduler.InstallOrUpdate();
        }
    }

    private sealed class TaskRow(LockTask task)
    {
        public string Id { get; } = task.Id;
        public string TargetPath { get; } = task.TargetPath;
        public DateTime DueAt { get; } = task.DueAt;
        public bool IsLocked { get; } = task.IsLocked;
        public string Status => task.IsLocked ? "已锁定" : "等待中";
    }
}
