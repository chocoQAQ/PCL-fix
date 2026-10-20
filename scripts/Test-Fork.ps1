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
if ($secret -notmatch 'Public ReadOnly OAuthClientId As String = If\(Environment.GetEnvironmentVariable\("PCL_MS_CLIENT_ID"\), "e1dc8448-c3a0-43df-8ebd-bb146c003941"\)') {
    throw 'The registered public Microsoft client ID or its override changed. Review authentication before publishing.'
}
$setup = Get-Content "$root/Plain Craft Launcher 2/Pages/PageSetup/PageSetupUI.xaml" -Raw -Encoding UTF8
if ($setup -match 'UiLauncherTheme|PanLauncherTheme|LabLauncherTheme' -or $secret -match 'Function ThemeUnlock|Sub ThemeCheck') {
    throw 'Theme selection or unlock code was reintroduced. The fork keeps the default blue interface.'
}
$base = Get-Content "$root/Plain Craft Launcher 2/Modules/Base/ModBase.vb" -Raw -Encoding UTF8
if ($base -notmatch 'Public Const VersionDisplay As String = "PCL (下载)?修复版 ') { throw 'Missing fork version label.' }
$about = Get-Content "$root/Plain Craft Launcher 2/Pages/PageOther/PageOtherAbout.xaml" -Raw -Encoding UTF8
if ($about -notmatch '第三方修复版' -or $about -notmatch 'https://github.com/chocoQAQ/PCL-fix') {
    throw 'Missing third-party attribution or fork source link.'
}
if ($secret -notmatch 'OpenWebsite\("https://github.com/chocoQAQ/PCL-fix/releases/latest"\)') {
    throw 'The update button must point to fork releases.'
}
Write-Host 'Fork download policy, Microsoft client ID, default interface, identity, settings isolation and release link verified.'
