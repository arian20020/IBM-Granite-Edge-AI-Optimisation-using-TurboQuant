# Manual verification status

This record separates checks of the instructions from tests of the application.

## Current setup-instruction checks

The [starter guide](Granite-Start-Here.md) targets signed package version 1.0.3.0. The ZIP SHA-256 is `8054E6219BF5BB568C93A354801234FD1F0C2D23BA0D93AF2EAE69D35EC8A697`.

- The seven starter-guide PowerShell blocks passed syntax parsing.
- ZIP extraction and matching-folder reuse passed locally. Numbered browser filenames were tested with a simulated file listing, and a wrong checksum stopped the step.
- The extracted package passed the setup script's bundle-hash and Microsoft dependency-signature checks. Nothing was installed by these checks.
- These checks do not establish a clean installation, upgrade or both model journeys on another laptop. Publishing documentation does not verify the bytes currently served by OneDrive.

## Earlier manual review

- The registered demonstration package and its executable hash matched the run guide.
- The inspected process used that installation and Windows reported it as responsive. This was not a new model journey or a clean launch test.
- The test-only E2E project built with zero warnings and errors. Discovery found exactly three NativeInspectionRoutes tests. The two journey source files are included with this update.
- All eight PowerShell examples in the manuals passed syntax parsing. Parsing does not prove installation, downloading or export succeeds.
- The download preferences, model size and checksum were compared with PinnedGraniteModelCatalog. The pinned IBM model file listing was accessible. A new full model download was not performed.

## Still not established

- Installation, launch and both runtime routes on an examiner's separate PC.
- A complete signed installer, its dependency bundle and a release download.
- A source-to-binary build record for the existing demonstration copy.
- Automatic chat-history restoration or uninstall data retention.

The three earlier live inspection results remain in the [application evidence](../testing/application-verification/README.md). They are not new results from this documentation review. Use the [package handover](../../release-evidence/Package-Handover.md) for the remaining release work.

The signed package and its setup commands are now documented, alongside the historical demonstration route. Do not describe the installer as fully tested on a clean independent computer.
