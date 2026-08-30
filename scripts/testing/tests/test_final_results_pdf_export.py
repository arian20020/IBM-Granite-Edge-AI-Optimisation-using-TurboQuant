import os
import subprocess
import sys
import time
from datetime import date
from pathlib import Path

import pytest
from pypdf import PdfReader


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.report_model import Report, ReportParagraph, ReportSection


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
EXPORT_SCRIPT = REPOSITORY_ROOT / "scripts" / "testing" / "Export-Final-Results-Pdf.ps1"
WORD_EXE = Path(r"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE")


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


def _invoke_export(docx: Path, pdf: Path, timeout_seconds: int) -> subprocess.CompletedProcess[str]:
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
        ],
        capture_output=True,
        text=True,
        timeout=timeout_seconds + 20,
        check=False,
    )


def test_pdf_export_rejects_a_missing_source_without_starting_word(tmp_path):
    result = _invoke_export(tmp_path / "missing.docx", tmp_path / "report.pdf", 5)

    assert result.returncode != 0
    assert "DOCX input does not exist" in (result.stdout + result.stderr)


@pytest.mark.skipif(sys.platform != "win32" or not WORD_EXE.is_file(), reason="Microsoft Word is required")
def test_pdf_export_is_hidden_read_only_bounded_and_searchable(tmp_path):
    docx = tmp_path / "report.docx"
    pdf = tmp_path / "report.pdf"
    _renderer()(
        Report(
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
        ),
        docx,
    )
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
