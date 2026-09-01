import base64
import csv
import json
import os
import subprocess
import sys
import time
from contextlib import contextmanager
from datetime import date
from pathlib import Path

import pytest
from pypdf import PdfReader


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.report_model import Report, ReportParagraph, ReportSection


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
EXPORT_SCRIPT = REPOSITORY_ROOT / "scripts" / "testing" / "Export-Final-Results-Pdf.ps1"


def _find_word_executable() -> Path | None:
    if sys.platform != "win32":
        return None
    import winreg

    registry_views = (winreg.KEY_WOW64_64KEY, winreg.KEY_WOW64_32KEY)
    for hive in (winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE):
        for view in registry_views:
            try:
                with winreg.OpenKey(
                    hive,
                    r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE",
                    0,
                    winreg.KEY_READ | view,
                ) as key:
                    registered = Path(winreg.QueryValue(key, None))
            except OSError:
                continue
            if registered.is_file():
                return registered

    for environment_name in ("ProgramFiles", "ProgramFiles(x86)"):
        program_files = os.environ.get(environment_name)
        if not program_files:
            continue
        for relative in (
            Path("Microsoft Office/root/Office16/WINWORD.EXE"),
            Path("Microsoft Office/Office16/WINWORD.EXE"),
        ):
            candidate = Path(program_files) / relative
            if candidate.is_file():
                return candidate
    return None


WORD_EXE = _find_word_executable()


def _renderer():
    from scripts.testing.final_results.docx_renderer import render_docx

    return render_docx


def _powershell() -> str:
    return str(
        Path(os.environ.get("SystemRoot", r"C:\Windows"))
        / "System32"
        / "WindowsPowerShell"
        / "v1.0"
        / "powershell.exe"
    )


def _invoke_export(
    docx: Path,
    pdf: Path,
    timeout_seconds: int,
    *extra_arguments: str,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(EXPORT_SCRIPT),
            "-DocxPath",
            str(docx),
            "-PdfPath",
            str(pdf),
            "-TimeoutSeconds",
            str(timeout_seconds),
            *extra_arguments,
        ],
        capture_output=True,
        text=True,
        timeout=timeout_seconds + 20,
        check=False,
    )


def _word_process_ids() -> set[int]:
    result = subprocess.run(
        ["tasklist.exe", "/FI", "IMAGENAME eq WINWORD.EXE", "/FO", "CSV", "/NH"],
        capture_output=True,
        text=True,
        check=True,
    )
    rows = csv.reader(result.stdout.splitlines())
    return {
        int(row[1])
        for row in rows
        if len(row) >= 2 and row[0].casefold() == "winword.exe"
    }


def _process_exists(process_id: int) -> bool:
    result = subprocess.run(
        ["tasklist.exe", "/FI", f"PID eq {process_id}", "/FO", "CSV", "/NH"],
        capture_output=True,
        text=True,
        check=True,
    )
    return any(
        len(row) >= 2 and row[1].isdigit() and int(row[1]) == process_id
        for row in csv.reader(result.stdout.splitlines())
    )


def _wait_for_process_exit(process_id: int, timeout_seconds: float = 15) -> bool:
    deadline = time.monotonic() + timeout_seconds
    while _process_exists(process_id) and time.monotonic() < deadline:
        time.sleep(0.1)
    return not _process_exists(process_id)


def _wait_for_word_process_ids(expected: set[int], timeout_seconds: float = 15) -> set[int]:
    deadline = time.monotonic() + timeout_seconds
    observed = _word_process_ids()
    while observed != expected and time.monotonic() < deadline:
        time.sleep(0.1)
        observed = _word_process_ids()
    return observed


def _read_test_process_identity(process_id: int) -> dict[str, object]:
    reader = r'''
$ErrorActionPreference = "Stop"
$process = Get-Process -Id ([int]$env:FINAL_RESULTS_TEST_PID) -ErrorAction Stop
try {
    @{
        pid = $process.Id
        process_start_time_utc_ticks = $process.StartTime.ToUniversalTime().Ticks
        executable_path = $process.Path
    } | ConvertTo-Json -Compress
}
finally {
    $process.Dispose()
}
'''
    encoded = base64.b64encode(reader.encode("utf-16-le")).decode("ascii")
    environment = os.environ.copy()
    environment["FINAL_RESULTS_TEST_PID"] = str(process_id)
    result = subprocess.run(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-EncodedCommand",
            encoded,
        ],
        check=True,
        capture_output=True,
        text=True,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    return json.loads(result.stdout)


def _causal_process_identity_is_gone(identity: dict[str, object]) -> bool:
    validator = r'''
$ErrorActionPreference = "Stop"
$identity = $env:FINAL_RESULTS_TEST_IDENTITY | ConvertFrom-Json
$process = $null
try {
    try {
        $process = [Diagnostics.Process]::GetProcessById([int]$identity.pid)
    }
    catch [ArgumentException] {
        exit 0
    }
    $matches = (
        $process.StartTime.ToUniversalTime().Ticks -eq [int64]$identity.process_start_time_utc_ticks -and
        [StringComparer]::OrdinalIgnoreCase.Equals(
            [IO.Path]::GetFullPath($process.MainModule.FileName),
            [IO.Path]::GetFullPath([string]$identity.executable_path)
        )
    )
    if ($matches) { exit 1 }
    exit 0
}
finally {
    if ($null -ne $process) { $process.Dispose() }
}
'''
    encoded = base64.b64encode(validator.encode("utf-16-le")).decode("ascii")
    environment = os.environ.copy()
    environment["FINAL_RESULTS_TEST_IDENTITY"] = json.dumps(identity, separators=(",", ":"))
    result = subprocess.run(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-EncodedCommand",
            encoded,
        ],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    return result.returncode == 0


def _terminate_causally_identified_test_word(identity: dict[str, object]) -> None:
    validator = r'''
$ErrorActionPreference = "Stop"
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class TestWordCleanupIdentity {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);
}
"@
$identity = $env:FINAL_RESULTS_TEST_IDENTITY | ConvertFrom-Json
$pidValue = [int]$identity.pid
$hwnd = [int64]$identity.hwnd
if ([TestWordCleanupIdentity]::IsWindow([IntPtr]$hwnd)) {
    [uint32]$mappedPid = 0
    [void][TestWordCleanupIdentity]::GetWindowThreadProcessId([IntPtr]$hwnd, [ref]$mappedPid)
    if ([int]$mappedPid -ne $pidValue) { exit 3 }
}
$process = Get-Process -Id $pidValue -ErrorAction Stop
try {
    $matches = (
        $process.ProcessName -ieq "WINWORD" -and
        $process.StartTime.ToUniversalTime().Ticks -eq [int64]$identity.process_start_time_utc_ticks -and
        [StringComparer]::OrdinalIgnoreCase.Equals(
            [IO.Path]::GetFullPath($process.Path),
            [IO.Path]::GetFullPath([string]$identity.executable_path)
        )
    )
    if (-not $matches) { exit 4 }
    $process.Kill()
    if (-not $process.WaitForExit(5000)) { exit 5 }
}
finally {
    $process.Dispose()
}
$candidate = $null
try {
    try { $candidate = [Diagnostics.Process]::GetProcessById($pidValue) }
    catch [ArgumentException] { exit 0 }
    $stillMatches = (
        $candidate.StartTime.ToUniversalTime().Ticks -eq [int64]$identity.process_start_time_utc_ticks -and
        [StringComparer]::OrdinalIgnoreCase.Equals(
            [IO.Path]::GetFullPath($candidate.MainModule.FileName),
            [IO.Path]::GetFullPath([string]$identity.executable_path)
        )
    )
    if ($stillMatches) { exit 6 }
}
finally {
    if ($null -ne $candidate) { $candidate.Dispose() }
}
'''
    encoded = base64.b64encode(validator.encode("utf-16-le")).decode("ascii")
    environment = os.environ.copy()
    environment["FINAL_RESULTS_TEST_IDENTITY"] = json.dumps(identity, separators=(",", ":"))
    subprocess.run(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-EncodedCommand",
            encoded,
        ],
        check=True,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    if not _causal_process_identity_is_gone(identity):
        raise AssertionError("causally identified test Word process did not exit")


def _terminate_causally_identified_test_process(identity: dict[str, object]) -> None:
    validator = r'''
$ErrorActionPreference = "Stop"
$identity = $env:FINAL_RESULTS_TEST_IDENTITY | ConvertFrom-Json
$pidValue = [int]$identity.pid
$process = Get-Process -Id $pidValue -ErrorAction Stop
try {
    $matches = (
        $process.StartTime.ToUniversalTime().Ticks -eq [int64]$identity.process_start_time_utc_ticks -and
        [StringComparer]::OrdinalIgnoreCase.Equals(
            [IO.Path]::GetFullPath($process.Path),
            [IO.Path]::GetFullPath([string]$identity.executable_path)
        )
    )
    if (-not $matches) { exit 4 }
    $process.Kill()
    if (-not $process.WaitForExit(5000)) { exit 5 }
}
finally {
    $process.Dispose()
}
$candidate = $null
try {
    try { $candidate = [Diagnostics.Process]::GetProcessById($pidValue) }
    catch [ArgumentException] { exit 0 }
    $stillMatches = (
        $candidate.StartTime.ToUniversalTime().Ticks -eq [int64]$identity.process_start_time_utc_ticks -and
        [StringComparer]::OrdinalIgnoreCase.Equals(
            [IO.Path]::GetFullPath($candidate.MainModule.FileName),
            [IO.Path]::GetFullPath([string]$identity.executable_path)
        )
    )
    if ($stillMatches) { exit 6 }
}
finally {
    if ($null -ne $candidate) { $candidate.Dispose() }
}
'''
    encoded = base64.b64encode(validator.encode("utf-16-le")).decode("ascii")
    environment = os.environ.copy()
    environment["FINAL_RESULTS_TEST_IDENTITY"] = json.dumps(identity, separators=(",", ":"))
    subprocess.run(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-EncodedCommand",
            encoded,
        ],
        check=True,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    if not _causal_process_identity_is_gone(identity):
        raise AssertionError("causally identified test worker process did not exit")


@contextmanager
def _pre_existing_hidden_word_session(tmp_path: Path):
    receipt = tmp_path / "pre-existing-word.json"
    stop = tmp_path / "stop-pre-existing-word"
    baseline = _word_process_ids()
    helper = r'''
$ErrorActionPreference = "Stop"
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class TestWordWindowIdentity {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
"@
$word = $null
$documents = $null
$document = $null
$window = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $documents = $word.Documents
    $document = $documents.Add()
    $window = $word.ActiveWindow
    $hwnd = [int64]$window.GetType().InvokeMember(
        "Hwnd",
        [System.Reflection.BindingFlags]::GetProperty,
        $null,
        $window,
        $null
    )
    [uint32]$wordPid = 0
    [void][TestWordWindowIdentity]::GetWindowThreadProcessId([IntPtr]$hwnd, [ref]$wordPid)
    $process = Get-Process -Id $wordPid -ErrorAction Stop
    try {
        @{
            pid = [int]$wordPid
            hwnd = $hwnd
            process_start_time_utc_ticks = $process.StartTime.ToUniversalTime().Ticks
            executable_path = $process.Path
        } | ConvertTo-Json -Compress | Set-Content -LiteralPath $env:FINAL_RESULTS_TEST_RECEIPT -Encoding UTF8
    }
    finally {
        $process.Dispose()
    }
    while (-not (Test-Path -LiteralPath $env:FINAL_RESULTS_TEST_STOP)) {
        Start-Sleep -Milliseconds 100
    }
}
finally {
    if ($null -ne $document) {
        [object]$saveDocumentChanges = 0
        try { $document.Close([ref]$saveDocumentChanges) } finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($document)
        }
    }
    if ($null -ne $window) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($window)
    }
    if ($null -ne $documents) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($documents)
    }
    if ($null -ne $word) {
        [object]$saveWordChanges = 0
        [object]$originalFormat = 0
        [object]$routeDocument = 0
        try { $word.Quit([ref]$saveWordChanges, [ref]$originalFormat, [ref]$routeDocument) } finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($word)
        }
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
'''
    encoded = base64.b64encode(helper.encode("utf-16-le")).decode("ascii")
    environment = os.environ.copy()
    environment["FINAL_RESULTS_TEST_RECEIPT"] = str(receipt)
    environment["FINAL_RESULTS_TEST_STOP"] = str(stop)
    process = subprocess.Popen(
        [
            _powershell(),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-EncodedCommand",
            encoded,
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    identity = None
    try:
        deadline = time.monotonic() + 20
        while not receipt.is_file() and process.poll() is None and time.monotonic() < deadline:
            time.sleep(0.1)
        if not receipt.is_file():
            stdout, stderr = process.communicate(timeout=5)
            raise AssertionError(f"hidden Word helper did not start: {stdout}{stderr}")
        identity = json.loads(receipt.read_text(encoding="utf-8-sig"))
        assert int(identity["pid"]) in _word_process_ids()
        yield baseline, identity
    finally:
        stop.touch()
        try:
            process.wait(timeout=20)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
            if identity is not None:
                _terminate_causally_identified_test_word(identity)
        assert _wait_for_word_process_ids(baseline) == baseline


def _pdf_report() -> Report:
    return Report(
        title="PDF export integration report",
        route_id="pdf-export-test",
        revision="R1",
        generated_date=date(2026, 8, 30),
        sections=(
            ReportSection(
                title="Result",
                blocks=(ReportParagraph("Searchable exported content."),),
            ),
        ),
    )


@pytest.mark.skipif(sys.platform != "win32", reason="Windows tasklist is required")
def test_process_exists_reports_the_current_process():
    assert _process_exists(os.getpid()) is True


@pytest.mark.skipif(sys.platform != "win32", reason="Windows process identity is required")
def test_test_teardown_helpers_reject_a_mismatched_causal_identity():
    identity = _read_test_process_identity(os.getpid())
    identity["process_start_time_utc_ticks"] = (
        int(identity["process_start_time_utc_ticks"]) + 1
    )

    with pytest.raises(subprocess.CalledProcessError):
        _terminate_causally_identified_test_process(identity)
    with pytest.raises(subprocess.CalledProcessError):
        _terminate_causally_identified_test_word({**identity, "hwnd": 0})


def test_pdf_export_rejects_a_missing_source_without_starting_word(tmp_path):
    result = _invoke_export(tmp_path / "missing.docx", tmp_path / "report.pdf", 5)

    assert result.returncode != 0
    assert "DOCX input does not exist" in (result.stdout + result.stderr)


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_pdf_export_is_hidden_read_only_bounded_and_searchable(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)
    before_mtime = docx.stat().st_mtime_ns
    started = time.monotonic()

    result = _invoke_export(docx, pdf, 60)

    assert result.returncode == 0, result.stdout + result.stderr
    assert time.monotonic() - started < 80
    assert docx.stat().st_mtime_ns == before_mtime
    assert pdf.read_bytes().startswith(b"%PDF-")
    reader = PdfReader(pdf)
    assert len(reader.pages) > 0
    assert "PDF export integration report" in "".join(page.extract_text() or "" for page in reader.pages)


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_successful_export_preserves_a_pre_existing_hidden_word_session(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        result = _invoke_export(docx, pdf, 60)

        assert result.returncode == 0, result.stdout + result.stderr
        assert int(pre_existing["pid"]) in _word_process_ids()
        assert _wait_for_word_process_ids(expected) == expected


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_export_timeout_kills_only_the_causally_attributed_word_instance(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        result = _invoke_export(
            docx,
            pdf,
            1,
            "-TestExportDelaySeconds",
            "5",
        )

        assert result.returncode != 0
        assert "export exceeded the 1 second timeout" in (result.stdout + result.stderr)
        assert int(pre_existing["pid"]) in _word_process_ids()
        assert _wait_for_word_process_ids(expected) == expected


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_attribution_failure_never_selects_or_kills_a_snapshot_word_pid(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        result = _invoke_export(docx, pdf, 10, "-TestAttributionFailure")

        assert result.returncode != 0
        assert "causal Word attribution failed" in (result.stdout + result.stderr)
        assert int(pre_existing["pid"]) in _word_process_ids()
        assert _wait_for_word_process_ids(expected) == expected


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_precausal_timeout_preserves_worker_for_cooperative_cleanup_and_preexisting_word(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    worker_pid_path = tmp_path / "export-worker.pid"
    activation_path = tmp_path / "export-word-activated.json"
    operation_directory = tmp_path / "startup-operation"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        try:
            result = _invoke_export(
                docx,
                pdf,
                60,
                "-StartupTimeoutSeconds",
                "8",
                "-TestPreCausalReceiptDelaySeconds",
                "40",
                "-TestWorkerPidPath",
                str(worker_pid_path),
                "-TestWordActivatedPath",
                str(activation_path),
                "-TestOperationDirectoryPath",
                str(operation_directory),
            )

            assert result.returncode != 0
            assert "cleanup_unverified" in (result.stdout + result.stderr).casefold()
            activation = json.loads(activation_path.read_text(encoding="utf-8"))
            assert int(activation["pid"]) != int(pre_existing["pid"])
            worker_pid = int(worker_pid_path.read_text(encoding="ascii"))
            worker_identity_path = operation_directory / "worker-identity.json"
            worker_identity = json.loads(worker_identity_path.read_text(encoding="utf-8"))
            assert int(worker_identity["pid"]) == worker_pid
            cleanup = json.loads(
                (operation_directory / "parent-termination.json").read_text(encoding="utf-8")
            )
            assert cleanup["source"] == "parent_precausal_safe_failure"
            assert cleanup["cleanup_succeeded"] is False
            assert cleanup["word_exited"] is False
            assert int(pre_existing["pid"]) in _word_process_ids()
            assert _process_exists(worker_pid) is True
            # The test deliberately withholds the causal containment receipt for
            # 40 seconds. Give that exact worker a slightly larger bounded window
            # to finish cooperative cleanup after the parent safely declines to
            # terminate any pre-receipt Word PID.
            assert _wait_for_process_exit(worker_pid, timeout_seconds=45)
            worker_cleanup = json.loads(
                (operation_directory / "worker-cleanup.json").read_text(encoding="utf-8")
            )
            assert worker_cleanup["source"] == "worker"
            assert worker_cleanup["word_exited"] is True
            assert worker_cleanup["cleanup_succeeded"] is True
            assert _causal_process_identity_is_gone(activation)
            assert _causal_process_identity_is_gone(worker_identity)
            assert _wait_for_word_process_ids(expected) == expected
        finally:
            worker_identity_path = operation_directory / "worker-identity.json"
            teardown_errors: list[Exception] = []
            activation_identity = None
            worker_identity = None

            for receipt_path, assign in (
                (activation_path, "activation"),
                (worker_identity_path, "worker"),
            ):
                if not receipt_path.is_file():
                    continue
                try:
                    identity = json.loads(receipt_path.read_text(encoding="utf-8"))
                    if assign == "activation":
                        activation_identity = identity
                    else:
                        worker_identity = identity
                except Exception as error:
                    teardown_errors.append(error)

            if activation_identity is not None:
                try:
                    if not _causal_process_identity_is_gone(activation_identity):
                        _terminate_causally_identified_test_word(activation_identity)
                    assert _causal_process_identity_is_gone(activation_identity)
                except Exception as error:
                    teardown_errors.append(error)

            if worker_identity is not None:
                try:
                    if not _causal_process_identity_is_gone(worker_identity):
                        if activation_identity is None:
                            raise AssertionError(
                                "refusing to terminate the test worker without its causal Word identity"
                            )
                        if not _causal_process_identity_is_gone(activation_identity):
                            raise AssertionError(
                                "refusing to terminate the test worker before its causal Word exits"
                            )
                        _terminate_causally_identified_test_process(worker_identity)
                    assert _causal_process_identity_is_gone(worker_identity)
                except Exception as error:
                    teardown_errors.append(error)

            if teardown_errors:
                raise AssertionError(
                    "causal pre-receipt test teardown did not verify every available identity"
                ) from teardown_errors[0]


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_worker_waits_for_parent_identity_acknowledgement_before_fast_export(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)
    baseline = _word_process_ids()

    result = _invoke_export(
        docx,
        pdf,
        60,
        "-TestParentValidationDelaySeconds",
        "3",
    )

    assert result.returncode == 0, result.stdout + result.stderr
    assert pdf.read_bytes().startswith(b"%PDF-")
    assert _wait_for_word_process_ids(baseline) == baseline


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_cleanup_failure_receipt_is_not_treated_as_success(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    operation_directory = tmp_path / "cleanup-failure-operation"
    _renderer()(_pdf_report(), docx)
    baseline = _word_process_ids()

    result = _invoke_export(
        docx,
        pdf,
        60,
        "-TestCleanupFailure",
        "-TestOperationDirectoryPath",
        str(operation_directory),
    )

    assert result.returncode != 0
    assert "cleanup verification failed" in (result.stdout + result.stderr).casefold()
    cleanup = json.loads(
        (operation_directory / "worker-cleanup.json").read_text(encoding="utf-8")
    )
    assert cleanup["cleanup_succeeded"] is False
    assert cleanup["word_exited"] is True
    assert cleanup["attempts"]["application_quit"]["attempted"] is True
    assert cleanup["attempts"]["application_quit"]["succeeded"] is False
    assert _wait_for_word_process_ids(baseline) == baseline


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_cleanup_observation_can_verify_causal_exit_after_previous_five_second_limit(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    operation_directory = tmp_path / "delayed-cleanup-observation"
    _renderer()(_pdf_report(), docx)
    baseline = _word_process_ids()

    started = time.monotonic()
    result = _invoke_export(
        docx,
        pdf,
        60,
        "-TestCleanupFailure",
        "-TestMinimumCleanupObservationSeconds",
        "6",
        "-TestOperationDirectoryPath",
        str(operation_directory),
    )
    elapsed = time.monotonic() - started

    assert result.returncode != 0
    assert "cleanup verification failed" in (result.stdout + result.stderr).casefold()
    cleanup = json.loads(
        (operation_directory / "worker-cleanup.json").read_text(encoding="utf-8")
    )
    assert cleanup["cleanup_observation_timeout_seconds"] == 15
    assert cleanup["minimum_cleanup_observation_seconds"] == 6
    assert cleanup["cleanup_observation_elapsed_ms"] >= 6000
    assert cleanup["word_exited"] is True
    assert cleanup["cleanup_succeeded"] is False
    assert 6 <= elapsed < 30
    assert _wait_for_word_process_ids(baseline) == baseline


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_injected_false_force_kill_wait_result_preserves_failure_status_and_receipts(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    operation_directory = tmp_path / "kill-failure-operation"
    _renderer()(_pdf_report(), docx)
    baseline = _word_process_ids()

    result = _invoke_export(
        docx,
        pdf,
        1,
        "-TestExportDelaySeconds",
        "5",
        "-TestForceKillWaitResultFailure",
        "-TestOperationDirectoryPath",
        str(operation_directory),
    )

    assert result.returncode != 0
    assert "exit wait did not confirm force termination" in (result.stdout + result.stderr).casefold()
    termination = json.loads(
        (operation_directory / "parent-termination.json").read_text(encoding="utf-8")
    )
    assert termination["cleanup_succeeded"] is False
    assert termination["word_exited"] is False
    assert termination["kill_issued"] is True
    assert termination["wait_for_exit_succeeded"] is False
    assert (operation_directory / "word-identity.json").is_file()
    assert (operation_directory / "worker-identity.json").is_file()
    assert _wait_for_word_process_ids(baseline) == baseline


@pytest.mark.skipif(sys.platform != "win32" or WORD_EXE is None, reason="Microsoft Word is required")
def test_export_timeout_uses_stable_identity_after_original_hwnd_disappears(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        result = _invoke_export(
            docx,
            pdf,
            1,
            "-TestExportDelaySeconds",
            "5",
            "-TestCloseIdentityWindowBeforeExport",
        )

        assert result.returncode != 0
        assert "export exceeded the 1 second timeout" in (result.stdout + result.stderr)
        assert int(pre_existing["pid"]) in _word_process_ids()
        assert _wait_for_word_process_ids(expected) == expected
