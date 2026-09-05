using System.Net;
using System.Security.Cryptography;
using System.Text;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class DownloadServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SoftwareCatalogTests", Guid.NewGuid().ToString("N"));
    public DownloadServiceTests() => Directory.CreateDirectory(_folder);
    [Fact]
    public async Task StreamsHttpsFileReportsProgressAndCalculatesHash()
    {
        var bytes = Encoding.UTF8.GetBytes("safe installer payload"); var progress = new List<DownloadProgress>();
        var service = new DownloadService(Client(HttpStatusCode.OK, bytes));
        var result = await service.DownloadAsync(Candidate("https://example.test/Tool-2.0-x64.exe"), Staging, Destination, new Progress<DownloadProgress>(progress.Add), TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.True(result.Status == DownloadStatus.Completed, result.Error); Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), result.Sha256); Assert.NotNull(result.FinalPath); Assert.True(File.Exists(result.FinalPath)); Assert.Contains(progress, x => x.Status == DownloadStatus.Downloading && x.BytesReceived == bytes.Length);
    }
    [Fact]
    public async Task RejectsHttpAndExpectedHashMismatchWithoutFinalFile()
    {
        var http = await new DownloadService(Client(HttpStatusCode.OK, [1])).DownloadAsync(Candidate("http://example.test/tool.exe"), Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal(DownloadStatus.Error, http.Status); Assert.False(Directory.Exists(Staging) && Directory.EnumerateFiles(Staging).Any());
        var mismatch = await new DownloadService(Client(HttpStatusCode.OK, [1])).DownloadAsync(Candidate("https://example.test/tool.exe") with { Sha256 = "00" }, Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal(DownloadStatus.Error, mismatch.Status); Assert.False(Directory.Exists(Staging) && Directory.EnumerateFiles(Staging).Any());
    }
    [Fact]
    public async Task RejectsRedirectWhoseFinalUrlIsHttp()
    {
        var client = new HttpClient(new RedirectHandler(new Uri("http://unsafe.test/tool.exe"))); var result = await new DownloadService(client).DownloadAsync(Candidate("https://example.test/tool.exe"), Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal(DownloadStatus.Error, result.Status); Assert.Contains("Redirect", result.Error!);
    }
    [Fact]
    public async Task TimesOutWithoutCreatingCompletedFile()
    {
        var result = await new DownloadService(new HttpClient(new DelayedHandler())).DownloadAsync(Candidate("https://example.test/tool.exe"), Staging, Destination, null, TimeSpan.FromMilliseconds(30), CancellationToken.None);
        Assert.Equal(DownloadStatus.Error, result.Status); Assert.Contains("время", result.Error!, StringComparison.OrdinalIgnoreCase); Assert.False(Directory.Exists(Destination) && Directory.EnumerateFiles(Destination).Any());
    }
    [Fact]
    public async Task VerifiesContentLengthAndExpectedHash()
    {
        var bytes = Encoding.UTF8.GetBytes("verified"); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var success = await new DownloadService(Client(HttpStatusCode.OK, bytes)).DownloadAsync(Candidate("https://example.test/tool.exe") with { Sha256 = hash }, Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None); Assert.Equal(DownloadStatus.Completed, success.Status);
        var mismatch = await new DownloadService(Client(HttpStatusCode.OK, bytes, bytes.Length + 1)).DownloadAsync(Candidate("https://example.test/other.exe"), Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None); Assert.Equal(DownloadStatus.Error, mismatch.Status); Assert.False(Directory.EnumerateFiles(Staging, "*.part").Any());
    }
    [Fact]
    public async Task UserCancellationAndHttpsRedirectLeaveNoCompletedFile()
    {
        using var cancellation = new CancellationTokenSource(); var downloading = new DownloadService(new HttpClient(new DelayedHandler())).DownloadAsync(Candidate("https://example.test/tool.exe"), Staging, Destination, null, TimeSpan.FromSeconds(5), cancellation.Token); cancellation.Cancel(); var cancelled = await downloading; Assert.Equal(DownloadStatus.Cancelled, cancelled.Status); Assert.False(Directory.Exists(Destination) && Directory.EnumerateFiles(Destination).Any());
        var redirect = await new DownloadService(new HttpClient(new RedirectHandler(new Uri("https://safe.test/tool.exe")))).DownloadAsync(Candidate("https://example.test/tool.exe"), Staging, Destination, null, TimeSpan.FromSeconds(5), CancellationToken.None); Assert.Equal(DownloadStatus.Completed, redirect.Status);
    }
    [Fact]
    public void CleansOnlyPartialFilesInOwnStagingDirectory()
    {
        Directory.CreateDirectory(Staging); File.WriteAllText(Path.Combine(Staging, "old.part"), "x"); File.WriteAllText(Path.Combine(Staging, "keep.exe"), "x");
        DownloadService.CleanupPartials(Staging);
        Assert.False(File.Exists(Path.Combine(Staging, "old.part"))); Assert.True(File.Exists(Path.Combine(Staging, "keep.exe")));
    }
    [Fact]
    public async Task FinalizeDetectsIdenticalAndConflictingDestinationFiles()
    {
        Directory.CreateDirectory(Staging); Directory.CreateDirectory(Destination); var bytes = Encoding.UTF8.GetBytes("installer"); var hash = Convert.ToHexString(SHA256.HashData(bytes)); var existing = Path.Combine(Destination, "tool.exe"); await File.WriteAllBytesAsync(existing, bytes);
        var sameStaged = Path.Combine(Staging, "same.exe"); await File.WriteAllBytesAsync(sameStaged, bytes); var same = await DownloadService.FinalizeAsync(sameStaged, Destination, "tool.exe", hash, CancellationToken.None); Assert.Equal(DownloadStatus.AlreadyExists, same.Status); Assert.True(File.Exists(sameStaged));
        var differentStaged = Path.Combine(Staging, "different.exe"); await File.WriteAllBytesAsync(differentStaged, [9, 8, 7]); var different = await DownloadService.FinalizeAsync(differentStaged, Destination, "tool.exe", Convert.ToHexString(SHA256.HashData([9, 8, 7])), CancellationToken.None); Assert.Equal(DownloadStatus.Error, different.Status); Assert.Equal(bytes, await File.ReadAllBytesAsync(existing));
    }
    [Theory]
    [InlineData("..\\evil.exe", "download.bin")]
    [InlineData("CON.exe", "download.bin")]
    [InlineData("C:\\evil.exe", "download.bin")]
    [InlineData("tool<bad>.exe", "tool_bad_.exe")]
    [InlineData("tool. ", "tool")]
    public void SanitizesUnsafeNames(string input, string expected) => Assert.Equal(expected, DownloadService.SanitizeFileName(input));
    private string Staging => Path.Combine(_folder, "staging"); private string Destination => Path.Combine(_folder, "destination");
    private static DownloadCandidate Candidate(string url) => new("Test", "id", "2.0", "2.0", Path.GetFileName(new Uri(url).AbsolutePath), new Uri(url));
    private static HttpClient Client(HttpStatusCode status, byte[] bytes, long? length = null) => new(new Handler(status, bytes, length));
    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
    private sealed class Handler(HttpStatusCode status, byte[] bytes, long? length = null) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { var content = new ByteArrayContent(bytes); if (length is not null) content.Headers.ContentLength = length; return Task.FromResult(new HttpResponseMessage(status) { Content = content }); } }
    private sealed class RedirectHandler(Uri finalUri) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri), Content = new ByteArrayContent([1]) }); }
    private sealed class DelayedHandler : HttpMessageHandler { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); } }
}
