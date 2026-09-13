# GGUF runtime capability boundary

This project decodes strict, bounded runtime manifests, compares the packaged
manifest with its trusted embedded copy, verifies every package member, and
validates complete CPU configurations without ranking or fallback.

Production packages are loaded only from an explicit application subtree. The
verifier rejects unlisted files, path traversal, reparse points, length/hash
changes, role duplication, and executable architecture mismatches.
