# Registers Unity's Smart Merge (UnityYAMLMerge) as the git merge driver for scenes, prefabs and
# assets (.gitattributes points them at it). Run once per clone, after Unity is installed.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\unity-env.ps1"
$editorDir = Split-Path (Get-UnityEditorPath)
$merge = Join-Path $editorDir 'Data\Tools\UnityYAMLMerge.exe'
if (-not (Test-Path $merge)) { throw "UnityYAMLMerge not found at $merge" }
$mergeFwd = $merge -replace '\\', '/'
git -C $RepoRoot config merge.unityyamlmerge.name 'Unity SmartMerge (UnityYAMLMerge)'
git -C $RepoRoot config merge.unityyamlmerge.driver "'$mergeFwd' merge -h -p --force %O %B %A %A"
git -C $RepoRoot config merge.unityyamlmerge.recursive binary
Write-Host "Smart Merge registered: $merge"
