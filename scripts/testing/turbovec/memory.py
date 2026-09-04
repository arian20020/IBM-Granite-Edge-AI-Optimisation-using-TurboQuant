"""Unambiguous process and system memory accounting contracts."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class MemorySample:
    total_physical_ram_bytes: int
    available_system_ram_bytes: int
    committed_memory_bytes: int
    pagefile_used_bytes: int
    process_working_set_bytes: int
    shared_gpu_memory_bytes: int

    def __post_init__(self) -> None:
        if any(value < 0 for value in self.__dict__.values()):
            raise ValueError("memory values must be non-negative")


def memory_accounting(before: MemorySample, after: MemorySample, *, peak_process_working_set_bytes: int) -> dict[str, int]:
    if peak_process_working_set_bytes < max(before.process_working_set_bytes, after.process_working_set_bytes):
        raise ValueError("peak process working set cannot be below an observed working set")
    return {
        "total_physical_ram_bytes": before.total_physical_ram_bytes,
        "baseline_process_working_set_bytes": before.process_working_set_bytes,
        "final_process_working_set_bytes": after.process_working_set_bytes,
        "peak_process_working_set_bytes": peak_process_working_set_bytes,
        "incremental_process_working_set_bytes": after.process_working_set_bytes - before.process_working_set_bytes,
        "available_system_ram_before_bytes": before.available_system_ram_bytes,
        "available_system_ram_after_bytes": after.available_system_ram_bytes,
        "available_system_ram_delta_bytes": after.available_system_ram_bytes - before.available_system_ram_bytes,
        "committed_memory_before_bytes": before.committed_memory_bytes,
        "committed_memory_after_bytes": after.committed_memory_bytes,
        "committed_memory_delta_bytes": after.committed_memory_bytes - before.committed_memory_bytes,
        "pagefile_used_before_bytes": before.pagefile_used_bytes,
        "pagefile_used_after_bytes": after.pagefile_used_bytes,
        "shared_gpu_memory_before_bytes": before.shared_gpu_memory_bytes,
        "shared_gpu_memory_after_bytes": after.shared_gpu_memory_bytes,
        "shared_gpu_memory_delta_bytes": after.shared_gpu_memory_bytes - before.shared_gpu_memory_bytes,
    }
