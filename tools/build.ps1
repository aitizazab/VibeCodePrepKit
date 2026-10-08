# Builds the game in batch mode.
#   powershell -File tools/build.ps1 -Target Windows|WebGL|All [-Development]
# Each target builds into Builds/.staging-<target> and replaces Builds/<target> only on success,
# so a failed or killed build never leaves a half-written folder behind. Safe to re-run.
param(
    [ValidateSet('Windows', 'WebGL', 'All')][string]$Target = 'All',
    [switch]$Development
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked

$targets = if ($Target -eq 'All') { @('Windows', 'WebGL') } else { @($Target) }
$builds = Join-Path $RepoRoot 'Builds'
$failed = @()

foreach ($t in $targets) {
    $staging = Join-Path $builds ".staging-$t"
    $final = Join-Path $builds $t
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    $log = Join-Path $OutDir "build-$t.log"
    $unityArgs = @('-quit', '-executeMethod', 'JamKit.EditorTools.BuildScript.BuildFromCommandLine',
                   '-jamTarget', $t, '-jamOut', "`"$staging`"")
    if ($Development) { $unityArgs += '-jamDev' }
    $started = Get-Date
    $code = Invoke-UnityBatch -UnityArgs $unityArgs -LogFile $log
    $ok = ($code -eq 0) -and (Select-String -Path $log -Pattern 'JAMKIT_BUILD_OK' -Quiet)
    if ($ok) {
        if (Test-Path $final) { Remove-Item -Recurse -Force $final }
        Move-Item $staging $final
        Write-Host ("{0}: OK in {1:n0} s -> {2}" -f $t, ((Get-Date) - $started).TotalSeconds, $final)
    } else {
        $failed += $t
        Write-Host "${t}: FAILED (exit $code). Last errors from $($log):"
        Select-String -Path $log -Pattern 'error CS|JAMKIT_BUILD_FAIL|Error building|BuildFailedException' |
            Select-Object -Last 15 | ForEach-Object { Write-Host "  $($_.Line)" }
        if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    }
}
if ($failed.Count -gt 0) { exit 1 }
