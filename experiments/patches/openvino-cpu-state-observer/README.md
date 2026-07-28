# OpenVINO CPU state-allocation observer patch set

This directory contains the portable project patch set applied to the immutable
OpenVINO `2026.2.1` baseline at commit
`ede283a88e35465f0d680dabbf1f44080f8fc387`.

Patch files must use the `.patch` suffix. The workspace controller applies them
in lexical filename order to a local, no-hardlink clone on branch
`project/cpu-state-allocation-observer`. It refuses a dirty upstream,
controlling worktree, or destination and records the ordered patch names,
tracked blob IDs, SHA-256 values, source/derived tree identities, and clean
state beside the derived checkout. New checkouts are prepared in a same-parent
staging directory and published only after complete validation, so a failed
attempt never exposes a partial final destination.

The observer is project instrumentation, not upstream OpenVINO functionality.
It must remain private, default-off, and compiled only for explicitly enabled
CPU debug-capability builds.
