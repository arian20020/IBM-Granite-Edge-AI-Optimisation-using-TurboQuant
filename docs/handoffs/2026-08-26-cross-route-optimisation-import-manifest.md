# Cross-route optimisation import ledger

This ledger freezes the authorities and verification rules for bounded component imports. The JSON beside this file is the machine authority; this document is its review narrative.

| Component | Pinned authority | Status |
|---|---|---|
| UO1 | `origin/feature/cross-route-optimisation-ui-v1@8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8` | Pending |
| GGUF runtime | `origin/feature/gguf-cli-chat-production@bacb3f4106e0191b05b870358342f8158765396d` | Pending |
| OpenVINO route | `origin/feature/openvino-optimisation-adapter-v1@f0189ed187ba900f27bade5fde282ae4e99e8d7b` | Pending |

The planning base is `092589c38981ad86bb73c7c97dff01ab8b5a6c8e`; historical contracts are pinned at `e254385997392601102b16acf19244437803bdcc`; the separately built llama.cpp quantiser source is pinned at `3f7c29d318e317b63f54c558bc69803963d7d88c`.

No component is currently authorized for import. A component becomes authorized only after its JSON record is changed to `Verified` and contains complete source provenance, destination classifications, dependency closure, excluded shared paths, integration patch groups, and verification commands. Exact files retain commit:path:blob byte identity. Adapted files are reconstructed from a single declared authority base through a hash-pinned tracked patch. Created files have no invented source blob and must be proven absent from the declared integration base and created from `/dev/null` by their hash-pinned patch.

A component import commit must change only its manifest-approved destination paths and these two manifest files.
