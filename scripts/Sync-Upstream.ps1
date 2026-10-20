[CmdletBinding()]
param([string]$UpstreamUrl = 'https://github.com/Meloong-Git/PCL.git')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path $PSScriptRoot -Parent)

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed ($LASTEXITCODE)." }
}

if (git status --porcelain) { throw 'Sync requires a clean working tree.' }
Invoke-Git fetch --no-tags $UpstreamUrl main
$upstream = (git rev-parse FETCH_HEAD).Trim()
git merge-base --is-ancestor $upstream HEAD
if ($LASTEXITCODE -eq 0) {
    Write-Host 'Upstream is already included; no update is needed.'
    if ($env:GITHUB_OUTPUT) { 'changed=false' >> $env:GITHUB_OUTPUT }
    exit 0
}
if ($LASTEXITCODE -ne 1) { throw 'Unable to compare upstream history.' }

git merge --no-ff --no-commit $upstream
if ($LASTEXITCODE -ne 0) {
    $conflicts = git diff --name-only --diff-filter=U
    git merge --abort
    throw "Upstream merge conflicts require review. main is unchanged. Conflicts: $($conflicts -join ', ')"
}

try {
    & "$PSScriptRoot/Test-Fork.ps1"
    Invoke-Git submodule update --init --recursive
    Invoke-Git -c 'user.name=github-actions[bot]' -c 'user.email=41898282+github-actions[bot]@users.noreply.github.com' commit -m "Merge upstream $upstream and preserve the download fix"
} catch {
    git merge --abort
    throw
}
if ($env:GITHUB_OUTPUT) { 'changed=true' >> $env:GITHUB_OUTPUT }
Write-Host "Merged upstream $upstream locally; build and download checks must pass before pushing."
