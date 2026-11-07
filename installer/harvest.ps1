param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [Parameter(Mandatory = $true)][string]$OutputPath
)
# 将 publish 输出目录生成 WiX v4/v5 harvest 清单（heat 在 WiX v5 CLI 中已移除）
$ErrorActionPreference = "Stop"
$root = (Resolve-Path $PublishDir).Path.Replace('/', '\')
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
$script:cid = 0
$script:compRefs = New-Object System.Collections.Generic.List[string]

function Walk($dir) {
    foreach ($d in (Get-ChildItem -LiteralPath $dir -Directory)) {
        $safe = ($d.Name -replace '[^A-Za-z0-9_.]', '_')
        $script:cid++
        $dirId = "dir$($script:cid)_$safe"
        [void]$script:sb.AppendLine("      <Directory Id=`"$dirId`" Name=`"$($d.Name)`">")
        Walk $d.FullName
        [void]$script:sb.AppendLine("      </Directory>")
    }
    foreach ($f in (Get-ChildItem -LiteralPath $dir -File)) {
        $script:cid++
        $compId = "cmp$($script:cid)"
        $fileId = "file$($script:cid)"
        $rel = $f.FullName.Substring($root.Length + 1)
        [void]$script:sb.AppendLine("        <Component Id=`"$compId`" Guid=`"`*`"><File Id=`"$fileId`" Source=`"`$(var.PublishDir)\$rel`" /></Component>")
        [void]$script:compRefs.Add($compId)
    }
}

Walk $root
[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles">')
foreach ($c in $script:compRefs) { [void]$sb.AppendLine("      <ComponentRef Id=`"$c`" />") }
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')
Set-Content -Path $OutputPath -Value $sb.ToString() -Encoding utf8
Write-Host "harvest components: $($script:compRefs.Count)"
