# Manual verification status

This record separates checks of the instructions from tests of the application.

## Inspection issue found on a fresh laptop

The supplied 1.0.4.0 package failed model inspection on one new laptop because the packaged process rejected its redirected temporary folder. A local source fix was tested under package identity and the local app completed GGUF inspection. Hardware inspection then required the separately installed pinned `llmfit` tool; after that, the local app reached compatibility. The main README's new tool-install command was tested with a separate test destination, including a repeat run.

The 1.0.5.0 preview was assembled from 1.0.4.0 with the fixed foundation DLL and a new self-signed publisher. It installed alongside the old app on the tested laptop; the user reported that the application was working after trying GGUF and OpenVINO. Its extracted setup bundle passed the checksum and dependency checks, and recognised the existing preview. A fresh-computer installation and complete source rebuild remain unverified. The historical results below remain records of their original scopes.

## Beginner-manual alignment

In the earlier documentation review, all eleven files in `docs/manuals` were reviewed to distinguish packaged setup from model downloads, historical installations and developer builds. The two beginner guides now link to the main README for the additional hardware tool command; historical details remain labelled.

In that earlier review, all 35 PowerShell code blocks across the manuals passed syntax parsing. Installation, model downloads and security-setting changes were not executed for that review. Those historical counts do not cover subsequent edits.

## Latest test-only verification

[CI run 35231533452](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/35231533452) completed successfully for commit `ca0eeba7`: **1,959 tests passed** with the workflow's stated exclusions. The test-only changes were merged into main; application code and the 1.0.4.0 ZIP were not changed by that commit.

Separately, 57 focused packaged tests passed locally with matching build inputs, and the pinned-runtime GGUF harness passed 20 tests including the four cache handoff cases. Those four positive cases are categorised as `RequiresVerifiedGgufRuntimeClosure` and are not counted in the ordinary CI run. The earlier worker handshake timeout did not recur in that successful CI run; its timeout and assertions were not relaxed.

These results do not prove a clean installation or every model journey on an examiner's laptop. [Run the app](Run-the-App.md) now leads with the current starter-guide sequence; historical demonstration details remain labelled as historical.

## Current setup-instruction checks

The [starter guide](Granite-Start-Here.md) targets the self-signed 1.0.5.0 preview. The ZIP SHA-256 is `87AD9CE4E829F8DD4CF3709D6851B693DCA38B635AB9F5D768619FFB64A44AAC`. The bullets below describe the earlier 1.0.4.0 checks unless stated otherwise.

- All eleven starter-guide PowerShell blocks passed syntax parsing; this does not establish successful execution on another laptop. The optional Smart App Control diagnostic block only prints warnings and opens settings; no security setting was changed to test these instructions.
- ZIP extraction and matching-folder reuse passed locally. Numbered browser filenames were tested with a simulated file listing, and a wrong checksum stopped the step.
- The extracted package passed the setup script's bundle-hash and Microsoft dependency-signature checks. Nothing was installed by these checks.
- The signed 1.0.4.0 package passed signature and block-map verification for 24,024 payload files. Application/runtime bytes match the locally tested preview except for the staged installation manifest. No installed application was changed during final packaging.
- Setup safety tests covered current-version reuse and rejection of development registrations, missing installations and outdated launches. Tiny model fixtures covered flat extraction, matching-folder reuse, download-mark removal, mismatched-file refusal and archive traversal rejection.
- At the earlier packaging review, the compatibility suite passed 1,338 tests; the label suite passed 43; focused OpenVINO and GGUF reimport suites each passed 20. Selected local OpenVINO chat and reimport checks succeeded. The later filtered UI-host CI result is recorded above; an all-format/hardware matrix remains unverified.
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
