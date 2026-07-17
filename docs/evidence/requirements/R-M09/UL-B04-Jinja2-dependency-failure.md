# R-M09 â€” UL-B04 Missing Jinja2 Dependency

## Failure identity

- Failure ID: `FAIL-UL-B04-R001-JINJA2-MISSING`
- Failed run: `UL-B04-R001`
- Retest: `UL-B04-R002`
- Final status: Resolved

## Observation

CTest completed all 52 registered tests. Fifty-one passed. The only failing
test was `test-jinja-py`, which reported that Python could not import
`jinja2`.

## Root cause

The Python interpreter selected for the repository test environment did not
contain the Jinja2 package required by the optional Python cross-check.

## Corrective action

An isolated Python 3.11 environment was created outside the repository and
Jinja2 3.1.6 was installed. The complete 52-test suite was rerun without
rebuilding llama.cpp.

## Retest result

`UL-B04-R002` passed all 52 tests.

## Boundary

This was a repository-test dependency failure. It was not a native llama.cpp
test failure, model failure, inference failure, performance failure, or
TurboQuant failure.
