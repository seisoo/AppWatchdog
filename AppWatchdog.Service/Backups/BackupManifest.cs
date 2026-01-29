namespace AppWatchdog.Service.Backups;

/// <summary>
/// Describes a backup artifact and its contents.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>
    /// Gets or sets the backup plan identifier.
    /// </summary>
    public string PlanId { get; set; } = "";

    /// <summary>
    /// Gets or sets the backup plan name.
    /// </summary>
    public string PlanName { get; set; } = "";

    /// <summary>
    /// Gets or sets the creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the source type label.
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// Gets or sets the source label.
    /// </summary>
    public string SourceLabel { get; set; } = "";

    /// <summary>
    /// Gets or sets the backup mode label.
    /// </summary>
    public string Mode { get; set; } = "";

    /// <summary>
    /// Gets or sets the list of archived entries.
    /// </summary>
    public List<ManifestEntry> Entries { get; set; } = new();

    /// <summary>
    /// Gets or sets the SQL backup file name when applicable.
    /// </summary>
    public string? SqlBakFileName { get; set; }
}

/// <summary>
/// Describes a single entry in a backup archive.
/// </summary>
public sealed class ManifestEntry
{
    /// <summary>
    /// Gets or sets the relative path in the archive.
    /// </summary>
    public string RelativePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the entry size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the last write timestamp in UTC.
    /// </summary>
    public DateTimeOffset LastWriteUtc { get; set; }
}
