# Software Catalog

Portable Windows catalog for local software installers. Stages 1–3 provide safe file enumeration, metadata extraction, product grouping and update discovery. Stage 4 adds explicit update download discovery, HTTPS-only streaming into `Cache\Staging`, SHA-256 verification, metadata/product validation and portable download history.

Software Catalog never executes installers. It does not invoke `winget install` or `winget upgrade`; an installer is only downloaded and staged/imported after validation.

Requires .NET 10 SDK for development. Run `dotnet build`, `dotnet test`, or `./tools/publish-portable.ps1`. The published application stores persistent data only beside the executable: `Data`, `Config`, `Logs`, `Cache`, and `Backups`.

Persistent state remains beside the executable; the default download destination is the portable `Downloads` directory and can be made absolute in settings.
