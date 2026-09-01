import sys
from datetime import date
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.reporting.markdown_renderer import render_markdown
from scripts.testing.reporting.report_model import (
    Report,
    ReportNote,
    ReportParagraph,
    ReportSection,
    ReportTable,
)


def test_render_markdown_writes_canonical_two_section_report(tmp_path):
    report = Report(
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
                    ReportNote("Status labels are explicit; empty cells are not measurements."),
                ),
            ),
            ReportSection(
                title="Status results",
                blocks=(
                    ReportTable(
                        table_id="T-01",
                        title="Execution status",
                        subtitle="Completed and unavailable configurations.",
                        columns=("Case", "Status", "Evidence"),
                        rows=(
                            ("granite|3b", "Passed", "EV-001"),
                            ("granite-8b", "Not collected", "EV-002<br>literal\nsee limitation"),
                        ),
                        footnotes=("`Not collected` means the historical campaign did not record the metric.",),
                    ),
                ),
            ),
        ),
    )
    output = tmp_path / "report.md"

    render_markdown(report, output)

    assert output.read_text(encoding="utf-8") == (
        "# OpenVINO final results\n"
        "\n"
        "| Document control | Value |\n"
        "| --- | --- |\n"
        "| Route ID | openvino-official-upstream |\n"
        "| Revision | R2 |\n"
        "| Generated date | 2026-08-30 |\n"
        "| Evidence IDs | EV-001, EV-002 |\n"
        "\n"
        "## Executive summary\n"
        "\n"
        "Measurements are reported from normalized rows.\n"
        "\n"
        "> Note: Status labels are explicit; empty cells are not measurements.\n"
        "\n"
        "## Status results\n"
        "\n"
        "### T-01 — Execution status\n"
        "\n"
        "Completed and unavailable configurations.\n"
        "\n"
        "| Case | Status | Evidence |\n"
        "| --- | --- | --- |\n"
        "| granite\\|3b | Passed | EV-001 |\n"
        "| granite-8b | Not collected | EV-002\\<br>literal<br>see limitation |\n"
        "\n"
        "*`Not collected` means the historical campaign did not record the metric.*\n"
    )


def test_render_markdown_rejects_table_rows_with_unequal_widths(tmp_path):
    report = Report(
        title="Invalid report",
        route_id="route-1",
        revision="R1",
        generated_date=date(2026, 8, 30),
        sections=(
            ReportSection(
                title="Results",
                blocks=(
                    ReportTable(
                        table_id="T-01",
                        title="Bad table",
                        columns=("One", "Two"),
                        rows=(("only one",),),
                    ),
                ),
            ),
        ),
    )

    with pytest.raises(ValueError, match="T-01.*2.*1"):
        render_markdown(report, tmp_path / "report.md")
