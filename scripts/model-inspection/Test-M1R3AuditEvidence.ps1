[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ManifestPath,
    [string] $ReceiptPath,
    [string] $NativeReceiptPath,
    [string] $RepositoryRoot,
    [string] $ExpectedFrozenCommit = '4748fe04f19afdf6b27c4c12502b84db325e7294',
    [string] $ExpectedFrozenTree = 'fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91',
    [string] $ExpectedBranch = 'audit/ucl-m1-model-inspection-remediation-r3'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:MaximumJsonBytes = 4MB
$script:ExpectedReportPath = 'docs/audits/2026-08-28/M1-model-inspection-remediation-r3.md'
$script:ExpectedManifestPath = 'docs/audits/2026-08-28/evidence/M1-model-inspection-evidence-r3.json'
$script:EvidenceSchemaPath = 'docs/audits/2026-08-28/schemas/10-EVIDENCE-MANIFEST-SCHEMA.json'
$script:ReceiptSchemaPath = 'docs/audits/2026-08-28/schemas/10-HANDOFF-RECEIPT-SCHEMA.json'

function Read-BoundedJson {
    param([string] $Path, [string] $Kind)
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.Length -le 0 -or $item.Length -gt $script:MaximumJsonBytes) {
        throw "$Kind must contain between 1 byte and 4 MiB."
    }
    try {
        return Get-Content -Raw -LiteralPath $item.FullName |
            ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "$Kind is malformed JSON."
    }
}

function Get-RequiredProperty {
    param([object] $Object, [string] $Name, [string] $Kind)
    if ($null -eq $Object) { throw "$Kind is missing." }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "$Kind requires property '$Name'."
    }
    return $property.Value
}

function Get-RequiredString {
    param([object] $Object, [string] $Name, [string] $Kind)
    $value = Get-RequiredProperty $Object $Name $Kind
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw "$Kind property '$Name' must be a non-empty string."
    }
    return [string] $value
}

function Get-RequiredInteger {
    param([object] $Object, [string] $Name, [string] $Kind)
    $value = Get-RequiredProperty $Object $Name $Kind
    if ($value -isnot [byte] -and
        $value -isnot [int16] -and
        $value -isnot [int32] -and
        $value -isnot [int64]) {
        throw "$Kind property '$Name' must be an integer."
    }
    return [int64] $value
}

function Assert-Equal {
    param([object] $Actual, [object] $Expected, [string] $Message)
    if ($Actual -cne $Expected) {
        throw "$Message (actual '$Actual', expected '$Expected')."
    }
}

function Assert-SchemaProperties {
    param(
        [object] $Object,
        [string[]] $Allowed,
        [string[]] $Required,
        [string] $Kind
    )
    $actual = @($Object.PSObject.Properties.Name)
    $extra = @($actual | Where-Object { $_ -cnotin $Allowed })
    if ($extra.Count -gt 0) {
        throw "$Kind contains unsupported schema property '$($extra[0])'."
    }
    $missing = @($Required | Where-Object { $_ -cnotin $actual })
    if ($missing.Count -gt 0) {
        throw "$Kind is missing required schema property '$($missing[0])'."
    }
}

function Assert-UtcTimestamp {
    param([string] $Value, [string] $Kind)
    if ($Value -cnotmatch '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$') {
        throw "$Kind must be a whole-second UTC timestamp."
    }
    try { [void] [DateTimeOffset]::ParseExact($Value, 'yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture) }
    catch { throw "$Kind is not a valid UTC timestamp." }
}

function Assert-ManifestSchema {
    param([object] $Manifest)
    $required = @('schemaVersion', 'workerId', 'frozenSourceCommit', 'evidenceSubjectCommit',
        'evidenceSubjectTree', 'createdAtUtc', 'route', 'evidenceStatus', 'report',
        'inputs', 'outputs', 'commands', 'blockers', 'nonClaims')
    Assert-SchemaProperties $Manifest $required $required 'Evidence manifest'
    Assert-UtcTimestamp (Get-RequiredString $Manifest 'createdAtUtc' 'Evidence manifest') 'Evidence createdAtUtc'
    $status = Get-RequiredString $Manifest 'evidenceStatus' 'Evidence manifest'
    if ($status -cnotin @('passed', 'failed', 'blocked', 'mixed')) { throw 'Evidence status is outside the approved schema enum.' }
    $routes = @($Manifest.route)
    if ($routes.Count -eq 0 -or @($routes | Select-Object -Unique).Count -ne $routes.Count -or
        @($routes | Where-Object { $_ -cnotin @('shared', 'gguf', 'openvino') }).Count -gt 0) {
        throw 'Evidence route is outside the approved schema.'
    }
    Assert-SchemaProperties $Manifest.report @('path', 'sha256', 'bytes') @('path', 'sha256', 'bytes') 'Evidence report'
    foreach ($collection in @(@($Manifest.inputs), @($Manifest.outputs))) {
        if ($collection.Count -eq 0) { throw 'Evidence input and output arrays must be non-empty.' }
        foreach ($item in $collection) {
            Assert-SchemaProperties $item @('kind', 'id', 'sha256', 'bytes', 'digest', 'evidenceGrade') @('kind', 'id', 'evidenceGrade') 'Evidence item'
            $grade = Get-RequiredString $item 'evidenceGrade' 'Evidence item'
            if ($grade -cnotin @('measured', 'parsed', 'verified', 'estimated', 'fixture', 'blocked')) { throw 'Evidence grade is outside the approved schema enum.' }
        }
    }
    foreach ($command in @($Manifest.commands)) {
        Assert-SchemaProperties $command @('id', 'exitCode', 'discovered', 'executed', 'passed', 'failed', 'skipped', 'resultSha256') @('id', 'exitCode', 'discovered', 'executed', 'passed', 'failed', 'skipped') 'Evidence command'
        $id = Get-RequiredString $command 'id' 'Evidence command'
        if ($id -cnotmatch '^[A-Z0-9][A-Z0-9._-]{1,79}$') { throw 'Evidence command id is outside the approved schema pattern.' }
        [void] (Get-RequiredInteger $command 'exitCode' $id)
    }
}

function Assert-ReceiptSchema {
    param([object] $Receipt)
    $required = @('schemaVersion', 'workerId', 'frozenSourceCommit', 'frozenSourceTree',
        'branch', 'finalTip', 'finalTree', 'worktreeClean', 'completedAtUtc', 'report',
        'transport', 'remoteRef', 'bundle', 'evidenceManifest', 'testTotals', 'nativeDisposition')
    Assert-SchemaProperties $Receipt $required $required 'M1 handoff receipt'
    Assert-UtcTimestamp (Get-RequiredString $Receipt 'completedAtUtc' 'M1 handoff receipt') 'Receipt completedAtUtc'
    Assert-SchemaProperties $Receipt.report @('path', 'sha256', 'bytes') @('path', 'sha256', 'bytes') 'Receipt report'
    Assert-SchemaProperties $Receipt.evidenceManifest @('path', 'sha256', 'bytes', 'evidenceSubjectCommit', 'evidenceSubjectTree') @('path', 'sha256', 'bytes', 'evidenceSubjectCommit', 'evidenceSubjectTree') 'Receipt manifest'
    Assert-SchemaProperties $Receipt.testTotals @('discovered', 'executed', 'passed', 'failed', 'skipped') @('discovered', 'executed', 'passed', 'failed', 'skipped') 'Receipt testTotals'
}

function Assert-GitObject {
    param([string] $Value, [string] $Kind)
    if ($Value -cnotmatch '^[0-9a-f]{40}$') {
        throw "$Kind must be a lowercase 40-character Git object id."
    }
}

function Assert-Sha256 {
    param([string] $Value, [string] $Kind)
    if ($Value -cnotmatch '^[0-9a-f]{64}$') {
        throw "$Kind must be a lowercase SHA-256 digest."
    }
}

function Get-FileIdentity {
    param([string] $Path, [string] $Kind)
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.Length -le 0) { throw "$Kind must not be empty." }
    return [pscustomobject]@{
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        bytes = [int64] $item.Length
    }
}

function Invoke-Git {
    param([string[]] $Arguments, [switch] $AcceptNonZero)
    $output = @(& git -C $script:RepositoryRoot @Arguments 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    if (-not $AcceptNonZero -and $exitCode -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')."
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n").Trim()
    }
}

function Get-GitOutput {
    param([string[]] $Arguments)
    return (Invoke-Git $Arguments).Output
}

function Assert-Ancestor {
    param([string] $Ancestor, [string] $Descendant, [string] $Message)
    $result = Invoke-Git @('merge-base', '--is-ancestor', $Ancestor, $Descendant) -AcceptNonZero
    if ($result.ExitCode -ne 0) { throw $Message }
}

function Get-RepositoryRelativePath {
    param([string] $Path, [string] $Kind)
    $fullPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
    $rootPrefix = $script:RepositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Kind must be inside the repository."
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Assert-CommittedFile {
    param([string] $Commit, [string] $RelativePath, [string] $FullPath, [string] $Kind)
    $worktreeBlob = Get-GitOutput @('hash-object', '--no-filters', '--', $FullPath)
    $committedBlob = Get-GitOutput @('rev-parse', "$Commit`:$RelativePath")
    if ($worktreeBlob -cne $committedBlob) {
        throw "$Kind Git blob contents do not match the exact file bytes."
    }
}

function Assert-IdentityObject {
    param([object] $Record, [string] $Path, [string] $Kind)
    $identity = Get-FileIdentity $Path $Kind
    $sha256 = Get-RequiredString $Record 'sha256' $Kind
    $bytes = Get-RequiredInteger $Record 'bytes' $Kind
    Assert-Sha256 $sha256 "$Kind SHA-256"
    Assert-Equal $sha256 $identity.sha256 "$Kind SHA-256 does not match exact bytes"
    Assert-Equal $bytes $identity.bytes "$Kind byte count does not match exact bytes"
}

function Get-CommandTotals {
    param([object] $Manifest)
    $commands = @(Get-RequiredProperty $Manifest 'commands' 'Evidence manifest')
    if ($commands.Count -eq 0) { throw 'Evidence manifest commands must be a non-empty array.' }
    $totals = [ordered]@{ discovered = 0L; executed = 0L; passed = 0L; failed = 0L; skipped = 0L }
    foreach ($command in $commands) {
        $id = Get-RequiredString $command 'id' 'Evidence command'
        $counts = [ordered]@{}
        foreach ($name in @('discovered', 'executed', 'passed', 'failed', 'skipped')) {
            $counts[$name] = Get-RequiredInteger $command $name $id
            if ($counts[$name] -lt 0) { throw "$id command counts must be non-negative." }
            $totals[$name] += $counts[$name]
        }
        if ($counts.executed -ne ($counts.passed + $counts.failed + $counts.skipped)) {
            throw "$id executed must equal passed + failed + skipped " +
                "($($counts.executed) != $($counts.passed) + $($counts.failed) + $($counts.skipped))."
        }
        if ($counts.discovered -lt $counts.executed) {
            throw "$id discovered must be greater than or equal to executed."
        }
    }
    if ($totals.discovered -le 0 -or $totals.executed -le 0) {
        throw 'Evidence manifest aggregate discovered and executed totals must be non-zero.'
    }
    return [pscustomobject] $totals
}

function Assert-StableKinds {
    param([object] $Manifest)
    $items = @(
        @(Get-RequiredProperty $Manifest 'inputs' 'Evidence manifest') +
        @(Get-RequiredProperty $Manifest 'outputs' 'Evidence manifest'))
    if ($items.Count -eq 0) { throw 'Evidence inputs and outputs must not both be empty.' }
    $kinds = @($items | ForEach-Object {
        Get-RequiredString $_ 'kind' 'Evidence item'
    } | Select-Object -Unique)
    foreach ($required in @('modelSource', 'modelInspectionResult', 'modelInspectionHandoff')) {
        if ($required -cnotin $kinds) { throw "Evidence is missing stable kind '$required'." }
    }
}

function Assert-PrivateEvidence {
    param([string[]] $Paths)
    $content = ($Paths | ForEach-Object {
        Get-Content -Raw -LiteralPath $_
    }) -join "`n"
    if ($content -match '(?i)([a-z]:\\|/users/|\\\\)') {
        throw 'Committed evidence privacy rejected a local or UNC path.'
    }
    foreach ($value in @($env:USERNAME, $env:COMPUTERNAME)) {
        if (-not [string]::IsNullOrWhiteSpace($value) -and
            $content.IndexOf($value, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw 'Committed evidence privacy rejected a user or host identity.'
        }
    }
}

try {
    $script:RepositoryRoot = if ($RepositoryRoot) {
        (Resolve-Path -LiteralPath $RepositoryRoot).Path
    } else {
        (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    }
    $manifestFullPath = (Resolve-Path -LiteralPath $ManifestPath).Path
    $manifest = Read-BoundedJson $manifestFullPath 'Evidence manifest'
    $evidenceSchemaFullPath = Join-Path $script:RepositoryRoot $script:EvidenceSchemaPath
    $receiptSchemaFullPath = Join-Path $script:RepositoryRoot $script:ReceiptSchemaPath
    $evidenceSchema = Read-BoundedJson $evidenceSchemaFullPath 'Evidence manifest schema'
    $receiptSchema = Read-BoundedJson $receiptSchemaFullPath 'Handoff receipt schema'
    Assert-Equal $evidenceSchema.'$id' 'https://granite-edge-ai.local/schemas/audit-evidence-manifest-v1.json' 'Evidence schema identity is invalid'
    Assert-Equal $receiptSchema.'$id' 'https://granite-edge-ai.local/schemas/audit-worker-handoff-receipt-v1.json' 'Receipt schema identity is invalid'
    Assert-ManifestSchema $manifest
    $totals = Get-CommandTotals $manifest

    Assert-Equal (Get-RequiredString $manifest 'workerId' 'Evidence manifest') 'M1' 'Evidence workerId is invalid'
    Assert-Equal (Get-RequiredInteger $manifest 'schemaVersion' 'Evidence manifest') 1L 'Evidence schemaVersion is invalid'
    Assert-Equal (Get-RequiredString $manifest 'frozenSourceCommit' 'Evidence manifest') $ExpectedFrozenCommit 'Evidence frozen source commit is invalid'
    Assert-StableKinds $manifest

    $subjectCommit = Get-RequiredString $manifest 'evidenceSubjectCommit' 'Evidence manifest'
    $subjectTree = Get-RequiredString $manifest 'evidenceSubjectTree' 'Evidence manifest'
    Assert-GitObject $subjectCommit 'Evidence subject commit'
    Assert-GitObject $subjectTree 'Evidence subject tree'

    $report = Get-RequiredProperty $manifest 'report' 'Evidence manifest'
    $reportRelative = Get-RequiredString $report 'path' 'Evidence report'
    Assert-Equal $reportRelative $script:ExpectedReportPath 'R3 report path is invalid'
    $manifestRelative = Get-RepositoryRelativePath $manifestFullPath 'Evidence manifest'
    Assert-Equal $manifestRelative $script:ExpectedManifestPath 'R3 manifest path is invalid'
    $reportFullPath = Join-Path $script:RepositoryRoot $reportRelative
    Assert-IdentityObject $report $reportFullPath 'Evidence report'

    $actualFrozenTree = Get-GitOutput @('rev-parse', "$ExpectedFrozenCommit^{tree}")
    Assert-Equal $actualFrozenTree $ExpectedFrozenTree 'Frozen source tree is invalid'
    $actualSubjectTree = Get-GitOutput @('rev-parse', "$subjectCommit^{tree}")
    Assert-Equal $subjectTree $actualSubjectTree 'Evidence subject tree is invalid'
    Assert-Ancestor $ExpectedFrozenCommit $subjectCommit 'Evidence subject does not preserve frozen ancestry.'

    if (-not $ReceiptPath) {
        throw 'R3 receipt path is required after arithmetic validation.'
    }
    $receiptFullPath = (Resolve-Path -LiteralPath $ReceiptPath).Path
    $receipt = Read-BoundedJson $receiptFullPath 'M1 handoff receipt'
    Assert-ReceiptSchema $receipt
    Assert-Equal (Get-RequiredString $receipt 'workerId' 'M1 handoff receipt') 'M1' 'Receipt workerId is invalid'
    Assert-Equal (Get-RequiredInteger $receipt 'schemaVersion' 'M1 handoff receipt') 1L 'Receipt schemaVersion is invalid'
    Assert-Equal (Get-RequiredString $receipt 'frozenSourceCommit' 'M1 handoff receipt') $ExpectedFrozenCommit 'Receipt frozen source commit is invalid'
    Assert-Equal (Get-RequiredString $receipt 'frozenSourceTree' 'M1 handoff receipt') $ExpectedFrozenTree 'Receipt frozen source tree is invalid'
    Assert-Equal (Get-RequiredString $receipt 'branch' 'M1 handoff receipt') $ExpectedBranch 'Receipt branch is invalid'
    $finalTip = Get-RequiredString $receipt 'finalTip' 'M1 handoff receipt'
    $finalTree = Get-RequiredString $receipt 'finalTree' 'M1 handoff receipt'
    Assert-GitObject $finalTip 'Final tip'
    Assert-GitObject $finalTree 'Final tree'
    if ($finalTip -ceq $subjectCommit) { throw 'Evidence subject commit must differ from the later final tip.' }
    Assert-Equal (Get-GitOutput @('rev-parse', 'HEAD')) $finalTip 'Final tip does not match repository HEAD'
    Assert-Equal (Get-GitOutput @('rev-parse', "$finalTip^{tree}")) $finalTree 'Final tree is invalid'
    Assert-Ancestor $subjectCommit $finalTip 'Evidence subject is not an ancestor of the final tip.'
    Assert-Ancestor $ExpectedFrozenCommit $finalTip 'Final tip does not preserve frozen ancestry.'

    $receiptReport = Get-RequiredProperty $receipt 'report' 'M1 handoff receipt'
    Assert-Equal (Get-RequiredString $receiptReport 'path' 'Receipt report') $reportRelative 'Receipt report path does not join the manifest'
    Assert-IdentityObject $receiptReport $reportFullPath 'Receipt report'
    $manifestRecord = Get-RequiredProperty $receipt 'evidenceManifest' 'M1 handoff receipt'
    Assert-Equal (Get-RequiredString $manifestRecord 'path' 'Receipt manifest') $manifestRelative 'Receipt manifest path does not join the exact manifest'
    Assert-Equal (Get-RequiredString $manifestRecord 'evidenceSubjectCommit' 'Receipt manifest') $subjectCommit 'Receipt evidence subject commit does not join the manifest'
    Assert-Equal (Get-RequiredString $manifestRecord 'evidenceSubjectTree' 'Receipt manifest') $subjectTree 'Receipt evidence subject tree does not join the manifest'
    Assert-IdentityObject $manifestRecord $manifestFullPath 'Receipt manifest'
    Assert-CommittedFile $finalTip $reportRelative $reportFullPath 'Evidence report'
    Assert-CommittedFile $finalTip $manifestRelative $manifestFullPath 'Evidence manifest'
    Assert-CommittedFile $finalTip $script:EvidenceSchemaPath $evidenceSchemaFullPath 'Evidence manifest schema'
    Assert-CommittedFile $finalTip $script:ReceiptSchemaPath $receiptSchemaFullPath 'Handoff receipt schema'

    $receiptTotals = Get-RequiredProperty $receipt 'testTotals' 'M1 handoff receipt'
    foreach ($name in @('discovered', 'executed', 'passed', 'failed', 'skipped')) {
        Assert-Equal (Get-RequiredInteger $receiptTotals $name 'Receipt testTotals') $totals.$name "Receipt testTotals $name does not equal manifest aggregate"
    }
    if ($totals.executed -ne ($totals.passed + $totals.failed + $totals.skipped) -or
        $totals.discovered -lt $totals.executed) {
        throw 'Receipt testTotals arithmetic is invalid.'
    }

    $remoteRef = Get-RequiredString $receipt 'remoteRef' 'M1 handoff receipt'
    Assert-Equal $remoteRef "refs/remotes/origin/$ExpectedBranch" 'Receipt remote ref is invalid'
    Assert-Equal (Get-GitOutput @('rev-parse', $remoteRef)) $finalTip 'Pushed remote ref does not match final tip'
    Assert-Equal (Get-RequiredString $receipt 'transport' 'M1 handoff receipt') 'remote' 'Receipt transport must be remote'
    if ((Get-RequiredProperty $receipt 'worktreeClean' 'M1 handoff receipt') -cne $true) {
        throw 'Receipt must assert a clean worktree.'
    }
    if (-not [string]::IsNullOrEmpty((Get-GitOutput @('status', '--porcelain', '--untracked-files=all')))) {
        throw 'M1 worktree must be clean.'
    }

    $nativeDisposition = Get-RequiredString $receipt 'nativeDisposition' 'M1 handoff receipt'
    if ($nativeDisposition -cnotin @('not-run', 'blocked', 'passed')) {
        throw 'Native disposition must be not-run, blocked, or passed.'
    }
    if ($nativeDisposition -ceq 'passed') {
        if (-not $NativeReceiptPath) { throw 'Passed native disposition requires an M1 native receipt.' }
        $native = Read-BoundedJson $NativeReceiptPath 'M1 native receipt'
        Assert-Equal (Get-RequiredString $native 'workerId' 'M1 native receipt') 'M1' 'Native receipt workerId is invalid'
        if ((Get-RequiredProperty $native 'phaseClosed' 'M1 native receipt') -cne $true -or
            (Get-RequiredProperty $native 'processCleanupVerified' 'M1 native receipt') -cne $true) {
            throw 'M1 native receipt must close the phase and verify process cleanup.'
        }
        $nativeRecord = Get-RequiredProperty $receipt 'nativeReceipt' 'M1 handoff receipt'
        Assert-IdentityObject $nativeRecord $NativeReceiptPath 'M1 native receipt'
        $inputHandoff = Get-RequiredProperty $native 'inputHandoffReceipt' 'M1 native receipt'
        $inputPath = Get-RequiredString $inputHandoff 'path' 'Native input handoff receipt'
        Assert-IdentityObject $inputHandoff $inputPath 'Native input handoff receipt'
    } elseif ($NativeReceiptPath) {
        throw 'A native receipt cannot accompany a non-passing native disposition.'
    }

    Assert-PrivateEvidence @($manifestFullPath, $reportFullPath)
    Write-Output (
        'M1 R3 evidence identity, arithmetic, hashes, Git blobs, ancestry, remote ref, and privacy passed for {0} command row(s).' -f
        @($manifest.commands).Count)
    exit 0
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
