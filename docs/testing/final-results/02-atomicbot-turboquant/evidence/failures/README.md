# Failures and deviations

Runtime accounting is 19 Passed. Setup, memory-gate, timeout, and quality safety events are nonterminal scoped deviations in data/deviations.csv; they are not silently removed or converted into runtime failures.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the AtomicBot TurboQuant route.

### Start here

Continue with [`curated-logs/`](curated-logs/README.md).

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Folders

| Folder | What it contains |
| --- | --- |
| [`curated-logs/`](curated-logs/README.md) | Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the AtomicBot TurboQuant route. |

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)
- [curated-logs guide](curated-logs/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
