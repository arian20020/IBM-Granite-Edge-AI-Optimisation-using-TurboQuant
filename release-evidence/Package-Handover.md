# Package handover

This is an open release record, not an installer receipt. The existing installed-app tests are linked in the [release index](README.md). They do not prove a clean installation.

| Required detail | Current position |
| --- | --- |
| Download location and package filename | `build-version-1.0.5.0/Granite-Edge-AI-Setup.zip` (Git LFS); SHA-256 `87AD9CE4E829F8DD4CF3709D6851B693DCA38B635AB9F5D768619FFB64A44AAC` (584,521,838 bytes). |
| Package identity and source provenance | The 1.0.5.0 test preview was assembled from the signed 1.0.4.0 package, replacing the hardware foundation DLL with the locally built fix and changing package identity to the separately signed preview. It is not a complete source rebuild. Its self-signed test certificate expires 2 November 2027. |
| Inspection fixes and required hardware tool | The preview package was installed and opened alongside 1.0.4.0 on one laptop, where the inspection journey was exercised. The setup ZIP omits `llmfit`; the main guide downloads its pinned, hash-checked release separately. Fresh-computer installation and the full matrix of model journeys remain unverified. |
| Source commit and complete build recipe | Candidate revision is recorded, but its link to the installed binary was not independently proved. |
| Tool versions and runtime inputs | Partial instructions exist in the build guide. Exact external stages, their hashes and signing requirements still need one verified record. Never publish signing keys. |
| Clean install, Start launch and both inspection routes | Not recorded as a clean-machine test. Use a separate suitable environment, not the only working installation. |
| Uninstall and retained data | Not verified. Back up required data first. |
| Release tag and final report bundle | Not assigned by this documentation work. |

For each completed row, record the date, exact package and an evidence link. Do not replace “not recorded” with “passed” just because compilation succeeds.

Documentation, test evidence and diagram files also need inclusion in the final reviewed commit. Check `git status --short`; a local file is not part of a fresh checkout until committed. Review unrelated files separately rather than staging everything.
