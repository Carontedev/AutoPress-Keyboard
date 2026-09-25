param(
    [string]$ReleaseDirectory = 'artifacts/AutoPress-win-x64',
    [string]$DestinationPath = 'artifacts/AutoPress-win-x64.zip',
    [switch]$Stable
)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
$candidate = $null
$checksumCandidate = $null
try {
    $release = [IO.Path]::GetFullPath($ReleaseDirectory)
    $destination = [IO.Path]::GetFullPath($DestinationPath)
    $executable = Join-Path $release 'AutoPress.exe'
    if (!(Test-Path -LiteralPath $executable)) { throw 'Publish the executable first.' }
    if (!(Test-Path -LiteralPath 'LICENSE')) { throw 'The product license is required.' }
    if ($Stable) {
        if ((Get-AuthenticodeSignature -LiteralPath $executable).Status -ne 'Valid') { throw 'Stable release requires a valid code-signing signature.' }
        if ((Get-Item -LiteralPath $executable).VersionInfo.ProductVersion -match 'beta|alpha|preview|rc') { throw 'Stable release cannot use a prerelease executable.' }
    }

    $readme = Join-Path $release 'README.txt'
    Copy-Item -LiteralPath scripts/portable-readme.txt -Destination $readme
    $license = Join-Path $release 'LICENSE.txt'
    $terms = [IO.File]::ReadAllText((Join-Path (Get-Location) 'LICENSE'))
    $terms += [Environment]::NewLine + [Environment]::NewLine + 'THIRD-PARTY COMPONENT LICENSES AND NOTICES' + [Environment]::NewLine
    foreach ($notice in @('dotnet-LICENSE.txt', 'dotnet-THIRD-PARTY-NOTICES.txt', 'windowsdesktop-LICENSE.txt')) {
        $terms += [Environment]::NewLine + $notice + [Environment]::NewLine
        $terms += [IO.File]::ReadAllText((Join-Path (Get-Location) ('licenses/' + $notice)))
    }
    [IO.File]::WriteAllText($license, $terms, [Text.UTF8Encoding]::new($false))
    $items = @($executable, $readme, $license)
    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $candidate = Join-Path $parent ('.package-' + [Guid]::NewGuid().ToString('N') + '.zip')
    Compress-Archive -LiteralPath $items -DestinationPath $candidate
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($candidate)
    try {
        $entry = $archive.GetEntry('AutoPress.exe')
        if ($archive.Entries.Count -ne 3 -or !$entry -or !$archive.GetEntry('README.txt') -or !$archive.GetEntry('LICENSE.txt')) {
            throw 'Archive must contain only AutoPress.exe, README.txt and LICENSE.txt.'
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        $stream = $entry.Open()
        try { $archivedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($archivedHash -ne (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash) { throw 'Archived executable checksum mismatch.' }
    } finally { $archive.Dispose() }

    $zipHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    $checksumCandidate = Join-Path $parent ('.checksum-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($checksumCandidate, ($zipHash + '  ' + [IO.Path]::GetFileName($destination) + [Environment]::NewLine))
    if (Test-Path -LiteralPath $destination) { [IO.File]::Replace($candidate, $destination, [NullString]::Value) }
    else { [IO.File]::Move($candidate, $destination) }
    $checksum = $destination + '.sha256'
    if (Test-Path -LiteralPath $checksum) { [IO.File]::Replace($checksumCandidate, $checksum, [NullString]::Value) }
    else { [IO.File]::Move($checksumCandidate, $checksum) }
    Get-FileHash -LiteralPath $executable,$destination -Algorithm SHA256
} finally {
    foreach ($temporary in @($candidate,$checksumCandidate)) {
        if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -ErrorAction SilentlyContinue }
    }
    Pop-Location
}
