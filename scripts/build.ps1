param([switch]$Publish, [switch]$Smoke)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$sdk = Join-Path $env:LOCALAPPDATA 'AutoPressKeyboard\sdk\dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $sdk build src/AutoPress.App/AutoPress.App.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build falló.' }
    & $sdk test tests/AutoPress.Core.Tests/AutoPress.Core.Tests.csproj -c Release --logger 'trx;LogFileName=core.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Pruebas del núcleo fallaron.' }
    & $sdk test tests/AutoPress.Windows.Tests/AutoPress.Windows.Tests.csproj -c Release --logger 'trx;LogFileName=windows.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Pruebas de entrada de Windows fallaron.' }
    if ($Smoke) {
        & $sdk run --project tests/AutoPress.Windows.Smoke/AutoPress.Windows.Smoke.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Pruebas de Windows fallaron.' }
    }
    if ($Publish) {
        & $sdk publish src/AutoPress.App/AutoPress.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/AutoPress-win-x64
        if ($LASTEXITCODE -ne 0) { throw 'Publicación falló.' }
        & (Join-Path $PSScriptRoot 'package.ps1')
    }
} finally { Pop-Location }
