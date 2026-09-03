# Patches

Contains controlled patch inputs used by selected experimental builds.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled patch inputs used by selected experimental builds.

### Start here

Continue with [`openvino-cpu-state-observer/`](openvino-cpu-state-observer/README.md).

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`openvino-cpu-state-observer/`](openvino-cpu-state-observer/README.md) | Contains the private, default-off instrumentation patch set used to observe OpenVINO CPU state allocation. It is project instrumentation, not upstream functionality. |
| [`openvino-turboquant/`](openvino-turboquant/README.md) | Contains the ordered TurboQuant and cache-telemetry patch series applied to a pinned OpenVINO GenAI baseline for the experimental route. |

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [openvino-cpu-state-observer guide](openvino-cpu-state-observer/README.md)
- [openvino-turboquant guide](openvino-turboquant/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
