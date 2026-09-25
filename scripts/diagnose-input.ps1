$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class AutoPressInputDiagnostic {
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll")] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll")] static extern bool GetTokenInformation(IntPtr token, int kind, out int value, int size, out int required);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out int process);
    public static string Elevated(int id) {
        IntPtr process = OpenProcess(0x1000, false, id), token;
        if (process == IntPtr.Zero) return "unknown";
        try {
            if (!OpenProcessToken(process, 8, out token)) return "unknown";
            try { int value, size; return GetTokenInformation(token, 20, out value, 4, out size) ? (value == 0 ? "no" : "yes") : "unknown"; }
            finally { CloseHandle(token); }
        } finally { CloseHandle(process); }
    }
}
'@
$foregroundId = 0
[AutoPressInputDiagnostic]::GetWindowThreadProcessId([AutoPressInputDiagnostic]::GetForegroundWindow(), [ref]$foregroundId) | Out-Null
Get-Process -Name AutoPress,RobloxPlayerBeta -ErrorAction SilentlyContinue | ForEach-Object {
    [pscustomobject]@{ Name = $_.ProcessName; ProcessId = $_.Id; Elevated = [AutoPressInputDiagnostic]::Elevated($_.Id); Foreground = $_.Id -eq $foregroundId }
} | Format-Table -AutoSize
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$expected = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts\AutoPress-win-x64\AutoPress.exe'))
Get-Process -Name AutoPress -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $expected -and $_.MainWindowHandle -ne 0 } | ForEach-Object {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($_.MainWindowHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($item in $items) {
        $current = $item.Current
        if ($current.AutomationId -in @('StateText','CountText','ElapsedText','FeedbackText','TargetSummaryText')) {
            [pscustomobject]@{ Field = $current.AutomationId; Text = $current.Name }
        }
    }
} | Format-Table -AutoSize
