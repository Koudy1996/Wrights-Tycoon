param([switch]$Probe, [string]$ReportPath)
$ErrorActionPreference = "Stop"
Add-Type -Path (Join-Path $PSScriptRoot "IsolatedGraphicsProcess.cs")

if ($Probe) {
    Add-Type -AssemblyName System.Windows.Forms
    $form = New-Object System.Windows.Forms.Form
    try {
        $form.Text = "Wright Tycoon isolated desktop verification"
        $form.Show()
        [System.Windows.Forms.Application]::DoEvents()
        [pscustomobject]@{
            Desktop = [WrightTycoon.Workflow.IsolatedGraphicsProcess]::CurrentDesktopName()
            InputDesktop = [WrightTycoon.Workflow.IsolatedGraphicsProcess]::InputDesktopName()
            WindowCreated = ($form.Handle -ne [IntPtr]::Zero)
        } | ConvertTo-Json | Set-Content -LiteralPath $ReportPath
    }
    finally { $form.Close(); $form.Dispose() }
    exit 0
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$logsRoot = Join-Path $projectRoot "Logs/Workflow"
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
$desktopName = "WrightTycoonTests-$([Guid]::NewGuid().ToString('N'))"
$report = Join-Path $logsRoot "desktop-probe-$([Guid]::NewGuid().ToString('N')).json"
$inputBefore = [WrightTycoon.Workflow.IsolatedGraphicsProcess]::InputDesktopName()
$hostExecutable = Join-Path $PSHOME "powershell.exe"
if (-not (Test-Path -LiteralPath $hostExecutable)) { $hostExecutable = Join-Path $PSHOME "pwsh.exe" }
$arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -Probe -ReportPath "{1}"' -f $PSCommandPath, $report
$exitCode = [WrightTycoon.Workflow.IsolatedGraphicsProcess]::Run($hostExecutable, $arguments, $projectRoot, $desktopName, 30)
if ($exitCode -ne 0) { throw "Desktop probe exited with $exitCode." }
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($result.Desktop -ne $desktopName -or -not $result.WindowCreated) { throw "Probe did not create its window on the isolated desktop." }
if ($result.InputDesktop -ne $inputBefore -or [WrightTycoon.Workflow.IsolatedGraphicsProcess]::InputDesktopName() -ne $inputBefore) {
    throw "The active input desktop changed."
}
$exitCode = [WrightTycoon.Workflow.IsolatedGraphicsProcess]::Run($hostExecutable,
    '-NoLogo -NoProfile -NonInteractive -Command "exit 7"', $projectRoot, "WrightTycoonTests-$([Guid]::NewGuid().ToString('N'))", 30)
if ($exitCode -ne 7) { throw "Child failure was not propagated." }
$timedOut = $false
try {
    [void][WrightTycoon.Workflow.IsolatedGraphicsProcess]::Run($hostExecutable,
        '-NoLogo -NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"', $projectRoot,
        "WrightTycoonTests-$([Guid]::NewGuid().ToString('N'))", 1)
}
catch {
    if ($_.Exception.ToString() -notmatch 'time limit') { throw }
    $timedOut = $true
}
if (-not $timedOut) { throw "Timeout did not stop the child." }
Write-Host "Isolation checks passed: real window on hidden desktop; input desktop unchanged; exit code propagated; timeout cleanup executed."
Write-Host "Evidence: $report"
