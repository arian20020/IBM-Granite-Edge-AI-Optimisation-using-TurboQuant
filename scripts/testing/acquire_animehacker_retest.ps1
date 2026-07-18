param(
    [string]$RepositoryUrl = 'https://github.com/animehacker/llama-turboquant.git',
    [string]$UpstreamUrl = 'https://github.com/ggml-org/llama.cpp.git',
    [string]$CheckoutPath = 'C:\Users\Student\experiments\animehacker-tq3-0-2026-07-18',
    [string]$EvidencePath = 'experiments\raw-results\animehacker-tq3-0\2026-07-18\acquisition'
)

$ErrorActionPreference = 'Stop'
$head = git ls-remote --symref $RepositoryUrl HEAD
if ($LASTEXITCODE -ne 0) { throw 'Unable to query remote HEAD' }
$branchLine = $head | Where-Object { $_ -like 'ref:*HEAD' }
$shaLine = $head | Where-Object { $_ -match '^[0-9a-f]{40}\s+HEAD$' }
$branch = (($branchLine -split '\s+')[1] -replace '^refs/heads/', '')
$sha = ($shaLine -split '\s+')[0]

if (-not (Test-Path -LiteralPath $CheckoutPath)) {
    git clone --filter=blob:none --branch $branch $RepositoryUrl $CheckoutPath
    if ($LASTEXITCODE -ne 0) { throw 'Clone failed' }
}
git -C $CheckoutPath fetch origin $branch
git -C $CheckoutPath switch --detach $sha
if ($LASTEXITCODE -ne 0) { throw 'Unable to pin checkout' }
$status = @(git -C $CheckoutPath status --porcelain)
if ($status.Count -ne 0) { throw 'Pinned checkout is not clean' }
if (-not (git -C $CheckoutPath remote get-url upstream 2>$null)) {
    git -C $CheckoutPath remote add upstream $UpstreamUrl
}
git -C $CheckoutPath fetch --filter=blob:none upstream master
if ($LASTEXITCODE -ne 0) { throw 'Unable to fetch upstream base history' }
$upstreamBase = git -C $CheckoutPath merge-base $sha upstream/master

New-Item -ItemType Directory -Force -Path $EvidencePath | Out-Null
$metadata = [ordered]@{
    repository_url = $RepositoryUrl
    default_branch = $branch
    commit = $sha
    upstream_url = $UpstreamUrl
    upstream_base_commit = $upstreamBase
    checkout_path = $CheckoutPath
    acquired_utc = (Get-Date).ToUniversalTime().ToString('o')
    clean = $true
    submodules = @(git -C $CheckoutPath submodule status)
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidencePath 'repository.json') -Encoding utf8
