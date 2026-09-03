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
| [`builds/`](builds/README.md) | This folder groups the builds material used by the testing workflow. |
| [`campaigns/`](campaigns/README.md) | Implements route-specific test campaign logic behind the supported command line tools. |
| [`environments/`](environments/README.md) | This folder groups the environments material used by the testing workflow. |
| [`runs/`](runs/README.md) | This folder groups the runs material used by the testing workflow. |
| [`templates/`](templates/README.md) | This folder groups the templates material used by the testing workflow. |

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
