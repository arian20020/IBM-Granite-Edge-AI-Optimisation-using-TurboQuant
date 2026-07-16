# AtomicBot TurboQuant Manifests

`retest-matrix.json` is the machine-readable WB-02 execution matrix. It pins the
upstream repository commit and declares every controlled build and runtime row,
including the two high-memory safety gates. The runner validates the manifest
strictly and refuses duplicate IDs or unknown backend/guard values.

Store one immutable manifest per run using `test-id/run-id/manifest.json`.
