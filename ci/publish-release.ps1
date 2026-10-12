[CmdletBinding()]
param([string]$MetadataPath = './dist/release.json')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$env:GH_TOKEN -or !$env:GH_REPO) { throw 'GH_TOKEN and GH_REPO are required.' }
$release = Get-Content $MetadataPath -Raw | ConvertFrom-Json
$tag = $release.tag
$assets = @($release.zip_path, $release.checksum_path)
foreach ($asset in $assets) { if (!(Test-Path $asset)) { throw "Release asset missing: $asset" } }
$existingJson = & gh release view $tag --repo $env:GH_REPO --json isDraft,assets 2>$null
$exists = $LASTEXITCODE -eq 0
if ($exists) {
    $existing = $existingJson | ConvertFrom-Json
    if (!$existing.isDraft) {
        $names = @($existing.assets | ForEach-Object name)
        foreach ($asset in $assets) {
            if ([IO.Path]::GetFileName($asset) -notin $names) { throw 'A published release already exists with missing expected assets; it will not be overwritten.' }
        }
        Write-Host "Release $tag is already published with both build assets."
        return
    }
    & gh release upload $tag @assets --repo $env:GH_REPO --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Draft asset upload failed.' }
    $flags = if ($release.prerelease) { @('--prerelease', '--latest=false') } else { @('--prerelease=false', '--latest') }
    & gh release edit $tag --repo $env:GH_REPO --draft=false @flags
    if ($LASTEXITCODE -ne 0) { throw 'Draft publication failed.' }
} else {
    $notesPath = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($MetadataPath))) 'release-notes.md'
    $notes = @"
Windows 7 原生 mihomo 客户端，使用 .NET Framework 4.0 API / C# 4。

- 下载 ``$($release.package_name).zip`` 并完整解压，再运行 ``AuroraProxy.exe``。
- 包含 mihomo v1.19.32 / windows-amd64-v1-go120 内核、源码、说明及许可证。
- Windows 7 SP1 推荐安装 .NET Framework 4.8。
- 已在 Windows Server 2022 构建环境通过勾选框缓冲区回归和真实内核接口检查；不等同于 Windows 7 实机验收。
- 用同名 ``.sha256`` 文件校验 ZIP；包内 ``SHA256SUMS.txt`` 校验各文件。
"@
    [IO.File]::WriteAllText($notesPath, $notes, [Text.UTF8Encoding]::new($false))
    $flags = if ($release.prerelease) { @('--prerelease', '--latest=false') } else { @('--latest') }
    & gh release create $tag @assets --repo $env:GH_REPO --verify-tag --title "Aurora Proxy $tag" --notes-file $notesPath @flags
    if ($LASTEXITCODE -ne 0) { throw 'Release creation or asset publication failed.' }
}
Write-Host "Published $tag with ZIP and SHA-256 assets."
