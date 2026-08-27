[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Invalid {
    [Console]::Out.WriteLine('artifact_privacy_invalid')
    exit 1
}

function Test-SensitiveText {
    param([Parameter(Mandatory)][string]$Text)

    if ($Text.IndexOf([char]0) -ge 0 -or
        $Text -match '(?i)(?:[a-z]:[\\/]|\\\\[^\\\s]+\\[^\\\s]+)' -or
        $Text -match '(?i)(?:^|[\s"''])/(?:users|home|tmp|var|etc)/' -or
        $Text -match '(?i)"(?:prompt|generatedText|environment|secret|password|token|credential|username|hostname|runnerName|computerName|account|stdout|stderr|modelBytes|tokenizerBytes|modelPath|packagePath)"\s*:' -or
        $Text -match '(?i)(?:authorization\s*:\s*bearer|gh[pousr]_[a-z0-9]{20,}|AKIA[0-9A-Z]{16}|BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY)' -or
        $Text -match '(?i)<Std(?:Out|Err)>|<Output>') {
        return $true
    }

    foreach ($identity in @([Environment]::UserName, [Environment]::MachineName)) {
        if (-not [string]::IsNullOrWhiteSpace($identity) -and
            $identity.Length -ge 3 -and
            $Text.IndexOf($identity, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }
    return $false
}

function Get-StrictUtf8Text {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][long]$MaximumBytes
    )

    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Length -le 0 -or $item.Length -gt $MaximumBytes -or
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'artifact-file-invalid'
    }
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    return $encoding.GetString([IO.File]::ReadAllBytes($item.FullName))
}

function Test-ArtifactFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$DisplayName,
        [bool]$AllowArchive = $true
    )

    $extension = [IO.Path]::GetExtension($DisplayName).ToLowerInvariant()
    $leafName = [IO.Path]::GetFileName($DisplayName)
    if ($leafName -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$') {
        throw 'artifact-name-invalid'
    }
    if ($extension -notin @('.json', '.trx', '.log', '.txt', '.zip')) {
        throw 'artifact-extension-invalid'
    }
    if ($extension -eq '.zip') {
        if (-not $AllowArchive) { throw 'nested-archive-invalid' }
        $archiveItem = Get-Item -LiteralPath $Path -Force
        if ($archiveItem.Length -le 0 -or $archiveItem.Length -gt 16777216 -or
            ($archiveItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'archive-file-invalid'
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($Path)
        try {
            if ($archive.Entries.Count -lt 1 -or $archive.Entries.Count -gt 128) {
                throw 'archive-entry-count-invalid'
            }
            [long]$expanded = 0
            foreach ($entry in $archive.Entries) {
                if ([string]::IsNullOrWhiteSpace($entry.Name) -or
                    [IO.Path]::IsPathRooted($entry.FullName) -or
                    $entry.FullName -match '(^|[\\/])\.\.([\\/]|$)' -or
                    $entry.Length -le 0 -or $entry.Length -gt 16777216) {
                    throw 'archive-entry-invalid'
                }
                if ([long]$entry.Length -gt (67108864 - $expanded)) {
                    throw 'archive-expanded-size-invalid'
                }
                $expanded += [long]$entry.Length
                $temporary = [IO.Path]::GetTempFileName()
                try {
                    $input = $entry.Open()
                    $output = [IO.File]::Open($temporary, [IO.FileMode]::Create,
                        [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try {
                        $buffer = [byte[]]::new(81920)
                        [long]$copied = 0
                        while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                            if ([long]$read -gt ([long]$entry.Length - $copied)) {
                                throw 'archive-entry-expanded-size-invalid'
                            }
                            $output.Write($buffer, 0, $read)
                            $copied += [long]$read
                        }
                        if ($copied -ne [long]$entry.Length) {
                            throw 'archive-entry-length-invalid'
                        }
                    }
                    finally { $output.Dispose(); $input.Dispose() }
                    Test-ArtifactFile -Path $temporary -DisplayName $entry.FullName `
                        -AllowArchive $false
                }
                finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
            }
        }
        finally { $archive.Dispose() }
        return
    }

    $text = Get-StrictUtf8Text -Path $Path -MaximumBytes 16777216
    if (Test-SensitiveText -Text $text) { throw 'artifact-sensitive-content' }
    if ($extension -eq '.json') {
        Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
        [OpenVinoClosedJson.StrictParser]::Validate($text, 16)
        $null = $text | ConvertFrom-Json -ErrorAction Stop
    }
    elseif ($extension -eq '.trx') {
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($text), $settings)
        try { while ($reader.Read()) { } }
        finally { $reader.Dispose() }
    }
    else {
        foreach ($line in @($text -split '\r?\n' | Where-Object {
                    -not [string]::IsNullOrWhiteSpace($_) })) {
            if ($line -cnotmatch '^[A-Z][A-Z0-9_]{1,63}=(?:passed|failed|zero_residue|[0-9]{1,12}|[0-9a-f]{40}|[0-9a-f]{64})$') {
                throw 'untyped-text-artifact'
            }
        }
    }
}

try {
    $root = [IO.Path]::GetFullPath($ArtifactRoot)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Stop-Invalid }
    $rootItem = Get-Item -LiteralPath $root -Force
    if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Invalid }
    $directories = @(Get-ChildItem -LiteralPath $root -Directory -Recurse -Force)
    if ($directories | Where-Object {
            $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        Stop-Invalid
    }
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
    if ($files.Count -lt 1 -or $files.Count -gt 256) { Stop-Invalid }
    foreach ($file in $files) {
        Test-ArtifactFile -Path $file.FullName -DisplayName $file.Name
    }
    [Console]::Out.WriteLine('artifact_privacy_valid')
    exit 0
}
catch {
    Stop-Invalid
}
