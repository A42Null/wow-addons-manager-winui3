<#
.SYNOPSIS
    构建 MSI 安装包。

.DESCRIPTION
    优先使用 Advanced Installer（读取仓库根目录的 .aip 工程，可用 /build 命令行构建）；
    若本机没有 Advanced Installer，则回退到 WiX v5（installer/harvest.ps1 + Product.wxs）。

    注意：Advanced Installer 工程中的文件源路径相对于 .aip 所在目录，
    因此必须把待打包的发布目录放到 <repo>\versions\WoWAddonManager-win-x64。

.PARAMETER Version
    版本号（默认 0.4.1），用于输出文件名。

.PARAMETER PublishDir
    待打包的发布目录（默认 versions\WoWAddonManager-win-x64）。

.PARAMETER OutputDir
    MSI 输出目录（默认 versions）。

.PARAMETER ForceWix
    强制使用 WiX，忽略已安装的 Advanced Installer。

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
        throw "发布目录不存在: $PublishDir"
    }

    # ---------- 1) Advanced Installer ----------
    function Find-AdvancedInstaller {
        $candidates = @()
        try {
            $keys = @(
                'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
            )
            foreach ($k in $keys) {
                $candidates += Get-ItemProperty $k -ErrorAction SilentlyContinue |
                    Where-Object { $_.DisplayName -like 'Advanced Installer*' } |
                    Select-Object -ExpandProperty InstallLocation -ErrorAction SilentlyContinue
            }
        } catch { }
        foreach ($dir in $candidates) {
            if (-not $dir) { continue }
            foreach ($rel in @('bin\x86\AdvancedInstaller.com', 'bin\x64\AdvancedInstaller.com')) {
                $p = Join-Path $dir $rel
                if (Test-Path $p) { return $p }
            }
        }
        return $null
    }

    if (-not $ForceWix -and $aip) {
        $ai = Find-AdvancedInstaller
        if ($ai) {
            Write-Host "[MSI] 使用 Advanced Installer: $ai"
            & $ai /build $aip.FullName
            if ($LASTEXITCODE -ne 0) {
                throw "Advanced Installer 构建失败 (exit=$LASTEXITCODE)"
            }
            $setupDir = Join-Path $repoRoot ($aip.BaseName + "-SetupFiles")
            $built = Get-ChildItem -Path $setupDir -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $built) { throw "Advanced Installer 未生成 MSI（$setupDir）" }
            New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot $OutputDir) | Out-Null
            Copy-Item $built.FullName $outMsi -Force
            Write-Host "[MSI] 已生成: $outMsi"
            Get-Item $outMsi | Select-Object Name, Length, LastWriteTime
            exit 0
        }
        Write-Host "[MSI] 未检测到 Advanced Installer，改用 WiX 回退方案"
    }

    # ---------- 2) WiX v5 回退 ----------
    if (-not (Test-Path (Join-Path $repoRoot "installer\Product.wxs"))) {
        throw "缺少 installer\Product.wxs，无法使用 WiX 回退方案"
    }
    Write-Host "[MSI] 使用 WiX v5 构建"
    & powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "installer\harvest.ps1") `
        -PublishDir $PublishDir -OutputPath (Join-Path $repoRoot "harvest.wxs")
    if ($LASTEXITCODE -ne 0) { throw "harvest.ps1 失败" }

    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Host "[MSI] 安装 WiX v5 ..."
        & dotnet tool install --global wix --version 5.0.2
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" +
                     [System.Environment]::GetEnvironmentVariable("PATH", "User")
        $wix = Get-Command wix -ErrorAction SilentlyContinue
    }
    if (-not $wix) { throw "找不到 wix 命令" }

    & wix build -arch x64 -define "PublishDir=$PublishDir" -define "MsiVersion=$Version" `
        -out $outMsi (Join-Path $repoRoot "installer\Product.wxs") (Join-Path $repoRoot "harvest.wxs")
    if ($LASTEXITCODE -ne 0) { throw "wix build 失败" }
    Write-Host "[MSI] 已生成: $outMsi"
    Get-Item $outMsi | Select-Object Name, Length, LastWriteTime
}
finally {
    Pop-Location
}
