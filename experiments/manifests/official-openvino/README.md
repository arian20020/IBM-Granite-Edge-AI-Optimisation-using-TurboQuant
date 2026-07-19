# Official OpenVINO Manifests

Store one immutable manifest per run using `test-id/run-id/manifest.json`.

The formal retest is pinned by `retest-matrix.json` to OpenVINO `2026.2.1` and
OpenVINO GenAI `2026.2.1.0`. Acquire the isolated Python environment, exact
release-tag source checkouts, hashes, package inventory, and device inventory:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/testing/acquire_official_openvino.ps1
```

Machine-local dependencies are placed in `.venv-official-openvino-2026.2.1/`
and `external/official-openvino/2026-07-19/`; both are ignored by Git. Auditable
outputs are written beneath
`experiments/raw-results/official-openvino/2026-07-19/{acquisition,environment}/`.
The script is resumable, rejects mismatched tags/remotes, dirty checkouts,
incorrect package versions, and a missing OpenVINO CPU or GPU device.
