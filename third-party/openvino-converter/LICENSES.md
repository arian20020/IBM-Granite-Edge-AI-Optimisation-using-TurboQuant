# Sealed converter closure licenses

The CPython embedding archive is pinned in `python-runtime.lock.json`; it is
licensed under PSF-2.0 and its upstream license text must accompany any
redistribution.

The resolved binary-only CPython 3.13 / Windows x64 wheel closure is recorded
in `wheel-manifest.json`. Every entry records its exact source URL, filename,
length, SHA-256, package/version, declared license, ABI/platform, and the
`include-wheel-license-files` redistribution disposition. The build extracts
each complete wheel, retaining its `.dist-info/licenses` or equivalent license
payload in the sealed application-private closure. Packages with bundled
third-party notices, including NumPy, SciPy, Torch, OpenVINO, and NNCF, retain
those notices unchanged.

The reviewed direct train is Optimum Intel 2.1.0, Optimum 2.3.0, Transformers
5.5.4, OpenVINO 2026.3.0, OpenVINO GenAI 2026.3.0.0, and NNCF 3.3.0. Optimum
2.3.0 replaces the originally proposed incompatible Optimum 2.1.0 pin and was
explicitly approved on 2026-08-22 before resolving the closure.
