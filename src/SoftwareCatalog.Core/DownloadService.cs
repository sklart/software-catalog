using System.Security.Cryptography;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;
public sealed class DownloadService(HttpClient client, IAppLogger? logger = null)
{
    public async Task<DownloadResult> DownloadAsync(DownloadCandidate candidate, string stagingDirectory, string destinationDirectory, IProgress<DownloadProgress>? progress, TimeSpan timeout, CancellationToken token)
    {
        if (candidate.Uri is null || candidate.Uri.Scheme != Uri.UriSchemeHttps) return new(DownloadStatus.Error, null, null, "Разрешены только HTTPS URL.");
        Directory.CreateDirectory(stagingDirectory); CleanupPartials(stagingDirectory);
        var name = SanitizeFileName(candidate.FileName ?? Path.GetFileName(candidate.Uri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(Path.GetExtension(name))) return new(DownloadStatus.Error, null, null, "У файла отсутствует допустимое расширение.");
        var part = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.part");
        var started = DateTimeOffset.UtcNow;
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            using var response = await client.GetAsync(candidate.Uri, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) return new(DownloadStatus.Error, null, null, "Redirect на не-HTTPS URL запрещён.");
            if (!response.IsSuccessStatusCode) return new(DownloadStatus.Error, null, null, $"HTTP {(int)response.StatusCode}");
            var total = response.Content.Headers.ContentLength; long received = 0;
            await using var input = await response.Content.ReadAsStreamAsync(linked.Token); await using var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
            var buffer = new byte[131072]; int count;
            while ((count = await input.ReadAsync(buffer, linked.Token)) > 0) { await output.WriteAsync(buffer.AsMemory(0, count), linked.Token); received += count; var seconds = Math.Max(.001, (DateTimeOffset.UtcNow - started).TotalSeconds); progress?.Report(new(received, total, total is > 0 ? received * 100d / total : null, received / seconds, DownloadStatus.Downloading, name)); }
            await output.FlushAsync(linked.Token); if (total is not null && total != received) return new(DownloadStatus.Error, null, null, "Размер скачанного файла не совпадает с Content-Length.");
            progress?.Report(new(received, total, 100, null, DownloadStatus.Verifying, name));
            string hash; await using (var fs = File.OpenRead(part)) hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, linked.Token));
            if (!string.IsNullOrWhiteSpace(candidate.Sha256) && !hash.Equals(candidate.Sha256.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) { logger?.Error("download", "SHA-256 mismatch"); return new(DownloadStatus.Error, null, hash, "SHA-256 не совпадает."); }
            var staged = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(name)}"); File.Move(part, staged); logger?.Information("download", $"Staged {name}"); return new(DownloadStatus.Completed, staged, hash);
        }
        catch (OperationCanceledException) { return new(token.IsCancellationRequested ? DownloadStatus.Cancelled : DownloadStatus.Error, null, null, token.IsCancellationRequested ? "Отменено." : "Превышено время скачивания."); }
        catch (Exception ex) { logger?.Error("download", ex.Message); return new(DownloadStatus.Error, null, null, ex.Message); }
        finally { if (File.Exists(part)) File.Delete(part); }
    }
    public static async Task<DownloadResult> FinalizeAsync(string stagedPath, string destinationDirectory, string fileName, string sha256, CancellationToken token)
    { Directory.CreateDirectory(destinationDirectory); var final = Path.Combine(destinationDirectory, SanitizeFileName(fileName)); if (File.Exists(final)) { var old = await HashFileAsync(final, token); return old.Equals(sha256, StringComparison.OrdinalIgnoreCase) ? new(DownloadStatus.AlreadyExists, final, sha256) : new(DownloadStatus.Error, null, sha256, "Конфликт имени файла в папке назначения."); } File.Move(stagedPath, final); return new(DownloadStatus.Completed, final, sha256); }
    public static void CleanupPartials(string stagingDirectory) { if (!Directory.Exists(stagingDirectory)) return; foreach (var file in Directory.EnumerateFiles(stagingDirectory, "*.part", SearchOption.TopDirectoryOnly)) try { File.Delete(file); } catch { } }
    public static string SanitizeFileName(string? value) { var name = Path.GetFileName(value ?? string.Empty).Trim().TrimEnd('.', ' '); foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); if (name.Contains("..") || string.IsNullOrWhiteSpace(name) || IsReserved(name)) return "download.bin"; return name; }
    private static bool IsReserved(string name) => new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" }.Contains(Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase);
    private static async Task<string> HashFileAsync(string path, CancellationToken token) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)); }
}
