# Release verification command

Run from the repository root. This validates the settled package through the supported contributor CLI. These checks do not rerun benchmarks or quality scoring, regenerate reports, invoke Word, or rewrite catalogs. They do not modify evidence.

```powershell
python -m scripts.testing.cli.validate_results --route animehacker --output-root docs/testing/final-results
```

Internal normalizers and finalizers are provenance implementation details, not an additional reproduction command surface.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the animehacker TQ3_0 route.

### Start here

Begin with [`commands.md`](commands.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Folders

| Folder | What it contains |
| --- | --- |
| [`protocol/`](protocol/README.md) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the animehacker TQ3_0 route. |
| [`quality/`](quality/README.md) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the animehacker TQ3_0 route. |
| [`scripts/`](scripts/README.md) | Contains route-specific experiment scripts and compatibility entry points. Here it applies to the animehacker TQ3_0 route. |
| [`system/`](system/README.md) | Records the hardware, software and repository identity of the test environment. Here it applies to the animehacker TQ3_0 route. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`commands.md`](commands.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |
| [`dependencies.md`](dependencies.md) | Readable Markdown document titled “Validation dependencies”. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)
- [quality guide](quality/README.md)
- [scripts guide](scripts/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
