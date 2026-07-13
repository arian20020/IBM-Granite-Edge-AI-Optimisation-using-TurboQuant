"""Generate the six controlled DOCX testing workbooks from source-controlled Markdown.

The Markdown files are the canonical, reviewable workbook templates in Git. The DOCX
files are generated reporting artefacts that can be opened and completed in Microsoft Word.
"""

# Import the standard-library modules used for command-line parsing and file handling.
from __future__ import annotations

import argparse
import re
import sys
import zipfile
from datetime import datetime
from pathlib import Path
from typing import Iterable, List, Sequence

# Import python-docx types used to create and format Word documents.
from docx import Document
from docx.enum.section import WD_ORIENT
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


# Define the six canonical Markdown workbook template names.
WORKBOOK_TEMPLATES: Sequence[str] = (
    "01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md",
    "02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md",
    "03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md",
    "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md",
    "05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md",
    "06_Cross_Route_Controlled_Comparison_Workbook_v1.md",
)


# Convert a Markdown table cell back into readable text.
def clean_cell(value: str) -> str:
    """Remove Markdown escaping used inside table cells."""

    # Restore literal vertical bars and line breaks from the canonical Markdown form.
    return value.strip().replace("\\|", "|").replace("<br>", "\n")


# Decide whether a Markdown line is the separator row of a table.
def is_separator_row(cells: Sequence[str]) -> bool:
    """Return True when every table cell contains only Markdown separator syntax."""

    # Accept common forms such as --- and :---: while rejecting normal data rows.
    return bool(cells) and all(re.fullmatch(r":?-{3,}:?", cell.strip()) for cell in cells)


# Parse one Markdown table beginning at the supplied line index.
def parse_table(lines: Sequence[str], start_index: int) -> tuple[List[List[str]], int]:
    """Read consecutive Markdown table rows and return rows plus the next line index."""

    # Collect all consecutive lines that begin and end with a table delimiter.
    raw_rows: List[List[str]] = []
    index = start_index
    while index < len(lines):
        line = lines[index].strip()
        if not (line.startswith("|") and line.endswith("|")):
            break
        # Split the row while discarding the empty values outside the edge pipes.
        raw_rows.append([clean_cell(cell) for cell in line[1:-1].split("|")])
        index += 1

    # Remove the Markdown separator row directly after the header when present.
    if len(raw_rows) >= 2 and is_separator_row(raw_rows[1]):
        raw_rows.pop(1)

    # Normalize row widths so python-docx can create a rectangular table safely.
    width = max((len(row) for row in raw_rows), default=0)
    normalized = [row + [""] * (width - len(row)) for row in raw_rows]
    return normalized, index


# Set a cell's background colour using WordprocessingML.
def set_cell_fill(cell, fill_hex: str) -> None:
    """Apply a solid hexadecimal fill colour to a Word table cell."""

    # Find or create the cell shading element and assign the requested fill colour.
    properties = cell._tc.get_or_add_tcPr()
    shading = properties.find(qn("w:shd"))
    if shading is None:
        shading = OxmlElement("w:shd")
        properties.append(shading)
    shading.set(qn("w:fill"), fill_hex)


# Mark a table row as a repeating header row for multi-page tables.
def mark_repeat_header(row) -> None:
    """Tell Word to repeat the supplied row at the top of later pages."""

    # Add the WordprocessingML table-header flag to the row properties.
    row_properties = row._tr.get_or_add_trPr()
    table_header = OxmlElement("w:tblHeader")
    table_header.set(qn("w:val"), "true")
    row_properties.append(table_header)


# Apply consistent presentation to a generated table.
def style_table(table) -> None:
    """Format the generated table for clear printing and Word editing."""

    # Use a standard grid so every editable field remains visually distinct.
    table.style = "Table Grid"
    table.autofit = True

    # Format the first row as a dark-blue repeating header.
    if table.rows:
        mark_repeat_header(table.rows[0])
        for cell in table.rows[0].cells:
            set_cell_fill(cell, "1F4E78")
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            for paragraph in cell.paragraphs:
                for run in paragraph.runs:
                    run.font.bold = True
                    run.font.color.rgb = RGBColor(255, 255, 255)
                    run.font.size = Pt(7)

    # Format body rows with compact readable text and alternating pale shading.
    for row_index, row in enumerate(table.rows[1:], start=1):
        for cell in row.cells:
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if row_index % 2 == 0:
                set_cell_fill(cell, "EAF2F8")
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_after = Pt(0)
                for run in paragraph.runs:
                    run.font.size = Pt(7)


# Configure the document page, styles, headings and footer.
def configure_document(document: Document, footer_text: str) -> None:
    """Apply the controlled workbook's page and typography settings."""

    # Use landscape A4 with narrow margins so wide evidence tables remain usable.
    section = document.sections[0]
    section.orientation = WD_ORIENT.LANDSCAPE
    section.page_width, section.page_height = section.page_height, section.page_width
    section.top_margin = Inches(0.45)
    section.bottom_margin = Inches(0.45)
    section.left_margin = Inches(0.45)
    section.right_margin = Inches(0.45)

    # Use Aptos as the default document font at a compact but readable size.
    normal = document.styles["Normal"]
    normal.font.name = "Aptos"
    normal.font.size = Pt(9)

    # Style heading levels with the campaign's blue visual hierarchy.
    for style_name, size in (("Title", 19), ("Heading 1", 15), ("Heading 2", 12), ("Heading 3", 10)):
        style = document.styles[style_name]
        style.font.name = "Aptos Display"
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor(31, 78, 120)

    # Add a centered footer so printed pages remain identifiable.
    footer = section.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
    footer_run = footer.add_run(footer_text)
    footer_run.font.name = "Aptos"
    footer_run.font.size = Pt(8)


# Add simple inline Markdown emphasis to a Word paragraph.
def add_inline_markdown(paragraph, text: str) -> None:
    """Convert paired double-asterisk spans into bold Word runs."""

    # Split around paired **bold** spans while preserving all surrounding text.
    parts = re.split(r"(\*\*.+?\*\*)", text)
    for part in parts:
        if not part:
            continue
        if part.startswith("**") and part.endswith("**") and len(part) >= 4:
            run = paragraph.add_run(part[2:-2])
            run.bold = True
        else:
            paragraph.add_run(part)


# Add a normal or list paragraph while preserving the Markdown meaning.
def add_text_paragraph(document: Document, text: str) -> None:
    """Add one non-table Markdown line to the Word document."""

    # Select the correct Word paragraph style and strip only the list marker.
    if text.startswith("- "):
        paragraph = document.add_paragraph(style="List Bullet")
        content = text[2:]
    elif re.match(r"^\d+\.\s+", text):
        paragraph = document.add_paragraph(style="List Number")
        content = re.sub(r"^\d+\.\s+", "", text)
    else:
        paragraph = document.add_paragraph()
        content = text

    # Convert simple inline bold markers instead of exposing raw Markdown in Word.
    add_inline_markdown(paragraph, content)

    # Keep explanatory text compact around the large evidence tables.
    paragraph.paragraph_format.space_after = Pt(3)


# Normalize package metadata so repeated generation produces identical DOCX bytes.
def normalize_docx_package(output_path: Path) -> None:
    """Rewrite the DOCX ZIP with stable member ordering and timestamps."""

    # Read every package member before replacing the archive.
    with zipfile.ZipFile(output_path, "r") as source:
        members = [(info, source.read(info.filename)) for info in source.infolist()]

    # Write a normalized temporary package with a stable ZIP timestamp.
    temporary_path = output_path.with_suffix(output_path.suffix + ".tmp")
    with zipfile.ZipFile(temporary_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as target:
        for original, data in sorted(members, key=lambda item: item[0].filename):
            info = zipfile.ZipInfo(original.filename, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = original.external_attr
            info.internal_attr = original.internal_attr
            info.create_system = original.create_system
            info.flag_bits = original.flag_bits
            target.writestr(info, data)

    # Atomically replace the non-normalized archive.
    temporary_path.replace(output_path)


# Convert one canonical Markdown workbook into a DOCX file.
def convert_markdown_to_docx(markdown_path: Path, output_path: Path) -> None:
    """Create one Word workbook from its source-controlled Markdown template."""

    # Read the exact canonical text using UTF-8.
    lines = markdown_path.read_text(encoding="utf-8").splitlines()

    # Create a new Word document and apply stable metadata and page settings.
    document = Document()
    fixed_timestamp = datetime(2026, 7, 13, 0, 0, 0)
    document.core_properties.author = "Granite-TurboQuant Controlled Retest Campaign"
    document.core_properties.last_modified_by = "Granite-TurboQuant Controlled Retest Campaign"
    document.core_properties.created = fixed_timestamp
    document.core_properties.modified = fixed_timestamp
    document.core_properties.revision = 1
    configure_document(document, "Granite-TurboQuant Controlled Retest Campaign v1 - generated working copy")

    # Walk through the Markdown line by line and convert each block.
    index = 0
    first_heading_used = False
    while index < len(lines):
        stripped = lines[index].strip()

        # Skip blank lines because Word paragraph spacing supplies separation.
        if not stripped:
            index += 1
            continue

        # Convert Markdown headings into Word heading styles.
        heading_match = re.match(r"^(#{1,6})\s+(.*)$", stripped)
        if heading_match:
            level = len(heading_match.group(1))
            heading_text = heading_match.group(2).strip()
            if not first_heading_used:
                paragraph = document.add_paragraph(heading_text, style="Title")
                paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
                first_heading_used = True
            else:
                document.add_heading(heading_text, level=min(level, 3))
            index += 1
            continue

        # Convert a consecutive Markdown table into one editable Word table.
        if stripped.startswith("|") and stripped.endswith("|"):
            rows, next_index = parse_table(lines, index)
            if rows and rows[0]:
                table = document.add_table(rows=len(rows), cols=len(rows[0]))
                for row_index, row_values in enumerate(rows):
                    for column_index, value in enumerate(row_values):
                        table.cell(row_index, column_index).text = value
                style_table(table)
                document.add_paragraph().paragraph_format.space_after = Pt(2)
            index = next_index
            continue

        # Convert all remaining lines into regular or list paragraphs.
        add_text_paragraph(document, stripped)
        index += 1

    # Ensure the destination exists, save the workbook and normalize its package bytes.
    output_path.parent.mkdir(parents=True, exist_ok=True)
    document.save(output_path)
    normalize_docx_package(output_path)


# Resolve the repository root from the script's installed path.
def default_repository_root() -> Path:
    """Return the repository root when the script remains under scripts/testing."""

    # Move from scripts/testing/Generate-Controlled-Workbooks.py to the repository root.
    return Path(__file__).resolve().parents[2]


# Parse command-line options and generate every controlled workbook.
def main(argv: Iterable[str] | None = None) -> int:
    """Generate the workbook package and return a process exit code."""

    # Define simple command-line options for repository and output locations.
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=default_repository_root())
    parser.add_argument("--output-directory", type=Path, default=None)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    # Resolve the source-controlled template and generated-output directories.
    repository_root = arguments.repository_root.resolve()
    template_directory = repository_root / "docs" / "testing" / "workbooks" / "text-templates"
    output_directory = (
        arguments.output_directory.resolve()
        if arguments.output_directory is not None
        else repository_root / "docs" / "testing" / "workbooks" / "generated"
    )

    # Stop with a clear error when the template folder is missing.
    if not template_directory.is_dir():
        print(f"ERROR: Template directory not found: {template_directory}", file=sys.stderr)
        return 2

    # Generate all six files and fail clearly if any required template is absent.
    for template_name in WORKBOOK_TEMPLATES:
        template_path = template_directory / template_name
        if not template_path.is_file():
            print(f"ERROR: Required template not found: {template_path}", file=sys.stderr)
            return 3
        output_path = output_directory / template_path.with_suffix(".docx").name
        convert_markdown_to_docx(template_path, output_path)
        print(f"Generated: {output_path}")

    # Return zero only after every workbook is generated successfully.
    return 0


# Run the command-line entry point and propagate its exit status.
if __name__ == "__main__":
    raise SystemExit(main())
