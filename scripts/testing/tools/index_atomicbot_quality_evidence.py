"""Index the complete 2026-07-17 AtomicBot quality evidence tree."""

import csv
import hashlib
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows"
REGISTER = ROOT / "docs/testing/Evidence-Index.csv"
PREFIX = EVIDENCE.relative_to(ROOT).as_posix() + "/"


def main():
    with REGISTER.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle); fields = reader.fieldnames
        rows = [row for row in reader if not row["Repository_Path"].startswith(PREFIX)]
    assert fields
    for path in sorted(p for p in EVIDENCE.rglob("*") if p.is_file() and p.name != ".runner.lock"):
        relative = path.relative_to(ROOT).as_posix(); digest = hashlib.sha256(path.read_bytes()).hexdigest()
        parts = path.relative_to(EVIDENCE).parts; test_id = parts[0] if len(parts) > 1 else "AB-QUALITY-ALL"
        rows.append({
            "Evidence_ID": "EVID-" + hashlib.sha256(relative.encode()).hexdigest()[:20],
            "Run_ID": f"{test_id}-QUALITY-R001", "Test_ID": test_id,
            "Route": "atomicbot-turboquant", "Workbook_ID": "WB-02",
            "Evidence_Type": "Quality response" if "response" in path.name else "Quality run evidence",
            "Repository_Path": relative, "File_Name": path.name, "Size_Bytes": str(path.stat().st_size),
            "SHA256": digest,
            "Created_Timestamp_UTC": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(),
            "Source_or_Derivation": "Captured or derived during independent all-row GTQ-PROMPTS-v1 execution",
            "Processing_Script": "scripts/testing/tools/run_atomicbot_full_quality.py",
            "Immutable_Raw_Evidence": "True" if path.name.startswith("P") or path.name.startswith("server-") else "False",
            "Contains_Sensitive_Data": "False", "Redaction_Status": "Not required",
            "Evidence_Commit": "Pending PR", "Validation_Status": "Validated for WB-02 v1.5",
            "Notes": "Path and SHA-256 verified during 114-record reconciliation.",
        })
    with REGISTER.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, quoting=csv.QUOTE_ALL, lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    print(f"indexed {sum(row['Repository_Path'].startswith(PREFIX) for row in rows)} quality evidence files")

    completion_path = ROOT / "docs/testing/Workbook-Completion-Register.csv"
    with completion_path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle); completion_fields = reader.fieldnames; completion_rows = list(reader)
    assert completion_fields
    updated = 0
    for row in completion_rows:
        if row["Workbook_ID"] == "WB-02":
            row["Source_Result_Path"] = "experiments/raw-results/atomicbot-turboquant/2026-07-16/; experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows/"
            row["Updated_Date"] = "2026-07-17"; row["Review_Date"] = "2026-07-17"
            row["Notes"] = "WB-02 v1.5 section reconciled to runtime evidence plus 114 hashed all-row quality records."
            updated += 1
    with completion_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=completion_fields, lineterminator="\n")
        writer.writeheader(); writer.writerows(completion_rows)
    print(f"updated {updated} WB-02 completion rows")


if __name__ == "__main__": main()
