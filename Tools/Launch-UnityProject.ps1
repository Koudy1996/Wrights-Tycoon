param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$UnityPath = ""
)

function Resolve-UnityPath {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path $ExplicitPath)) {
        return (Resolve-Path $ExplicitPath).Path
    }

    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe",
        "C:\Program Files\Unity\Editor\Unity.exe",
        "C:\Program Files (x86)\Unity\Editor\Unity.exe"
    )

    foreach ($pattern in $candidates) {
        $matches = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
    }

    return $null
}

$resolvedUnity = Resolve-UnityPath -ExplicitPath $UnityPath
if (-not $resolvedUnity) {
    Write-Host "Unity Editor not found. Install a Unity 6 LTS editor in Unity Hub first."
    exit 1
}

if (-not (Test-Path $ProjectPath)) {
    Write-Host "Project path not found: $ProjectPath"
    exit 1
}

Write-Host "Launching Unity:"
Write-Host "  Editor: $resolvedUnity"
Write-Host "  Project: $ProjectPath"

Start-Process -FilePath $resolvedUnity -ArgumentList "-projectPath `"$ProjectPath`""
