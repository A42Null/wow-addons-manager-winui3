<#
.SYNOPSIS
    Build the MSI installer.

.DESCRIPTION
    Priority 1: Advanced Installer (reads the .aip project in the repo root, built via /build).
    Fallback  : WiX v5 (installer/harvest.ps1 + installer/Product.wxs).

    IMPORTANT: This script is intentionally ASCII-only (no BOM) so that it runs
    correctly under Windows PowerShell 5.1 as well as PowerShell 7.

    The Advanced Installer project references files with paths relative to the
    repository root, therefore the publish output must be placed at
    <repo>\versions\WoWAddonManager-win-x64.

.PARAMETER Version
    Version string, e.g. 0.4.1 (used in the output file name).

.PARAMETER PublishDir
    Directory with the files to package (default: versions\WoWAddonManager-win-x64).

.PARAMETER OutputDir
    Where the MSI is written (default: versions).

.PARAMETER ForceWix
    Skip Advanced Installer and always use the WiX fallback.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer/build-msi.ps1 -Version 0.4.1
#>
[CmdletBinding()]
param(
    [string]$Version = "0.4.1",
    [string]$PublishDir = "versions\WoWAddonManager-win-x64",
    [string]$OutputDir = "versions",
    [switch]$ForceWix
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $aip = Get-ChildItem -Path $repoRoot -Filter "*.aip" | Select-Object -First 1
    $msiName = "WoWAddonManager-$Version-x64.msi"
    $outMsi = Join-Path $repoRoot (Join-Path $OutputDir $msiName)

    if (-not (Test-Path $PublishDir)) {
        throw "Publish directory not found: $PublishDir"
    }

    function Find-AdvancedInstaller {
        $dirs = @()
        try {
            $keys = @(
                'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
            )
            foreach ($k in $keys) {
                $dirs += Get-ItemProperty $k -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like 'Advanced Installer*' } |
                    Select-Object -ExpandProperty InstallLocation -ErrorAction SilentlyContinue
            }
        } catch { }
        foreach ($d in $dirs) {
            if (-not $d) { continue }
            foreach ($rel in @('bin\x86\AdvancedInstaller.com', 'bin\x64\AdvancedInstaller.com')) {
                $p = Join-Path $d $rel
                if (Test-Path $p) { return $p }
            }
        }
        return $null
    }

    if (-not $ForceWix -and $aip) {
        $ai = Find-AdvancedInstaller
        if ($ai) {
            Write-Host "[MSI] Using Advanced Installer: $ai"
            & $ai /build $aip.FullName
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[MSI] Advanced Installer build failed (exit=$LASTEXITCODE)"
            } else {
                $setupDir = Join-Path $repoRoot ($aip.BaseName + "-SetupFiles")
                $built = Get-ChildItem -Path $setupDir -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($built) {
                    New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot $OutputDir) | Out-Null
                    Copy-Item $built.FullName $outMsi -Force
                    Write-Host "[MSI] Generated: $outMsi"
                    Get-Item $outMsi | Select-Object Name, Length, LastWriteTime
                    exit 0
                }
                Write-Host "[MSI] No MSI produced in $setupDir"
            }
            Write-Host "[MSI] Falling back to WiX"
        } else {
            Write-Host "[MSI] Advanced Installer not found, using WiX fallback"
        }
    }

    # ---- WiX v5 fallback ----
    $wxs = Join-Path $repoRoot "installer\Product.wxs"
    if (-not (Test-Path $wxs)) {
        throw "Missing $wxs, cannot use the WiX fallback"
    }
    Write-Host "[MSI] Building with WiX v5"
    & powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "installer\harvest.ps1") `
        -PublishDir $PublishDir -OutputPath (Join-Path $repoRoot "harvest.wxs")
    if ($LASTEXITCODE -ne 0) { throw "harvest.ps1 failed" }

    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Host "[MSI] Installing WiX v5 ..."
        & dotnet tool install --global wix --version 5.0.2
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" +
                     [System.Environment]::GetEnvironmentVariable("PATH", "User")
        $wix = Get-Command wix -ErrorAction SilentlyContinue
    }
    if (-not $wix) { throw "wix command not found" }

    & wix build -arch x64 -define "PublishDir=$PublishDir" -define "MsiVersion=$Version" `
        -out $outMsi $wxs (Join-Path $repoRoot "harvest.wxs")
    if ($LASTEXITCODE -ne 0) { throw "wix build failed" }
    Write-Host "[MSI] Generated: $outMsi"
    Get-Item $outMsi | Select-Object Name, Length, LastWriteTime
}
finally {
    Pop-Location
}
