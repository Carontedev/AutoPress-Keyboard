$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $workspace ('artifacts/qa/package-test-' + [Guid]::NewGuid().ToString('N'))
$release = Join-Path $fixture 'release'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$exe = Join-Path $release 'AutoPress.exe'
$zip = Join-Path $fixture 'package.zip'
[IO.File]::WriteAllBytes($exe, [byte[]](1,2,3,4))
& (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip | Out-Null
$before = (Get-FileHash -LiteralPath $zip).Hash
$lock = [IO.File]::Open($exe, 'Open', 'ReadWrite', 'None')
$failed = $false
try {
    try { & (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip | Out-Null }
    catch { $failed = $true }
} finally { $lock.Dispose() }
if (!$failed -or (Get-FileHash -LiteralPath $zip).Hash -ne $before) { throw 'Failed packaging did not preserve previous ZIP.' }
$blocked = $false
try { & (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip -Stable | Out-Null }
catch { $blocked = $true }
if (!$blocked -or (Get-FileHash -LiteralPath $zip).Hash -ne $before) { throw 'Stable gate did not preserve previous ZIP.' }
if (!(Get-Content -LiteralPath ($zip + '.sha256')).StartsWith($before)) { throw 'Checksum does not match preserved ZIP.' }
& (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip | Out-Null
if (!(Get-Content -LiteralPath ($zip + '.sha256')).StartsWith((Get-FileHash -LiteralPath $zip).Hash)) { throw 'Replacement checksum mismatch.' }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.Count -ne 3 -or !$archive.GetEntry('AutoPress.exe') -or !$archive.GetEntry('README.txt') -or !$archive.GetEntry('LICENSE.txt')) {
        throw 'Unexpected release contents.'
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('LICENSE.txt').Open())
    try { $license = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if (!$license.Contains('AUTOPRESS KEYBOARD PROPRIETARY LICENSE') -or !$license.Contains('Sell, resell') -or !$license.Contains('Permission is hereby granted')) {
        throw 'Product terms or bundled runtime notices are missing.'
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('README.txt').Open())
    try { $readme = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($readme.Length -gt 2500 -or !$readme.Contains('Ctrl + Alt + F10')) { throw 'README must be short and retain emergency instructions.' }
} finally { $archive.Dispose() }
Write-Output 'PASS: three-file archive, product/runtime licenses, concise README, failure preservation, stable gate.'
