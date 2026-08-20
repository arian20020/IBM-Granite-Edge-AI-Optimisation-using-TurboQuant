"""Stable, dependency-free contracts shared by the research CLI modules."""

from dataclasses import dataclass


@dataclass(frozen=True)
class Document:
    relative_path: str
    text: str
    sha256: str = ""


@dataclass(frozen=True)
class Chunk:
    chunk_id: int
    relative_path: str
    start: int
    end: int
    text: str


class ResearchError(RuntimeError):
    """Expected research-pipeline failure with a fixed, privacy-safe code."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(code)
