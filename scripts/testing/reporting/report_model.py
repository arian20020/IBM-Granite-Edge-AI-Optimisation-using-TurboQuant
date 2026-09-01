"""Rendering-neutral blocks for canonical final-results reports.

Report builders turn normalized rows into these immutable presentation blocks.
Renderers intentionally receive no raw-evidence inputs.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date
from typing import TypeAlias


@dataclass(frozen=True, slots=True)
class ReportParagraph:
    """A plain narrative paragraph."""

    text: str


@dataclass(frozen=True, slots=True)
class ReportNote:
    """A visible report note, distinct from table footnotes."""

    text: str


@dataclass(frozen=True, slots=True)
class ReportTable:
    """A table with stable identity and presentation metadata."""

    table_id: str
    title: str
    columns: tuple[str, ...]
    rows: tuple[tuple[str, ...], ...]
    subtitle: str = ""
    footnotes: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        object.__setattr__(self, "columns", tuple(self.columns))
        object.__setattr__(self, "rows", tuple(tuple(row) for row in self.rows))
        object.__setattr__(self, "footnotes", tuple(self.footnotes))


ReportBlock: TypeAlias = ReportParagraph | ReportTable | ReportNote


@dataclass(frozen=True, slots=True)
class ReportSection:
    """A top-level report section containing ordered presentation blocks."""

    title: str
    blocks: tuple[ReportBlock, ...] = ()

    def __post_init__(self) -> None:
        object.__setattr__(self, "blocks", tuple(self.blocks))


@dataclass(frozen=True, slots=True)
class Report:
    """A complete route report assembled exclusively from normalized rows."""

    title: str
    route_id: str
    revision: str
    generated_date: date
    sections: tuple[ReportSection, ...] = ()
    evidence_ids: tuple[str, ...] = field(default_factory=tuple)

    def __post_init__(self) -> None:
        object.__setattr__(self, "sections", tuple(self.sections))
        object.__setattr__(self, "evidence_ids", tuple(self.evidence_ids))
