# Build WallP for Windows as a portable .exe and .zip
# Usage (from repo root): .\windows\scripts\build-app.ps1 [Release|Debug]
#
# Output (windows/build/):
#   WallP\WallP.exe       — portable executable + dependencies
#   WallP-X.Y.Z.zip       — zip archive  (Release only)

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$RepoRoot = Split-Path -Parent $ProjectDir

$AppName = 'WallP'
$Csproj = Join-Path $ProjectDir "$AppName\$AppName.csproj"
$BuildDir = Join-Path $ProjectDir 'build'
$PublishDir = Join-Path $BuildDir $AppName

# Read version from csproj
[xml]$csprojXml = Get-Content $Csproj
$Version = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "=== Building $AppName $Version ($Configuration) ==="

# Clean previous output
if (Test-Path $BuildDir) {
    Remove-Item $BuildDir -Recurse -Force
}
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null

# Publish: framework-dependent (requires .NET 10 Desktop Runtime on the user's machine)
# For self-contained, add -p:SelfContained=true and --runtime win-x64.
Write-Host "  [1/2] Publishing..."
& dotnet publish $Csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained false `
    --output $PublishDir `
    -p:PublishSingleFile=false `
    -p:DebugType=embedded `
    --nologo `
    --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed."
    exit 1
}

if ($Configuration -ne 'Release') {
    Write-Host ""
    Write-Host "=== Build complete ==="
    Write-Host "  App: $PublishDir\$AppName.exe"
    exit 0
}

# Create ZIP
Write-Host "  [2/2] Creating ZIP..."
$ZipPath = Join-Path $BuildDir "$AppName-$Version.zip"
Compress-Archive -Path $PublishDir -DestinationPath $ZipPath -CompressionLevel Optimal
$ZipSize = (Get-Item $ZipPath).Length
Write-Host ("         {0:N1} MB  $AppName-$Version.zip" -f ($ZipSize / 1MB))

Write-Host ""
Write-Host "=== Build complete ==="
Write-Host "  App: $PublishDir\$AppName.exe"
Write-Host "  ZIP: $ZipPath"
Write-Host ""
Write-Host "To run:     & '$PublishDir\$AppName.exe'"
