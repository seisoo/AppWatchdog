namespace AppWatchdog.Service.Backups;

/// <summary>
/// Stores backup artifacts on the local file system.
/// </summary>
public sealed class LocalStorage : IBackupStorage
{
    private readonly string _dir;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStorage"/> class.
    /// </summary>
    /// <param name="directory">Target directory path.</param>
    public LocalStorage(string directory)
    {
        _dir = string.IsNullOrWhiteSpace(directory) ? BackupPaths.Root : directory;
        Directory.CreateDirectory(_dir);
    }

    /// <summary>
    /// Uploads a file to the local storage directory.
    /// </summary>
    /// <param name="localFile">Local file path.</param>
    /// <param name="remoteName">Destination file name.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task UploadAsync(string localFile, string remoteName, IProgress<int>? progress, CancellationToken ct)
    {
        var dst = Path.Combine(_dir, remoteName);
        File.Copy(localFile, dst, overwrite: true);
        progress?.Report(100);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Downloads a file from the local storage directory.
    /// </summary>
    /// <param name="remoteName">Source file name.</param>
    /// <param name="localFile">Destination file path.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DownloadAsync(string remoteName, string localFile, IProgress<int>? progress, CancellationToken ct)
    {
        var src = Path.Combine(_dir, remoteName);
        File.Copy(src, localFile, overwrite: true);
        progress?.Report(100);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lists artifacts in the local storage directory.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of artifact names.</returns>
    public Task<List<string>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_dir))
            return Task.FromResult(new List<string>());

        var list = Directory.GetFiles(_dir)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()!;
        return Task.FromResult(list);
    }

    /// <summary>
    /// Deletes an artifact from local storage.
    /// </summary>
    /// <param name="remoteName">Artifact file name.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DeleteAsync(string remoteName, CancellationToken ct)
    {
        var p = Path.Combine(_dir, remoteName);
        if (File.Exists(p))
            File.Delete(p);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the storage implementation.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
