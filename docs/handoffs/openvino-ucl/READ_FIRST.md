# OpenVINO UCL handoff — read first

This archive is a continuation kit, not release evidence.

Baseline implementation: `c1e0fe2f`

Branch: `feature/openvino-route`

## Safe start

1. Copy the outer ZIP and its sibling `.sha256` file to the UCL Intel laptop.
2. Recompute the ZIP SHA-256 and compare it ordinally with the lowercase value
   in the sibling file before extracting anything.
3. Extract the ZIP into a new explicit directory, for example
   `D:\OpenVino-UCL-Handoff`.
4. Choose a different new destination, for example `D:\OpenVino-UCL-Work`, and
   run:

   ```powershell
   $bundleRoot = 'D:\OpenVino-UCL-Handoff'
   $destinationRoot = 'D:\OpenVino-UCL-Work'
   & "$bundleRoot\tools\Initialize-UclHandoff.ps1" `
       -BundleRoot $bundleRoot `
       -DestinationRoot $destinationRoot
   ```

5. Require the exact terminal line `openvino_ucl_handoff_initialized`.
6. Open `D:\OpenVino-UCL-Work\repository` in Codex, or the corresponding
   repository below the destination you selected.
7. Paste the entire `CONTINUATION_PROMPT.md` into the new worker.

Do not manually copy files into the reconstructed repository or closure roots.
If initialization reports `openvino_ucl_handoff_invalid`, preserve the bundle,
capture only the path-safe failing disposition, and diagnose the mismatch from
a new empty destination.

## Prerequisites

- Windows x64 on the authorized UCL Intel laptop.
- Git for Windows and Windows PowerShell 5.1 or later.
- .NET SDK 10.0.301, or the repository-compatible SDK selected by its build
  configuration.
- Visual Studio/MSBuild and VSTest with WinUI/AppContainer test tooling.
- At least 8 GB of free disk space beyond the extracted outer bundle.
- Authorized access to the pinned Granite model required by the campaign.
- The approved GitHub self-hosted runner labels and protected environment if
  the trusted workflows will be dispatched.

## Trust boundary

The official worker, TurboQuant worker, converter closure, local task reports,
and local test results represented by this archive are transferred inputs.
They are not trusted UCL evidence and cannot close hosted execution, UCL
hardware, external security/license, controlled RTM, or release-acceptance
gates.

Do not place any of the following in retained evidence:

- absolute paths, usernames, hostnames, runner names, or environment dumps;
- credentials, tokens, authorization headers, or private keys;
- user prompts, generated text, model/tokenizer bytes, or local model paths;
- raw stdout/stderr or untyped native logs.

The correct starting release disposition is `openvino_release_blocked`. The
feature is accepted only when the ordered gate returns
`openvino_release_accepted` on one clean immutable candidate with genuine
hosted/UCL evidence and required approvals.
