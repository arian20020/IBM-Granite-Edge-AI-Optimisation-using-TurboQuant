# Independent Evidence Recovery Test

**Evidence ID:** EV-RACL-RECOVERY-20260914<br>
**Test date:** 2026-09-14<br>
**Tester:** Arian B<br>
**Result:** Pass<br>
**Related assumptions:** `A-001`; `A-015`

## Purpose

This test checks whether the evidence held at the report cut-off can be recovered from an independent OneDrive copy without corruption.

## Method

The project owner uploaded the final and raw evidence folders to OneDrive, downloaded the backup through the OneDrive website, and extracted it into a new local folder. The original and restored files were then compared by relative path, byte length and SHA-256 hash.

The direct comparison covered:

- `docs/testing/final-results`;
- `experiments/raw-results`.

## Results

| Evidence set | Original files | Restored files | Original bytes | Restored bytes | Differences |
|---|---:|---:|---:|---:|---:|
| Final evidence | 226 | 226 | 14,040,901 | 14,040,901 | 0 |
| Raw evidence | 2,482 | 2,482 | 81,978,567 | 81,978,567 | 0 |

The comparison also created one deterministic fingerprint for each file tree. Each line used `relative path|byte length|file SHA-256`, sorted by relative path and encoded as UTF-8 with LF line endings.

| Evidence set | Original fingerprint | Restored fingerprint |
|---|---|---|
| Final evidence | `a1b21c40eee679afeb10d82ea0adf2cde0b029ab59438989f8415a3a520d2c05` | `a1b21c40eee679afeb10d82ea0adf2cde0b029ab59438989f8415a3a520d2c05` |
| Raw evidence | `39ba4aa6195864ae4ddf1bc93de57c9b5a8de9e748685ddf8ce8c8d3f7337050` | `39ba4aa6195864ae4ddf1bc93de57c9b5a8de9e748685ddf8ce8c8d3f7337050` |

## Conclusion

The tested report-cut-off evidence was recovered from the independent cloud copy with no missing, added or changed files. This confirms `A-001` and `A-015` for the two tested evidence folders. The test must be repeated if either evidence folder changes.

## Boundary

This test proves recovery of the two named evidence folders. It does not prove recovery of unrelated repository files or later evidence added after the test.
