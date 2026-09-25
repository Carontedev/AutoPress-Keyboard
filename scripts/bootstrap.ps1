$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'AutoPressKeyboard\sdk'
$installer = Join-Path ([IO.Path]::GetTempPath()) ('autopress-dotnet-install-' + [Guid]::NewGuid().ToString('N') + '.ps1')
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
try {
    & $installer -Channel '10.0' -InstallDir $installRoot -NoPath
    & (Join-Path $installRoot 'dotnet.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo verificar el SDK.' }
} finally {
    Remove-Item -LiteralPath $installer -ErrorAction SilentlyContinue
}
