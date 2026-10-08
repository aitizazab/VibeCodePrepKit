# The shell's full check, one Unity process at a time:
#   core tests (dotnet) -> rebuild shell scenes -> EditMode tests -> PlayMode tests -> Windows build -> smoke.
#   powershell -File tools/verify.ps1 [-From Scenes|EditMode|PlayMode|Build|Smoke] [-WebGL]
# Writes a one-line-per-step summary to out/verify-summary.txt. Stops at the first failing step.
# Safe to re-run (each step cleans its own outputs; builds publish only on success).
param(
    [ValidateSet('Core', 'Scenes', 'EditMode', 'PlayMode', 'Build', 'Smoke')][string]$From = 'Core',
    [ValidateSet('Core', 'Scenes', 'EditMode', 'PlayMode', 'Build', 'Smoke')][string]$To = 'Smoke',
    [switch]$WebGL
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$summary = Join-Path $OutDir 'verify-summary.txt'
"verify started $(Get-Date -Format s)" | Set-Content -Encoding utf8 $summary
$order = 'Core', 'Scenes', 'EditMode', 'PlayMode', 'Build', 'Smoke'
$start = [array]::IndexOf($order, $From)
$end = [array]::IndexOf($order, $To)

function Step([string]$name, [scriptblock]$body) {
    $i = [array]::IndexOf($order, $name)
    if ($i -lt $start -or $i -gt $end) { return }
    $t = Get-Date
    Write-Host "=== $name ==="
    $ok = & $body
    $line = "{0,-9} {1} in {2:n0} s" -f $name, $(if ($ok) { 'OK' } else { 'FAILED' }), ((Get-Date) - $t).TotalSeconds
    Add-Content -Encoding utf8 $summary $line
    Write-Host $line
    if (-not $ok) { Write-Host "Stopped at $name. Summary: $summary"; exit 1 }
}

Step 'Core' {
    & dotnet test (Join-Path $RepoRoot 'core-tests\All\All.Tests.csproj') --nologo -v q | Out-Host
    $a = $LASTEXITCODE
    & dotnet build (Join-Path $RepoRoot 'core-tests\UnityLayerEditor\UnityLayerEditor.Check.csproj') --nologo -v q | Out-Host
    $b = $LASTEXITCODE
    & dotnet build (Join-Path $RepoRoot 'core-tests\UnityLayerPlayer\UnityLayerPlayer.Check.csproj') --nologo -v q | Out-Host
    ($a -eq 0) -and ($b -eq 0) -and ($LASTEXITCODE -eq 0)
}

Step 'Scenes' {
    Assert-ProjectUnlocked
    $log = Join-Path $OutDir 'scenes.log'
    $code = Invoke-UnityBatch -UnityArgs @('-quit', '-executeMethod', 'JamKit.EditorTools.SceneBuilder.RebuildFromCommandLine') -LogFile $log
    Select-String -Path $log -Pattern 'error CS|JAMKIT_SCENES|JamKit: ' | Select-Object -Last 20 | ForEach-Object { Write-Host "  $($_.Line)" }
    ($code -eq 0) -and (Select-String -Path $log -Pattern 'JAMKIT_SCENES_OK' -Quiet)
}

Step 'EditMode' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test.ps1') -Only EditMode | Out-Host
    $LASTEXITCODE -eq 0
}

Step 'PlayMode' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'test.ps1') -Only PlayMode | Out-Host
    $LASTEXITCODE -eq 0
}

Step 'Build' {
    $target = if ($WebGL) { 'All' } else { 'Windows' }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Target $target | Out-Host
    $ok = $LASTEXITCODE -eq 0
    foreach ($t in 'Windows', 'WebGL') {
        $dir = Join-Path $RepoRoot "Builds\$t"
        if (Test-Path $dir) {
            $mb = (Get-ChildItem $dir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
            Add-Content -Encoding utf8 $summary ("  {0} build: {1:n1} MB" -f $t, $mb)
        }
    }
    $ok
}

Step 'Smoke' {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'smoke.ps1') -Seconds 20 | Out-Host
    $LASTEXITCODE -eq 0
}

Add-Content -Encoding utf8 $summary "verify finished $(Get-Date -Format s): all OK"
Write-Host "All steps passed. Summary: $summary"
