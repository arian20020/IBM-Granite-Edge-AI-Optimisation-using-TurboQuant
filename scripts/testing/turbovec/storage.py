"""Equivalent storage accounting for Exact and compressed indexes."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class StorageComponents:
    vector_data_bytes: int
    index_metadata_bytes: int
    document_chunk_metadata_bytes: int
    serialization_overhead_bytes: int
    auxiliary_lookup_bytes: int

    def __post_init__(self) -> None:
        if any(value < 0 for value in self.__dict__.values()):
            raise ValueError("storage component bytes must be non-negative")

    @property
    def equivalent_total_bytes(self) -> int:
        return sum(self.__dict__.values())

    def to_dict(self) -> dict[str, int]:
        return {**self.__dict__, "equivalent_total_bytes": self.equivalent_total_bytes}


def compare_storage(exact: StorageComponents, candidate: StorageComponents, *, vectors: int) -> dict[str, object]:
    if vectors <= 0:
        raise ValueError("vector count must be positive")
    if candidate.equivalent_total_bytes <= 0:
        raise ValueError("candidate equivalent storage must be positive")
    return {
        "exact": exact.to_dict(),
        "candidate": candidate.to_dict(),
        "exact_equivalent_bytes": exact.equivalent_total_bytes,
        "candidate_equivalent_bytes": candidate.equivalent_total_bytes,
        "exact_bytes_per_vector": exact.equivalent_total_bytes / vectors,
        "candidate_bytes_per_vector": candidate.equivalent_total_bytes / vectors,
        "storage_ratio": exact.equivalent_total_bytes / candidate.equivalent_total_bytes,
    }
