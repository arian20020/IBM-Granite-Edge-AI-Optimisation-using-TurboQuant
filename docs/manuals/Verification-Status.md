# Manual verification status

This record separates checks of the instructions from tests of the application.

## Current setup-instruction checks

The [starter guide](Granite-Start-Here.md) targets signed package version 1.0.4.0. The ZIP SHA-256 is `6788E6E293EB1D7129733F4C7246AABA26F8BC5F068751C35806418C17391126`.

- All nine starter-guide PowerShell blocks passed syntax parsing; this does not establish successful execution on another laptop.
- ZIP extraction and matching-folder reuse passed locally. Numbered browser filenames were tested with a simulated file listing, and a wrong checksum stopped the step.
- The extracted package passed the setup script's bundle-hash and Microsoft dependency-signature checks. Nothing was installed by these checks.
- The signed 1.0.4.0 package passed signature and block-map verification for 24,024 payload files. Application/runtime bytes match the locally tested preview except for the staged installation manifest. No installed application was changed during final packaging.
- Setup safety tests covered current-version reuse and rejection of development registrations, missing installations and outdated launches. Tiny model fixtures covered flat extraction, matching-folder reuse, download-mark removal, mismatched-file refusal and archive traversal rejection.
- The compatibility suite passed 1,338 tests; the label suite passed 43; focused OpenVINO and GGUF reimport suites each passed 20. Selected local OpenVINO chat and reimport checks succeeded. A complete UI-host test suite and all-format/hardware matrix were not run.
- IBM's pinned official Granite 4.1 3B Q4_K_M file metadata matches the guide's model size and SHA-256. A new full official download was not performed. The exact prepared raw OpenVINO ZIP remains a OneDrive download; no equivalent official ZIP was verified.
- These checks do not establish a clean installation, upgrade or both model journeys on another laptop. Publishing documentation does not verify the bytes currently served by OneDrive.

## Earlier manual review

- The registered demonstration package and its executable hash matched the run guide.
- The inspected process used that installation and Windows reported it as responsive. This was not a new model journey or a clean launch test.
- The test-only E2E project built with zero warnings and errors. Discovery found exactly three NativeInspectionRoutes tests. The two journey source files are included with this update.
- All eight PowerShell examples in the manuals passed syntax parsing. Parsing does not prove installation, downloading or export succeeds.
- The download preferences, model size and checksum were compared with PinnedGraniteModelCatalog. The pinned IBM model file listing was accessible. A new full model download was not performed.

## Still not established

- Installation, launch and both runtime routes on an examiner's separate PC.
- The final replacement ZIP being served by the existing OneDrive link; uploading it remains a separate handover step.
- A source-to-binary build record for the existing demonstration copy.
- Automatic chat-history restoration or uninstall data retention.

The three earlier live inspection results remain in the [application evidence](../testing/application-verification/README.md). They are not new results from this documentation review. Use the [package handover](../../release-evidence/Package-Handover.md) for the remaining release work.

The signed package and its setup commands are now documented, alongside the historical demonstration route. Do not describe the installer as fully tested on a clean independent computer.
