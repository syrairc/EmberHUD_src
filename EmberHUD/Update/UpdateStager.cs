using System.IO.Compression;
using System.Security.Cryptography;
using EmberHUD.Updater;

namespace EmberHUD.Host.Update;

internal sealed class UpdateStager(HttpClient http, string updatesDir)
{
    private const long MaxZipBytes = 200L * 1024 * 1024;
    private const long MaxManifestBytes = 16L * 1024 * 1024;
    private const long MaxExtractedBytes = 1024L * 1024 * 1024;

    public async Task<StagedUpdate?> Stage(ReleaseInfo release, Action<string> report, CancellationToken ct)
    {
        var version = release.Version.ToString();
        var versionDirectory = Path.Combine(updatesDir, version);
        var partFile = Path.Combine(updatesDir, version + ".zip.part");
        try
        {
            DeleteStale();
            Directory.CreateDirectory(updatesDir);

            var manifestBytes = await Download(release.ManifestUrl, MaxManifestBytes, ct);
            if (!Matches(manifestBytes, release.ManifestSha256)) return Fail("manifest digest does not match", versionDirectory, partFile, report);

            await DownloadToFile(release.ZipUrl, partFile, MaxZipBytes, ct);
            if (!Matches(partFile, release.ZipSha256)) return Fail("zip digest does not match", versionDirectory, partFile, report);

            var filesDirectory = Path.Combine(versionDirectory, "files");
            Directory.CreateDirectory(filesDirectory);
            var manifestPath = Path.Combine(versionDirectory, "manifest.json");
            File.WriteAllBytes(manifestPath, manifestBytes);

            if (Extract(partFile, filesDirectory, manifestBytes) is { } problem) return Fail(problem, versionDirectory, partFile, report);
            if (Verify(manifestPath, filesDirectory, version, report) is { } mismatch) return Fail(mismatch, versionDirectory, partFile, report);

            File.Delete(partFile);
            var staged = new StagedUpdate { Version = version, FilesDirectory = filesDirectory, ManifestPath = manifestPath };
            staged.Save(updatesDir);
            return staged;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Cleanup(versionDirectory, partFile);
            throw;
        }
        catch (Exception ex)
        {
            return Fail($"staging failed - {ex.Message}", versionDirectory, partFile, report);
        }
    }

    private void DeleteStale()
    {
        if (!Directory.Exists(updatesDir)) return;
        foreach (var directory in Directory.GetDirectories(updatesDir))
        {
            if (System.Version.TryParse(Path.GetFileName(directory), out _)) Directory.Delete(directory, true);
        }
        foreach (var part in Directory.GetFiles(updatesDir, "*.part")) File.Delete(part);
    }

    private async Task<byte[]> Download(string url, long maxBytes, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var buffer = new MemoryStream();
        await CopyCapped(response, buffer, maxBytes, ct);
        return buffer.ToArray();
    }

    private async Task DownloadToFile(string url, string path, long maxBytes, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var file = File.Create(path);
        await CopyCapped(response, file, maxBytes, ct);
    }

    private static async Task CopyCapped(HttpResponseMessage response, Stream destination, long maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidDataException("download is larger than the allowed size");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new InvalidDataException("download is larger than the allowed size");
            await destination.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    private static bool Matches(byte[] data, string sha256) =>
        string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), sha256, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string file, string sha256) =>
        string.Equals(Manifest.Hash(file), sha256, StringComparison.OrdinalIgnoreCase);

    private static string? Extract(string zipPath, string filesDirectory, byte[] manifestBytes)
    {
        var root = Path.GetFullPath(filesDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Sum(entry => entry.Length) > MaxExtractedBytes) return "zip is larger than the allowed size unpacked";
        var sawManifest = false;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;
            if (!Manifest.SafeRelative(entry.FullName)) return $"zip entry '{entry.FullName}' has an unsafe path";
            if (entry.FullName == "manifest.json")
            {
                if (entry.Length != manifestBytes.Length) return "zip manifest.json differs from the release manifest";
                using var stream = entry.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                if (!copy.ToArray().AsSpan().SequenceEqual(manifestBytes)) return "zip manifest.json differs from the release manifest";
                sawManifest = true;
                continue;
            }
            var target = Path.GetFullPath(Path.Combine(filesDirectory, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return $"zip entry '{entry.FullName}' escapes the staging folder";
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
        return sawManifest ? null : "zip has no manifest.json";
    }

    private static string? Verify(string manifestPath, string filesDirectory, string version, Action<string> report)
    {
        if (Manifest.Load(manifestPath, report) is not { } manifest) return "manifest could not be read";
        if (manifest.Version != version) return $"manifest version '{manifest.Version}' is not the release version '{version}'";
        foreach (var (relative, hash) in manifest.Files)
        {
            if (!Manifest.SafeRelative(relative)) return $"manifest path '{relative}' is unsafe";
            var file = Path.Combine(filesDirectory, relative);
            if (!File.Exists(file)) return $"manifest file '{relative}' is missing from the zip";
            if (!string.Equals(Manifest.Hash(file), hash, StringComparison.OrdinalIgnoreCase)) return $"manifest file '{relative}' does not match its hash";
        }
        return null;
    }

    private static StagedUpdate? Fail(string reason, string versionDirectory, string partFile, Action<string> report)
    {
        report(reason);
        Cleanup(versionDirectory, partFile);
        return null;
    }

    private static void Cleanup(string versionDirectory, string partFile)
    {
        try
        {
            if (Directory.Exists(versionDirectory)) Directory.Delete(versionDirectory, true);
            if (File.Exists(partFile)) File.Delete(partFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
