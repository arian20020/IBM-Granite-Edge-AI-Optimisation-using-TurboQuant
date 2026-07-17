"""Export the normalized task catalogue from the controlled RTM workbook."""

from __future__ import annotations

import argparse
from datetime import date
from pathlib import Path

from traceability_common import build_catalogue, write_json


def parse_arguments() -> argparse.Namespace:
    """Read and validate command-line arguments."""

    parser = argparse.ArgumentParser(
        description=(
            "Read the IXN RTM .xlsx workbook and create the normalized JSON "
            "catalogue used by the repository traceability documents."
        )
    )
    parser.add_argument("--workbook", required=True, type=Path, help="Path to the RTM .xlsx file.")
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=Path.cwd(),
        help="Repository root. Defaults to the current directory.",
    )
    parser.add_argument(
        "--generated-on",
        type=date.fromisoformat,
        default=date.today(),
        help="Snapshot date in YYYY-MM-DD format. Defaults to today.",
    )
    return parser.parse_args()


def main() -> int:
    """Create the task catalogue and source metadata files."""

    args = parse_arguments()
    workbook = args.workbook.resolve()
    repository_root = args.repository_root.resolve()

    if not workbook.is_file():
        raise FileNotFoundError(f"RTM workbook not found: {workbook}")

    catalogue = build_catalogue(workbook, args.generated_on)
    data_directory = repository_root / "docs" / "traceability" / "data"

    write_json(data_directory / "task-catalogue.json", catalogue)
    write_json(data_directory / "source-metadata.json", catalogue["metadata"])

    counts = catalogue["metadata"]["counts"]
    print("Traceability catalogue exported successfully.")
    print(f"Active tasks: {counts['active_tasks']}")
    print(f"All requirements: {counts['requirements_all_lifecycles']}")
    print(f"Work packages: {counts['work_packages']}")
    print(f"Engineering practices: {counts['engineering_practices']}")
    print(f"Output: {data_directory / 'task-catalogue.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
