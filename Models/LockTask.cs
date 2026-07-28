namespace DwgTimedEncryptor.Windows.Models;

public sealed class LockTask
{
    public required string Id { get; init; }
    public required string TargetPath { get; init; }
    public required DateTime DueAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string PublicKeyPem { get; init; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? LockedFilePath { get; set; }
}
