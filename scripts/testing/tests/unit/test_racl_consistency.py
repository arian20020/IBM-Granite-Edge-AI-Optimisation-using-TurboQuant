import re
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8")


def table_rows(markdown: str, prefix: str) -> list[list[str]]:
    rows = []
    for line in markdown.splitlines():
        if line.startswith(f"| {prefix}-"):
            rows.append([cell.strip() for cell in line.strip("|").split("|")])
    return rows


def test_assumption_ids_and_outcomes_match_the_summary():
    register = read("docs/risks/Assumption-Register.md")
    rows = table_rows(register, "A")

    assert [row[0] for row in rows] == [f"A-{number:03d}" for number in range(1, 18)]
    assert Counter(row[7] for row in rows) == Counter(
        {"Confirmed": 9, "Rejected": 1, "Pending": 7}
    )
    assert "Nine assumptions are `Confirmed`" in register
    assert "one is `Rejected`" in register
    assert "seven remain `Pending`" in register


def test_licence_ids_and_known_runtime_identity_are_complete():
    register = read("docs/risks/Licence-Register.md")
    rows = table_rows(register, "L")

    assert [row[0] for row in rows] == [f"L-{number:03d}" for number in range(1, 19)]
    assert "3f7c29d318e317b63f54c558bc69803963d7d88c" in register
    assert "final evidence manifest" in register


def test_report_cut_off_review_is_linked_from_current_records():
    paths = (
        "docs/risks/README.md",
        "docs/risks/Assumption-Register.md",
        "docs/risks/Licence-Register.md",
        "docs/risks/Licence-Review-Notes.md",
        "docs/risks/Control-and-Validation-Plan.md",
        "docs/risks/Cross-Register-Validation-Audit.md",
        "docs/risks/Review-Log.md",
        "docs/evidence/engineering-practices/EP-007/README.md",
        "docs/evidence/requirements/G-M05/README.md",
        "docs/evidence/work-packages/PD-05/README.md",
    )

    for path in paths:
        record = read(path)
        assert "RV-010" in record or "`RV-004`–`RV-011`" in record or "`RV-008`–`RV-011`" in record, (
            f"{path} does not include the RV-010 review"
        )


def test_evidence_records_show_the_current_review():
    paths = (
        "docs/evidence/engineering-practices/EP-007/README.md",
        "docs/evidence/requirements/G-M05/README.md",
        "docs/evidence/work-packages/PD-05/README.md",
    )

    for path in paths:
        record = read(path)
        assert "| Evidence record version | `1.3` |" in record
        assert "| Last reviewed | 2026-09-14 |" in record


def test_independent_recovery_review_is_recorded():
    paths = (
        "docs/risks/README.md",
        "docs/risks/Assumption-Register.md",
        "docs/risks/Control-and-Validation-Plan.md",
        "docs/risks/Cross-Register-Validation-Audit.md",
        "docs/risks/Review-Log.md",
        "docs/evidence/engineering-practices/EP-007/README.md",
        "docs/evidence/requirements/G-M05/README.md",
        "docs/evidence/work-packages/PD-05/README.md",
    )

    for path in paths:
        assert "RV-011" in read(path), f"{path} does not link to RV-011"

    evidence = read(
        "docs/evidence/governance/Independent-Evidence-Recovery-Test-20260914.md"
    )
    assert "| Final evidence | 226 | 226 | 14,040,901 | 14,040,901 | 0 |" in evidence
    assert "| Raw evidence | 2,482 | 2,482 | 81,978,567 | 81,978,567 | 0 |" in evidence
    assert "a1b21c40eee679afeb10d82ea0adf2cde0b029ab59438989f8415a3a520d2c05" in evidence
    assert "39ba4aa6195864ae4ddf1bc93de57c9b5a8de9e748685ddf8ce8c8d3f7337050" in evidence


def test_turbovec_processed_summary_has_one_current_decision():
    summary = read("experiments/processed-results/EXP-TV-COMP-001/README.md")
    raw_summary = read("experiments/raw-results/turbovec/README.md")

    assert len(re.findall(r"^Latest decision:", summary, flags=re.MULTILINE)) == 1
    assert "Latest decision: **DEMONSTRATOR_ONLY**" in summary
    assert "Earlier decision: **BLOCKED**" in summary
    assert len(re.findall(r"^Latest validated run:", raw_summary, flags=re.MULTILINE)) == 1
    assert "Earlier run `EXP-TV-COMP-001-20260902T225731Z-001`" in raw_summary


def test_report_appendix_keeps_the_selected_controlled_outcomes():
    appendix = read("docs/report/appendices/Risk-Assumption-Licence-Registers.tex")

    expected_outcomes = {
        "A-005": "Confirmed",
        "A-006": "Confirmed",
        "A-007": "Pending",
        "A-008": "Pending",
        "A-009": "Confirmed",
        "A-010": "Confirmed",
        "A-012": "Rejected",
        "A-016": "Pending",
        "A-017": "Pending",
    }

    for assumption_id, outcome in expected_outcomes.items():
        rows = [
            row for row in re.split(r"\\\\", appendix)
            if re.search(rf"(?m)^\s*{assumption_id}\s*&", row)
        ]
        assert len(rows) == 1, f"Expected one appendix row for {assumption_id}"
        assert rf"\textbf{{{outcome}}}" in rows[0], (
            f"The appendix does not show {assumption_id} as {outcome}"
        )

    assert "\\texttt{llama.cpp} commit \\texttt{3f7c29d3}" in appendix
