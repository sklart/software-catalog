# Software Catalog

Portable Windows catalog for local software installers. Stage 1 provides safe file enumeration, SHA-256 cataloging, SQLite persistence, relative scan roots and a minimal WPF UI. It never executes scanned files.

Requires .NET 10 SDK for development. Run `dotnet build`, `dotnet test`, or `./tools/publish-portable.ps1`. The published application stores persistent data only beside the executable: `Data`, `Config`, `Logs`, `Cache`, and `Backups`.

PE/MSI analysis and update providers are planned for later stages.
