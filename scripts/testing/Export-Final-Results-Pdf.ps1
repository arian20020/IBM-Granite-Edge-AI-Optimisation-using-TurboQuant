[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DocxPath,

    [Parameter(Mandatory = $true)]
    [string]$PdfPath,

    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 180,

    [Parameter(DontShow = $true)]
    [ValidateRange(1, 300)]
    [int]$StartupTimeoutSeconds = 30,

    [Parameter(DontShow = $true)]
    [switch]$Worker,

    [Parameter(DontShow = $true)]
    [string]$OwnedWordReceiptPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerErrorPath,

    [Parameter(DontShow = $true)]
    [ValidateRange(0, 300)]
    [int]$TestExportDelaySeconds = 0,

    [Parameter(DontShow = $true)]
    [switch]$TestAttributionFailure,

    [Parameter(DontShow = $true)]
    [ValidateRange(0, 300)]
    [int]$TestPreReceiptDelaySeconds = 0,

    [Parameter(DontShow = $true)]
    [ValidateRange(0, 300)]
    [int]$TestParentValidationDelaySeconds = 0,

    [Parameter(DontShow = $true)]
    [string]$TestWorkerPidPath,

    [Parameter(DontShow = $true)]
    [string]$TestWordActivatedPath,

    [Parameter(DontShow = $true)]
    [string]$ParentAcknowledgementPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerContainmentReceiptPath,

    [Parameter(DontShow = $true)]
    [string]$ParentContainmentAcknowledgementPath,

    [Parameter(DontShow = $true)]
    [string]$ParentCancellationPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerCleanupAcknowledgementPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-SingleQuotedLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Initialize-WordWindowApi {
    if ($null -ne ("FinalResultsWordWindowApi" -as [type])) {
        return
    }
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class FinalResultsWordWindowApi {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);
}
"@
}

function Find-WordExecutable {
    $registryPaths = @(
        "Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE",
        "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE",
        "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE"
    )
    foreach ($registryPath in $registryPaths) {
        try {
            $key = Get-Item -LiteralPath $registryPath -ErrorAction Stop
            $registered = [string]$key.GetValue("")
            if (-not [string]::IsNullOrWhiteSpace($registered)) {
                $candidate = $registered.Trim().Trim('"')
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return (Resolve-Path -LiteralPath $candidate).Path
                }
            }
        }
        catch {
            continue
        }
    }

    $programFileRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    foreach ($programFileRoot in $programFileRoots) {
        foreach ($relativePath in @(
            "Microsoft Office\root\Office16\WINWORD.EXE",
            "Microsoft Office\Office16\WINWORD.EXE"
        )) {
            $candidate = Join-Path $programFileRoot $relativePath
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }
    throw "Microsoft Word was not found through App Paths or standard 64-/32-bit Office locations."
}

function Get-CausalWordIdentity {
    param([Parameter(Mandatory = $true)]$WordWindow)

    Initialize-WordWindowApi
    $bindingFlags = [System.Reflection.BindingFlags]::GetProperty
    try {
        $hwndValue = $WordWindow.GetType().InvokeMember(
            "Hwnd",
            $bindingFlags,
            $null,
            $WordWindow,
            $null
        )
    }
    catch {
        throw "causal Word attribution failed: the Word window did not expose Hwnd through COM dispatch. $($_.Exception.Message)"
    }
    $hwnd = [int64]$hwndValue
    if ($hwnd -le 0 -or -not [FinalResultsWordWindowApi]::IsWindow([IntPtr]$hwnd)) {
        throw "causal Word attribution failed: COM returned an invalid Word window handle."
    }

    [uint32]$wordProcessId = 0
    $threadId = [FinalResultsWordWindowApi]::GetWindowThreadProcessId(
        [IntPtr]$hwnd,
        [ref]$wordProcessId
    )
    if ($threadId -eq 0 -or $wordProcessId -eq 0) {
        throw "causal Word attribution failed: Windows could not map the COM window to a process."
    }

    $process = Get-Process -Id ([int]$wordProcessId) -ErrorAction Stop
    try {
        if ($process.ProcessName -ine "WINWORD") {
            throw "causal Word attribution failed: the COM window belongs to $($process.ProcessName), not WINWORD."
        }
        $executablePath = [System.IO.Path]::GetFullPath($process.Path)
        if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
            throw "causal Word attribution failed: the mapped Word executable path is unavailable."
        }
        return [ordered]@{
            pid = [int]$wordProcessId
            hwnd = $hwnd
            process_start_time_utc_ticks = $process.StartTime.ToUniversalTime().Ticks
            executable_path = $executablePath
        }
    }
    finally {
        $process.Dispose()
    }
}

function Write-WordIdentityReceipt {
    param(
        [Parameter(Mandatory = $true)]$Identity,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $temporaryPath = "$Path.$PID.tmp"
    try {
        $json = $Identity | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            $json,
            [System.Text.UTF8Encoding]::new($false)
        )
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Write-AtomicSignal {
    param([Parameter(Mandatory = $true)][string]$Path)

    $temporaryPath = "$Path.$PID.tmp"
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            "acknowledged",
            [System.Text.Encoding]::ASCII
        )
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-ValidatedOwnedWordProcess {
    param([Parameter(Mandatory = $true)][string]$ReceiptPath)

    if (-not (Test-Path -LiteralPath $ReceiptPath -PathType Leaf)) {
        throw "The causal Word identity receipt is missing."
    }
    try {
        $receipt = [System.IO.File]::ReadAllText($ReceiptPath) | ConvertFrom-Json
        $recordedPid = [int]$receipt.pid
        $recordedHwnd = [int64]$receipt.hwnd
        $recordedStartTicks = [int64]$receipt.process_start_time_utc_ticks
        $recordedPath = [System.IO.Path]::GetFullPath([string]$receipt.executable_path)
    }
    catch {
        throw "The causal Word identity receipt is malformed. $($_.Exception.Message)"
    }
    if ($recordedPid -le 0 -or $recordedHwnd -le 0 -or $recordedStartTicks -le 0) {
        throw "The causal Word identity receipt contains invalid identity values."
    }

    Initialize-WordWindowApi
    if (-not [FinalResultsWordWindowApi]::IsWindow([IntPtr]$recordedHwnd)) {
        throw "The recorded Word window no longer exists."
    }
    [uint32]$mappedPid = 0
    $threadId = [FinalResultsWordWindowApi]::GetWindowThreadProcessId(
        [IntPtr]$recordedHwnd,
        [ref]$mappedPid
    )
    if ($threadId -eq 0 -or [int]$mappedPid -ne $recordedPid) {
        throw "The recorded Word window no longer maps to the recorded process."
    }

    $candidate = Get-Process -Id $recordedPid -ErrorAction Stop
    try {
        $actualStartTicks = $candidate.StartTime.ToUniversalTime().Ticks
        $actualPath = [System.IO.Path]::GetFullPath($candidate.Path)
        $valid = (
            $candidate.ProcessName -ieq "WINWORD" -and
            $actualStartTicks -eq $recordedStartTicks -and
            [System.StringComparer]::OrdinalIgnoreCase.Equals($actualPath, $recordedPath)
        )
        if (-not $valid) {
            throw "The recorded Word process identity no longer matches PID, HWND, start time, and executable path."
        }
        return $candidate
    }
    catch {
        $candidate.Dispose()
        throw
    }
}

function Stop-ValidatedOwnedWordProcess {
    param([Parameter(Mandatory = $true)][string]$ReceiptPath)

    $ownedWord = Get-ValidatedOwnedWordProcess -ReceiptPath $ReceiptPath
    try {
        $ownedWord.Kill()
        [void]$ownedWord.WaitForExit(5000)
    }
    finally {
        $ownedWord.Dispose()
    }
}

function Invoke-WordPdfExport {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [Parameter(Mandatory = $true)][string]$AcknowledgementPath,
        [Parameter(Mandatory = $true)][string]$ContainmentReceiptPath,
        [Parameter(Mandatory = $true)][string]$ContainmentAcknowledgementPath,
        [Parameter(Mandatory = $true)][string]$CancellationPath,
        [Parameter(Mandatory = $true)][string]$CleanupAcknowledgementPath,
        [Parameter(Mandatory = $true)][int]$HandshakeTimeoutSeconds,
        [Parameter(Mandatory = $true)][int]$ExportDelaySeconds,
        [Parameter(Mandatory = $true)][int]$PreReceiptDelaySeconds,
        [AllowEmptyString()][string]$WordActivatedPath,
        [Parameter(Mandatory = $true)][bool]$ForceAttributionFailure
    )

    $word = $null
    $documents = $null
    $identityDocument = $null
    $document = $null
    $window = $null
    try {
        $word = New-Object -ComObject Word.Application
        $word.Visible = $false
        $word.DisplayAlerts = 0
        $word.ScreenUpdating = $false
        $word.AutomationSecurity = 3

        $documents = $word.Documents
        $identityDocument = $documents.Add()
        $window = $word.ActiveWindow
        $identity = Get-CausalWordIdentity -WordWindow $window
        if ($ForceAttributionFailure) {
            throw "causal Word attribution failed by explicit test control."
        }
        if (-not [string]::IsNullOrWhiteSpace($WordActivatedPath)) {
            Write-WordIdentityReceipt -Identity $identity -Path $WordActivatedPath
        }
        Write-WordIdentityReceipt -Identity $identity -Path $ContainmentReceiptPath
        $containmentDeadline = [datetime]::UtcNow.AddSeconds($HandshakeTimeoutSeconds)
        while (-not (Test-Path -LiteralPath $ContainmentAcknowledgementPath -PathType Leaf)) {
            if (Test-Path -LiteralPath $CancellationPath -PathType Leaf) {
                throw "Word startup was cancelled before causal ownership acknowledgement."
            }
            if ([datetime]::UtcNow -ge $containmentDeadline) {
                throw "Timed out waiting for causal Word ownership acknowledgement."
            }
            Start-Sleep -Milliseconds 25
        }
        if ($PreReceiptDelaySeconds -gt 0) {
            $preReceiptDeadline = [datetime]::UtcNow.AddSeconds($PreReceiptDelaySeconds)
            while ([datetime]::UtcNow -lt $preReceiptDeadline) {
                if (Test-Path -LiteralPath $CancellationPath -PathType Leaf) {
                    throw "Word startup was cancelled before publication of the public identity receipt."
                }
                Start-Sleep -Milliseconds 25
            }
        }
        Write-WordIdentityReceipt -Identity $identity -Path $ReceiptPath
        $validationDeadline = [datetime]::UtcNow.AddSeconds($HandshakeTimeoutSeconds)
        while (-not (Test-Path -LiteralPath $AcknowledgementPath -PathType Leaf)) {
            if (Test-Path -LiteralPath $CancellationPath -PathType Leaf) {
                throw "Word startup was cancelled before parent identity acknowledgement."
            }
            if ([datetime]::UtcNow -ge $validationDeadline) {
                throw "Timed out waiting for parent identity acknowledgement."
            }
            Start-Sleep -Milliseconds 25
        }

        $document = $documents.Open($SourcePath, $false, $true)
        if ($ExportDelaySeconds -gt 0) {
            Start-Sleep -Seconds $ExportDelaySeconds
        }
        $document.ExportAsFixedFormat($DestinationPath, 17)
        if (-not (Test-Path -LiteralPath $DestinationPath -PathType Leaf)) {
            throw "Word returned without creating the requested PDF."
        }
    }
    finally {
        if ($null -ne $document) {
            [object]$saveDocumentChanges = 0
            try { $document.Close([ref]$saveDocumentChanges) } catch { Write-Warning $_.Exception.Message }
            try { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document) }
                catch { Write-Warning $_.Exception.Message }
        }
        if ($null -ne $identityDocument) {
            [object]$saveIdentityChanges = 0
            try { $identityDocument.Close([ref]$saveIdentityChanges) } catch { Write-Warning $_.Exception.Message }
            try { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($identityDocument) }
                catch { Write-Warning $_.Exception.Message }
        }
        if ($null -ne $window) {
            try { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($window) }
                catch { Write-Warning $_.Exception.Message }
        }
        if ($null -ne $documents) {
            try { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($documents) }
                catch { Write-Warning $_.Exception.Message }
        }
        if ($null -ne $word) {
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
            try { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word) }
                catch { Write-Warning $_.Exception.Message }
        }
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        try { Write-AtomicSignal -Path $CleanupAcknowledgementPath }
        catch { Write-Warning $_.Exception.Message }
    }
}

function Read-WorkerError {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return [System.IO.File]::ReadAllText($Path)
    }
    return "No worker error detail was available."
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
[void](Find-WordExecutable)

if ($Worker) {
    if ([string]::IsNullOrWhiteSpace($OwnedWordReceiptPath)) {
        throw "OwnedWordReceiptPath is required in worker mode."
    }
    if ([string]::IsNullOrWhiteSpace($ParentAcknowledgementPath)) {
        throw "ParentAcknowledgementPath is required in worker mode."
    }
    foreach ($requiredWorkerPath in @(
        $WorkerContainmentReceiptPath,
        $ParentContainmentAcknowledgementPath,
        $ParentCancellationPath,
        $WorkerCleanupAcknowledgementPath
    )) {
        if ([string]::IsNullOrWhiteSpace($requiredWorkerPath)) {
            throw "All worker containment and cancellation paths are required in worker mode."
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($TestWorkerPidPath)) {
        [System.IO.File]::WriteAllText(
            [System.IO.Path]::GetFullPath($TestWorkerPidPath),
            $PID.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            [System.Text.Encoding]::ASCII
        )
    }
    try {
        Invoke-WordPdfExport -SourcePath $source -DestinationPath $destination `
            -ReceiptPath $OwnedWordReceiptPath `
            -AcknowledgementPath $ParentAcknowledgementPath `
            -ContainmentReceiptPath $WorkerContainmentReceiptPath `
            -ContainmentAcknowledgementPath $ParentContainmentAcknowledgementPath `
            -CancellationPath $ParentCancellationPath `
            -CleanupAcknowledgementPath $WorkerCleanupAcknowledgementPath `
            -HandshakeTimeoutSeconds ($StartupTimeoutSeconds + 10) `
            -ExportDelaySeconds $TestExportDelaySeconds `
            -PreReceiptDelaySeconds $TestPreReceiptDelaySeconds `
            -WordActivatedPath $TestWordActivatedPath `
            -ForceAttributionFailure $TestAttributionFailure.IsPresent
        exit 0
    }
    catch {
        if (-not [string]::IsNullOrWhiteSpace($WorkerErrorPath)) {
            [System.IO.File]::WriteAllText(
                $WorkerErrorPath,
                $_.Exception.ToString(),
                [System.Text.UTF8Encoding]::new($false)
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
$identityReceipt = Join-Path $operationDirectory "word-identity.json"
$containmentReceipt = Join-Path $operationDirectory "word-containment-identity.json"
$containmentAcknowledgement = Join-Path $operationDirectory "word-contained.ack"
$parentAcknowledgement = Join-Path $operationDirectory "parent-validated.ack"
$parentCancellation = Join-Path $operationDirectory "parent-cancel.signal"
$workerCleanupAcknowledgement = Join-Path $operationDirectory "worker-cleanup.ack"
$workerError = Join-Path $operationDirectory "worker-error.txt"
$child = $null
$handshakeComplete = $false
$containmentEstablished = $false
$wordCleanupAttempted = $false
$ownedWordTerminated = $false
$preserveOperationDirectory = $false

try {
    $commandParts = @(
        "& " + (ConvertTo-SingleQuotedLiteral -Value $PSCommandPath),
        "-DocxPath " + (ConvertTo-SingleQuotedLiteral -Value $source),
        "-PdfPath " + (ConvertTo-SingleQuotedLiteral -Value $destination),
        "-TimeoutSeconds $TimeoutSeconds",
        "-StartupTimeoutSeconds $StartupTimeoutSeconds",
        "-Worker",
        "-OwnedWordReceiptPath " + (ConvertTo-SingleQuotedLiteral -Value $identityReceipt),
        "-ParentAcknowledgementPath " + (ConvertTo-SingleQuotedLiteral -Value $parentAcknowledgement),
        "-WorkerContainmentReceiptPath " + (ConvertTo-SingleQuotedLiteral -Value $containmentReceipt),
        "-ParentContainmentAcknowledgementPath " + (ConvertTo-SingleQuotedLiteral -Value $containmentAcknowledgement),
        "-ParentCancellationPath " + (ConvertTo-SingleQuotedLiteral -Value $parentCancellation),
        "-WorkerCleanupAcknowledgementPath " + (ConvertTo-SingleQuotedLiteral -Value $workerCleanupAcknowledgement),
        "-WorkerErrorPath " + (ConvertTo-SingleQuotedLiteral -Value $workerError),
        "-TestExportDelaySeconds $TestExportDelaySeconds",
        "-TestPreReceiptDelaySeconds $TestPreReceiptDelaySeconds"
    )
    if ($TestAttributionFailure) {
        $commandParts += "-TestAttributionFailure"
    }
    if (-not [string]::IsNullOrWhiteSpace($TestWorkerPidPath)) {
        $commandParts += "-TestWorkerPidPath " + (
            ConvertTo-SingleQuotedLiteral -Value ([System.IO.Path]::GetFullPath($TestWorkerPidPath))
        )
    }
    if (-not [string]::IsNullOrWhiteSpace($TestWordActivatedPath)) {
        $commandParts += "-TestWordActivatedPath " + (
            ConvertTo-SingleQuotedLiteral -Value ([System.IO.Path]::GetFullPath($TestWordActivatedPath))
        )
    }
    $command = $commandParts -join " "
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $powerShellExecutable = (Get-Process -Id $PID).Path
    $child = Start-Process -FilePath $powerShellExecutable -ArgumentList @(
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy", "Bypass",
        "-EncodedCommand", $encodedCommand
    ) -WindowStyle Hidden -PassThru
    $startupDeadline = [datetime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while (-not $handshakeComplete -and [datetime]::UtcNow -lt $startupDeadline) {
        if (-not $containmentEstablished -and (Test-Path -LiteralPath $containmentReceipt -PathType Leaf)) {
            $containedWord = Get-ValidatedOwnedWordProcess -ReceiptPath $containmentReceipt
            try {
                $containmentEstablished = $true
                Write-AtomicSignal -Path $containmentAcknowledgement
            }
            finally {
                $containedWord.Dispose()
            }
        }
        if (Test-Path -LiteralPath $identityReceipt -PathType Leaf) {
            if ($TestParentValidationDelaySeconds -gt 0) {
                Start-Sleep -Seconds $TestParentValidationDelaySeconds
            }
            $validatedWord = Get-ValidatedOwnedWordProcess -ReceiptPath $identityReceipt
            $validatedWord.Dispose()
            Write-AtomicSignal -Path $parentAcknowledgement
            $handshakeComplete = $true
            break
        }
        if ($child.HasExited) {
            break
        }
        Start-Sleep -Milliseconds 50
    }

    if (-not $handshakeComplete) {
        $startupTimedOut = -not $child.HasExited
        if (-not $child.HasExited) {
            Write-AtomicSignal -Path $parentCancellation
            $cleanupDeadline = [datetime]::UtcNow.AddSeconds(10)
            while (
                -not $child.HasExited -and
                -not (Test-Path -LiteralPath $workerCleanupAcknowledgement -PathType Leaf) -and
                [datetime]::UtcNow -lt $cleanupDeadline
            ) {
                Start-Sleep -Milliseconds 50
            }
        }
        if (
            -not $child.HasExited -and
            (Test-Path -LiteralPath $workerCleanupAcknowledgement -PathType Leaf)
        ) {
            [void]$child.WaitForExit(5000)
        }
        if (-not $child.HasExited -and $containmentEstablished) {
            Stop-ValidatedOwnedWordProcess -ReceiptPath $containmentReceipt
            $wordCleanupAttempted = $true
            $ownedWordTerminated = $true
            $child.Kill()
            [void]$child.WaitForExit(5000)
        }
        if ($startupTimedOut) {
            if (-not $containmentEstablished -and -not $child.HasExited) {
                $preserveOperationDirectory = $true
                throw (
                    "Word startup timeout occurred before causal ownership validation; cancellation was " +
                    "requested but cleanup was not acknowledged. The worker and Word were not " +
                    "force-terminated."
                )
            }
            throw (
                "Word startup timeout after $StartupTimeoutSeconds second(s); the worker " +
                "performed cooperative COM cleanup, or the revalidated causally owned Word process was terminated."
            )
        }
        $detail = Read-WorkerError -Path $workerError
        throw (
            "causal Word attribution failed during startup; COM cleanup was attempted when possible, " +
            "and no Word process lacking a validated causal identity was terminated. " +
            "$detail"
        )
    }

    if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
        $validatedWord = Get-ValidatedOwnedWordProcess -ReceiptPath $identityReceipt
        $validatedWord.Dispose()
        Stop-ValidatedOwnedWordProcess -ReceiptPath $identityReceipt
        $wordCleanupAttempted = $true
        $ownedWordTerminated = $true
        if (-not $child.HasExited) {
            $child.Kill()
            [void]$child.WaitForExit(5000)
        }
        throw "Word PDF export exceeded the $TimeoutSeconds second timeout."
    }

    $child.Refresh()
    if ($child.ExitCode -ne 0) {
        throw "Word PDF export worker failed with exit code $($child.ExitCode). $(Read-WorkerError -Path $workerError)"
    }
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "Word PDF export completed without producing: $destination"
    }
}
finally {
    if ($null -ne $child) {
        try {
            if (-not $child.HasExited) {
                try { Write-AtomicSignal -Path $parentCancellation } catch { Write-Warning $_.Exception.Message }
                $cleanupDeadline = [datetime]::UtcNow.AddSeconds(10)
                while (
                    -not $child.HasExited -and
                    -not (Test-Path -LiteralPath $workerCleanupAcknowledgement -PathType Leaf) -and
                    [datetime]::UtcNow -lt $cleanupDeadline
                ) {
                    Start-Sleep -Milliseconds 50
                }
            }
            if (
                -not $child.HasExited -and
                (Test-Path -LiteralPath $workerCleanupAcknowledgement -PathType Leaf)
            ) {
                [void]$child.WaitForExit(5000)
            }
            if (-not $child.HasExited -and $containmentEstablished -and -not $wordCleanupAttempted) {
                try {
                    Stop-ValidatedOwnedWordProcess -ReceiptPath $containmentReceipt
                    $wordCleanupAttempted = $true
                    $ownedWordTerminated = $true
                }
                catch { Write-Warning $_.Exception.Message }
                if (-not $child.HasExited -and $ownedWordTerminated) {
                    $child.Kill()
                    [void]$child.WaitForExit(5000)
                }
            }
            if (-not $child.HasExited) {
                $preserveOperationDirectory = $true
            }
            if (-not $child.HasExited) {
                [void]$child.WaitForExit(5000)
            }
        }
        finally {
            $child.Dispose()
        }
    }
    if (-not $preserveOperationDirectory -and (Test-Path -LiteralPath $operationDirectory)) {
        Remove-Item -LiteralPath $operationDirectory -Recurse -Force
    }
}
