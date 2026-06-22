[CmdletBinding()]
param(
    [ValidateSet("Test", "Checkpoint", "Release")]
    [string]$Action = "Test",

    [ValidateSet("All", "Windows", "Mac")]
    [string]$Platform = "All",

    [string]$UnityPath,

    [switch]$SkipTests,

    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$logsRoot = Join-Path $projectRoot "Logs\Workflow"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null

function Resolve-UnityEditor {
    if ($UnityPath) {
        if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
            throw "Unity editor was not found at '$UnityPath'."
        }

        return (Resolve-Path -LiteralPath $UnityPath).Path
    }

    $versionFile = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
    $versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if (-not $versionLine) {
        throw "Could not read the Unity editor version from '$versionFile'."
    }

    $version = $versionLine.Matches[0].Groups[1].Value.Trim()
    $candidate = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity $version was not found at '$candidate'. Pass -UnityPath explicitly."
    }

    return $candidate
}

function Invoke-Unity {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $script:unityEditor
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.Arguments = ($Arguments | ForEach-Object {
        '"' + $_.Replace('"', '\"') + '"'
    }) -join ' '

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($exitCode -ne 0) {
        throw "Unity exited with code $exitCode. See the workflow log for details."
    }
}

function Invoke-EditModeTests {
    $resultPath = Join-Path $logsRoot "editmode-$timestamp.xml"
    $logPath = Join-Path $logsRoot "editmode-$timestamp.log"

    Write-Host "Running the complete EditMode suite..."
    Invoke-Unity -Arguments @(
        "-batchmode",
        "-nographics",
        "-projectPath", $projectRoot,
        "-runTests",
        "-testPlatform", "EditMode",
        "-testResults", $resultPath,
        "-logFile", $logPath
    )

    [xml]$results = Get-Content -LiteralPath $resultPath -Raw
    $run = $results.'test-run'
    if (-not $run -or [int]$run.failed -gt 0) {
        throw "EditMode tests failed. Results: '$resultPath'."
    }

    Write-Host "EditMode tests passed: $($run.passed)/$($run.total) in $($run.duration)s."
}

function Get-BuildMethod {
    $mode = if ($Action -eq "Release") { "CleanRelease" } else { "Incremental" }
    switch ($Platform) {
        "All" { return "WrightTycoon.Game.Editor.WrightTycoonBuildPipeline.BuildAll$mode" }
        "Windows" { return "WrightTycoon.Game.Editor.WrightTycoonBuildPipeline.BuildWindows$mode" }
        "Mac" { return "WrightTycoon.Game.Editor.WrightTycoonBuildPipeline.BuildMac$mode" }
    }
}

function Invoke-LauncherBuild {
    $method = Get-BuildMethod
    $logPath = Join-Path $logsRoot "build-$($Action.ToLowerInvariant())-$($Platform.ToLowerInvariant())-$timestamp.log"
    Write-Host "Building $Platform launcher(s) through $method..."
    Invoke-Unity -Arguments @(
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath", $projectRoot,
        "-executeMethod", $method,
        "-logFile", $logPath
    )
}

function Invoke-WindowsSmokeTest {
    $executable = Join-Path $projectRoot "Builds\Windows\Release\WrightTycoon.exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Windows launcher was not found at '$executable'."
    }

    $logPath = Join-Path $logsRoot "windows-smoke-$timestamp.log"
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $escapedLogPath = $logPath.Replace('"', '\"')
    $startInfo.Arguments = "-batchmode -nographics -logFile `"$escapedLogPath`""

    Write-Host "Starting event-driven Windows launcher smoke test..."
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    try {
        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 200
            if (Test-Path -LiteralPath $logPath) {
                $log = Get-Content -LiteralPath $logPath -Raw
                $campaignReady = -not [string]::IsNullOrEmpty($log) -and $log.Contains("[Campaign] Loaded ")
                $launcherReady = -not [string]::IsNullOrEmpty($log) -and $log.Contains("[LaunchBootstrap] Launch runner created at runtime.")
                if ($campaignReady -and $launcherReady) {
                    Write-Host "Windows launcher smoke test passed."
                    return
                }
            }

            if ($process.HasExited) {
                throw "Windows launcher exited before reaching the ready state. See '$logPath'."
            }
        }

        throw "Windows launcher did not reach the ready state within 30 seconds. See '$logPath'."
    }
    finally {
        if (-not $process.HasExited) {
            $process.Kill()
            [void]$process.WaitForExit(5000)
        }

        $process.Dispose()
    }
}

$unityEditor = Resolve-UnityEditor
Write-Host "Unity: $unityEditor"
Write-Host "Workflow: $Action | Platform: $Platform"

if (-not $SkipTests) {
    Invoke-EditModeTests
}

if ($Action -ne "Test") {
    Invoke-LauncherBuild

    if (-not $SkipSmoke -and $Platform -in @("All", "Windows")) {
        Invoke-WindowsSmokeTest
    }
}

Write-Host "Workflow completed successfully. Logs: $logsRoot"
