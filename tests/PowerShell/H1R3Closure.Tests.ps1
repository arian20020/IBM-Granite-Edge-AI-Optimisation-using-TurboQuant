$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$validator = Join-Path $repositoryRoot 'scripts\audits\Test-H1R3Closure.ps1'

function New-H1R3LedgerFixture {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable] $Counts
    )

    $root = Join-Path $TestDrive ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    & git -C $root init --quiet
    & git -C $root config core.autocrlf false
    & git -C $root config user.name 'H1 R3 Test'
    & git -C $root config user.email 'h1-r3@example.invalid'
    [IO.File]::WriteAllText((Join-Path $root 'seed.txt'), "fixture`n", [Text.UTF8Encoding]::new($false))
    & git -C $root add seed.txt
    & git -C $root commit --quiet -m 'fixture subject'
    $commit = (& git -C $root rev-parse HEAD).Trim()
    $tree = (& git -C $root show -s --format=%T HEAD).Trim()
    $row = [ordered]@{
        id = 'FIXTURE'
        invocationId = 'fixture-v1'
        exitCode = if ($Counts.failed -gt 0) { 1 } else { 0 }
        discovered = $Counts.discovered
        executed = $Counts.executed
        passed = $Counts.passed
        failed = $Counts.failed
        skipped = $Counts.skipped
    }
    $document = [ordered]@{
        schemaVersion = 1
        workerId = 'H1'
        campaign = 'R3'
        createdAtUtc = '2026-08-29T00:00:00Z'
        subjectTree = $tree
        commands = @($row)
        totals = [ordered]@{
            discovered = $Counts.discovered
            executed = $Counts.executed
            passed = $Counts.passed
            failed = $Counts.failed
            skipped = $Counts.skipped
        }
    }
    $ledger = Join-Path $root 'ledger.json'
    [IO.File]::WriteAllText(
        $ledger,
        (($document | ConvertTo-Json -Depth 8) + "`n"),
        [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{ Root = $root; Commit = $commit; Tree = $tree; Ledger = $ledger }
}

function Invoke-H1R3LedgerFixture {
    param([Parameter(Mandatory = $true)] $Fixture)
    $output = & $validator -Operation ValidateLedger `
        -RepositoryRoot $Fixture.Root `
        -LedgerPath $Fixture.Ledger `
        -ExpectedSubjectCommit $Fixture.Commit `
        -ExpectedSubjectTree $Fixture.Tree 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
}

function Get-H1R3FileRecord {
    param([string] $Root, [string] $RelativePath)
    $path = Join-Path $Root $RelativePath
    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
        bytes = ([IO.FileInfo] $path).Length
    }
}

function Write-H1R3Json {
    param([string] $Path, $Value)
    [IO.File]::WriteAllText(
        $Path,
        (($Value | ConvertTo-Json -Depth 12) + "`n"),
        [Text.UTF8Encoding]::new($false))
}

function New-H1R3ClosureFixture {
    $root = Join-Path $TestDrive ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    & git -C $root init --quiet
    & git -C $root config core.autocrlf false
    & git -C $root config user.name 'H1 R3 Test'
    & git -C $root config user.email 'h1-r3@example.invalid'
    [IO.File]::WriteAllText((Join-Path $root 'seed.txt'), 'subject', [Text.UTF8Encoding]::new($false))
    & git -C $root add seed.txt
    & git -C $root commit --quiet -m 'fixture subject'
    $subject = (& git -C $root rev-parse HEAD).Trim()
    $subjectTree = (& git -C $root show -s --format=%T HEAD).Trim()

    [IO.File]::WriteAllText((Join-Path $root 'report.md'), 'fixture report', [Text.UTF8Encoding]::new($false))
    $row = [ordered]@{
        id = 'FIXTURE'; invocationId = 'fixture-v1'; exitCode = 0
        discovered = 1; executed = 1; passed = 1; failed = 0; skipped = 0
    }
    $managed = [ordered]@{
        schemaVersion = 1; workerId = 'H1'; campaign = 'R3'
        createdAtUtc = '2026-08-29T00:00:00Z'; subjectTree = $subjectTree
        commands = @($row)
        totals = [ordered]@{ discovered = 1; executed = 1; passed = 1; failed = 0; skipped = 0 }
    }
    $native = [ordered]@{
        schemaVersion = 1; workerId = 'H1'; campaign = 'R3'; phaseClosed = $true
        cleanupVerified = $true; nativeDisposition = 'passed'; commands = @($row)
    }
    Write-H1R3Json (Join-Path $root 'managed.json') $managed
    Write-H1R3Json (Join-Path $root 'native.json') $native
    $manifest = [ordered]@{
        schemaVersion = 2; workerId = 'H1'; campaign = 'R3'
        baseCommit = $subject; evidenceSubjectCommit = $subject; evidenceSubjectTree = $subjectTree
        report = Get-H1R3FileRecord $root 'report.md'
        managedLedger = Get-H1R3FileRecord $root 'managed.json'
        nativeLedger = Get-H1R3FileRecord $root 'native.json'
        outputs = @(
            [ordered]@{ kind = 'hardwareSnapshot'; id = 'fixture'; evidenceGrade = 'verified' },
            [ordered]@{ kind = 'availableMemory'; id = 'fixture'; evidenceGrade = 'verified' },
            [ordered]@{ kind = 'safetyBudget'; id = 'fixture'; evidenceGrade = 'verified' }
        )
    }
    Write-H1R3Json (Join-Path $root 'manifest.json') $manifest
    & git -C $root add report.md managed.json native.json manifest.json
    & git -C $root commit --quiet -m 'fixture evidence'
    $tip = (& git -C $root rev-parse HEAD).Trim()
    $tipTree = (& git -C $root show -s --format=%T HEAD).Trim()
    & git -C $root update-ref refs/remotes/origin/h1-fixture $tip

    $handoffPath = Join-Path $TestDrive (([Guid]::NewGuid().ToString('N')) + '-handoff.json')
    $receiptPath = Join-Path $TestDrive (([Guid]::NewGuid().ToString('N')) + '-native.json')
    $handoff = [ordered]@{ workerId = 'H1'; campaign = 'R3'; phaseClosed = $true }
    Write-H1R3Json $handoffPath $handoff
    $handoffHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $handoffPath).Hash.ToLowerInvariant()
    $receipt = [ordered]@{
        workerId = 'H1'; campaign = 'R3'; phaseClosed = $true
        cleanupVerified = $true; nativeDisposition = 'passed'; handoffReceiptSha256 = $handoffHash
    }
    Write-H1R3Json $receiptPath $receipt
    return [pscustomobject]@{
        Root = $root; Subject = $subject; SubjectTree = $subjectTree; Tip = $tip; TipTree = $tipTree
        Manifest = (Join-Path $root 'manifest.json'); Managed = (Join-Path $root 'managed.json')
        Native = (Join-Path $root 'native.json'); Report = (Join-Path $root 'report.md')
        Handoff = $handoffPath; Receipt = $receiptPath; RemoteRef = 'refs/remotes/origin/h1-fixture'
    }
}

function Invoke-H1R3ClosureFixture {
    param([Parameter(Mandatory = $true)] $Fixture)
    $output = & $validator -Operation ValidateClosure `
        -RepositoryRoot $Fixture.Root -ManifestPath $Fixture.Manifest `
        -ManagedLedgerPath $Fixture.Managed -NativeLedgerPath $Fixture.Native `
        -ReportPath $Fixture.Report -HandoffPath $Fixture.Handoff `
        -NativeReceiptPath $Fixture.Receipt -ExpectedBase $Fixture.Subject `
        -ExpectedSubjectCommit $Fixture.Subject -ExpectedSubjectTree $Fixture.SubjectTree `
        -ExpectedFinalTip $Fixture.Tip -ExpectedFinalTree $Fixture.TipTree `
        -RemoteRef $Fixture.RemoteRef 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
}

Describe 'H1 R3 committed evidence closure' {
    It 'rejects R2 command hashes that have no committed result ledger' {
        # Mutation guarded: removing the committed-ledger requirement must fail this test.
        $r2Manifest = Join-Path $repositoryRoot `
            'docs\audits\2026-08-28\evidence\H1-hardware-evidence-v1.json'

        $output = & $validator `
            -Operation ValidateLedger `
            -RepositoryRoot $repositoryRoot `
            -LedgerPath $r2Manifest `
            -ExpectedSubjectCommit '1273808c31d4025adc0f494349ecdac188c250a9' `
            -ExpectedSubjectTree '03eead0ca570535831e064efc5771a5e35993916' 2>&1

        $LASTEXITCODE | Should Not Be 0
        ($output -join "`n") | Should Match '^H1R3-COMMITTED-RESULT-MISSING$'
    }

    It 'rejects executed totals that do not equal passed failed and skipped' {
        # Mutation guarded: accepting inconsistent arithmetic must fail this test.
        $fixture = New-H1R3LedgerFixture @{ discovered = 2; executed = 2; passed = 1; failed = 0; skipped = 0 }
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-ARITHMETIC'
    }

    It 'rejects discovered totals below executed totals' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 1; executed = 2; passed = 2; failed = 0; skipped = 0 }
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-DISCOVERY'
    }

    It 'rejects zero test discovery' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 0; executed = 0; passed = 0; failed = 0; skipped = 0 }
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-ZERO-DISCOVERY'
    }

    It 'rejects duplicate JSON members' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 1; executed = 1; passed = 1; failed = 0; skipped = 0 }
        [IO.File]::WriteAllText($fixture.Ledger, '{"workerId":"H1","workerId":"H1"}', [Text.UTF8Encoding]::new($false))
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-JSON-DUPLICATE'
    }

    It 'rejects JSON larger than one MiB' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 1; executed = 1; passed = 1; failed = 0; skipped = 0 }
        [IO.File]::WriteAllBytes($fixture.Ledger, [byte[]]::new(1048577))
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-JSON-BOUNDS'
    }

    It 'rejects a subject tree mismatch' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 1; executed = 1; passed = 1; failed = 0; skipped = 0 }
        $fixture.Tree = '0' * 40
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-SUBJECT-TREE'
    }

    It 'accepts a valid deterministic ledger' {
        $fixture = New-H1R3LedgerFixture @{ discovered = 3; executed = 3; passed = 2; failed = 1; skipped = 0 }
        $actual = Invoke-H1R3LedgerFixture $fixture
        $actual.ExitCode | Should Be 0
        $actual.Output | Should Be 'H1R3-OK'
    }

    It 'rejects a stale R2 handoff' {
        $fixture = New-H1R3ClosureFixture
        Write-H1R3Json $fixture.Handoff ([ordered]@{ workerId = 'H1'; campaign = 'R2'; phaseClosed = $true })
        $actual = Invoke-H1R3ClosureFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-STALE-CAMPAIGN'
    }

    It 'rejects a remote ref at a different tip' {
        $fixture = New-H1R3ClosureFixture
        & git -C $fixture.Root update-ref $fixture.RemoteRef $fixture.Subject
        $actual = Invoke-H1R3ClosureFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-REMOTE-TIP'
    }

    It 'rejects native cleanup false' {
        $fixture = New-H1R3ClosureFixture
        $receipt = Get-Content -Raw -LiteralPath $fixture.Receipt | ConvertFrom-Json
        $receipt.cleanupVerified = $false
        Write-H1R3Json $fixture.Receipt $receipt
        $actual = Invoke-H1R3ClosureFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-NATIVE-CLEANUP'
    }

    It 'rejects a native receipt bound to different handoff bytes' {
        # Mutation guarded: hashing any bytes except the published handoff must fail this test.
        $fixture = New-H1R3ClosureFixture
        $receipt = Get-Content -Raw -LiteralPath $fixture.Receipt | ConvertFrom-Json
        $receipt.handoffReceiptSha256 = '0' * 64
        Write-H1R3Json $fixture.Receipt $receipt
        $actual = Invoke-H1R3ClosureFixture $fixture
        $actual.ExitCode | Should Not Be 0
        $actual.Output | Should Be 'H1R3-NATIVE-JOIN'
    }

    It 'accepts a complete joined closure fixture' {
        $fixture = New-H1R3ClosureFixture
        $actual = Invoke-H1R3ClosureFixture $fixture
        $actual.ExitCode | Should Be 0
        $actual.Output | Should Be 'H1R3-OK'
    }
}
