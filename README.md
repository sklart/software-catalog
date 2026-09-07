# Software Catalog

## Stage 5: archive and safe cleanup

Stage 5 adds a local installer archive, retention-plan preview, SHA-256 duplicate detection, recoverable Trash, and explicit permanent cleanup. Retention never permanently deletes files automatically: it can only propose moving older installers to the managed archive.

Portable Windows catalog for local software installers. Stages 1–3 provide safe file enumeration, metadata extraction, product grouping and update discovery. Stage 4 adds explicit update download discovery, HTTPS-only streaming into `Cache\Staging`, SHA-256 verification, metadata/product validation and portable download history.

Software Catalog never installs or executes installers. It does not invoke `winget install` or `winget upgrade`; an installer is only downloaded and staged/imported after validation. Successful downloads are added to the local installer catalog.

Requires .NET 10 SDK for development. Run `dotnet build`, `dotnet test`, or `./tools/publish-portable.ps1`. The published application stores persistent data only beside the executable: `Data`, `Config`, `Logs`, `Cache`, and `Backups`.

Persistent state remains beside the executable; the default download destination is the portable `Downloads` directory and can be made absolute in settings.
# Software Catalog

## Update-source diagnostics

`ProviderErrorKind` classifies an individual update-check result (timeout, rate limit, authentication, network, invalid response, ambiguity and not found). It is intentionally session-only: the existing persisted product state retains the human-readable error text, while the structured kind is not persisted across restart. This avoids a schema migration beyond Stage 6 Migration008.
