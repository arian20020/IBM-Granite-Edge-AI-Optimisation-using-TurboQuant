# Runs

This folder stores run manifests that bind an execution ID to its route, test case, configuration and evidence locations.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Use a run manifest to answer exactly what was requested, where its evidence belongs and which identifiers must appear in later reports.

### Start here

Continue with the generated `upstream-llama-cpp/` run-record folder described below.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| `upstream-llama-cpp/` | Contains generated run manifests for the upstream llama.cpp route. Individual run folders do not receive separate README files. |

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
