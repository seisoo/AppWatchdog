using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using Microsoft.Data.SqlClient;
using System.IO.Compression;
using System.Text.Json;

namespace AppWatchdog.Service.Backups;

/// <summary>
/// Creates and restores backup artifacts.
/// </summary>
public sealed class BackupEngine
{
    /// <summary>
    /// Creates a backup artifact for the given plan.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="report">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created artifact result.</returns>
    public async Task<BackupCreateResult> CreateBackupAsync(
        BackupPlanConfig plan,
        IBackupStorage storage,
        Action<int?, string, DateTimeOffset> report,
        CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var stamp = nowUtc.ToString("yyyyMMdd_HHmmss");
        var baseName = $"{Sanitize(plan.Id)}_{stamp}";
        var tmpZip = Path.Combine(BackupPaths.Staging, baseName + ".zip");
        var tmpOut = Path.Combine(BackupPaths.Staging, baseName + ".awdb");

        if (File.Exists(tmpZip)) File.Delete(tmpZip);
        if (File.Exists(tmpOut)) File.Delete(tmpOut);

        var manifest = new BackupManifest
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            CreatedUtc = nowUtc,
            SourceType = plan.Source.Type.ToString(),
            SourceLabel = SourceLabel(plan),
            Mode = BackupMode.Full.ToString()
        };

        report(0, "Prepare", nowUtc);

        string? sqlBak = null;
        long sizeBytes = 0;

        try
        {
            if (plan.Source.Type == BackupSourceType.MsSql)
            {
                report(5, "SQL Backup", nowUtc);
                sqlBak = await CreateSqlBackupAsync(plan, baseName, ct);
                manifest.SqlBakFileName = Path.GetFileName(sqlBak);
            }

            report(10, "Zip", nowUtc);
            await CreateZipAsync(plan, tmpZip, manifest, sqlBak, report, ct);

            if (plan.Crypto.Encrypt)
            {
                report(75, "Encrypt", nowUtc);
                var p = new Progress<int>(x =>
                {
                    var scaled = 75 + (int)Math.Clamp(x * 0.20, 0, 20);
                    report(scaled, "Encrypt", nowUtc);
                });

                await AesCrypto.EncryptFileAsync(tmpZip, tmpOut, plan.Crypto.Password ?? "", plan.Crypto.Iterations, p, ct);
            }
            else
            {
                File.Copy(tmpZip, tmpOut, overwrite: true);
            }

            if (File.Exists(tmpOut))
                sizeBytes = new FileInfo(tmpOut).Length;

            report(95, "Upload", nowUtc);
            var up = new Progress<int>(x => report(95 + (int)Math.Clamp(x * 0.05, 0, 5), "Upload", nowUtc));
            await storage.UploadAsync(tmpOut, Path.GetFileName(tmpOut), up, ct);

            await ApplyRetentionAsync(plan, storage, ct);

            if (plan.VerifyAfterCreate)
            {
                report(98, "Verify", nowUtc);
                await VerifyArtifactAsync(plan, storage, Path.GetFileName(tmpOut), ct);
            }

            report(100, "Done", nowUtc);
            return new BackupCreateResult
            {
                ArtifactName = Path.GetFileName(tmpOut),
                SizeBytes = sizeBytes,
                CreatedUtc = nowUtc
            };
        }
        finally
        {
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
            try { if (File.Exists(tmpOut)) File.Delete(tmpOut); } catch { }
            if (sqlBak != null)
            {
                try { if (File.Exists(sqlBak)) File.Delete(sqlBak); } catch { }
            }
        }
    }

    /// <summary>
    /// Restores a single backup artifact.
    /// </summary>
    /// <param name="restore">Restore plan configuration.</param>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="report">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RestoreAsync(
        RestorePlanConfig restore,
        BackupPlanConfig plan,
        IBackupStorage storage,
        Action<int?, string, DateTimeOffset> report,
        CancellationToken ct)
    {
        await RestoreSingleArtifactAsync(
            restore,
            plan,
            storage,
            restore.BackupArtifactName,
            report,
            ct);
    }

    /// <summary>
    /// Restores a full backup chain up to the selected artifact.
    /// </summary>
    /// <param name="restore">Restore plan configuration.</param>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="report">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RestoreIncrementalChainAsync(
        RestorePlanConfig restore,
        BackupPlanConfig plan,
        IBackupStorage storage,
        Action<int?, string, DateTimeOffset> report,
        CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        report(0, "Scan", nowUtc);

        var all = await storage.ListAsync(ct);

        var prefix = Sanitize(plan.Id) + "_";
        var candidates = all
            .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException("No backup artifacts found for this plan.");

        var chain = new List<(string Name, BackupManifest Manifest)>();

        for (int i = 0; i < candidates.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var a = candidates[i];
            var pct = (int)Math.Clamp((double)i / Math.Max(1, candidates.Count) * 15, 0, 15);
            report(pct, "Scan", nowUtc);

            var man = await ReadManifestFromArtifactAsync(plan, storage, a, ct);
            chain.Add((a, man));
        }

        var targetIndex = chain.FindIndex(x =>
            string.Equals(x.Name, restore.BackupArtifactName, StringComparison.OrdinalIgnoreCase));

        if (targetIndex < 0)
            throw new InvalidOperationException("Selected artifact not found in plan artifacts.");

        var fullIndex = -1;
        for (int i = targetIndex; i >= 0; i--)
        {
            if (string.Equals(chain[i].Manifest.Mode, BackupMode.Full.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                fullIndex = i;
                break;
            }
        }

        if (fullIndex < 0)
            throw new InvalidOperationException("No full backup found before selected artifact.");

        var restoreChain = chain
            .Skip(fullIndex)
            .Take(targetIndex - fullIndex + 1)
            .ToList();

        if (restoreChain.Count == 0)
            throw new InvalidOperationException("Restore chain is empty.");

        // For full-only backups, all will be Full mode, so just restore the single target
        var restoreForced = new RestorePlanConfig
        {
            Enabled = restore.Enabled,
            Id = restore.Id,
            Name = restore.Name,
            BackupPlanId = restore.BackupPlanId,
            BackupArtifactName = restore.BackupArtifactName,
            RestoreToDirectory = restore.RestoreToDirectory,
            OverwriteExisting = true,
            IncludePaths = restore.IncludePaths,
            RunOnce = restore.RunOnce,
            Target = restore.Target,
            Crypto = restore.Crypto
        };

        // Single restore since everything is Full
        ct.ThrowIfCancellationRequested();
        report(15, "Restore", DateTimeOffset.UtcNow);

        await RestoreSingleArtifactAsync(
            restoreForced,
            plan,
            storage,
            restore.BackupArtifactName,
            (pct, text, ts) => report((int)Math.Clamp(15 + (pct ?? 0) * 0.85, 0, 100), text, ts),
            ct);

        report(100, "Done", DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Restores a single backup artifact to the target directory.
    /// </summary>
    /// <param name="restore">Restore plan configuration.</param>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="artifactName">Artifact name.</param>
    /// <param name="report">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task RestoreSingleArtifactAsync(
        RestorePlanConfig restore,
        BackupPlanConfig plan,
        IBackupStorage storage,
        string artifactName,
        Action<int?, string, DateTimeOffset> report,
        CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        report(0, "Download", nowUtc);

        var tmpEnc = Path.Combine(BackupPaths.Staging, "restore_" + Guid.NewGuid().ToString("N") + ".awdb");
        var tmpZip = Path.Combine(BackupPaths.Staging, "restore_" + Guid.NewGuid().ToString("N") + ".zip");

        if (File.Exists(tmpEnc)) File.Delete(tmpEnc);
        if (File.Exists(tmpZip)) File.Delete(tmpZip);

        try
        {
            var dl = new Progress<int>(x => report((int)Math.Clamp(x * 0.30, 0, 30), "Download", nowUtc));
            await storage.DownloadAsync(artifactName, tmpEnc, dl, ct);

            report(35, "Decrypt", nowUtc);

            if (restore.Crypto.Encrypt)
            {
                await AesCrypto.DecryptToFileAsync(tmpEnc, tmpZip, restore.Crypto.Password ?? "", ct);
            }
            else
            {
                File.Copy(tmpEnc, tmpZip, overwrite: true);
            }

            report(45, "Extract", nowUtc);

            Directory.CreateDirectory(restore.RestoreToDirectory);

            using var zipFs = File.OpenRead(tmpZip);
            using var zip = new ZipArchive(zipFs, ZipArchiveMode.Read, leaveOpen: false);

            var entries = zip.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.FullName))
                .Where(e => !e.FullName.EndsWith("/"))
                .ToList();

            var selected = SelectEntries(entries, restore.IncludePaths);

            int total = Math.Max(1, selected.Count);
            int done = 0;

            foreach (var e in selected)
            {
                ct.ThrowIfCancellationRequested();

                var dst = Path.Combine(restore.RestoreToDirectory, e.FullName.Replace('/', Path.DirectorySeparatorChar));
                var dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrWhiteSpace(dstDir))
                    Directory.CreateDirectory(dstDir);

                if (File.Exists(dst) && !restore.OverwriteExisting)
                {
                    done++;
                    report(45 + (int)Math.Clamp(done * 55 / total, 0, 55), "Extract", nowUtc);
                    continue;
                }

                using var es = e.Open();
                using var ofs = File.Create(dst);
                await es.CopyToAsync(ofs, 1024 * 256, ct);

                done++;
                report(45 + (int)Math.Clamp(done * 55 / total, 0, 55), "Extract", nowUtc);
            }

            report(100, "Done", nowUtc);
        }
        finally
        {
            try { if (File.Exists(tmpEnc)) File.Delete(tmpEnc); } catch { }
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
        }
    }

    /// <summary>
    /// Filters archive entries using include path rules.
    /// </summary>
    /// <param name="all">All archive entries.</param>
    /// <param name="includePaths">Paths to include.</param>
    /// <returns>Filtered list of entries.</returns>
    private static List<ZipArchiveEntry> SelectEntries(List<ZipArchiveEntry> all, List<string> includePaths)
    {
        if (includePaths == null || includePaths.Count == 0)
            return all;

        var norm = includePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Replace('\\', '/').TrimStart('/'))
            .ToList();

        return all.Where(e =>
        {
            var name = e.FullName.Replace('\\', '/').TrimStart('/');
            return norm.Any(p =>
                string.Equals(name, p, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(p.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        }).ToList();
    }

    /// <summary>
    /// Creates a zip archive from the backup source.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="zipPath">Zip file path.</param>
    /// <param name="manifest">Manifest to populate.</param>
    /// <param name="sqlBakFile">Optional SQL backup file.</param>
    /// <param name="report">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task CreateZipAsync(
        BackupPlanConfig plan,
        string zipPath,
        BackupManifest manifest,
        string? sqlBakFile,
        Action<int?, string, DateTimeOffset> report,
        CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

        if (plan.Source.Type == BackupSourceType.File)
        {
            var file = plan.Source.Path;
            if (!File.Exists(file))
                throw new FileNotFoundException("Source file not found.", file);

            var rel = Path.GetFileName(file);
            await AddFileAsync(zip, file, rel, manifest, ct);
        }
        else if (plan.Source.Type == BackupSourceType.Folder)
        {
            var dir = plan.Source.Path;
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException(dir);

            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToList();
            int total = Math.Max(1, files.Count);
            int added = 0;

            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();

                var rel = Path.GetRelativePath(dir, f);

                await AddFileAsync(zip, f, rel.Replace('\\', '/'), manifest, ct);
                added++;

                var pct = 10 + (int)Math.Clamp((double)added / Math.Max(1, files.Count) * 60, 0, 60);
                report(pct, "Zip", nowUtc);
            }
        }
        else if (plan.Source.Type == BackupSourceType.MsSql)
        {
            if (sqlBakFile == null || !File.Exists(sqlBakFile))
                throw new InvalidOperationException("SQL backup missing.");

            await AddFileAsync(zip, sqlBakFile, Path.GetFileName(sqlBakFile), manifest, ct);
        }

        var manEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var ms = manEntry.Open())
        {
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await using var sw = new StreamWriter(ms);
            await sw.WriteAsync(json.AsMemory(), ct);
        }
    }

    /// <summary>
    /// Adds a file to the zip archive and records it in the manifest.
    /// </summary>
    /// <param name="zip">Zip archive.</param>
    /// <param name="fullPath">Full file path.</param>
    /// <param name="entryName">Entry name in the archive.</param>
    /// <param name="manifest">Manifest to populate.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task AddFileAsync(ZipArchive zip, string fullPath, string entryName, BackupManifest manifest, CancellationToken ct)
    {
        var fi = new FileInfo(fullPath);
        var e = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using (var es = e.Open())
        await using (var fs = File.OpenRead(fullPath))
        {
            await fs.CopyToAsync(es, 1024 * 256, ct);
        }

        manifest.Entries.Add(new ManifestEntry
        {
            RelativePath = entryName.Replace('\\', '/'),
            Size = fi.Length,
            LastWriteUtc = fi.LastWriteTimeUtc
        });
    }

    /// <summary>
    /// Creates a SQL Server backup file.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="baseName">Base file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the generated backup file.</returns>
    private async Task<string> CreateSqlBackupAsync(BackupPlanConfig plan, string baseName, CancellationToken ct)
    {
        var tmp = Path.Combine(BackupPaths.Staging, baseName + ".bak");
        if (File.Exists(tmp)) File.Delete(tmp);

        using var conn = new SqlConnection(plan.Source.SqlConnectionString);
        await conn.OpenAsync(ct);

        var cmdText = $"BACKUP DATABASE [{plan.Source.SqlDatabase}] TO DISK = @p WITH INIT, COMPRESSION";

        using var cmd = new SqlCommand(cmdText, conn);
        cmd.Parameters.AddWithValue("@p", tmp);

        await cmd.ExecuteNonQueryAsync(ct);

        return tmp;
    }

    /// <summary>
    /// Verifies that an uploaded artifact contains a manifest.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="artifactName">Artifact name.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task VerifyArtifactAsync(BackupPlanConfig plan, IBackupStorage storage, string artifactName, CancellationToken ct)
    {
        var tmpEnc = Path.Combine(BackupPaths.Staging, "verify_" + Guid.NewGuid().ToString("N") + ".awdb");
        var tmpZip = Path.Combine(BackupPaths.Staging, "verify_" + Guid.NewGuid().ToString("N") + ".zip");

        if (File.Exists(tmpEnc)) File.Delete(tmpEnc);
        if (File.Exists(tmpZip)) File.Delete(tmpZip);

        try
        {
            await storage.DownloadAsync(artifactName, tmpEnc, null, ct);

            if (plan.Crypto.Encrypt)
                await AesCrypto.DecryptToFileAsync(tmpEnc, tmpZip, plan.Crypto.Password ?? "", ct);
            else
                File.Copy(tmpEnc, tmpZip, overwrite: true);

            using var fs = File.OpenRead(tmpZip);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var man = zip.GetEntry("manifest.json");
            if (man == null)
                throw new InvalidOperationException("Manifest missing.");
        }
        finally
        {
            try { if (File.Exists(tmpEnc)) File.Delete(tmpEnc); } catch { }
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
        }
    }

    /// <summary>
    /// Applies retention rules to stored artifacts.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task ApplyRetentionAsync(BackupPlanConfig plan, IBackupStorage storage, CancellationToken ct)
    {
        var keep = Math.Max(1, plan.Retention.KeepLast);
        var all = await storage.ListAsync(ct);

        var prefix = Sanitize(plan.Id) + "_";
        var candidates = all
            .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count <= keep)
            return;

        foreach (var del in candidates.Skip(keep))
        {
            try
            {
                await storage.DeleteAsync(del, ct);
            }
            catch (Exception ex)
            {
                FileLogStore.WriteLine("WARN", $"Retention delete failed '{del}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reads the manifest from a stored backup artifact.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <param name="storage">Storage backend.</param>
    /// <param name="artifactName">Artifact name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed manifest.</returns>
    private async Task<BackupManifest> ReadManifestFromArtifactAsync(
    BackupPlanConfig plan,
    IBackupStorage storage,
    string artifactName,
    CancellationToken ct)
    {
        var tmpEnc = Path.Combine(
            BackupPaths.Staging,
            "man_" + Guid.NewGuid().ToString("N") + ".awdb");

        var tmpZip = Path.Combine(
            BackupPaths.Staging,
            "man_" + Guid.NewGuid().ToString("N") + ".zip");

        try
        {
            await storage.DownloadAsync(artifactName, tmpEnc, null, ct);

            if (plan.Crypto.Encrypt)
                await AesCrypto.DecryptToFileAsync(tmpEnc, tmpZip, plan.Crypto.Password, ct);
            else
                File.Copy(tmpEnc, tmpZip, true);

            using var fs = File.OpenRead(tmpZip);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, false);
            var entry = zip.GetEntry("manifest.json");
            if (entry == null)
                throw new InvalidOperationException("Manifest missing.");

            using var sr = new StreamReader(entry.Open());
            var json = await sr.ReadToEndAsync();

            var opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var man = JsonSerializer.Deserialize<BackupManifest>(json, opt);
            if (man == null)
                throw new InvalidOperationException("Invalid manifest.");

            return man;
        }
        finally
        {
            FileHelper.TryDelete(tmpEnc);
            FileHelper.TryDelete(tmpZip);
        }
    }

    /// <summary>
    /// Sanitizes an identifier for file name usage.
    /// </summary>
    /// <param name="s">Input string.</param>
    /// <returns>Sanitized identifier.</returns>
    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "backup";

        var bad = Path.GetInvalidFileNameChars();
        var parts = s.Split(bad, StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join("_", parts);
        return string.IsNullOrWhiteSpace(joined) ? "backup" : joined;
    }

    /// <summary>
    /// Builds a human-readable label for the backup source.
    /// </summary>
    /// <param name="plan">Backup plan configuration.</param>
    /// <returns>Source label.</returns>
    private static string SourceLabel(BackupPlanConfig plan)
    {
        return plan.Source.Type switch
        {
            BackupSourceType.MsSql => plan.Source.SqlDatabase,
            _ => plan.Source.Path
        };
    }
}

public sealed class BackupCreateResult
{
    public required string ArtifactName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
}
