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
if (!$failed -or (Get-FileHash -LiteralPath $zip).Hash -ne $before) { throw 'Failed packaging did not preserve the previous ZIP.' }
$blocked = $false
try { & (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip -Stable | Out-Null }
catch { $blocked = $true }
if (!$blocked -or (Get-FileHash -LiteralPath $zip).Hash -ne $before) { throw 'Stable gate did not preserve the previous package.' }
if (!(Get-Content -LiteralPath ($zip + '.sha256')).StartsWith($before)) { throw 'Checksum does not match the preserved package.' }
& (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -DestinationPath $zip | Out-Null
if (!(Get-Content -LiteralPath ($zip + '.sha256')).StartsWith((Get-FileHash -LiteralPath $zip).Hash)) { throw 'Replacement package checksum mismatch.' }
Write-Output 'PASS: verified archive, failure preserves previous ZIP/checksum, and unsigned stable release is blocked.'
