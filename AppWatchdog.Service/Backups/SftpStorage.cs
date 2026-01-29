using Renci.SshNet;
using Renci.SshNet.Common;
using System.Security.Cryptography;

namespace AppWatchdog.Service.Backups;

/// <summary>
/// Stores backup artifacts on an SFTP server.
/// </summary>
public sealed class SftpStorage : IBackupStorage
{
    private readonly SftpClient _client;
    private readonly string _remoteDir;
    private readonly string? _fingerprint;

    /// <summary>
    /// Initializes a new instance of the <see cref="SftpStorage"/> class.
    /// </summary>
    /// <param name="host">SFTP host.</param>
    /// <param name="port">SFTP port.</param>
    /// <param name="user">Username.</param>
    /// <param name="password">Password.</param>
    /// <param name="remoteDir">Remote directory path.</param>
    /// <param name="fingerprint">Optional host key fingerprint.</param>
    public SftpStorage(string host, int port, string user, string password, string remoteDir, string? fingerprint)
    {
        _remoteDir = string.IsNullOrWhiteSpace(remoteDir) ? "/" : remoteDir;
        _fingerprint = string.IsNullOrWhiteSpace(fingerprint) ? null : fingerprint.Trim();

        var conn = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, password));
        _client = new SftpClient(conn);
        _client.HostKeyReceived += OnHostKeyReceived;
        _client.Connect();
        EnsureDir(_remoteDir);
        _client.ChangeDirectory(_remoteDir);
    }

    /// <summary>
    /// Validates the host key when provided.
    /// </summary>
    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        if (_fingerprint == null)
        {
            e.CanTrust = true;
            return;
        }

        var fp = ToFingerprint(e.FingerPrint);
        e.CanTrust = string.Equals(fp, _fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a raw host key to a SHA256 fingerprint string.
    /// </summary>
    /// <param name="bytes">Raw host key bytes.</param>
    /// <returns>Fingerprint string.</returns>
    private static string ToFingerprint(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return "SHA256:" + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Ensures the remote directory exists.
    /// </summary>
    /// <param name="path">Remote directory path.</param>
    private void EnsureDir(string path)
    {
        if (path == "/")
            return;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        foreach (var p in parts)
        {
            current = current.EndsWith("/") ? current + p : current + "/" + p;
            if (!_client.Exists(current))
                _client.CreateDirectory(current);
        }
    }

    /// <summary>
    /// Uploads a local file to the SFTP server.
    /// </summary>
    /// <param name="localFile">Local file path.</param>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UploadAsync(string localFile, string remoteName, IProgress<int>? progress, CancellationToken ct)
    {
        using var fs = File.OpenRead(localFile);
        long total = fs.Length;
        long done = 0;

        await Task.Run(() =>
        {
            _client.UploadFile(fs, remoteName, uploaded =>
            {
                done = (long)uploaded;
                if (total > 0 && progress != null)
                    progress.Report((int)Math.Clamp(done * 100 / total, 0, 100));
            });
        }, ct);

        progress?.Report(100);
    }

    /// <summary>
    /// Downloads a remote object from the SFTP server.
    /// </summary>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="localFile">Local file path.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DownloadAsync(string remoteName, string localFile, IProgress<int>? progress, CancellationToken ct)
    {
        using var fs = File.Create(localFile);
        var attrs = _client.GetAttributes(remoteName);
        long total = attrs.Size;
        long done = 0;

        await Task.Run(() =>
        {
            _client.DownloadFile(remoteName, fs, downloaded =>
            {
                done = (long)downloaded;
                if (total > 0 && progress != null)
                    progress.Report((int)Math.Clamp(done * 100 / total, 0, 100));
            });
        }, ct);

        progress?.Report(100);
    }

    /// <summary>
    /// Lists artifacts in the remote directory.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of artifact names.</returns>
    public Task<List<string>> ListAsync(CancellationToken ct)
    {
        var list = _client.ListDirectory(_remoteDir)
            .Where(f => f.IsRegularFile)
            .Select(f => f.Name)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// Deletes an artifact from the SFTP server.
    /// </summary>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DeleteAsync(string remoteName, CancellationToken ct)
    {
        if (_client.Exists(remoteName))
            _client.DeleteFile(remoteName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the SFTP client.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        try { _client.Dispose(); } catch { }
        return ValueTask.CompletedTask;
    }
}
