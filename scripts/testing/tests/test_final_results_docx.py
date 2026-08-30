import sys
import zipfile
from datetime import date
from pathlib import Path

from docx import Document
from docx.enum.section import WD_ORIENT
from lxml import etree


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.markdown_renderer import render_markdown
from scripts.testing.final_results.report_model import (
    Report,
    ReportNote,
    ReportParagraph,
    ReportSection,
    ReportTable,
)


WORD_NAMESPACE = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}


def _renderer():
    from scripts.testing.final_results.docx_renderer import render_docx

    return render_docx


def _parity_comparator():
    from scripts.testing.final_results.parity import compare_markdown_docx

    return compare_markdown_docx


def _report() -> Report:
    return Report(
        title="OpenVINO final results",
        route_id="openvino-official-upstream",
        revision="R2",
        generated_date=date(2026, 8, 30),
        evidence_ids=("EV-001", "EV-002"),
        sections=(
            ReportSection(
                title="Executive summary",
                blocks=(
                    ReportParagraph("Measurements are reported from normalized rows."),
                    ReportNote("Status colour is supplementary to explicit text."),
                    ReportTable(
                        table_id="T-01",
                        title="Execution status",
                        subtitle="Completed and unavailable configurations.",
                        columns=("Case", "Status", "Evidence"),
                        rows=(
                            ("granite|3b", "Passed", "EV-001"),
                            ("granite-8b", "Not collected", "EV-002\nsee limitation"),
                        ),
                        footnotes=("Not collected means the campaign did not record the metric.",),
                    ),
                    ReportTable(
                        table_id="T-02",
                        title="Wide audit detail",
                        columns=("A", "B", "C", "D", "E", "F", "G"),
                        rows=(("1", "2", "3", "4", "5", "6", "Blocked"),),
                    ),
                    ReportParagraph("Narrative resumes in portrait orientation."),
                ),
            ),
        ),
    )


def _word_xml(docx_path: Path, member: str) -> etree._Element:
    with zipfile.ZipFile(docx_path) as package:
        return etree.fromstring(package.read(member))


def test_render_docx_applies_professional_accessible_document_structure(tmp_path):
    output = tmp_path / "report.docx"

    _renderer()(_report(), output)

    document_xml = _word_xml(output, "word/document.xml")
    fills = document_xml.xpath("//w:shd/@w:fill", namespaces=WORD_NAMESPACE)
    assert "0F2747" in fills
    assert "0F766E" in fills
    assert "0B63CE" in fills

    document = Document(output)
    table_text = [
        [[cell.text for cell in row.cells] for row in table.rows]
        for table in document.tables
    ]
    assert ["granite|3b", "Passed", "EV-001"] in table_text[1]
    assert ["granite-8b", "Not collected", "EV-002\nsee limitation"] in table_text[1]
    assert ["1", "2", "3", "4", "5", "6", "Blocked"] in table_text[2]

    rows = document_xml.xpath("//w:tbl/w:tr", namespaces=WORD_NAMESPACE)
    assert rows
    assert all(row.xpath("./w:trPr/w:cantSplit", namespaces=WORD_NAMESPACE) for row in rows)
    table_headers = document_xml.xpath("//w:tbl/w:tr[1]/w:trPr/w:tblHeader", namespaces=WORD_NAMESPACE)
    assert len(table_headers) == len(document.tables)

    footer_parts = []
    with zipfile.ZipFile(output) as package:
        for name in package.namelist():
            if name.startswith("word/footer") and name.endswith(".xml"):
                footer_parts.append(package.read(name).decode("utf-8"))
    combined_footer = "".join(footer_parts)
    assert "openvino-official-upstream" in combined_footer
    assert "R2" in combined_footer
    assert "PAGE" in combined_footer


def test_render_docx_uses_landscape_only_beyond_the_wide_table_threshold(tmp_path):
    output = tmp_path / "report.docx"

    _renderer()(_report(), output)

    document = Document(output)
    orientations = [section.orientation for section in document.sections]
    assert orientations == [WD_ORIENT.PORTRAIT, WD_ORIENT.LANDSCAPE, WD_ORIENT.PORTRAIT]

    def report_with_estimated_width(widths: tuple[int, ...]) -> Report:
        return Report(
            title="Width boundary",
            route_id="width-test",
            revision="R1",
            generated_date=date(2026, 8, 30),
            sections=(
                ReportSection(
                    title="Results",
                    blocks=(
                        ReportTable(
                            table_id="T-01",
                            title="Boundary table",
                            columns=tuple("X" * width for width in widths),
                            rows=(tuple("1" for _ in widths),),
                        ),
                        ReportParagraph("Orientation reset marker."),
                    ),
                ),
            ),
        )

    at_boundary = tmp_path / "at-boundary.docx"
    beyond_boundary = tmp_path / "beyond-boundary.docx"
    _renderer()(report_with_estimated_width((15, 16, 16, 16, 16, 16)), at_boundary)
    _renderer()(report_with_estimated_width((16, 16, 16, 16, 16, 16)), beyond_boundary)

    assert [section.orientation for section in Document(at_boundary).sections] == [
        WD_ORIENT.PORTRAIT
    ]
    assert [section.orientation for section in Document(beyond_boundary).sections] == [
        WD_ORIENT.PORTRAIT,
        WD_ORIENT.LANDSCAPE,
        WD_ORIENT.PORTRAIT,
    ]


def test_render_docx_normalizes_package_timestamps_and_bytes(tmp_path):
    first = tmp_path / "first.docx"
    second = tmp_path / "second.docx"

    _renderer()(_report(), first)
    _renderer()(_report(), second)

    with zipfile.ZipFile(first) as package:
        assert package.infolist()
        assert {info.date_time for info in package.infolist()} == {(1980, 1, 1, 0, 0, 0)}
    assert first.read_bytes() == second.read_bytes()


def test_compare_markdown_docx_reports_exact_heading_and_table_cell_parity(tmp_path):
    markdown = tmp_path / "report.md"
    docx = tmp_path / "report.docx"
    render_markdown(_report(), markdown)
    _renderer()(_report(), docx)

    result = _parity_comparator()(markdown, docx)

    assert result["matches"] is True
    assert result["headings_match"] is True
    assert result["tables_match"] is True
    assert result["headings"]["markdown"] == [
        "OpenVINO final results",
        "Executive summary",
        "T-01 — Execution status",
        "T-02 — Wide audit detail",
    ]
    assert result["headings"]["markdown"] == result["headings"]["docx"]
    assert result["tables"]["markdown"] == result["tables"]["docx"]
    assert result["differences"] == []


def test_compare_markdown_docx_identifies_a_changed_cell(tmp_path):
    markdown = tmp_path / "report.md"
    docx = tmp_path / "report.docx"
    render_markdown(_report(), markdown)
    _renderer()(_report(), docx)
    changed = Document(docx)
    changed.tables[1].cell(1, 1).text = "Failed"
    changed.save(docx)

    result = _parity_comparator()(markdown, docx)

    assert result["matches"] is False
    assert result["headings_match"] is True
    assert result["tables_match"] is False
    assert result["differences"] == [
        {
            "kind": "tables",
            "markdown": result["tables"]["markdown"],
            "docx": result["tables"]["docx"],
        }
    ]


def test_compare_markdown_docx_preserves_literal_backslashes_in_cells(tmp_path):
    report = Report(
        title="Path report",
        route_id="path-test",
        revision="R1",
        generated_date=date(2026, 8, 30),
        sections=(
            ReportSection(
                title="Evidence",
                blocks=(
                    ReportTable(
                        table_id="T-01",
                        title="Paths",
                        columns=("Evidence path",),
                        rows=((r"evidence\run-001\result.json",),),
                    ),
                ),
            ),
        ),
    )
    markdown = tmp_path / "report.md"
    docx = tmp_path / "report.docx"
    render_markdown(report, markdown)
    _renderer()(report, docx)

    result = _parity_comparator()(markdown, docx)

    assert result["matches"] is True
