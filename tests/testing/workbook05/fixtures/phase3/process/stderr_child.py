"""Exit through a deterministic non-zero stderr path."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    parser.parse_args()
    sys.stderr.write("deterministic-stderr\n")
    return 7


if __name__ == "__main__":
    raise SystemExit(main())
