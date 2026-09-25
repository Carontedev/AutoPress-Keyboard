param([switch]$Trace, [string]$ExecutablePath = 'artifacts/AutoPress-win-x64/AutoPress.exe')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $workspace $ExecutablePath
$previousSettings = $env:AUTOPRESS_SETTINGS_DIRECTORY
$env:AUTOPRESS_SETTINGS_DIRECTORY = Join-Path $workspace 'artifacts\qa\release-settings'
$process = $null
try {
    $startup = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
    if (!$process.WaitForInputIdle(15000)) { throw 'La aplicación no terminó de iniciar.' }
    $startup.Stop()
    # Let initial WPF layout and tiered JIT settle before measuring idle.
    Start-Sleep -Seconds 10
    $process.Refresh()
    if ($process.HasExited) { throw ('La aplicación terminó con código ' + $process.ExitCode) }
    if ($Trace) {
        & (Join-Path $workspace 'artifacts\diagnostics\dotnet-trace.exe') collect --process-id $process.Id --profile dotnet-sampled-thread-time --duration 00:00:08 -o (Join-Path $workspace 'artifacts\qa\idle.nettrace')
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo capturar la traza.' }
    }
    $cpuBefore = $process.TotalProcessorTime.TotalMilliseconds
    $sample = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 10
    $sample.Stop()
    $process.Refresh()
    $cpuMs = $process.TotalProcessorTime.TotalMilliseconds - $cpuBefore
    [pscustomobject]@{
        StartupToInputIdleMs = [math]::Round($startup.Elapsed.TotalMilliseconds)
        WorkingSetMiB = [math]::Round($process.WorkingSet64 / 1MB, 1)
        PrivateBytesMiB = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        IdleSampleMs = [math]::Round($sample.Elapsed.TotalMilliseconds)
        IdleCpuMs = [math]::Round($cpuMs, 2)
        IdleCpuPercentOneCore = [math]::Round(100 * $cpuMs / $sample.Elapsed.TotalMilliseconds, 3)
        ExecutableMiB = [math]::Round((Get-Item -LiteralPath $executable).Length / 1MB, 1)
    } | Format-List
    if (!$process.CloseMainWindow()) { throw 'No se pudo solicitar el cierre de la ventana.' }
    if (!$process.WaitForExit(5000)) { throw 'La aplicación no se cerró a tiempo.' }
    if ($process.ExitCode -ne 0) { throw ('La aplicación terminó con código ' + $process.ExitCode) }
    Write-Output 'PASS: executable autónomo inicia y cierra con código 0, sin enviar teclas.'
} finally {
    if ($null -ne $process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    if ($null -eq $previousSettings) { Remove-Item Env:\AUTOPRESS_SETTINGS_DIRECTORY -ErrorAction SilentlyContinue }
    else { $env:AUTOPRESS_SETTINGS_DIRECTORY = $previousSettings }
}
