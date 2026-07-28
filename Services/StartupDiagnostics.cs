namespace DwgTimedEncryptor.Windows.Services;

public static class StartupDiagnostics
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ecrypt4Dwg");

    public static string LogPath => Path.Combine(DirectoryPath, "startup-error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 诊断日志不应掩盖原始启动错误。
        }
    }
}
