using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public interface IInstallerMetadataExtractor
{
    bool CanExtract(InstallerKind kind);
    InstallerMetadata Extract(string path, InstallerKind kind);
}

public sealed record InstallerMetadata(
    InstallerKind Kind, string? ProductName = null, string? ProductVersion = null, string? Publisher = null,
    string? FileVersion = null, string? FileDescription = null, string? Architecture = null,
    MetadataSource Source = MetadataSource.None, MetadataStatus Status = MetadataStatus.NotProcessed,
    string? Error = null, string? ProductCode = null, string? UpgradeCode = null);

public sealed class InstallerMetadataService(IEnumerable<IInstallerMetadataExtractor>? extractors = null)
{
    private readonly IInstallerMetadataExtractor[] _extractors = extractors?.ToArray() ?? [new PeMetadataExtractor(), new MsiMetadataExtractor(), new MsixMetadataExtractor()];
    public InstallerMetadata Extract(string path)
    {
        var kind = Identify(path);
        var extractor = _extractors.FirstOrDefault(item => item.CanExtract(kind));
        var metadata = extractor is null ? new InstallerMetadata(kind, Status: MetadataStatus.Partial) : extractor.Extract(path, kind);
        var fallback = FilenameParser.Parse(Path.GetFileNameWithoutExtension(path));
        if (string.IsNullOrWhiteSpace(metadata.ProductName)) metadata = metadata with { ProductName = fallback.ProductName, Source = fallback.ProductName is null ? metadata.Source : MetadataSource.FileNameFallback };
        if (string.IsNullOrWhiteSpace(metadata.ProductVersion)) metadata = metadata with { ProductVersion = fallback.ProductVersion, Source = fallback.ProductVersion is null ? metadata.Source : MetadataSource.FileNameFallback };
        if (string.IsNullOrWhiteSpace(metadata.Architecture)) metadata = metadata with { Architecture = fallback.Architecture };
        if (metadata.Status == MetadataStatus.NotProcessed) metadata = metadata with { Status = MetadataStatus.Partial };
        return metadata;
    }
    public static InstallerKind Identify(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".exe" => InstallerKind.Executable, ".msi" => InstallerKind.Msi, ".msix" => InstallerKind.Msix, ".msixbundle" => InstallerKind.MsixBundle, ".zip" => InstallerKind.ZipArchive, ".7z" => InstallerKind.SevenZipArchive, _ => InstallerKind.Unknown };
}

public sealed class PeMetadataExtractor : IInstallerMetadataExtractor
{
    public bool CanExtract(InstallerKind kind) => kind == InstallerKind.Executable;
    public InstallerMetadata Extract(string path, InstallerKind kind)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var architecture = ReadArchitecture(path);
            var useful = new[] { info.ProductName, info.ProductVersion, info.CompanyName, info.FileVersion, info.FileDescription }.Any(value => !string.IsNullOrWhiteSpace(value));
            return new(kind, info.ProductName, info.ProductVersion, info.CompanyName, info.FileVersion, info.FileDescription, architecture, MetadataSource.PeVersionInfo, useful ? MetadataStatus.Success : MetadataStatus.Partial);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
        {
            return new(kind, Source: MetadataSource.PeVersionInfo, Status: MetadataStatus.Failed, Error: ShortError(exception));
        }
    }
    private static string? ReadArchitecture(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D) throw new BadImageFormatException("Missing MZ header");
        stream.Position = 0x3C; var offset = reader.ReadInt32();
        if (offset < 0 || offset > stream.Length - 6) throw new BadImageFormatException("Invalid PE header offset");
        stream.Position = offset;
        if (reader.ReadUInt32() != 0x00004550) throw new BadImageFormatException("Missing PE header");
        return reader.ReadUInt16() switch { 0x014c => "x86", 0x8664 => "x64", 0x01c0 => "ARM", 0xAA64 => "ARM64", _ => "Unknown" };
    }
    internal static string ShortError(Exception exception) => exception.Message.Length <= 300 ? exception.Message : exception.Message[..300];
}

public sealed class MsiMetadataExtractor : IInstallerMetadataExtractor
{
    public bool CanExtract(InstallerKind kind) => kind == InstallerKind.Msi;
    public InstallerMetadata Extract(string path, InstallerKind kind)
    {
        if (!OperatingSystem.IsWindows()) return new(kind, Source: MetadataSource.MsiDatabase, Status: MetadataStatus.Failed, Error: "MSI metadata is available only on Windows.");
        try
        {
            uint result = MsiOpenDatabase(path, IntPtr.Zero, out var database);
            if (result != 0) return new(kind, Source: MetadataSource.MsiDatabase, Status: MetadataStatus.Failed, Error: $"Windows Installer error {result}.");
            try
            {
                string? Read(string property) { return Query(database, property); }
                var name = Read("ProductName"); var version = Read("ProductVersion"); var publisher = Read("Manufacturer");
                return new(kind, name, version, publisher, Source: MetadataSource.MsiDatabase, Status: name is null && version is null && publisher is null ? MetadataStatus.Partial : MetadataStatus.Success, ProductCode: Read("ProductCode"), UpgradeCode: Read("UpgradeCode"));
            }
            finally { MsiCloseHandle(database); }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException)
        { return new(kind, Source: MetadataSource.MsiDatabase, Status: MetadataStatus.Failed, Error: PeMetadataExtractor.ShortError(exception)); }
    }
    private static string? Query(IntPtr database, string property)
    {
        var sql = $"SELECT `Value` FROM `Property` WHERE `Property`='{property}'";
        if (MsiDatabaseOpenView(database, sql, out var view) != 0) return null;
        try { if (MsiViewExecute(view, IntPtr.Zero) != 0 || MsiViewFetch(view, out var record) != 0) return null; try { var size = 0; MsiRecordGetString(record, 1, null, ref size); var value = new System.Text.StringBuilder(size + 1); return MsiRecordGetString(record, 1, value, ref size) == 0 ? value.ToString() : null; } finally { MsiCloseHandle(record); } }
        finally { MsiCloseHandle(view); }
    }
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiOpenDatabase(string path, IntPtr persist, out IntPtr database);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiDatabaseOpenView(IntPtr database, string query, out IntPtr view);
    [DllImport("msi.dll")] private static extern uint MsiViewExecute(IntPtr view, IntPtr record);
    [DllImport("msi.dll")] private static extern uint MsiViewFetch(IntPtr view, out IntPtr record);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiRecordGetString(IntPtr record, uint field, System.Text.StringBuilder? value, ref int size);
    [DllImport("msi.dll")] private static extern uint MsiCloseHandle(IntPtr handle);
}

public sealed class MsixMetadataExtractor : IInstallerMetadataExtractor
{
    public bool CanExtract(InstallerKind kind) => kind is InstallerKind.Msix or InstallerKind.MsixBundle;
    public InstallerMetadata Extract(string path, InstallerKind kind)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var manifestName = kind == InstallerKind.MsixBundle ? "AppxMetadata/AppxBundleManifest.xml" : "AppxManifest.xml";
            var entry = archive.Entries.FirstOrDefault(value => value.FullName.Equals(manifestName, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return new(kind, Source: MetadataSource.MsixManifest, Status: MetadataStatus.Failed, Error: "Package manifest was not found.");
            using var stream = entry.Open(); var document = XDocument.Load(stream);
            var identity = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Identity");
            if (identity is null) return new(kind, Source: MetadataSource.MsixManifest, Status: MetadataStatus.Failed, Error: "Package identity was not found.");
            var properties = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Properties");
            var display = properties?.Elements().FirstOrDefault(node => node.Name.LocalName == "DisplayName")?.Value;
            var publisherDisplay = properties?.Elements().FirstOrDefault(node => node.Name.LocalName == "PublisherDisplayName")?.Value;
            return new(kind, display ?? (string?)identity.Attribute("Name"), (string?)identity.Attribute("Version"), publisherDisplay ?? (string?)identity.Attribute("Publisher"), Architecture: (string?)identity.Attribute("ProcessorArchitecture"), Source: MetadataSource.MsixManifest, Status: MetadataStatus.Success);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or System.Xml.XmlException)
        { return new(kind, Source: MetadataSource.MsixManifest, Status: MetadataStatus.Failed, Error: PeMetadataExtractor.ShortError(exception)); }
    }
}

public sealed record FileNameMetadata(string? ProductName, string? ProductVersion, string? Architecture);
public static partial class FilenameParser
{
    [GeneratedRegex(@"(?i)(?:^|[._-])v?(\d+(?:\.\d+){1,3})(?:$|[._-])")]
    private static partial Regex VersionPattern();
    [GeneratedRegex(@"(?i)(?:^|[._-])(x64|win64|amd64|x86|win32|arm64|arm)(?:$|[._-])")]
    private static partial Regex ArchitecturePattern();
    public static FileNameMetadata Parse(string name)
    {
        var versionMatch = VersionPattern().Match(name); var architectureMatch = ArchitecturePattern().Match(name);
        var version = versionMatch.Success ? versionMatch.Groups[1].Value : null;
        var architecture = architectureMatch.Success ? architectureMatch.Groups[1].Value.ToLowerInvariant() switch { "win64" or "amd64" => "x64", "win32" => "x86", var value => value.ToUpperInvariant() == "ARM64" ? "ARM64" : value.ToUpperInvariant() == "ARM" ? "ARM" : value } : null;
        if (version is null) return new(null, null, architecture);
        var product = name[..versionMatch.Index].Trim(' ', '.', '_', '-');
        product = Regex.Replace(product, "(?i)(setup|installer)$", "").Trim(' ', '.', '_', '-');
        return new(string.IsNullOrWhiteSpace(product) ? null : product, version, architecture);
    }
}

public static partial class VersionNormalizer
{
    [GeneratedRegex(@"(?i)^\s*(?:version\s*|v)?(\d+(?:\.\d+){1,3})\s*$")]
    private static partial Regex Pattern();
    public static string? Normalize(string? raw) => raw is null ? null : Pattern().Match(raw) is { Success: true } match ? match.Groups[1].Value : null;
}
