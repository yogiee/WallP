# Signs the repo-root appcast-windows.xml with the local ed25519 private key
# and writes the result to appcast-windows.xml.signature. NetSparkle's
# SecurityMode.Strict requires this companion file alongside the appcast,
# otherwise the running app rejects the appcast as "not valid" and surfaces
# a misleading "couldn't reach server" dialog.
#
# Run after updating appcast-windows.xml with a new <item>, before committing.
# Usage (from repo root): .\windows\scripts\sign-appcast.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$Appcast = Join-Path $RepoRoot 'appcast-windows.xml'
$SignaturePath = "$Appcast.signature"

$Tool = Join-Path $env:USERPROFILE '.dotnet\tools\netsparkle-generate-appcast.exe'
if (-not (Test-Path $Tool)) {
    Write-Error "netsparkle-generate-appcast not found at $Tool. Install with: dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator"
    exit 1
}

if (-not (Test-Path $Appcast)) {
    Write-Error "appcast-windows.xml not found at $Appcast"
    exit 1
}

$Output = & $Tool --generate-signature $Appcast 2>&1
$Match = $Output | Select-String -Pattern 'Signature:\s+([A-Za-z0-9+/=]+)' | Select-Object -First 1
if (-not $Match) {
    Write-Error "Could not parse signature from tool output: $Output"
    exit 1
}
$Signature = $Match.Matches.Groups[1].Value

# Single line, trailing newline so editors don't show "no newline at EOF" warnings.
[System.IO.File]::WriteAllText($SignaturePath, "$Signature`n")
Write-Host "Wrote $SignaturePath"
