# Compiles a Unity-generated .csproj with Unity's bundled Roslyn.
#
# The project has no test framework, so this is the fast feedback loop for
# script changes: it catches every compile error without opening the editor.
# It does not replace Unity's console - the editor is still the authority.
#
#   powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -Project Assembly-CSharp-Editor.csproj
#
# Editor assemblies reference the runtime assembly through <ProjectReference>
# rather than a HintPath, so those are resolved to their built DLLs under
# Library/ScriptAssemblies. That directory holds Unity's last successful build,
# which can be stale - if a referenced project changed since Unity last
# compiled, run this on that project first, or open the editor.

param(
    [string]$Project = 'Assembly-CSharp.csproj',
    [string]$Csc = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data\DotNetSdkRoslyn\csc.dll'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root $Project

if (-not (Test-Path $projectPath)) { throw "Project not found: $projectPath" }
if (-not (Test-Path $Csc)) { throw "Roslyn not found: $Csc. Pass -Csc with your Unity version's path." }

[xml]$xml = Get-Content $projectPath

$defines = ($xml.Project.PropertyGroup | Where-Object { $_.DefineConstants } |
    Select-Object -First 1).DefineConstants
$sources = @($xml.Project.ItemGroup.Compile | Where-Object { $_.Include } |
    ForEach-Object { Join-Path $root $_.Include })

# Unity regenerates the csproj files, so between editor sessions the Compile
# list drifts from what is on disk: deleted scripts linger and new ones are
# absent. Reconcile against Assets/Scripts, which is where this project's own
# code lives - without this the check silently skips brand new files, which is
# exactly where errors are most likely.
$scriptRoot = Join-Path $root 'Assets\Scripts'
$editorRoot = Join-Path $scriptRoot 'Editor'
$onDisk = @(Get-ChildItem -Path $scriptRoot -Filter *.cs -Recurse -File |
    ForEach-Object { $_.FullName })
$isEditorProject = $Project -like '*Editor*'
$owned = @($onDisk | Where-Object {
    $inEditor = $_.StartsWith($editorRoot, [System.StringComparison]::OrdinalIgnoreCase)
    if ($isEditorProject) { $inEditor } else { -not $inEditor }
})

$stale = @($sources | Where-Object { -not (Test-Path $_) })
$sources = @($sources | Where-Object { Test-Path $_ })
$added = @($owned | Where-Object { $sources -notcontains $_ })
$sources = @($sources) + $added
$refs = [System.Collections.Generic.List[string]]::new()
# HintPaths to the Unity install are absolute; package ones are repo-relative.
foreach ($r in $xml.Project.ItemGroup.Reference) {
    if (-not $r.HintPath) { continue }
    if ([System.IO.Path]::IsPathRooted($r.HintPath)) { $refs.Add($r.HintPath) }
    else { $refs.Add((Join-Path $root $r.HintPath)) }
}

$outDir = Join-Path ([System.IO.Path]::GetTempPath()) 'unity-compile-check'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# The editor assembly reaches the game's runtime types through a
# ProjectReference on Assembly-CSharp. Library/ScriptAssemblies only holds
# whatever Unity built last, so on a stale copy the editor code still compiles
# against types the working tree has already deleted. Build the runtime
# assembly first and prefer that fresh output.
if ($isEditorProject -and $Project -ne 'Assembly-CSharp.csproj') {
    Write-Host 'Building Assembly-CSharp first so editor code compiles against current runtime types...'
    & powershell -ExecutionPolicy Bypass -File $PSCommandPath -Project 'Assembly-CSharp.csproj' -Csc $Csc | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'FAILED - the runtime assembly does not compile; fix that before checking the editor assembly.'
        exit 1
    }
}

# <ProjectReference Include="Other.csproj" /> resolves to the DLL built for that
# assembly: this script's fresh output when present, otherwise Unity's.
$scriptAssemblies = Join-Path $root 'Library\ScriptAssemblies'
$missingProjects = [System.Collections.Generic.List[string]]::new()
foreach ($p in $xml.Project.ItemGroup.ProjectReference) {
    if (-not $p.Include) { continue }
    $name = [System.IO.Path]::GetFileNameWithoutExtension($p.Include) + '.dll'
    $fresh = Join-Path $outDir $name
    $unity = Join-Path $scriptAssemblies $name
    if (Test-Path $fresh) { $refs.Add($fresh) }
    elseif (Test-Path $unity) { $refs.Add($unity) }
    else { $missingProjects.Add($p.Include) }
}

if (-not $sources) { throw "No <Compile Include> entries in $Project." }

$out = Join-Path $outDir ([System.IO.Path]::GetFileNameWithoutExtension($Project) + '.dll')
$rsp = Join-Path $outDir ([System.IO.Path]::GetFileNameWithoutExtension($Project) + '.rsp')
if (Test-Path $out) { Remove-Item $out -Force }

# Every path is quoted: reference paths run through "C:\Program Files\...".
# The response file must be BOM-less UTF-8 or csc reads the BOM as part of the
# first argument.
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('-target:library')
$lines.Add("-out:`"$out`"")
$lines.Add('-nostdlib+')
$lines.Add('-langversion:9.0')
$lines.Add('-nowarn:0169,0414,0649')
$lines.Add('-preferreduilang:en-US')
if ($defines) { $lines.Add("-define:$($defines -replace ';',',')") }
foreach ($r in $refs) { $lines.Add("-r:`"$r`"") }
foreach ($s in $sources) { $lines.Add("`"$s`"") }
[System.IO.File]::WriteAllLines($rsp, $lines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Compiling $Project - $($sources.Count) sources, $($refs.Count) references"
if ($stale.Count -gt 0) {
    Write-Host "Listed in the csproj but gone from disk ($($stale.Count)) - stale csproj, skipped:"
    foreach ($s in $stale) { Write-Host "  $([System.IO.Path]::GetFileName($s))" }
}
if ($added.Count -gt 0) {
    Write-Host "On disk but missing from the csproj ($($added.Count)) - added:"
    foreach ($a in $added) { Write-Host "  $([System.IO.Path]::GetFileName($a))" }
}
if ($missingProjects.Count -gt 0) {
    Write-Host "Referenced projects with no built DLL in Library/ScriptAssemblies (types from these will be unresolved):"
    foreach ($m in $missingProjects) { Write-Host "  $m" }
}

$output = & dotnet $Csc "@$rsp" 2>&1 | Out-String
$exit = $LASTEXITCODE

# Trust the exit code and the artifact, not a text search. An earlier version of
# this script grepped stdout for ': error ' and reported OK when csc had in fact
# never run - it passed a BOM-prefixed response file with unquoted paths, so
# nothing compiled and nothing was printed.
if ($exit -ne 0 -or -not (Test-Path $out)) {
    Write-Host $output
    if ($exit -eq 0) { Write-Host "csc exited 0 but produced no assembly at $out" }
    Write-Host "FAILED"
    exit 1
}

Write-Host $output.Trim()
Write-Host "OK - no compile errors ($([System.IO.Path]::GetFileName($out)) written)"
exit 0
