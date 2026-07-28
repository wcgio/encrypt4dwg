using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class FileCryptographyService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DWGLOCK1");
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public (string PublicKeyPem, string PrivateKeyPem) CreateKeyPair()
    {
        using var rsa = RSA.Create(3072);
        return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    public string EncryptFile(string sourcePath, string publicKeyPem)
    {
        var outputPath = sourcePath + ".locked";
        if (File.Exists(outputPath))
        {
            throw new IOException($"锁定文件已存在，拒绝覆盖：{outputPath}");
        }

        var plaintext = File.ReadAllBytes(sourcePath);
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(aesKey, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        byte[] encryptedAesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(publicKeyPem);
            encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        }

        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(sourcePath)!,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Magic);
                Span<byte> length = stackalloc byte[4];
                BinaryPrimitives.WriteInt32BigEndian(length, encryptedAesKey.Length);
                stream.Write(length);
                stream.Write(encryptedAesKey);
                stream.Write(nonce);
                stream.Write(ciphertext);
                stream.Write(tag);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            CryptographicOperations.ZeroMemory(aesKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }

        return outputPath;
    }

    public string DecryptFile(string lockedPath, string privateKeyPem, string? outputPath = null)
    {
        var content = File.ReadAllBytes(lockedPath);
        if (content.Length < Magic.Length + 4 + NonceSize + TagSize || !content.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("不是有效的 .locked 文件。");
        }

        var encryptedKeyLength = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(Magic.Length, 4));
        var cursor = Magic.Length + 4;
        if (encryptedKeyLength <= 0 || content.Length < cursor + encryptedKeyLength + NonceSize + TagSize)
        {
            throw new InvalidDataException(".locked 文件头损坏。");
        }

        var encryptedKey = content.AsSpan(cursor, encryptedKeyLength).ToArray();
        cursor += encryptedKeyLength;
        var nonce = content.AsSpan(cursor, NonceSize).ToArray();
        cursor += NonceSize;
        var ciphertextLength = content.Length - cursor - TagSize;
        var ciphertext = content.AsSpan(cursor, ciphertextLength).ToArray();
        var tag = content.AsSpan(cursor + ciphertextLength, TagSize).ToArray();

        byte[] aesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(privateKeyPem);
            aesKey = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
        }

        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(aesKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }

        outputPath ??= lockedPath.EndsWith(".locked", StringComparison.OrdinalIgnoreCase)
            ? lockedPath[..^".locked".Length]
            : lockedPath + ".decrypted";
        if (File.Exists(outputPath))
        {
            throw new IOException($"输出文件已存在，拒绝覆盖：{outputPath}");
        }

        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(plaintext);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            CryptographicOperations.ZeroMemory(plaintext);
        }

        return outputPath;
    }
}
