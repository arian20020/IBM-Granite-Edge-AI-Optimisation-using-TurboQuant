# Package handover

This is an open release record, not an installer receipt. The existing installed-app tests are linked in the [release index](README.md). They do not prove a clean installation.

| Required detail | Current position |
| --- | --- |
| Download location and package filename | `build-version-1.0.4.0/Granite-Edge-AI-Setup.zip` (Git LFS); package SHA-256 `6788E6E293EB1D7129733F4C7246AABA26F8BC5F068751C35806418C17391126` (585,596,470 bytes). The hash matches the value enforced by the installation guide. |
| Package identity and source provenance | The setup ZIP is the 1.0.4.0 test package. Its full source-to-package build provenance has not been independently established. |
| Source commit and complete build recipe | Candidate revision is recorded, but its link to the installed binary was not independently proved. |
| Tool versions and runtime inputs | Partial instructions exist in the build guide. Exact external stages, their hashes and signing requirements still need one verified record. Never publish signing keys. |
| Clean install, Start launch and both inspection routes | Not recorded as a clean-machine test. Use a separate suitable environment, not the only working installation. |
| Uninstall and retained data | Not verified. Back up required data first. |
| Release tag and final report bundle | Not assigned by this documentation work. |

For each completed row, record the date, exact package and an evidence link. Do not replace “not recorded” with “passed” just because compilation succeeds.

Documentation, test evidence and diagram files also need inclusion in the final reviewed commit. Check `git status --short`; a local file is not part of a fresh checkout until committed. Review unrelated files separately rather than staging everything.
