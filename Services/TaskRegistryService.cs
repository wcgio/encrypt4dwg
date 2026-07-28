using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DwgTimedEncryptor.Windows.Models;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class TaskRegistryService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DwgTimedEncryptor.Registry.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DwgTimedEncryptor");

    private string RegistryPath => Path.Combine(_directory, "registry.dat");

    public TaskRegistry Load()
    {
        if (!File.Exists(RegistryPath))
        {
            return new TaskRegistry();
        }

        var encrypted = File.ReadAllBytes(RegistryPath);
        var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<TaskRegistry>(json, JsonOptions) ?? new TaskRegistry();
    }

    public void Save(TaskRegistry registry)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.SerializeToUtf8Bytes(registry, JsonOptions);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        var temporaryPath = Path.Combine(_directory, $"registry-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, RegistryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public string LogPath
    {
        get
        {
            Directory.CreateDirectory(_directory);
            return Path.Combine(_directory, "check.log");
        }
    }
}
