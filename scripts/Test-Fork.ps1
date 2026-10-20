[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$secret = Get-Content "$root/Plain Craft Launcher 2/Modules/ModSecret.vb" -Raw -Encoding UTF8
if ($secret -notmatch '(?m)^\s*Public Const RestrictMcDownload As Boolean = False\s*$') {
    throw 'Minecraft downloads are restricted or the upstream download policy changed. Review the fix before publishing.'
}
if ($secret -notmatch 'Public Const RegFolder As String = "PCLFix"') {
    throw 'The fork must keep its own registry settings.'
}
$base = Get-Content "$root/Plain Craft Launcher 2/Modules/Base/ModBase.vb" -Raw -Encoding UTF8
if ($base -notmatch 'Public Const VersionDisplay As String = "PCL 下载修复版 ') { throw 'Missing fork version label.' }
$about = Get-Content "$root/Plain Craft Launcher 2/Pages/PageOther/PageOtherAbout.xaml" -Raw -Encoding UTF8
if ($about -notmatch '第三方修复版' -or $about -notmatch 'https://github.com/chocoQAQ/PCL-fix') {
    throw 'Missing third-party attribution or fork source link.'
}
if ($secret -notmatch 'OpenWebsite\("https://github.com/chocoQAQ/PCL-fix/releases/latest"\)') {
    throw 'The update button must point to fork releases.'
}
Write-Host 'Fork download policy, identity, settings isolation and release link verified.'
