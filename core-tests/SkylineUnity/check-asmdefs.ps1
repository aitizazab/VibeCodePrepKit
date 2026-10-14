# Per-assembly compile check for Skyline's Unity side. SkylineUnity.Check.csproj compiles everything as ONE assembly, so a
# missing asmdef reference passes there and fails only in Unity (it happened twice). This script reads each Skyline asmdef,
# generates a project for it under core-tests/SkylineUnity/asm/ that compiles ONLY that asmdef's folder (minus nested asmdef
# folders) against ONLY the assemblies its .asmdef lists (other Skyline asmdefs as project references, not transitive; the Unity
# package DLLs from Library/ScriptAssemblies by name; the engine/editor DLLs when the asmdef uses engine references), and builds
# them in dependency order. A reference an asmdef is missing fails here with the same error Unity would give. Compile only.
#   powershell -NoProfile -File core-tests/SkylineUnity/check-asmdefs.ps1            (also: -Only JamKit.SkylineRepo.Tests)
# Needs Unity 6000.3.25f1 installed and the project opened once (Library/ScriptAssemblies), like SkylineUnity.Check.csproj.
param(
    [string]$UnityEditorDir = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor',
    [string[]]$Only = @()
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '..\..')).Path
$scriptAsm = Join-Path $root 'Library\ScriptAssemblies'
$genDir = Join-Path $here 'asm'

# The Skyline asmdefs and what they build on inside the repo (folder paths relative to the repo root).
$folders = @(
    'Packages\com.vibejam.jamkit\Runtime\Core',
    'Packages\com.vibejam.jamkit\Runtime\Skyline',
    'Packages\com.vibejam.jamkit\Runtime\Unity',
    'Packages\com.vibejam.jamkit\Tests\Core',
    'Packages\com.vibejam.jamkit\Tests\Skyline',
    'Assets\JamKit\Templates\SkylineRepo\Scripts',
    'Assets\JamKit\Templates\SkylineRepo\Editor',
    'Assets\JamKit\Templates\SkylineRepo\Editor\Tests',
    'Assets\JamKit\Templates\SkylineRepo\Editor\Recording'
)
$recorderBuilt = Test-Path (Join-Path $scriptAsm 'Unity.Recorder.Editor.dll')

$defs = @{}   # name -> @{ Name; Dir; Json }
foreach ($f in $folders) {
    $dir = Join-Path $root $f
    $file = Get-ChildItem -Path $dir -Filter '*.asmdef' -File | Select-Object -First 1
    if ($null -eq $file) { throw "no asmdef in $f" }
    $json = Get-Content -Raw -Path $file.FullName | ConvertFrom-Json
    $defs[$json.name] = @{ Name = $json.name; Dir = $dir; Json = $json }
}

# every asmdef folder in the repo (nested ones are excluded from their parent's sources)
$allAsmdefDirs = @(Get-ChildItem -Path (Join-Path $root 'Assets'), (Join-Path $root 'Packages') -Filter '*.asmdef' -File -Recurse |
    Where-Object { $_.FullName -notmatch '\\(Library|PackageCache)\\' } | ForEach-Object { $_.DirectoryName })

New-Item -ItemType Directory -Force -Path $genDir | Out-Null
Get-ChildItem -Path $genDir -Directory | Remove-Item -Recurse -Force

function Esc([string]$s) { [System.Security.SecurityElement]::Escape($s) }

$problems = @()
$order = New-Object System.Collections.Generic.List[string]
$visiting = @{}
function Visit([string]$name) {
    if ($order.Contains($name)) { return }
    if ($visiting.ContainsKey($name)) { throw "asmdef reference cycle at $name" }
    $visiting[$name] = $true
    foreach ($r in @($defs[$name].Json.references)) { if ($defs.ContainsKey($r)) { Visit $r } }
    $visiting.Remove($name)
    $order.Add($name)
}
foreach ($n in $defs.Keys) { Visit $n }

$built = @()
foreach ($name in $order) {
    $d = $defs[$name]; $j = $d.Json
    $isEditor = (@($j.includePlatforms) -contains 'Editor')
    if (@($j.defineConstraints) -contains 'JAMKIT_RECORDER' -and -not $recorderBuilt) { Write-Host "skip  $name (the Recorder's assemblies are not built; the code is #if'd out)"; continue }
    $defines = 'UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER'
    $defines += ';UNITY_EDITOR'   # Unity compiles runtime assemblies with it too, in the editor
    foreach ($c in @($j.defineConstraints)) { if ($c -and $c -ne 'JAMKIT_RECORDER') { $defines += ';' + $c } }
    if ($recorderBuilt) { $defines += ';JAMKIT_RECORDER' }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<Project Sdk="Microsoft.NET.Sdk">')
    [void]$sb.AppendLine('  <PropertyGroup>')
    [void]$sb.AppendLine('    <TargetFramework>netstandard2.1</TargetFramework><IsTestProject>false</IsTestProject>')
    [void]$sb.AppendLine('    <NoWarn>$(NoWarn);MSB3270;MSB3277;CS0649;CS0414;CS8019</NoWarn>')
    [void]$sb.AppendLine('    <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>')
    [void]$sb.AppendLine('    <AssemblyName>' + $name + '</AssemblyName><RootNamespace>' + $name + '</RootNamespace>')
    [void]$sb.AppendLine('    <DefineConstants>$(DefineConstants);' + $defines + '</DefineConstants>')
    [void]$sb.AppendLine('  </PropertyGroup>')
    [void]$sb.AppendLine('  <ItemGroup>')
    [void]$sb.AppendLine('    <PackageReference Remove="Microsoft.NET.Test.Sdk" /><PackageReference Remove="NUnit" /><PackageReference Remove="NUnit3TestAdapter" />')
    # Directory.Build.props links Shared files into every project; this one gets exactly its asmdef folder instead
    [void]$sb.AppendLine('    <Compile Remove="$(PkgRoot)Runtime\Core\Shared\**\*.cs" /><Compile Remove="$(PkgRoot)Tests\Core\Shared\**\*.cs" />')
    $nested = @($allAsmdefDirs | Where-Object { $_ -ne $d.Dir -and $_.StartsWith($d.Dir + "\") })
    $excl = ""
    if ($nested.Count -gt 0) { $excl = " Exclude=`"" + (($nested | ForEach-Object { (Esc $_) + '\**\*.cs' }) -join ";") + "`"" }
    [void]$sb.AppendLine("    <Compile Include=`"" + (Esc $d.Dir) + '\**\*.cs' + "`"" + $excl + " />")
    if (-not $j.noEngineReferences) {
        [void]$sb.AppendLine('    <Reference Include="' + (Esc $UnityEditorDir) + '\Data\Managed\UnityEngine\UnityEngine*.dll" Private="false" />')
        $ed = if ($isEditor) { 'UnityEditor*.dll' } else { 'UnityEditor.CoreModule.dll' }   # a runtime assembly still needs the editor core module (a base type of the Volume classes)
        [void]$sb.AppendLine('    <Reference Include="' + (Esc $UnityEditorDir) + '\Data\Managed\UnityEngine\' + $ed + '" Private="false" />')
    }
    foreach ($r in @($j.references)) {
        if ($defs.ContainsKey($r)) {
            if ($order.IndexOf($r) -ge 0 -and (-not ($defs[$r].Json.defineConstraints -contains 'JAMKIT_RECORDER') -or $recorderBuilt)) {
                [void]$sb.AppendLine('    <ProjectReference Include="..\' + $r + '\' + $r + '.csproj" />')
            }
            continue
        }
        $dll = Join-Path $scriptAsm ($r + '.dll')
        if (Test-Path $dll) {
            [void]$sb.AppendLine('    <Reference Include="' + (Esc $dll) + '" Private="false" />')
            # a precompiled assembly brings the assemblies it was built against (the Look library calls into Perf)
            if ($r -eq 'JamKit.Look' -or $r -eq 'JamKit.Look.Editor') {
                $perf = Join-Path $scriptAsm 'JamKit.Perf.dll'
                if (Test-Path $perf) { [void]$sb.AppendLine('    <Reference Include="' + (Esc $perf) + '" Private="false" />') }
            }
        } else {
            $problems += "$name : reference '$r' is neither a Skyline asmdef nor a built assembly in Library/ScriptAssemblies"
        }
    }
    if (@($j.precompiledReferences) -contains 'nunit.framework.dll') {
        [void]$sb.AppendLine('    <UnityNUnit Include="' + (Esc $root) + '\Library\PackageCache\com.unity.ext.nunit@*\net40\unity-custom\nunit.framework.dll" />')
        [void]$sb.AppendLine('    <Reference Include="nunit.framework"><HintPath>@(UnityNUnit)</HintPath><Private>false</Private></Reference>')
    }
    [void]$sb.AppendLine('  </ItemGroup>')
    [void]$sb.AppendLine('</Project>')
    $pdir = Join-Path $genDir $name
    New-Item -ItemType Directory -Force -Path $pdir | Out-Null
    Set-Content -Path (Join-Path $pdir ($name + '.csproj')) -Value $sb.ToString() -Encoding utf8
    $built += $name
}
if ($problems.Count -gt 0) { $problems | ForEach-Object { Write-Host "UNRESOLVED $_" }; exit 1 }

$failed = @()
foreach ($name in $built) {
    if ($Only.Count -gt 0 -and $Only -notcontains $name) { continue }
    $out = & dotnet build (Join-Path $genDir "$name\$name.csproj") --nologo -v q -p:NuGetAudit=false 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failed += $name
        Write-Host "FAIL  $name"
        $out | Where-Object { $_ -match 'error ' } | Sort-Object -Unique | Select-Object -First 15 | ForEach-Object { Write-Host "      $_" }
    } else { Write-Host "ok    $name" }
}
if ($failed.Count -gt 0) { Write-Host "Per-assembly compile check failed: $($failed -join ', ')"; exit 1 }
Write-Host 'Per-assembly compile check: every Skyline asmdef compiles against only its own references.'
