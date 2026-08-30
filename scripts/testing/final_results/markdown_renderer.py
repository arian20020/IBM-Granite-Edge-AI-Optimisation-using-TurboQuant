"""Deterministic canonical Markdown rendering for final-results reports."""

from __future__ import annotations

from pathlib import Path

from .report_model import Report, ReportNote, ReportParagraph, ReportTable


def _escape_markdown_cell(value: str) -> str:
    """Escape the table delimiters Markdown cannot represent verbatim."""
    return (
        value.replace("|", "\\|")
        .replace("\r\n", "\n")
        .replace("\r", "\n")
        .replace("\n", "<br>")
    )


def _table_lines(table: ReportTable) -> list[str]:
    width = len(table.columns)
    for row in table.rows:
        if len(row) != width:
            raise ValueError(
                f"table {table.table_id!r} has {width} columns but a row has {len(row)} cells"
            )

    lines = [
        f"### {table.table_id} — {table.title}",
    ]
    if table.subtitle:
        lines.extend(("", table.subtitle))
    lines.extend(
        (
            "",
            "| " + " | ".join(_escape_markdown_cell(column) for column in table.columns) + " |",
            "| " + " | ".join("---" for _ in table.columns) + " |",
        )
    )
    lines.extend(
        "| " + " | ".join(_escape_markdown_cell(cell) for cell in row) + " |"
        for row in table.rows
    )
    for footnote in table.footnotes:
        lines.extend(("", f"*{footnote}*"))
    return lines


def _render_lines(report: Report) -> list[str]:
    lines = [
        f"# {report.title}",
        "",
        "| Document control | Value |",
        "| --- | --- |",
        f"| Route ID | {_escape_markdown_cell(report.route_id)} |",
        f"| Revision | {_escape_markdown_cell(report.revision)} |",
        f"| Generated date | {report.generated_date.isoformat()} |",
        f"| Evidence IDs | {', '.join(_escape_markdown_cell(identifier) for identifier in report.evidence_ids)} |",
    ]
    for section in report.sections:
        lines.extend(("", f"## {section.title}"))
        for block in section.blocks:
            lines.append("")
            if isinstance(block, ReportParagraph):
                lines.append(block.text)
            elif isinstance(block, ReportNote):
                lines.append(f"> Note: {block.text}")
            elif isinstance(block, ReportTable):
                lines.extend(_table_lines(block))
            else:  # pragma: no cover - defensive guard for malformed callers.
                raise TypeError(f"unsupported report block: {type(block)!r}")
    return lines


def render_markdown(report: Report, output: Path) -> None:
    """Render ``report`` as canonical UTF-8 Markdown with LF line endings."""
    content = "\n".join(_render_lines(report)) + "\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(content, encoding="utf-8", newline="\n")
