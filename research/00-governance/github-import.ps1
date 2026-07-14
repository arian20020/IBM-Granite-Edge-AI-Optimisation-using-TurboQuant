param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot
)

# Stop immediately if a command fails.
$ErrorActionPreference = "Stop"

# Resolve the location of this packaged research directory.
$PackagedResearch = Resolve-Path (Join-Path $PSScriptRoot "..")

# Create the destination path inside the selected Git repository.
$Destination = Join-Path $RepositoryRoot "research"

# Avoid replacing an existing research directory without the user's decision.
if (Test-Path $Destination) {
    throw "The destination already exists: $Destination. Review or remove it before importing."
}

# Copy the complete GitHub-ready research tree.
Copy-Item -Path $PackagedResearch -Destination $Destination -Recurse

# Move into the Git repository and create one traceable documentation commit.
Push-Location $RepositoryRoot
try {
    git status --short
    git add research
    git commit -m "docs(research): add curated Granite and TurboQuant research"
    git status
}
finally {
    Pop-Location
}
