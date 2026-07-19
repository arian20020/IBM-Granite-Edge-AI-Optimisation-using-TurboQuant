"""Update WB-03 controlled-manifest hashes after deterministic generation."""

from __future__ import annotations

import argparse
import csv
import hashlib
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--template", type=Path, required=True)
    parser.add_argument("--docx", type=Path, required=True)
    args = parser.parse_args()
    with args.manifest.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream); fields = reader.fieldnames; rows = list(reader)
    if not fields:
        raise ValueError("manifest header missing")
    found = False
    for row in rows:
        if row["Workbook_ID"] == "WB-03":
            row["Canonical_Template_SHA256"] = sha256(args.template)
            row["Last_Validated_DOCX_SHA256"] = sha256(args.docx)
            row["Status"] = ("Formal v1.5 animehacker TQ3_0 evidence alignment: recovered AH-09/AH-10 "
                            "use Level Zero level_zero:0; AH-10 final pilot records 70.0 MiB KV and "
                            "8969.625 MiB peak WS; no full-GPU TQ3 cache claim")
            row["Revision"] = "1.5"
            found = True
    if not found:
        raise ValueError("WB-03 manifest row missing")
    with args.manifest.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
        writer.writeheader(); writer.writerows(rows)
    print("WB-03 manifest updated to v1.5")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
