# PD-08 â€” Upstream llama.cpp Repository-Test Evidence

## Status of this checkpoint

The pinned upstream llama.cpp CPU Release build passed its complete
repository-provided test suite after the declared Python cross-check dependency
was supplied in an isolated environment.

| Run | Result | Summary |
|---|---|---|
| `UL-B04-R001` | Failed | 51 of 52 tests passed. Only `test-jinja-py` failed because Jinja2 was absent. |
| `UL-B04-R002` | Passed | 52 of 52 tests passed with Python 3.11.9 and Jinja2 3.1.6. |

## Controlled identities

- Route: `upstream-llama-cpp`
- Source tag: `b9870`
- Source commit: `2d973636e292ee6f75fadcf08d29cb33511f509f`
- Build: `BUILD-UL-CPU-B9870-002`
- Configuration: `CONFIG-UPSTREAM-CPU-X64-002`
- Environment: `ENV-20260714-INTEL-LAPTOP-01`

## Interpretation

The R001 failure was a test-environment dependency failure. The native
`test-jinja` test passed, and the isolated-dependency retest then passed the
complete suite. No rebuild was required.

## Claim boundary

This checkpoint verifies the repository-test gate for the pinned upstream CPU
build. It does not verify IBM Granite model loading, inference, performance,
quality, TurboQuant activation, the application test project, application
fixtures, application CI, packaging, or clean-checkout application validation.

PD-08 therefore remains in progress until the project application has its own
automated test project, repeatable validation command, Windows CI, and a
successful clean-checkout application build/test record.
