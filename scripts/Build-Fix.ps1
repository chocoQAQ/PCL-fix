[CmdletBinding()]
param([string]$MSBuildPath = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
& "$PSScriptRoot/Test-Fork.ps1"

if (-not $MSBuildPath) {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) { $MSBuildPath = $command.Source }
    else {
        $vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
        if (Test-Path $vswhere) {
            $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
        }
    }
}
if (-not $MSBuildPath -or -not (Test-Path $MSBuildPath)) { throw 'Install Visual Studio Build Tools with .NET desktop build tools and the .NET Framework 4.8 targeting pack.' }

$artifacts = Join-Path $root artifacts
New-Item $artifacts -ItemType Directory -Force | Out-Null
& $MSBuildPath 'Plain Craft Launcher 2.sln' -restore -m -nologo -verbosity:minimal -p:Configuration=Release -fl "-flp:logfile=$artifacts/build.log;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Release build failed. See $artifacts/build.log." }

$commit = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the build commit.' }
$package = Join-Path $artifacts "PCL-fix-$($commit.Substring(0, 12))"
New-Item $package -ItemType Directory -Force | Out-Null
Copy-Item "$root/Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe" $package -Force
Copy-Item "$root/Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe.config" $package -Force
Copy-Item "$root/LICENCE", "$root/README.md" $package -Force
@{
    repository = 'https://github.com/chocoQAQ/PCL-fix'
    commit = $commit
    dirty = [bool](git status --porcelain)
    upstream = 'https://github.com/Meloong-Git/PCL'
    built_at_utc = [DateTime]::UtcNow.ToString('o')
    configuration = 'Release'
} | ConvertTo-Json | Set-Content "$package/build-info.json" -Encoding UTF8
$zip = "$package.zip"
Compress-Archive -LiteralPath (Get-ChildItem $package -File).FullName -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$artifacts/SHA256SUMS.txt" -Encoding ascii
Write-Host "Built $zip"
