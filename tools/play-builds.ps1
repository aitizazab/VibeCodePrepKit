# One playable Windows build per template, for feel tests without opening the editor.
#   powershell -File tools/play-builds.ps1 [-Templates BulletHell,Doomlike] [-RestoreTo Doomlike]
# Each: switch template -> Windows build -> copy to Builds/Play/<Template>/. Restores the active
# template at the end so tracked files (GameConfig, EditorBuildSettings) end where they started.
param(
    [string[]]$Templates = @('BulletHell', 'Doomlike', 'Story'),
    [string]$RestoreTo = 'Story'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
# powershell -File passes "A,B,C" as one string; accept both forms
$Templates = @($Templates | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$switch = @{
    Sandbox    = 'JamKit.Sandbox.EditorTools.SandboxTemplate.UseFromCommandLine'
    BulletHell = 'JamKit.BulletHell.EditorTools.BulletHellTemplate.UseFromCommandLine'
    Doomlike   = 'JamKit.Doomlike.EditorTools.DoomlikeTemplate.UseFromCommandLine'
    Story      = 'JamKit.Narrative.EditorTools.StoryTemplate.UseFromCommandLine'
    VisitingHours = 'JamKit.VisitingHours.EditorTools.VisitingHoursTemplate.UseFromCommandLine'
}
function Use-Template([string]$name) {
    Assert-ProjectUnlocked
    $log = Join-Path $OutDir "template-$name.log"
    $code = Invoke-UnityBatch -UnityArgs @('-quit', '-executeMethod', $switch[$name]) -LogFile $log
    if ($code -ne 0 -or -not (Select-String -Path $log -Pattern 'JAMKIT_TEMPLATE_OK' -Quiet)) { throw "Switching to $name failed: see $log" }
}
$failed = @()
foreach ($t in $Templates) {
    try {
        Use-Template $t
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Target Windows | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Windows build failed for $t" }
        $dest = Join-Path $RepoRoot "Builds\Play\$t"
        if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        Copy-Item (Join-Path $RepoRoot 'Builds\Windows') $dest -Recurse
        Write-Host "${t}: ready -> $dest"
    } catch { $failed += $t; Write-Host "${t}: FAILED - $($_.Exception.Message)" }
}
if ($RestoreTo) { Use-Template $RestoreTo; Write-Host "Active template restored: $RestoreTo" }
if ($failed.Count -gt 0) { exit 1 }
