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
if (Test-Path "$output\Data") { throw 'Persistent data was included in publish output.' }
Compress-Archive -Path "$output\*" -DestinationPath $zip -Force
Write-Host "Created $zip"
