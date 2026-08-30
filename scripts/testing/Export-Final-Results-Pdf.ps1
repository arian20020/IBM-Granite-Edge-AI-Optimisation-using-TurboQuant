[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DocxPath,

    [Parameter(Mandatory = $true)]
    [string]$PdfPath,

    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 180,

    [Parameter(DontShow = $true)]
    [switch]$Worker,

    [Parameter(DontShow = $true)]
    [string]$OwnedWordPidPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerErrorPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-SingleQuotedLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-WordPdfExport {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][string]$PidReceiptPath
    )

    $word = $null
    $documents = $null
    $document = $null
    $ownsWordProcess = $false
    $wordProcessId = 0
    $knownWordProcessIds = @(
        Get-Process -Name "WINWORD" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Id
    )
    try {
        $word = New-Object -ComObject Word.Application
        $word.Visible = $false
        $word.DisplayAlerts = 0
        $word.ScreenUpdating = $false
        $word.AutomationSecurity = 3

        $ownershipDeadline = [datetime]::UtcNow.AddSeconds(5)
        do {
            $newWordProcessIds = @(
                Get-Process -Name "WINWORD" -ErrorAction SilentlyContinue |
                    Where-Object { $_.Id -notin $knownWordProcessIds } |
                    Select-Object -ExpandProperty Id
            )
            if ($newWordProcessIds.Count -eq 1) {
                break
            }
            Start-Sleep -Milliseconds 100
        } while ([datetime]::UtcNow -lt $ownershipDeadline)
        if ($newWordProcessIds.Count -ne 1) {
            throw "Could not attribute exactly one new Word process to this exporter."
        }
        $wordProcessId = [int]$newWordProcessIds[0]
        $ownsWordProcess = $true
        [System.IO.File]::WriteAllText(
            $PidReceiptPath,
            $wordProcessId.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            [System.Text.Encoding]::ASCII
        )

        $documents = $word.Documents
        $document = $documents.Open($SourcePath, $false, $true)
        $document.ExportAsFixedFormat($DestinationPath, 17)
        if (-not (Test-Path -LiteralPath $DestinationPath -PathType Leaf)) {
            throw "Word returned without creating the requested PDF."
        }
    }
    finally {
        if ($null -ne $document) {
            [object]$saveDocumentChanges = 0
            try { $document.Close([ref]$saveDocumentChanges) } catch { Write-Warning $_.Exception.Message }
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document)
        }
        if ($null -ne $documents) {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($documents)
        }
        if ($null -ne $word) {
            if ($ownsWordProcess) {
                [object]$saveWordChanges = 0
                [object]$originalFormat = 0
                [object]$routeDocument = 0
                try {
                    $word.Quit(
                        [ref]$saveWordChanges,
                        [ref]$originalFormat,
                        [ref]$routeDocument
                    )
                }
                catch { Write-Warning $_.Exception.Message }
            }
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word)
        }
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        if ($ownsWordProcess -and $wordProcessId -gt 0) {
            $ownedWord = Get-Process -Id $wordProcessId -ErrorAction SilentlyContinue
            if ($null -ne $ownedWord) {
                try {
                    if (-not $ownedWord.WaitForExit(5000)) {
                        Stop-Process -Id $wordProcessId -Force -ErrorAction Stop
                        [void]$ownedWord.WaitForExit(5000)
                    }
                }
                finally {
                    $ownedWord.Dispose()
                }
            }
        }
    }
}

function Stop-OwnedWordProcess {
    param(
        [Parameter(Mandatory = $true)][string]$PidReceiptPath,
        [Parameter(Mandatory = $true)][datetime]$NotBefore
    )

    if (-not (Test-Path -LiteralPath $PidReceiptPath -PathType Leaf)) {
        return
    }
    $recordedPid = 0
    if (-not [int]::TryParse(
        [System.IO.File]::ReadAllText($PidReceiptPath).Trim(),
        [ref]$recordedPid
    )) {
        return
    }
    $candidate = Get-Process -Id $recordedPid -ErrorAction SilentlyContinue
    if ($null -eq $candidate) {
        return
    }
    try {
        $isOwnedWord = (
            $candidate.ProcessName -ieq "WINWORD" -and
            $candidate.StartTime -ge $NotBefore.AddSeconds(-2)
        )
        if ($isOwnedWord) {
            Stop-Process -Id $recordedPid -Force -ErrorAction Stop
            [void]$candidate.WaitForExit(5000)
        }
    }
    finally {
        $candidate.Dispose()
    }
}

$source = [System.IO.Path]::GetFullPath($DocxPath)
$destination = [System.IO.Path]::GetFullPath($PdfPath)

if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "DOCX input does not exist: $source"
}
if ([System.IO.Path]::GetExtension($source) -ine ".docx") {
    throw "DOCX input must have a .docx extension: $source"
}
if ([System.IO.Path]::GetExtension($destination) -ine ".pdf") {
    throw "PDF output must have a .pdf extension: $destination"
}

if ($Worker) {
    if ([string]::IsNullOrWhiteSpace($OwnedWordPidPath)) {
        throw "OwnedWordPidPath is required in worker mode."
    }
    try {
        Invoke-WordPdfExport -SourcePath $source -DestinationPath $destination `
            -PidReceiptPath $OwnedWordPidPath
        exit 0
    }
    catch {
        if (-not [string]::IsNullOrWhiteSpace($WorkerErrorPath)) {
            [System.IO.File]::WriteAllText(
                $WorkerErrorPath,
                $_.Exception.ToString(),
                [System.Text.Encoding]::UTF8
            )
        }
        exit 1
    }
}

$destinationDirectory = [System.IO.Path]::GetDirectoryName($destination)
if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
    [void][System.IO.Directory]::CreateDirectory($destinationDirectory)
}

$operationDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
    "granite-final-results-pdf-" + [System.Guid]::NewGuid().ToString("N")
)
[void][System.IO.Directory]::CreateDirectory($operationDirectory)
$pidReceipt = Join-Path $operationDirectory "word.pid"
$workerError = Join-Path $operationDirectory "worker-error.txt"
$child = $null

try {
    $command = @(
        "& " + (ConvertTo-SingleQuotedLiteral -Value $PSCommandPath),
        "-DocxPath " + (ConvertTo-SingleQuotedLiteral -Value $source),
        "-PdfPath " + (ConvertTo-SingleQuotedLiteral -Value $destination),
        "-TimeoutSeconds $TimeoutSeconds",
        "-Worker",
        "-OwnedWordPidPath " + (ConvertTo-SingleQuotedLiteral -Value $pidReceipt),
        "-WorkerErrorPath " + (ConvertTo-SingleQuotedLiteral -Value $workerError)
    ) -join " "
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $powerShellExecutable = (Get-Process -Id $PID).Path
    $child = Start-Process -FilePath $powerShellExecutable -ArgumentList @(
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy", "Bypass",
        "-EncodedCommand", $encodedCommand
    ) -WindowStyle Hidden -PassThru
    $childStartedAt = $child.StartTime

    if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
        try { $child.Kill() } catch { Write-Warning $_.Exception.Message }
        [void]$child.WaitForExit(5000)
        Stop-OwnedWordProcess -PidReceiptPath $pidReceipt -NotBefore $childStartedAt
        throw "Word PDF export exceeded the $TimeoutSeconds second timeout."
    }
    $child.Refresh()
    if ($child.ExitCode -ne 0) {
        $detail = "Word PDF export worker failed with exit code $($child.ExitCode)."
        if (Test-Path -LiteralPath $workerError -PathType Leaf) {
            $detail += " " + [System.IO.File]::ReadAllText($workerError)
        }
        throw $detail
    }
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "Word PDF export completed without producing: $destination"
    }
}
finally {
    if ($null -ne $child) {
        try {
            if (-not $child.HasExited) {
                $child.Kill()
                [void]$child.WaitForExit(5000)
            }
        }
        finally {
            $child.Dispose()
        }
    }
    if (Test-Path -LiteralPath $operationDirectory) {
        Remove-Item -LiteralPath $operationDirectory -Recurse -Force
    }
}
