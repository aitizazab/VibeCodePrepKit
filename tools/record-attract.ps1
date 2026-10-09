# Records attract mode (the active template playing itself) to an MP4 with Unity Recorder.
#   powershell -File tools/record-attract.ps1 [-Seconds 60] [-Out Recordings/broll.mp4]
# Opens the Unity editor WITH graphics (the Recorder can't capture under -batchmode or -nographics:
# no graphics pipeline), enters Play mode, starts attract mode, records 1080p at a constant 60 fps for
# -Seconds of video, then quits. A desktop session and a GPU are needed; the editor window shows
# while it records (don't click into it). Heavy: run it through the memory cap like a build, and not
# while another GPU job runs. Exit code 0 with the file path, 1 on failure (out/record-attract.log).
param(
    [double]$Seconds = 60,
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
Assert-ProjectUnlocked
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir 'record-attract.log'
if (Test-Path $log) { Remove-Item $log -Force }
$unity = Get-UnityEditorPath
$secs = $Seconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$all = @('-projectPath', "`"$RepoRoot`"", '-logFile', "`"$log`"",
    '-executeMethod', 'JamKit.EditorTools.Recording.JamKitRecorder.RecordAttractFromCommandLine',
    '-jamRecordSeconds', $secs)
if ($Out) { $all += @('-jamRecordOut', "`"$((Join-Path $RepoRoot $Out))`"") }
Write-Host "Unity: $unity"
Write-Host "Args : $($all -join ' ')"
$p = Start-Process -FilePath $unity -ArgumentList $all -PassThru
$null = $p.Handle
$p.WaitForExit()
$code = $p.ExitCode
if (Test-Path $log) {
    Select-String -Path $log -Pattern 'JAMKIT_RECORD|JamKit presenter|error CS' | Select-Object -Last 20 | ForEach-Object { Write-Host "  $($_.Line)" }
}
$ok = ($code -eq 0) -and (Test-Path $log) -and (Select-String -Path $log -Pattern 'JAMKIT_RECORD_OK' -Quiet)
if ($ok) { Write-Host 'Recording OK (see the JAMKIT_RECORD_OK line for the file).'; exit 0 }
Write-Host "Recording FAILED (exit $code). Log: $log"
exit 1
