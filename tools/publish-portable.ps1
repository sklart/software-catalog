$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts 'SoftwareCatalog-portable-win-x64'
$zip = "$output.zip"
Remove-Item $output,$zip -Recurse -Force -ErrorAction SilentlyContinue
dotnet restore "$root\SoftwareCatalog.slnx"
dotnet build "$root\SoftwareCatalog.slnx" -c Release --no-restore
dotnet test "$root\SoftwareCatalog.slnx" -c Release --no-build
dotnet publish "$root\src\SoftwareCatalog.UI\SoftwareCatalog.UI.csproj" -c Release -r win-x64 --self-contained true -o $output
foreach ($unexpected in @('Data', 'Logs', 'Cache', 'Backups', 'Config\settings.json')) { if (Test-Path (Join-Path $output $unexpected)) { throw "Persistent state was included: $unexpected" } }
if (Get-ChildItem $output -Recurse -Include *.db,*.db-wal,*.db-shm) { throw 'SQLite user data was included in publish output.' }
Compress-Archive -Path "$output\*" -DestinationPath $zip -Force
Write-Host "Created $zip"
