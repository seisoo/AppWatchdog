using System.Security.Cryptography;

namespace AppWatchdog.Service.Backups;

/// <summary>
/// Encrypts and decrypts backup archives with AES.
/// </summary>
public static class AesCrypto
{
    /// <summary>
    /// Encrypts a file using AES with a password-derived key.
    /// </summary>
    /// <param name="inputPath">Input file path.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="password">Encryption password.</param>
    /// <param name="iterations">PBKDF2 iterations.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task EncryptFileAsync(string inputPath, string outputPath, string password, int iterations, IProgress<int>? progress, CancellationToken ct)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var input = File.OpenRead(inputPath);
        using var output = File.Create(outputPath);

        await output.WriteAsync(new byte[] { (byte)'A', (byte)'W', (byte)'D', (byte)'B', 1 }, ct);
        await output.WriteAsync(BitConverter.GetBytes(iterations), ct);
        await output.WriteAsync(BitConverter.GetBytes(salt.Length), ct);
        await output.WriteAsync(salt, ct);
        await output.WriteAsync(BitConverter.GetBytes(iv.Length), ct);
        await output.WriteAsync(iv, ct);

        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);

        long total = input.Length;
        long done = 0;
        var buf = new byte[1024 * 1024];

        int read;
        while ((read = await input.ReadAsync(buf, 0, buf.Length, ct)) > 0)
        {
            await crypto.WriteAsync(buf, 0, read, ct);
            done += read;
            if (total > 0 && progress != null)
                progress.Report((int)Math.Clamp(done * 100 / total, 0, 100));
        }

        crypto.FlushFinalBlock();
    }

    /// <summary>
    /// Decrypts an encrypted backup archive to a file.
    /// </summary>
    /// <param name="inputPath">Encrypted input file path.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="password">Decryption password.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task DecryptToFileAsync(string inputPath, string outputPath, string password, CancellationToken ct)
    {
        using var input = File.OpenRead(inputPath);

        var magic = new byte[5];
        await ReadExactAsync(input, magic, ct);
        if (magic[0] != (byte)'A' || magic[1] != (byte)'W' || magic[2] != (byte)'D' || magic[3] != (byte)'B')
            throw new InvalidOperationException("Invalid encrypted backup header.");

        var itBytes = new byte[4];
        await ReadExactAsync(input, itBytes, ct);
        int iterations = BitConverter.ToInt32(itBytes, 0);

        var lenBytes = new byte[4];
        await ReadExactAsync(input, lenBytes, ct);
        int saltLen = BitConverter.ToInt32(lenBytes, 0);
        var salt = new byte[saltLen];
        await ReadExactAsync(input, salt, ct);

        await ReadExactAsync(input, lenBytes, ct);
        int ivLen = BitConverter.ToInt32(lenBytes, 0);
        var iv = new byte[ivLen];
        await ReadExactAsync(input, iv, ct);

        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var output = File.Create(outputPath);
        using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);

        await crypto.CopyToAsync(output, 1024 * 1024, ct);
    }

    /// <summary>
    /// Reads the exact number of bytes from a stream.
    /// </summary>
    /// <param name="s">Input stream.</param>
    /// <param name="buf">Buffer to fill.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task ReadExactAsync(Stream s, byte[] buf, CancellationToken ct)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int r = await s.ReadAsync(buf, off, buf.Length - off, ct);
            if (r <= 0)
                throw new EndOfStreamException();
            off += r;
        }
    }
}
