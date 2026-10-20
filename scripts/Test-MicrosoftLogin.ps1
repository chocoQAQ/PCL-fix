[CmdletBinding()]
param(
    [string]$LauncherPath = '',
    [switch]$Interactive,
    [switch]$Probe
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Interactive -and $Probe) { throw 'Choose either -Interactive or -Probe.' }
$root = Split-Path $PSScriptRoot -Parent
if (-not $LauncherPath) {
    $commit = (git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the build commit.' }
    $LauncherPath = "$root/artifacts/PCL-fix-$($commit.Substring(0, 12)).exe"
}
$work = Join-Path $root 'artifacts\auth-test'
New-Item $work -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $LauncherPath -Destination "$work/Plain Craft Launcher 2.exe" -Force
$framework = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$wpf = "$framework/WPF"
$references = @(
    "$work/Plain Craft Launcher 2.exe",
    "$root/PCLCS/bin/PCLCS.dll",
    "$root/PCLCS/bin/MeloongCore.dll",
    "$root/PCLCS/bin/MeloongCore.Wpf.dll",
    "$root/Plain Craft Launcher 2/Resources/Newtonsoft.Json.dll",
    "$wpf/WindowsBase.dll", "$wpf/PresentationCore.dll", "$wpf/PresentationFramework.dll",
    "$framework/System.Xaml.dll", "$framework/System.Net.Http.dll",
    "${env:ProgramFiles(x86)}/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Facades/netstandard.dll"
)
$compilerArgs = @('/nologo', '/target:exe', '/platform:anycpu', "/out:$work\AuthSmoke.exe")
$compilerArgs += $references | ForEach-Object { "/reference:$([IO.Path]::GetFullPath($_))" }
$compilerArgs += (Join-Path $PSScriptRoot 'AuthSmoke.cs')
& "$framework/csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Auth test compilation failed.' }
$mode = if ($Interactive) { 'interactive' } elseif ($Probe) { 'probe' } else { 'regression' }
# Interactive tokens are never written to disk or passed as command-line arguments.
& "$work/AuthSmoke.exe" $work $mode
if ($LASTEXITCODE -ne 0) { throw "Built launcher failed the Microsoft login test ($mode)." }
