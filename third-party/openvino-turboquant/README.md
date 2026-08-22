# Recovered OpenVINO CPU SDPA TurboQuant closure

OpenVINO `2026.3.0` already contains a merged TurboQuant codec in the Intel CPU
plugin. Upstream PR `#35853` added independent key/value algorithm selectors,
`u4` precision, WHT rotation, packed cache storage, per-token norms, and CPU
SDPA encode/score/accumulate dispatch. The public feature request `#35198`
remains open, and the controls are internal rather than a supported public
product surface, so this route remains experimental.

The application accepts only one bounded tuple:

- model: pinned IBM Granite 4.1 3B;
- head dimension: exactly 64;
- device: exactly `CPU`;
- attention: non-paged SDPA;
- key/value algorithm: exactly `TURBO`/`TURBO`;
- key/value precision: exactly `u4`/`u4`.

TBQ3, QJL, PolarQuant, GPU, PagedAttention, prefill compression, and arbitrary
head dimensions are excluded. The empty patch ledger is deliberate: changing
the released source would be less reproducible than consuming the exact merged
implementation. `Test-OpenVinoTurboQuantPatchClosure.ps1` verifies the clean
commit and every relevant source byte before a build or test is accepted.

The closure does not authorize application registration or redistribution.
Those actions remain closed until external security and license review, native
conformance, activation proof, and the later Granite evidence gates pass.
