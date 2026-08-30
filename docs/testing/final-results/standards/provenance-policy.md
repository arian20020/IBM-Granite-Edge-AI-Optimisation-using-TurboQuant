# Provenance policy

Every evidence row identifies its route and campaign, has a stable `evidence_id`, and records its role, repository-relative path, byte size, and SHA-256 digest. Portable deliverables use POSIX-style paths relative to the repository root; machine-specific absolute paths, drive-qualified paths, backslashes, and parent traversal are excluded.

SHA-256 values contain exactly 64 hexadecimal characters. Derived artifacts set `derived` to `true` and identify their input evidence IDs. A source label may preserve an original name or location without replacing the portable path or digest.

Evidence paths and hashes are validation inputs. A missing or mismatched artifact is an explicit integrity gap, not a silently repaired reference.
