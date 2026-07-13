"""Generate the deterministic P5 long-context retrieval fixture.

The generated text is intentionally repetitive but includes numbered paragraphs so
accidental truncation or reordering is visible. The unique answer marker appears only
in the final evidence paragraph.
"""

# Import the standard-library command-line and path helpers.
from __future__ import annotations

import argparse
from pathlib import Path


# Define stable content so every route receives the same context.
PARAGRAPH = (
    "Evidence paragraph {number:04d}: The controlled Granite-TurboQuant campaign "
    "records exact models, commits, commands, devices, memory, speed, quality, failures, "
    "and evidence hashes. This paragraph is filler and does not contain the answer marker."
)


# Generate the fixture at a predictable repository-relative location.
def main() -> int:
    """Write the repeated context and final marker paragraph."""

    # Parse an optional output path and paragraph count for controlled context scaling.
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("experiments/granite_turboquant_intel/prompts/fixtures/P5-long-context-v1.txt"),
    )
    parser.add_argument("--paragraphs", type=int, default=180)
    arguments = parser.parse_args()

    # Reject invalid counts instead of silently producing an unusable fixture.
    if arguments.paragraphs < 20:
        raise ValueError("At least 20 filler paragraphs are required.")

    # Create the destination and write deterministic UTF-8 content.
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    lines = [PARAGRAPH.format(number=index) for index in range(1, arguments.paragraphs + 1)]
    lines.append(
        "Final evidence paragraph: the unique controlled retrieval marker is IXN-TQ-7319."
    )
    arguments.output.write_text("\n\n".join(lines) + "\n", encoding="utf-8")
    print(f"Generated {arguments.output} with {arguments.paragraphs} filler paragraphs.")
    return 0


# Run the entry point only when invoked as a script.
if __name__ == "__main__":
    raise SystemExit(main())
