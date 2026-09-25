param(
    [string]$ReleaseDirectory = 'artifacts/AutoPress-win-x64',
    [string]$DestinationPath = 'artifacts/AutoPress-win-x64.zip',
    [switch]$Stable,
    [switch]$Minimal
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
    if ($Stable) {
        if (!(Test-Path -LiteralPath 'LICENSE')) { throw 'Stable release blocked: the owner must choose a distribution license.' }
        if ((Get-AuthenticodeSignature -LiteralPath $executable).Status -ne 'Valid') { throw 'Stable release blocked: a valid code-signing signature is required.' }
        if ((Get-Item -LiteralPath $executable).VersionInfo.ProductVersion -match 'beta|alpha|preview|rc') { throw 'Stable release blocked: executable is a prerelease.' }
    }
    if ($Minimal) {
        $readme = Join-Path $release 'README.txt'
        $text = [IO.File]::ReadAllText((Join-Path (Get-Location) 'docs/PORTABLE_README.txt'))
        foreach ($notice in @('dotnet-LICENSE.txt', 'dotnet-THIRD-PARTY-NOTICES.txt', 'windowsdesktop-LICENSE.txt')) {
            $text += [Environment]::NewLine + [Environment]::NewLine + $notice + [Environment]::NewLine
            $text += [IO.File]::ReadAllText((Join-Path (Get-Location) ('docs/legal/' + $notice)))
        }
        [IO.File]::WriteAllText($readme, $text, [Text.UTF8Encoding]::new($false))
        $items = @($executable, $readme)
    } else {
    Copy-Item -LiteralPath README.md -Destination (Join-Path $release 'README.md')
    New-Item -ItemType Directory -Path (Join-Path $release 'docs/assets') -Force | Out-Null
    Copy-Item -LiteralPath docs/assets/logo.png -Destination (Join-Path $release 'docs/assets/logo.png')
    New-Item -ItemType Directory -Path (Join-Path $release 'docs/research') -Force | Out-Null
    Copy-Item -LiteralPath docs/research/autopress-key-research.md -Destination (Join-Path $release 'docs/research/autopress-key-research.md')
    foreach ($document in @('VALIDATION.md','RELEASE_CHECKLIST.md','USER_GUIDE.md')) {
        $source = Join-Path 'docs' $document
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $release ('docs/' + $document)) }
    }
    $items = @($executable, (Join-Path $release 'README.md'), (Join-Path $release 'docs'))
    foreach ($document in @('LICENSE','DISTRIBUTION.md','CHANGELOG.md')) {
        if (Test-Path -LiteralPath $document) {
            $copy = Join-Path $release $document
            Copy-Item -LiteralPath $document -Destination $copy
            $items += $copy
        }
    }
    }
    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $candidate = Join-Path $parent ('.package-' + [Guid]::NewGuid().ToString('N') + '.zip')
    Compress-Archive -LiteralPath $items -DestinationPath $candidate
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($candidate)
    try {
        $entry = $archive.GetEntry('AutoPress.exe')
        $readmeName = if ($Minimal) { 'README.txt' } else { 'README.md' }
        if ($null -eq $entry -or $null -eq $archive.GetEntry($readmeName)) { throw 'Archive verification failed: required files missing.' }
        if ($Minimal -and $archive.Entries.Count -ne 2) { throw 'Minimal package must contain exactly the executable and README.txt.' }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $packedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($packedHash -ne (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash) { throw 'Archive verification failed: executable hash mismatch.' }
    } finally { $archive.Dispose() }
    $zipHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    $checksumCandidate = Join-Path $parent ('.checksum-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($checksumCandidate, ($zipHash + '  ' + [IO.Path]::GetFileName($destination) + [Environment]::NewLine))
    # Replace only a fully built and verified candidate; failure preserves the old ZIP.
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
