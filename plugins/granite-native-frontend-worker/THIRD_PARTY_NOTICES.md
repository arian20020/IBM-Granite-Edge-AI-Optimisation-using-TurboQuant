# Third-party provider notices

This repository does not vendor the full third-party provider repositories. Provider installation or registration uses reviewed, pinned revisions. External providers have no production write authority.

| Provider | Reviewed source | Revision/version | Licence/terms | Use |
|---|---|---|---|---|
| Microsoft WinUI Skills | `microsoft/win-dev-skills` | commit `68ae65d5c65ee87c3265a7f5abe3aaf97c7e6932`, plugin 0.6.0 | MIT; preview package includes unsigned helper binaries that must be reviewed | Native WinUI technical authority |
| Superpowers | `openai/plugins` distribution of `obra/superpowers` | commit `1e285826e604f66f7208f7ac4dba0fe8341d1f57`, plugin 6.3.0 | MIT | Development methodology |
| Uncodixfy | `cyxzdev/Uncodixfy` | commit `e0e028058b5259debdd94b78147c6d6c77bf7da2` | MIT | Source provenance for the local WinUI adaptation; upstream skill is not installed at runtime |
| Stark | distributed through `hashgraph-online/awesome-codex-plugins` | marketplace commit `0ff99e11ba9c2ef21446dc9960033c5c18183a97`; upstream `f0d010c/stark` commit `ff94e5b4e1c98d259f3cde9f806406c4528deed4`; plugin 0.7.2 | Apache-2.0 | Optional native design adviser |
| UI/UX Pro Max | `nextlevelbuilder/ui-ux-pro-max-skill` | commit `f23267105ad1f4ccd94af45d382584ad45b586f7`, CLI 2.5.0 | MIT for reviewed public package; premium material is excluded | Optional narrow WinUI research |
| Figma plugin | `openai/plugins` | commit `1e285826e604f66f7208f7ac4dba0fe8341d1f57`, plugin 2.0.20 | Figma Developer Terms | Optional approved design inspection |
| Product Design | `openai/role-specific-plugins` | commit `fe5608d2512a7d6a7b9821ce8a88c48464ecd6e4`, plugin 0.1.50 | MIT | Optional concept exploration |

The local `uncodixfy-winui-v2.yml` is a project compatibility policy that records which broad ideas are retained, overridden, or rejected for a native WinUI application. It is not a redistribution of the upstream skill.

Provider updates require a reviewed lock-file change. Never follow floating `main`, `master`, or `latest` references during a production campaign.
