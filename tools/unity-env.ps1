# Shared helpers, dot-sourced by the other tools:  . "$PSScriptRoot\unity-env.ps1"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$OutDir = Join-Path $RepoRoot 'out'

function Get-ProjectUnityVersion {
    $pv = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $pv)) { return $null }
    $line = Get-Content $pv | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    if ($line -match '^m_EditorVersion:\s*(\S+)') { return $Matches[1] }
    return $null
}

function Get-UnityEditorRoots {
    $roots = @('C:\Program Files\Unity\Hub\Editor')
    # The Hub's custom install location, if one was set
    $secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path $secondary) {
        try {
            $p = Get-Content $secondary -Raw | ConvertFrom-Json
            if ($p) { $roots += [string]$p }
        } catch {}
    }
    return $roots | Where-Object { $_ -and (Test-Path $_) }
}

# The editor matching ProjectVersion.txt, else the newest Unity 6 installed.
function Get-UnityEditorPath {
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) { return $env:UNITY_EDITOR }
    $want = Get-ProjectUnityVersion
    $found = @()
    foreach ($root in Get-UnityEditorRoots) {
        foreach ($dir in Get-ChildItem $root -Directory -ErrorAction SilentlyContinue) {
            $exe = Join-Path $dir.FullName 'Editor\Unity.exe'
            if (Test-Path $exe) { $found += [pscustomobject]@{ Version = $dir.Name; Exe = $exe } }
        }
    }
    if ($want) {
        $match = $found | Where-Object { $_.Version -eq $want } | Select-Object -First 1
        if ($match) { return $match.Exe }
        Write-Warning "Project wants Unity $want, which isn't installed; trying the newest Unity 6."
    }
    $newest = $found | Where-Object { $_.Version -like '6000.*' } | Sort-Object { [version](($_.Version -replace '[a-z].*$', '')) } | Select-Object -Last 1
    if ($newest) { return $newest.Exe }
    throw 'No Unity 6 editor found. Install one through Unity Hub (or set $env:UNITY_EDITOR).'
}

# Batch mode can't open a project the editor already has open.
function Assert-ProjectUnlocked {
    $lock = Join-Path $RepoRoot 'Temp\UnityLockfile'
    if (Test-Path $lock) {
        try {
            $s = [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None')
            $s.Close()
        } catch {
            throw 'The Unity editor has this project open. Close it, or work through the MCP link instead of batch mode.'
        }
    }
}

# Runs Unity in batch mode and waits (Unity.exe is a GUI app, so & would return at once).
function Invoke-UnityBatch {
    param([string[]]$UnityArgs, [string]$LogFile)
    $unity = Get-UnityEditorPath
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    $all = @('-batchmode', '-nographics', '-projectPath', "`"$RepoRoot`"", '-logFile', "`"$LogFile`"") + $UnityArgs
    Write-Host "Unity: $unity"
    Write-Host "Args : $($all -join ' ')"
    # Not -Wait: in Windows PowerShell that waits for the whole process tree, and Unity leaves its
    # C# compiler server (VBCSCompiler) idling for minutes after it exits.
    $p = Start-Process -FilePath $unity -ArgumentList $all -PassThru -NoNewWindow
    $null = $p.Handle   # keeps ExitCode readable after exit
    $p.WaitForExit()
    return $p.ExitCode
}
