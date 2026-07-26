# Compiles a Unity-generated .csproj with Unity's bundled Roslyn.
#
# The project has no test framework, so this is the fast feedback loop for
# script changes: it catches every compile error without opening the editor.
# It does not replace Unity's console - the editor is still the authority.
#
#   powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
#   powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -Project Assembly-CSharp-Editor.csproj
#
# Known false positive on Assembly-CSharp-Editor.csproj: GooglePlayGames ships
# without an asmdef, so PluginVersion resolves from both Assembly-CSharp and
# Google.Play.Games in the flat reference list and Roslyn reports CS0433. Unity
# separates those assemblies at build time, so that one error is not real.

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
$sources = $xml.Project.ItemGroup.Compile | Where-Object { $_.Include } |
    ForEach-Object { Join-Path $root $_.Include }
$refs = $xml.Project.ItemGroup.Reference | Where-Object { $_.HintPath } |
    ForEach-Object { $_.HintPath }

if (-not $sources) { throw "No <Compile Include> entries in $Project." }

$out = Join-Path ([System.IO.Path]::GetTempPath()) ("compile-check-" + [System.IO.Path]::GetFileNameWithoutExtension($Project) + ".dll")
$rsp = Join-Path ([System.IO.Path]::GetTempPath()) 'compile-check.rsp'

$lines = @('-target:library', "-out:$out", '-nostdlib+', '-noconfig', '-langversion:9.0', '-nowarn:0169,0414,0649')
if ($defines) { $lines += "-define:$($defines -replace ';',',')" }
foreach ($r in $refs) { $lines += "-r:$r" }
foreach ($s in $sources) { $lines += "$s" }
Set-Content -Path $rsp -Value $lines -Encoding utf8

Write-Host "Compiling $Project - $($sources.Count) sources, $($refs.Count) references"
$output = & dotnet $Csc "@$rsp" 2>&1 | Out-String
$errors = $output -split "`r?`n" | Where-Object { $_ -match ': error ' }

if ($errors) {
    Write-Host $output
    Write-Host "FAILED - $($errors.Count) error(s)"
    exit 1
}

Write-Host 'OK - no compile errors'
exit 0
