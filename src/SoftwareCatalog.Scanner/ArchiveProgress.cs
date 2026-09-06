using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public sealed record ArchiveProgress(string FileName, ArchiveOperationType Operation, int ProcessedFiles, int TotalFiles, long BytesProcessed, long TotalBytes, ArchiveOperationStatus Status);
