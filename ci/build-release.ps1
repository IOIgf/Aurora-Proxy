# Requires PowerShell 7 and the Windows .NET Framework compiler.
[CmdletBinding()]
param([string]$Tag = '', [string]$OutputDirectory = 'dist')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$utf8 = [System.Text.UTF8Encoding]::new($false)
$program = Get-Content (Join-Path $root 'src/Program.cs') -Raw
if ([string]::IsNullOrEmpty($Tag)) {
    $match = [regex]::Match($program, 'AssemblyVersion\("(\d+\.\d+\.\d+)\.0"\)')
    if (!$match.Success) { throw 'Cannot determine the source version.' }
    $Tag = 'v' + $match.Groups[1].Value
}
if ($Tag -cnotmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw 'Use a version tag such as v1.1.8 or v1.2.0-beta.1.'
}
$version = $Tag.Substring(1)
$numericVersion = ($version -split '-')[0]
foreach ($part in $numericVersion.Split('.')) {
    if ([long]$part -gt 65534) { throw 'Version components must fit .NET assembly version limits.' }
}
$assemblyVersion = $numericVersion + '.0'
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
$output = [IO.Path]::GetFullPath($output)
if ($output -eq $root) { throw 'Output directory must not be the repository root.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$work = Join-Path $output ('work-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $work 'AuroraProxy'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    # Explicit package contents exclude Git metadata, credentials and temporary files.
    $items = @('AuroraProxy.csproj', 'AuroraProxy.exe.config', 'app.manifest', 'aurora.ico', 'build.cmd',
        'README.md', 'CHANGELOG.md', 'TESTING.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md',
        'src', 'tests', 'previews', 'core', 'third-party', 'ci')
    foreach ($item in $items) { Copy-Item (Join-Path $root $item) $stage -Recurse -Force }
    $core = Join-Path $stage 'core/mihomo.exe'
    $coreHash = [regex]::Match((Get-Content (Join-Path $root 'SHA256SUMS.txt') -Raw), '(?m)^([a-fA-F0-9]{64})  core/mihomo\.exe\r?$')
    if (!$coreHash.Success -or (Get-FileHash $core -Algorithm SHA256).Hash -ne $coreHash.Groups[1].Value) {
        throw 'Bundled mihomo checksum verification failed.'
    }

    # Update only staged sources: tags determine the EXE, footer and package version.
    $programPath = Join-Path $stage 'src/Program.cs'
    $versionPattern = '(Assembly(?:File)?Version\(")[^"]+("\))'
    if ([regex]::Matches($program, $versionPattern).Count -ne 2) { throw 'Expected both assembly version attributes.' }
    $program = [regex]::Replace($program, $versionPattern, {
        param($m) $m.Groups[1].Value + $assemblyVersion + $m.Groups[2].Value
    })
    $program = $program.Replace('namespace AuroraProxy {', '[assembly: AssemblyInformationalVersion("' + $version + '")]' + "`nnamespace AuroraProxy {")
    [IO.File]::WriteAllText($programPath, $program, $utf8)
    $mainPath = Join-Path $stage 'src/MainForm.cs'
    $main = Get-Content $mainPath -Raw
    $footerPattern = '(Windows 7\+\s+·\s+)v[0-9A-Za-z.-]+'
    if ([regex]::Matches($main, $footerPattern).Count -ne 1) { throw 'Expected one footer version.' }
    $main = [regex]::Replace($main, $footerPattern, { param($m) $m.Groups[1].Value + $Tag })
    [IO.File]::WriteAllText($mainPath, $main, $utf8)
    $readmePath = Join-Path $stage 'README.md'
    $readme = [regex]::Replace((Get-Content $readmePath -Raw), '\A# Aurora Proxy [^\r\n]+', '# Aurora Proxy ' + $version)
    [IO.File]::WriteAllText($readmePath, $readme, $utf8)

    # Pin Microsoft's net40 references instead of compiling against runner-installed 4.8 APIs.
    $referenceZip = Join-Path $work 'net40.zip'
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net40/1.0.3/microsoft.netframework.referenceassemblies.net40.1.0.3.nupkg' -OutFile $referenceZip
    if ((Get-FileHash $referenceZip -Algorithm SHA256).Hash -ne '54d6e20a1b61caf79395d6d71d091265e81f5a5705ac4ae52af45ca143c3c694') {
        throw 'The pinned .NET Framework reference package checksum does not match.'
    }
    $referenceRoot = Join-Path $work 'references'
    Expand-Archive $referenceZip $referenceRoot
    $references = Join-Path $referenceRoot 'build/.NETFramework/v4.0'
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    if (!(Test-Path $compiler)) { throw 'Windows .NET Framework C# compiler is required.' }
    $referenceNames = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Web.Extensions', 'System.Security', 'Accessibility')
    $common = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:4', '/utf8output', '/optimize+', '/platform:anycpu')
    foreach ($name in $referenceNames) { $common += '/reference:' + (Join-Path $references ($name + '.dll')) }
    $executable = Join-Path $stage 'AuroraProxy.exe'
    $compilerArgs = $common + @('/target:winexe', "/out:$executable", ('/win32manifest:' + (Join-Path $stage 'app.manifest')), ('/win32icon:' + (Join-Path $stage 'aurora.ico')))
    $sources = @(Get-ChildItem (Join-Path $stage 'src') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler @compilerArgs @sources
    if ($LASTEXITCODE -ne 0) { throw 'Client compilation failed.' }
    if ([Reflection.AssemblyName]::GetAssemblyName($executable).Version.ToString() -ne $assemblyVersion) { throw 'Built assembly version does not match the tag.' }

    # Run deterministic checkbox buffer regression and real-core integration on Windows.
    foreach ($test in @('CheckBoxFrameTests', 'IntegrationTests')) {
        $testExe = Join-Path $stage ($test + '.exe')
        & $compiler @common /target:exe "/out:$testExe" "/reference:$executable" (Join-Path $stage "tests/$test.cs")
        if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $test" }
        if ($test -eq 'IntegrationTests') { & $testExe $core } else { & $testExe }
        if ($LASTEXITCODE -ne 0) { throw "Regression checks failed: $test" }
        Remove-Item $testExe
    }
    Remove-Item (Join-Path $stage 'checkbox-complete-frame.png') -ErrorAction SilentlyContinue

    $manifest = @(Get-ChildItem $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/')
    })
    [IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'), $manifest, $utf8)
    $packageName = "AuroraProxy-Win7-x64-$Tag"
    $zipPath = Join-Path $output ($packageName + '.zip')
    Compress-Archive -Path $stage -DestinationPath $zipPath -CompressionLevel Optimal -Force
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($required in @('AuroraProxy/AuroraProxy.exe', 'AuroraProxy/core/mihomo.exe', 'AuroraProxy/SHA256SUMS.txt', 'AuroraProxy/src/Program.cs')) {
            if (!$zip.GetEntry($required)) { throw "Package entry missing: $required" }
        }
    } finally { $zip.Dispose() }
    $checksumPath = Join-Path $output ($packageName + '.sha256')
    [IO.File]::WriteAllText($checksumPath, (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zipPath) + "`n", $utf8)
    $metadata = @{ tag = $Tag; version = $version; package_name = $packageName; zip_path = $zipPath; checksum_path = $checksumPath; prerelease = $version.Contains('-') }
    [IO.File]::WriteAllText((Join-Path $output 'release.json'), ($metadata | ConvertTo-Json), $utf8)
    if ($env:GITHUB_OUTPUT) {
        foreach ($key in @('package_name', 'zip_path', 'checksum_path')) { "$key=$($metadata[$key])" | Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8 }
    }
    Write-Host "Built and verified $zipPath"
} finally {
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
}
