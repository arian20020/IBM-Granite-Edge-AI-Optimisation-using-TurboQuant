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

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder documents the controlled CPU state-allocation observer patch set. It is private project instrumentation, remains disabled by default and must not be described as upstream OpenVINO functionality.

### Start here

This is a navigation or evidence container. Use the folder explanations below to choose the next level.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
