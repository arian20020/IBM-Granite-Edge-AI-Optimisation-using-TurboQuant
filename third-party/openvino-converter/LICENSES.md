# Sealed converter closure licenses

The CPython embedding archive is pinned in `python-runtime.lock.json`; it is
licensed under PSF-2.0 and its upstream license text must accompany any
redistribution. The requested wheel train is deliberately unresolved: no wheel
is approved, downloaded, or redistributable from this repository.

`wheel-manifest.json` records the fail-closed resolver conflict. A later lock
may be accepted only when every wheel has a verified source URL, filename,
length, SHA-256, package license, and explicit redistribution disposition.
