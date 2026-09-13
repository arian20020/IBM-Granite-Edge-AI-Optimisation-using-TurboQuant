[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidencePath,
    [string]$TrustedRunMetadataPath = ''
)

$ErrorActionPreference = 'Stop'
function Stop-Invalid {
    [Console]::Out.WriteLine('ucl_evidence_binding_invalid')
    exit 1
}
function Test-ExactProperties($Value, [string[]]$Names) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { return $false }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) { return $false }
    for ($index = 0; $index -lt $Names.Count; $index++) {
        if ($actual[$index] -cne $Names[$index]) { return $false }
    }
    return $true
}

try {
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
    $privacy = & powershell.exe -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-OpenVinoEvidencePrivacy.ps1') `
        -EvidencePath $EvidencePath
    if ($LASTEXITCODE -ne 0 -or [string]$privacy -cne 'evidence_privacy_valid') {
        throw 'cpu-evidence-invalid'
    }
    $evidenceRaw = Get-OpenVinoClosedJsonText -Path $EvidencePath `
        -MaximumBytes 16384 -MaximumDepth 8
    $evidence = $evidenceRaw | ConvertFrom-Json -ErrorAction Stop
    $raw = Get-OpenVinoClosedJsonText -Path $TrustedRunMetadataPath `
        -MaximumBytes 4096 -MaximumDepth 3
    $metadata = $raw | ConvertFrom-Json -ErrorAction Stop
    $names = @('schemaVersion','provider','workflowPath','eventName','commitSha',
        'runId','runAttempt','jobName','artifactName','evidenceSha256')
    if (-not (Test-ExactProperties $metadata $names) -or
        $metadata.schemaVersion -isnot [int] -or $metadata.schemaVersion -ne 1 -or
        $metadata.provider -cne 'github-actions' -or
        $metadata.workflowPath -cne '.github/workflows/openvino-ucl-intel.yml' -or
        $metadata.eventName -cne 'workflow_dispatch' -or
        $metadata.commitSha -isnot [string] -or
        $metadata.commitSha -cnotmatch '^[0-9a-f]{40}$' -or
        $metadata.runId -isnot [int] -or $metadata.runId -le 0 -or
        $metadata.runAttempt -isnot [int] -or $metadata.runAttempt -le 0 -or
        $metadata.jobName -cne 'trusted-intel-cpu' -or
        $metadata.artifactName -cne 'openvino-ucl-intel-cpu-evidence' -or
        $metadata.evidenceSha256 -isnot [string] -or
        $metadata.evidenceSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $evidence.commitSha -isnot [string] -or
        $evidence.commitSha -cne $metadata.commitSha) {
        throw 'run-metadata-invalid'
    }
    $digest = (Get-FileHash -LiteralPath $EvidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($digest -cne $metadata.evidenceSha256) { throw 'evidence-digest-mismatch' }
    [Console]::Out.WriteLine('ucl_evidence_binding_valid_not_authenticated')
    exit 0
}
catch {
    Stop-Invalid
}
