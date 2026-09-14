# Package handover

This is an open release record, not an installer receipt. The existing installed-app tests are linked in the [release index](README.md). They do not prove a clean installation.

| Required detail | Current position |
| --- | --- |
| Download location and package filename | Not established in the repository. Obtain these from the owner before giving installation instructions. |
| Installer SHA-256 and package identity | Not recorded. The executable hash in the release index is not a replacement. |
| Source commit and complete build recipe | Candidate revision is recorded, but its link to the installed binary was not independently proved. |
| Tool versions and runtime inputs | Partial instructions exist in the build guide. Exact external stages, their hashes and signing requirements still need one verified record. Never publish signing keys. |
| Clean install, Start launch and both inspection routes | Not recorded as a clean-machine test. Use a separate suitable environment, not the only working installation. |
| Uninstall and retained data | Not verified. Back up required data first. |
| Release tag and final report bundle | Not assigned by this documentation work. |

For each completed row, record the date, exact package and an evidence link. Do not replace “not recorded” with “passed” just because compilation succeeds.

Documentation, test evidence and diagram files also need inclusion in the final reviewed commit. Check `git status --short`; a local file is not part of a fresh checkout until committed. Review unrelated files separately rather than staging everything.
