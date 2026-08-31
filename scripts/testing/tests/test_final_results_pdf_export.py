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


def _wait_for_process_exit(process_id: int, timeout_seconds: float = 15) -> bool:
    deadline = time.monotonic() + timeout_seconds
    while _process_exists(process_id) and time.monotonic() < deadline:
        time.sleep(0.1)
    return not _process_exists(process_id)
    return any(
        len(row) >= 2 and row[1].isdigit() and int(row[1]) == process_id
        for row in csv.reader(result.stdout.splitlines())
    )


def _wait_for_word_process_ids(expected: set[int], timeout_seconds: float = 15) -> set[int]:
    deadline = time.monotonic() + timeout_seconds
    observed = _word_process_ids()
    while observed != expected and time.monotonic() < deadline:
        time.sleep(0.1)
        observed = _word_process_ids()
    return observed


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
[uint32]$mappedPid = 0
if (-not [TestWordCleanupIdentity]::IsWindow([IntPtr]$hwnd)) { exit 2 }
[void][TestWordCleanupIdentity]::GetWindowThreadProcessId([IntPtr]$hwnd, [ref]$mappedPid)
if ([int]$mappedPid -ne $pidValue) { exit 3 }
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
    [void]$process.WaitForExit(5000)
}
finally {
    $process.Dispose()
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
        check=False,
        env=environment,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )


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
def test_startup_timeout_contains_pre_receipt_word_and_worker_only(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    worker_pid_path = tmp_path / "export-worker.pid"
    activation_path = tmp_path / "export-word-activated.json"
    operation_directory = tmp_path / "startup-operation"
    _renderer()(_pdf_report(), docx)

    with _pre_existing_hidden_word_session(tmp_path) as (baseline, pre_existing):
        expected = baseline | {int(pre_existing["pid"])}
        result = _invoke_export(
            docx,
            pdf,
            60,
            "-StartupTimeoutSeconds",
            "8",
            "-TestPreCausalReceiptDelaySeconds",
            "20",
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
        cleanup = json.loads(
            (operation_directory / "parent-termination.json").read_text(encoding="utf-8")
        )
        assert cleanup["source"] == "parent_precausal_safe_failure"
        assert cleanup["cleanup_succeeded"] is False
        assert cleanup["word_exited"] is False
        assert int(pre_existing["pid"]) in _word_process_ids()
        assert _wait_for_process_exit(worker_pid)
        assert _wait_for_word_process_ids(expected) == expected


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
def test_nonterminating_force_kill_preserves_failure_status_and_causal_receipts(tmp_path):
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
        "-TestForceKillDoesNotExit",
        "-TestOperationDirectoryPath",
        str(operation_directory),
    )

    assert result.returncode != 0
    assert "did not exit after force termination" in (result.stdout + result.stderr).casefold()
    termination = json.loads(
        (operation_directory / "parent-termination.json").read_text(encoding="utf-8")
    )
    assert termination["cleanup_succeeded"] is False
    assert termination["word_exited"] is False
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
