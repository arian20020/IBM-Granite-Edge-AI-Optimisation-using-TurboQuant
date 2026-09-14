# Manual verification status

This record separates checks of the instructions from tests of the application.

## Checked during the manual review

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

For assessment, the prepared demonstration computer is the documented launch route. Do not describe these manuals as a fully tested independent installation guide yet.
