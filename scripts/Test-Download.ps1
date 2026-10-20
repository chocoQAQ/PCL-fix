[CmdletBinding()]
param(
    [string]$Version = '1.21.1',
    [switch]$FullInstall,
    [string]$LauncherPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (-not $LauncherPath) { $LauncherPath = "$root/Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe" }
$work = Join-Path $root 'artifacts\download-test'
New-Item $work -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $LauncherPath -Destination "$work/Plain Craft Launcher 2.exe" -Force
Copy-Item -LiteralPath "$LauncherPath.config" -Destination "$work/DownloadSmoke.exe.config" -Force

$framework = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$wpf = "$framework/WPF"
$references = @(
    "$work/Plain Craft Launcher 2.exe",
    "$root/PCLCS/bin/PCLCS.dll",
    "$root/PCLCS/bin/MeloongCore.dll",
    "$root/PCLCS/bin/MeloongCore.Wpf.dll",
    "$root/Plain Craft Launcher 2/Resources/Newtonsoft.Json.dll",
    "$wpf/WindowsBase.dll",
    "$wpf/PresentationCore.dll",
    "$wpf/PresentationFramework.dll",
    "$framework/System.Xaml.dll",
    "${env:ProgramFiles(x86)}/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Facades/netstandard.dll"
)
$compilerArgs = @('/nologo', '/target:exe', '/platform:anycpu', "/out:$work\DownloadSmoke.exe")
$compilerArgs += $references | ForEach-Object { "/reference:$([IO.Path]::GetFullPath($_))" }
$compilerArgs += (Join-Path $PSScriptRoot 'DownloadSmoke.cs')
& "$framework/csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Download smoke test compilation failed.' }

& "$work/DownloadSmoke.exe" $work $Version ([bool]$FullInstall).ToString() 2>&1 | Tee-Object "$work/smoke.log"
if ($LASTEXITCODE -ne 0) { throw "Built launcher failed the download test. See $work/smoke.log." }
