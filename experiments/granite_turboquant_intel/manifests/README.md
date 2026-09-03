# Manifests

Records the exact inputs, versions and intended configurations for experiments.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records the exact inputs, versions and intended configurations for experiments.

### Start here

Continue with [`builds/`](builds/README.md).

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`builds/`](builds/README.md) | Records the repository revision, compiler settings and binary identity used for each controlled build. |
| [`campaigns/`](campaigns/README.md) | Implements route-specific test campaign logic behind the supported command line tools. |
| [`environments/`](environments/README.md) | Records the hardware, operating system, drivers and tool versions of each test machine. |
| [`runs/`](runs/README.md) | Binds individual run IDs to their test case, configuration, inputs and expected evidence locations. |
| [`templates/`](templates/README.md) | Provides reusable manifest skeletons; a template is an input format, not evidence that a run occurred. |

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [builds guide](builds/README.md)
- [campaigns guide](campaigns/README.md)
- [environments guide](environments/README.md)
- [runs guide](runs/README.md)
- [templates guide](templates/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
