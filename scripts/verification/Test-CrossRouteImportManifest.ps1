[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Component,
    [Parameter(Mandatory)][string]$SourceRepository,
    [Parameter(Mandatory)][string]$DestinationRepository,
    [string]$WriteAllowedPathspec
)

$ErrorActionPreference = 'Stop'
if ($WriteAllowedPathspec -and (Test-Path -LiteralPath $WriteAllowedPathspec)) { Remove-Item -LiteralPath $WriteAllowedPathspec -Force }

function Normalize-GitPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'A non-empty Git path is required.' }
    $value = $Path.Replace('\','/')
    while ($value.StartsWith('./', [StringComparison]::Ordinal)) { $value=$value.Substring(2) }
    $segments=@($value.Split('/'))
    if ($value.StartsWith('/') -or $value -match '^[A-Za-z]:' -or $value.Contains([char]0) -or $value -match '[\x00-\x1f\x7f]' -or
        @($segments | Where-Object { $_ -eq '.' -or $_ -eq '..' -or $_ -eq '' }).Count) { throw 'Absolute, non-canonical, or traversing Git path rejected.' }
    return $value
}
function Require-Hex([string]$Value, [int]$Length, [string]$Name) {
    if ($Value -cnotmatch ('^[0-9a-f]{' + $Length + '}$')) { throw "$Name must be exact lowercase hexadecimal." }
}
function Git-Text([string]$Repository, [string[]]$Arguments, [bool]$AllowFailure=$false) {
    $output = & git -C $Repository @Arguments 2>$null
    if ($LASTEXITCODE -and -not $AllowFailure) { throw 'A required Git identity check failed.' }
    return ($output -join "`n").Trim()
}
function Git-BlobBytes([string]$Repository, [string]$Blob) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName='git.exe'; $start.WorkingDirectory=$Repository; $start.UseShellExecute=$false; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.Arguments='cat-file blob ' + $Blob
    $process=[Diagnostics.Process]::Start($start); $memory=New-Object IO.MemoryStream; $process.StandardOutput.BaseStream.CopyTo($memory); $process.WaitForExit()
    if ($process.ExitCode) { throw 'Unable to read a declared Git blob.' }
    return $memory.ToArray()
}
function Sha256-Bytes([byte[]]$Bytes) {
    $sha=[Security.Cryptography.SHA256]::Create(); try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Git-NulRecords([string]$Repository, [string]$Arguments) {
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName='git.exe';$start.WorkingDirectory=$Repository;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.Arguments=$Arguments
    $process=[Diagnostics.Process]::Start($start);$memory=New-Object IO.MemoryStream;$process.StandardOutput.BaseStream.CopyTo($memory);$process.WaitForExit()
    if($process.ExitCode){throw 'Unable to obtain bounded Git path records.'}
    $raw=$memory.ToArray();$result=New-Object 'Collections.Generic.List[string]';$begin=0;$decoder=New-Object Text.UTF8Encoding($false,$true)
    for($i=0;$i-lt$raw.Length;$i++){if($raw[$i]-eq 0){if($i-gt$begin){$result.Add($decoder.GetString($raw,$begin,$i-$begin))};$begin=$i+1}}
    if($begin-ne$raw.Length){throw 'Git path records were not NUL terminated.'}
    return $result.ToArray()
}

$sourceRoot=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $SourceRepository).Path).TrimEnd('\','/')
$destinationRoot=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $DestinationRepository).Path).TrimEnd('\','/')
foreach ($pair in @(@($sourceRoot,'SourceRepository'),@($destinationRoot,'DestinationRepository'))) {
    $root=Git-Text $pair[0] @('rev-parse','--show-toplevel')
    $root=[IO.Path]::GetFullPath(($root -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\','/')
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($root,$pair[0])) { throw "$($pair[1]) must be the exact repository root." }
}
$manifestPath=Join-Path $destinationRoot 'docs\handoffs\2026-08-26-cross-route-optimisation-import-manifest.json'
$json=[IO.File]::ReadAllText($manifestPath)

# JavaScriptSerializer accepts duplicate keys, so validate object keys with a structural token pass first.
$tokenPattern='(?s)\s*(?:(?<string>"(?:\\.|[^"\\])*")|(?<punct>[{}\[\],:])|(?<literal>true|false|null|-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?))'
$tokenRegex=New-Object Text.RegularExpressions.Regex($tokenPattern)
$offset=0; $stack=New-Object Collections.Stack
while ($offset -lt $json.Length) {
    if ($json.Substring($offset) -match '^\s*$') { $offset=$json.Length; break }
    $match=$tokenRegex.Match($json,$offset)
    if (-not $match.Success -or $match.Index -ne $offset) { throw 'Manifest JSON contains an invalid token.' }
    $offset=$match.Index+$match.Length
    if ($match.Groups['punct'].Success) {
        $p=$match.Groups['punct'].Value
        if ($p -eq '{') { $stack.Push(@{ kind='object'; keys=(New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)); expectKey=$true }) }
        elseif ($p -eq '[') { $stack.Push(@{ kind='array' }) }
        elseif ($p -eq '}' -or $p -eq ']') { if ($stack.Count) { [void]$stack.Pop() } }
        elseif ($p -eq ',' -and $stack.Count -and $stack.Peek().kind -eq 'object') { $stack.Peek().expectKey=$true }
    } elseif ($match.Groups['string'].Success -and $stack.Count -and $stack.Peek().kind -eq 'object' -and $stack.Peek().expectKey) {
        $key=ConvertFrom-Json $match.Groups['string'].Value
        if (-not $stack.Peek().keys.Add([string]$key)) { throw "Duplicate JSON property rejected: $key" }
        $stack.Peek().expectKey=$false
    }
}
$manifest=$json | ConvertFrom-Json
$authorityByCommit=@{}; foreach($authority in @($manifest.authorities)) { Require-Hex $authority.commit 40 'authority commit'; if($authorityByCommit.ContainsKey($authority.commit)){throw 'Authority commits must be unique.'};$authorityByCommit[$authority.commit]=$authority }
$record=@($manifest.components | Where-Object name -CEQ $Component)
if ($record.Count -ne 1) { throw 'The requested component must have exactly one manifest record.' }
$record=$record[0]
if(-not $authorityByCommit.ContainsKey($record.sourceCommit) -or $authorityByCommit[$record.sourceCommit].ref -cne $record.sourceRef){throw 'Component source ref/commit is not a pinned authority pair.'}
if ($record.status -cne 'Verified') { throw "Component $Component is $($record.status); only Verified components authorize import." }
foreach ($field in @('allowedDestinationPaths','dependencyClosure','integrationPatches','verificationCommands')) {
    if (@($record.$field).Count -eq 0) { throw "Verified component has an empty $field array." }
}
foreach($field in @('allowedDestinationPaths','dependencyClosure','verificationCommands')){foreach($value in @($record.$field)){if([string]::IsNullOrWhiteSpace([string]$value)){throw "Verified component contains an empty $field entry."}}}

$sourceByDestination=@{}
foreach ($source in @($record.sources)) {
    $path=Normalize-GitPath $source.path; $destination=Normalize-GitPath $source.destination
    Require-Hex $source.commit 40 'source commit'; Require-Hex $source.blob 40 'source blob'; Require-Hex $source.sha256 64 'source SHA-256'
    if(-not $authorityByCommit.ContainsKey($source.commit)){throw 'Source commit is not declared in the authority table.'}
    [void](Git-Text $sourceRoot @('cat-file','-e',("{0}^{{commit}}" -f $source.commit)))
    if($sourceByDestination.ContainsKey($destination)){throw 'Multiple source records target one destination.'}
    $line=Git-Text $sourceRoot @('ls-tree',$source.commit,'--',$path)
    if ($line -notmatch '^(100644|100755) blob ([0-9a-f]{40})\t') { throw 'Source path is absent or has a forbidden Git mode.' }
    if ($Matches[2] -cne $source.blob) { throw 'Declared source blob does not match commit:path.' }
    $resolved=Git-Text $sourceRoot @('rev-parse',("{0}:{1}" -f $source.commit,$path))
    if ($resolved -cne $source.blob) { throw 'Resolved commit:path blob differs from the declaration.' }
    $bytes=Git-BlobBytes $sourceRoot $source.blob
    if ((Sha256-Bytes $bytes) -cne $source.sha256) { throw 'Declared source SHA-256 does not match commit:path bytes.' }
    $sourceByDestination[$destination]=@{ record=$source; bytes=$bytes }
}

$destinationByPath=@{}
foreach ($destination in @($record.destinations)) {
    $path=Normalize-GitPath $destination.path
    if ($destinationByPath.ContainsKey($path)) { throw 'Duplicate destination path.' }
    if ($destination.kind -cnotin @('Exact','Adapted','Created')) { throw 'Unknown destination kind.' }
    Require-Hex $destination.resultSha256 64 'destination result SHA-256'
    if ($destination.kind -eq 'Created' -and $sourceByDestination.ContainsKey($path)) { throw 'Created destinations cannot invent source blobs.' }
    if ($destination.kind -in @('Exact','Adapted') -and -not $sourceByDestination.ContainsKey($path)) { throw 'Imported destination lacks source provenance.' }
    $destinationByPath[$path]=$destination
}

foreach ($destination in $destinationByPath.Values | Where-Object kind -CEQ 'Exact') {
    if ($destination.patchGroupId) { throw 'Exact destinations cannot use a patch group.' }
    $actual=[IO.File]::ReadAllBytes((Join-Path $destinationRoot ($destination.path -replace '/', '\')))
    if ((Sha256-Bytes $actual) -cne $destination.resultSha256 -or (Sha256-Bytes $actual) -cne $sourceByDestination[$destination.path].record.sha256) { throw 'Exact destination bytes differ from commit:path:blob authority.' }
}

$declaredPatchPaths=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($group in @($record.integrationPatches)) {
    if([string]::IsNullOrWhiteSpace([string]$group.id)){throw 'Patch-group ID is required.'}
    if (@($record.integrationPatches | Where-Object id -CEQ $group.id).Count -ne 1) { throw 'Patch-group IDs must be unique.' }
    Require-Hex $group.baseCommit 40 'patch base commit'; Require-Hex $group.baseTree 40 'patch base tree'; Require-Hex $group.patchSha256 64 'patch SHA-256'
    [void](Git-Text $sourceRoot @('cat-file','-e',("{0}^{{commit}}" -f $group.baseCommit)))
    $baseTree=Git-Text $sourceRoot @('rev-parse',("{0}^{{tree}}" -f $group.baseCommit))
    if ($baseTree -cne $group.baseTree) { throw 'Patch-group base tree does not match its authority commit.' }
    $patchPath=Normalize-GitPath $group.patchPath; [void]$declaredPatchPaths.Add($patchPath)
    $patchFull=Join-Path $destinationRoot ($patchPath -replace '/', '\')
    $patchBytes=[IO.File]::ReadAllBytes($patchFull)
    if ((Sha256-Bytes $patchBytes) -cne $group.patchSha256) { throw 'Tracked integration patch SHA-256 differs.' }
    $members=@($destinationByPath.Values | Where-Object patchGroupId -CEQ $group.id)
    if ($members.Count -eq 0) { throw 'Patch group has no declared destinations.' }
    foreach ($member in $members) {
        if ($member.kind -eq 'Adapted' -and $sourceByDestination[$member.path].record.commit -cne $group.baseCommit) { throw 'Adapted files from different bases require separate patch groups.' }
        if ($member.kind -eq 'Created') {
            & git -C $sourceRoot cat-file -e ("{0}:{1}" -f $group.baseCommit,$member.path) 2>$null
            if ($LASTEXITCODE -eq 0) { throw 'Created destination already exists at the integration base.' }
        }
    }
    $strictPatchUtf8=New-Object Text.UTF8Encoding($false,$true);$patchText=$strictPatchUtf8.GetString($patchBytes)
    if($patchText.Contains('GIT binary patch')){throw 'Integration patches must be unified text patches.'}
    $oldHeaderCount=[regex]::Matches($patchText,'(?m)^--- ').Count;$newHeaderCount=[regex]::Matches($patchText,'(?m)^\+\+\+ ').Count
    if($oldHeaderCount-ne$members.Count-or$newHeaderCount-ne$members.Count){throw 'Patch must contain one paired unified-file header per destination.'}
    $createdCount=@($members|Where-Object kind -CEQ 'Created').Count;$devNullCount=[regex]::Matches($patchText,'(?m)^--- /dev/null\r?$').Count
    if($devNullCount-ne$createdCount){throw 'Only declared Created files may be introduced from /dev/null.'}
    $temporary=Join-Path ([IO.Path]::GetTempPath()) ('geai-import-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.Directory]::CreateDirectory($temporary) | Out-Null; & git -C $temporary init --quiet
        foreach ($member in $members | Where-Object kind -CEQ 'Adapted') {
            $full=Join-Path $temporary ($member.path -replace '/', '\'); [IO.Directory]::CreateDirectory((Split-Path -Parent $full)) | Out-Null
            [IO.File]::WriteAllBytes($full,$sourceByDestination[$member.path].bytes)
        }
        $quotedPatch='"' + $patchFull.Replace('"','\"') + '"';$numstat=@(Git-NulRecords $temporary ("apply --numstat -z -- $quotedPatch"));$patchDestinations=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
        foreach($stat in $numstat){if($stat-cnotmatch '^\d+\t\d+\t(.+)$'){throw 'Patch numstat is not a unified text record.'};$changedPath=Normalize-GitPath $Matches[1];if(-not$destinationByPath.ContainsKey($changedPath)-or$destinationByPath[$changedPath].patchGroupId-cne$group.id){throw 'Patch changes an undeclared destination.'};[void]$patchDestinations.Add($changedPath)}
        if($patchDestinations.Count-ne$members.Count){throw 'Patch does not reconstruct every declared group destination.'}
        & git -C $temporary apply --check --whitespace=nowarn -- $patchFull 2>$null; if ($LASTEXITCODE) { throw 'Integration patch failed git apply --check.' }
        & git -C $temporary apply --whitespace=nowarn -- $patchFull 2>$null; if ($LASTEXITCODE) { throw 'Integration patch failed to apply.' }
        foreach ($member in $members) {
            $reconstructed=[IO.File]::ReadAllBytes((Join-Path $temporary ($member.path -replace '/', '\')))
            $actual=[IO.File]::ReadAllBytes((Join-Path $destinationRoot ($member.path -replace '/', '\')))
            if ((Sha256-Bytes $reconstructed) -cne $member.resultSha256 -or (Sha256-Bytes $actual) -cne $member.resultSha256) { throw 'Reconstructed or destination result SHA-256 differs.' }
        }
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force } }
}
foreach ($destination in $destinationByPath.Values | Where-Object kind -in @('Adapted','Created')) {
    if (-not $destination.patchGroupId -or @($record.integrationPatches | Where-Object id -CEQ $destination.patchGroupId).Count -ne 1) { throw 'Adapted/Created destination lacks one patch group.' }
}
foreach($sourceDestination in $sourceByDestination.Keys){if(-not $destinationByPath.ContainsKey($sourceDestination)){throw 'Source provenance names no declared destination.'}}

$allowed=@($record.allowedDestinationPaths | ForEach-Object { Normalize-GitPath $_ })
foreach ($path in $destinationByPath.Keys) { if ($path -cnotin $allowed) { throw 'Destination is absent from allowedDestinationPaths.' } }
$manifestAllowed=@($manifest.policy.manifestPaths | ForEach-Object { Normalize-GitPath $_ })
foreach ($path in @(Git-NulRecords $destinationRoot 'diff --cached --name-only -z')) {
    $normalized=Normalize-GitPath $path
    if ($normalized -cnotin $allowed -and -not $declaredPatchPaths.Contains($normalized) -and $normalized -cnotin $manifestAllowed) { throw 'A staged path is outside the verified component boundary.' }
}
if ($WriteAllowedPathspec) {
    $statusRecords=@(Git-NulRecords $destinationRoot 'status --porcelain=v1 -z --untracked-files=all');$changed=New-Object 'Collections.Generic.List[string]'
    for($i=0;$i-lt$statusRecords.Count;$i++){$entry=$statusRecords[$i];if($entry.Length-lt 4-or$entry[2]-ne' '){throw 'Unexpected Git status record.'};$status=$entry.Substring(0,2);$changed.Add((Normalize-GitPath $entry.Substring(3)));if($status.Contains('R')-or$status.Contains('C')){if(++$i-ge$statusRecords.Count){throw 'Incomplete rename/copy status record.'};$changed.Add((Normalize-GitPath $statusRecords[$i]))}}
    foreach ($path in $changed) { if ($path -cnotin $allowed -and -not $declaredPatchPaths.Contains($path) -and $path -cnotin $manifestAllowed) { throw 'An actual changed path is outside the verified component boundary.' } }
    $bytes=New-Object 'Collections.Generic.List[byte]'; foreach($path in $changed){$bytes.AddRange([Text.Encoding]::UTF8.GetBytes($path));$bytes.Add(0)}; [IO.File]::WriteAllBytes($WriteAllowedPathspec,$bytes.ToArray())
}
Write-Output "Verified bounded import component: $Component"
