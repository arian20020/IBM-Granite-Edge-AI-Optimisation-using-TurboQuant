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
    [ValidateRange(0, 300)]
    [int]$TestPreCausalReceiptDelaySeconds = 0,

    [Parameter(DontShow = $true)]
    [switch]$TestCleanupFailure,

    [Parameter(DontShow = $true)]
    [switch]$TestForceKillDoesNotExit,

    [Parameter(DontShow = $true)]
    [switch]$TestCloseIdentityWindowBeforeExport,

    [Parameter(DontShow = $true)]
    [string]$TestOperationDirectoryPath,

    [Parameter(DontShow = $true)]
    [string]$ParentAcknowledgementPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerContainmentReceiptPath,

    [Parameter(DontShow = $true)]
    [string]$ParentContainmentAcknowledgementPath,

    [Parameter(DontShow = $true)]
    [string]$ParentCancellationPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerCleanupAcknowledgementPath,

    [Parameter(DontShow = $true)]
    [string]$WorkerIdentityReceiptPath
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

function Write-JsonReceipt {
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Write-WordIdentityReceipt -Identity $Value -Path $Path
}

function Get-ProcessIdentity {
    param([Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process)

    return [ordered]@{
        pid = $Process.Id
        process_start_time_utc_ticks = $Process.StartTime.ToUniversalTime().Ticks
        executable_path = [System.IO.Path]::GetFullPath($Process.Path)
    }
}

function Test-ProcessIdentityGone {
    param([Parameter(Mandatory = $true)]$Identity)

    try {
        $candidate = [System.Diagnostics.Process]::GetProcessById([int]$Identity.pid)
    }
    catch [System.ArgumentException] {
        return $true
    }
    catch { return $false }
    try {
        if ($candidate.HasExited) {
            return $true
        }
        try {
            return -not (
                $candidate.StartTime.ToUniversalTime().Ticks -eq [int64]$Identity.process_start_time_utc_ticks -and
                [System.StringComparer]::OrdinalIgnoreCase.Equals(
                    [System.IO.Path]::GetFullPath($candidate.Path),
                    [System.IO.Path]::GetFullPath([string]$Identity.executable_path)
                )
            )
        }
        catch {
            return $candidate.HasExited
        }
    }
    finally {
        $candidate.Dispose()
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
    param(
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [bool]$RequireLiveWindow = $true
    )

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

    if ($RequireLiveWindow) {
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
            throw "The recorded Word process identity no longer matches PID, start time, and executable path."
        }
        return $candidate
    }
    catch {
        $candidate.Dispose()
        throw
    }
}

function Stop-ValidatedOwnedWordProcess {
    param(
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [bool]$SimulateNonExit = $false
    )

    $identity = [System.IO.File]::ReadAllText($ReceiptPath) | ConvertFrom-Json
    $ownedWord = Get-ValidatedOwnedWordProcess -ReceiptPath $ReceiptPath -RequireLiveWindow $false
    try {
        if ($SimulateNonExit) {
            throw "The causally owned Word process did not exit after force termination."
        }
        $ownedWord.Kill()
        if (-not $ownedWord.WaitForExit(5000)) {
            throw "The causally owned Word process did not exit after force termination."
        }
    }
    finally {
        $ownedWord.Dispose()
    }
    if (-not (Test-ProcessIdentityGone -Identity $identity)) {
        throw "The causally owned Word process still matches its recorded identity after force termination."
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
        [Parameter(Mandatory = $true)][int]$PreCausalReceiptDelaySeconds,
        [AllowEmptyString()][string]$WordActivatedPath,
        [Parameter(Mandatory = $true)][bool]$ForceAttributionFailure,
        [Parameter(Mandatory = $true)][bool]$InjectCleanupFailure,
        [Parameter(Mandatory = $true)][bool]$CloseIdentityWindowBeforeExport
    )

    $word = $null
    $documents = $null
    $identityDocument = $null
    $document = $null
    $window = $null
    $identity = $null
    $operationError = $null
    $attempts = [ordered]@{}
    foreach ($attemptName in @(
        "document_close", "document_release", "identity_document_close",
        "identity_document_release", "window_release", "documents_release",
        "application_quit", "application_release"
    )) {
        $attempts[$attemptName] = [ordered]@{
            attempted = $false
            succeeded = $false
            error = $null
        }
    }
    try {
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
            if ($PreCausalReceiptDelaySeconds -gt 0) {
                Start-Sleep -Seconds $PreCausalReceiptDelaySeconds
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

            $attempts.identity_document_close.attempted = $true
            [object]$earlySaveIdentityChanges = 0
            $identityDocument.Close([ref]$earlySaveIdentityChanges)
            $attempts.identity_document_close.succeeded = $true
            $attempts.identity_document_release.attempted = $true
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($identityDocument)
            $attempts.identity_document_release.succeeded = $true
            $identityDocument = $null
            $attempts.window_release.attempted = $true
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($window)
            $attempts.window_release.succeeded = $true
            $window = $null
            if ($CloseIdentityWindowBeforeExport) {
                Start-Sleep -Milliseconds 500
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
        catch {
            $operationError = $_
        }
    }
    finally {
        if ($null -ne $document) {
            $attempts.document_close.attempted = $true
            [object]$saveDocumentChanges = 0
            try {
                $document.Close([ref]$saveDocumentChanges)
                $attempts.document_close.succeeded = $true
            }
            catch { $attempts.document_close.error = $_.Exception.Message }
            $attempts.document_release.attempted = $true
            try {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document)
                $attempts.document_release.succeeded = $true
            }
            catch { $attempts.document_release.error = $_.Exception.Message }
        }
        if ($null -ne $identityDocument) {
            $attempts.identity_document_close.attempted = $true
            [object]$saveIdentityChanges = 0
            try {
                $identityDocument.Close([ref]$saveIdentityChanges)
                $attempts.identity_document_close.succeeded = $true
            }
            catch { $attempts.identity_document_close.error = $_.Exception.Message }
            $attempts.identity_document_release.attempted = $true
            try {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($identityDocument)
                $attempts.identity_document_release.succeeded = $true
            }
            catch { $attempts.identity_document_release.error = $_.Exception.Message }
        }
        if ($null -ne $window) {
            $attempts.window_release.attempted = $true
            try {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($window)
                $attempts.window_release.succeeded = $true
            }
            catch { $attempts.window_release.error = $_.Exception.Message }
        }
        if ($null -ne $documents) {
            $attempts.documents_release.attempted = $true
            try {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($documents)
                $attempts.documents_release.succeeded = $true
            }
            catch { $attempts.documents_release.error = $_.Exception.Message }
        }
        if ($null -ne $word) {
            $attempts.application_quit.attempted = $true
            [object]$saveWordChanges = 0
            [object]$originalFormat = 0
            [object]$routeDocument = 0
            try {
                $word.Quit(
                    [ref]$saveWordChanges,
                    [ref]$originalFormat,
                    [ref]$routeDocument
                )
                if ($InjectCleanupFailure) {
                    throw "Application.Quit failure injected for cleanup verification testing."
                }
                $attempts.application_quit.succeeded = $true
            }
            catch { $attempts.application_quit.error = $_.Exception.Message }
            $attempts.application_release.attempted = $true
            try {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word)
                $attempts.application_release.succeeded = $true
            }
            catch { $attempts.application_release.error = $_.Exception.Message }
        }
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        $wordExited = $false
        if ($null -ne $identity) {
            $exitDeadline = [datetime]::UtcNow.AddSeconds(5)
            do {
                $wordExited = Test-ProcessIdentityGone -Identity $identity
                if (-not $wordExited) { Start-Sleep -Milliseconds 50 }
            } while (-not $wordExited -and [datetime]::UtcNow -lt $exitDeadline)
        }
        $attemptsSucceeded = $true
        foreach ($attempt in $attempts.Values) {
            if ($attempt.attempted -and -not $attempt.succeeded) {
                $attemptsSucceeded = $false
            }
        }
        $cleanup = [ordered]@{
            source = "worker"
            attempts = $attempts
            word_exited = $wordExited
            worker_exited = $false
            cleanup_succeeded = ($wordExited -and $attemptsSucceeded)
        }
        Write-JsonReceipt -Value $cleanup -Path $CleanupAcknowledgementPath
    }
    if ($null -ne $operationError) {
        throw $operationError
    }
    if (-not $cleanup.cleanup_succeeded) {
        throw "Word cleanup verification failed; see the preserved cleanup receipt."
    }
}

function Get-ValidatedProcessFromReceipt {
    param(
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [Parameter(Mandatory = $true)][string]$ExpectedExecutablePath,
        [int]$ExpectedPid = 0
    )

    try {
        $identity = [System.IO.File]::ReadAllText($ReceiptPath) | ConvertFrom-Json
        $recordedPid = [int]$identity.pid
        $recordedTicks = [int64]$identity.process_start_time_utc_ticks
        $recordedPath = [System.IO.Path]::GetFullPath([string]$identity.executable_path)
    }
    catch {
        throw "The process identity receipt is missing or malformed. $($_.Exception.Message)"
    }
    if ($recordedPid -le 0 -or $recordedTicks -le 0) {
        throw "The process identity receipt contains invalid values."
    }
    if ($ExpectedPid -gt 0 -and $recordedPid -ne $ExpectedPid) {
        throw "The process identity receipt does not identify the expected child process."
    }
    if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals(
        $recordedPath,
        [System.IO.Path]::GetFullPath($ExpectedExecutablePath)
    )) {
        throw "The process identity receipt executable does not match the expected executable."
    }

    $candidate = Get-Process -Id $recordedPid -ErrorAction Stop
    try {
        if (
            $candidate.StartTime.ToUniversalTime().Ticks -ne $recordedTicks -or
            -not [System.StringComparer]::OrdinalIgnoreCase.Equals(
                [System.IO.Path]::GetFullPath($candidate.Path),
                $recordedPath
            )
        ) {
            throw "The process no longer matches its recorded PID, start time, and executable path."
        }
        return $candidate
    }
    catch {
        $candidate.Dispose()
        throw
    }
}

function Stop-ValidatedProcessFromReceipt {
    param(
        [Parameter(Mandatory = $true)][string]$ReceiptPath,
        [Parameter(Mandatory = $true)][string]$ExpectedExecutablePath,
        [int]$ExpectedPid = 0
    )

    $identity = [System.IO.File]::ReadAllText($ReceiptPath) | ConvertFrom-Json
    $process = Get-ValidatedProcessFromReceipt `
        -ReceiptPath $ReceiptPath `
        -ExpectedExecutablePath $ExpectedExecutablePath `
        -ExpectedPid $ExpectedPid
    try {
        $process.Kill()
        if (-not $process.WaitForExit(5000)) {
            throw "The causally identified process did not exit after force termination."
        }
    }
    finally {
        $process.Dispose()
    }
    if (-not (Test-ProcessIdentityGone -Identity $identity)) {
        throw "The causally identified process still matches its identity after force termination."
    }
}

function Test-VerifiedWorkerCleanupReceipt {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }
    try {
        $cleanup = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
        return (
            $cleanup.word_exited -eq $true -and
            $cleanup.cleanup_succeeded -eq $true
        )
    }
    catch {
        return $false
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
        $WorkerCleanupAcknowledgementPath,
        $WorkerIdentityReceiptPath
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
    $workerProcess = Get-Process -Id $PID -ErrorAction Stop
    try {
        Write-JsonReceipt `
            -Value (Get-ProcessIdentity -Process $workerProcess) `
            -Path $WorkerIdentityReceiptPath
    }
    finally {
        $workerProcess.Dispose()
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
            -PreCausalReceiptDelaySeconds $TestPreCausalReceiptDelaySeconds `
            -WordActivatedPath $TestWordActivatedPath `
            -ForceAttributionFailure $TestAttributionFailure.IsPresent `
            -InjectCleanupFailure $TestCleanupFailure.IsPresent `
            -CloseIdentityWindowBeforeExport $TestCloseIdentityWindowBeforeExport.IsPresent
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

$externalOperationDirectory = -not [string]::IsNullOrWhiteSpace($TestOperationDirectoryPath)
if ($externalOperationDirectory) {
    $operationDirectory = [System.IO.Path]::GetFullPath($TestOperationDirectoryPath)
}
else {
    $operationDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
        "granite-final-results-pdf-" + [System.Guid]::NewGuid().ToString("N")
    )
}
[void][System.IO.Directory]::CreateDirectory($operationDirectory)
$identityReceipt = Join-Path $operationDirectory "word-identity.json"
$containmentReceipt = Join-Path $operationDirectory "word-containment-identity.json"
$workerIdentityReceipt = Join-Path $operationDirectory "worker-identity.json"
$containmentAcknowledgement = Join-Path $operationDirectory "word-contained.ack"
$parentAcknowledgement = Join-Path $operationDirectory "parent-validated.ack"
$parentCancellation = Join-Path $operationDirectory "parent-cancel.signal"
$workerCleanupAcknowledgement = Join-Path $operationDirectory "worker-cleanup.json"
$parentTerminationReceipt = Join-Path $operationDirectory "parent-termination.json"
$workerError = Join-Path $operationDirectory "worker-error.txt"
$child = $null
$handshakeComplete = $false
$containmentEstablished = $false
$cleanupVerified = $false
$terminationVerified = $false
$preserveOperationDirectory = $externalOperationDirectory

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
        "-WorkerIdentityReceiptPath " + (ConvertTo-SingleQuotedLiteral -Value $workerIdentityReceipt),
        "-WorkerErrorPath " + (ConvertTo-SingleQuotedLiteral -Value $workerError),
        "-TestExportDelaySeconds $TestExportDelaySeconds",
        "-TestPreReceiptDelaySeconds $TestPreReceiptDelaySeconds",
        "-TestPreCausalReceiptDelaySeconds $TestPreCausalReceiptDelaySeconds"
    )
    if ($TestAttributionFailure) {
        $commandParts += "-TestAttributionFailure"
    }
    if ($TestCleanupFailure) {
        $commandParts += "-TestCleanupFailure"
    }
    if ($TestCloseIdentityWindowBeforeExport) {
        $commandParts += "-TestCloseIdentityWindowBeforeExport"
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
            $cleanupDeadline = [datetime]::UtcNow.AddSeconds(2)
            while (
                -not $child.HasExited -and
                [datetime]::UtcNow -lt $cleanupDeadline
            ) {
                Start-Sleep -Milliseconds 50
            }
        }
        $cleanupVerified = ($child.HasExited -and (
            Test-VerifiedWorkerCleanupReceipt -Path $workerCleanupAcknowledgement
        ))
        if ($startupTimedOut) {
            if (-not $cleanupVerified) {
                $terminationStatus = [ordered]@{
                    source = "parent_precausal_safe_failure"
                    cleanup_succeeded = $false
                    word_exited = $false
                    worker_exited = $false
                    error = $null
                }
                if (-not $containmentEstablished) {
                    $terminationStatus.error = (
                        "cleanup_unverified: no causal Word HWND receipt exists; possible orphan. " +
                        "No Word PID or worker was force-terminated."
                    )
                    $preserveOperationDirectory = $true
                    Write-JsonReceipt -Value $terminationStatus -Path $parentTerminationReceipt
                    throw (
                        "Word startup timeout cleanup_unverified; possible orphan. No causal Word " +
                        "receipt existed, so no Word PID or worker was force-terminated."
                    )
                }
                try {
                    $terminationStatus.source = "parent_causal_receipt_fallback"
                    Stop-ValidatedOwnedWordProcess -ReceiptPath $containmentReceipt
                    $terminationStatus.word_exited = $true
                    $workerIdentity = [System.IO.File]::ReadAllText($workerIdentityReceipt) | ConvertFrom-Json
                    if (-not (Test-ProcessIdentityGone -Identity $workerIdentity)) {
                        Stop-ValidatedProcessFromReceipt `
                            -ReceiptPath $workerIdentityReceipt `
                            -ExpectedExecutablePath $powerShellExecutable `
                            -ExpectedPid $child.Id
                    }
                    $terminationStatus.worker_exited = Test-ProcessIdentityGone -Identity $workerIdentity
                    $terminationStatus.cleanup_succeeded = (
                        $terminationStatus.word_exited -and $terminationStatus.worker_exited
                    )
                    $terminationVerified = $terminationStatus.cleanup_succeeded
                }
                catch {
                    $terminationStatus.error = $_.Exception.Message
                    $preserveOperationDirectory = $true
                    Write-JsonReceipt -Value $terminationStatus -Path $parentTerminationReceipt
                    throw (
                        "Word startup timeout cleanup could not be verified; causal evidence was preserved. " +
                        $_.Exception.Message
                    )
                }
                Write-JsonReceipt -Value $terminationStatus -Path $parentTerminationReceipt
            }
            if (-not ($cleanupVerified -or $terminationVerified)) {
                $preserveOperationDirectory = $true
                throw "Word startup timeout cleanup was not verified; causal evidence was preserved."
            }
            throw (
                "Word startup timeout after $StartupTimeoutSeconds second(s); owned Word and worker exit were verified."
            )
        }
        if (-not $cleanupVerified) {
            $preserveOperationDirectory = $true
            throw "Word cleanup verification failed during startup; causal evidence was preserved."
        }
        $detail = Read-WorkerError -Path $workerError
        throw (
            "causal Word attribution failed during startup; COM cleanup was attempted when possible, " +
            "and no Word process lacking a validated causal identity was terminated. " +
            "$detail"
        )
    }

    if (-not $child.WaitForExit($TimeoutSeconds * 1000)) {
        $terminationStatus = [ordered]@{
            source = "parent_export_timeout"
            cleanup_succeeded = $false
            word_exited = $false
            worker_exited = $false
            error = $null
        }
        try {
            Stop-ValidatedOwnedWordProcess `
                -ReceiptPath $identityReceipt `
                -SimulateNonExit $TestForceKillDoesNotExit.IsPresent
            $terminationStatus.word_exited = $true
            $workerIdentity = [System.IO.File]::ReadAllText($workerIdentityReceipt) | ConvertFrom-Json
            if (-not (Test-ProcessIdentityGone -Identity $workerIdentity)) {
                Stop-ValidatedProcessFromReceipt `
                    -ReceiptPath $workerIdentityReceipt `
                    -ExpectedExecutablePath $powerShellExecutable `
                    -ExpectedPid $child.Id
            }
            $terminationStatus.worker_exited = Test-ProcessIdentityGone -Identity $workerIdentity
            $terminationStatus.cleanup_succeeded = (
                $terminationStatus.word_exited -and $terminationStatus.worker_exited
            )
            $terminationVerified = $terminationStatus.cleanup_succeeded
        }
        catch {
            $terminationStatus.error = $_.Exception.Message
            $preserveOperationDirectory = $true
            Write-JsonReceipt -Value $terminationStatus -Path $parentTerminationReceipt
            throw $_
        }
        Write-JsonReceipt -Value $terminationStatus -Path $parentTerminationReceipt
        throw "Word PDF export exceeded the $TimeoutSeconds second timeout."
    }

    $child.Refresh()
    $cleanupVerified = Test-VerifiedWorkerCleanupReceipt -Path $workerCleanupAcknowledgement
    if (-not $cleanupVerified) {
        $preserveOperationDirectory = $true
        throw "Word cleanup verification failed; causal receipts were preserved."
    }
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
                    [datetime]::UtcNow -lt $cleanupDeadline
                ) {
                    Start-Sleep -Milliseconds 50
                }
            }
            if ($child.HasExited -and -not $cleanupVerified -and -not $terminationVerified) {
                $cleanupVerified = Test-VerifiedWorkerCleanupReceipt -Path $workerCleanupAcknowledgement
            }
            if (-not $child.HasExited) { $preserveOperationDirectory = $true }
        }
        finally {
            $child.Dispose()
        }
    }
    if (-not ($cleanupVerified -or $terminationVerified)) {
        $preserveOperationDirectory = $true
    }
    if (-not $preserveOperationDirectory -and (Test-Path -LiteralPath $operationDirectory)) {
        Remove-Item -LiteralPath $operationDirectory -Recurse -Force
    }
}
