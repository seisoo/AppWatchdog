namespace AppWatchdog.Service.Backups;

/// <summary>
/// Defines a storage backend for backup artifacts.
/// </summary>
public interface IBackupStorage : IAsyncDisposable
{
    /// <summary>
    /// Uploads a local file to the storage backend.
    /// </summary>
    /// <param name="localFile">Local file path.</param>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadAsync(string localFile, string remoteName, IProgress<int>? progress, CancellationToken ct);

    /// <summary>
    /// Downloads a remote object to a local file.
    /// </summary>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="localFile">Local file path.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadAsync(string remoteName, string localFile, IProgress<int>? progress, CancellationToken ct);

    /// <summary>
    /// Lists available artifacts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of artifact names.</returns>
    Task<List<string>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Deletes an artifact by name.
    /// </summary>
    /// <param name="remoteName">Remote object name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string remoteName, CancellationToken ct);
}
