# Recovered OpenVINO CPU SDPA TurboQuant closure

OpenVINO main commit `f5f594dc0c9e5961785f0d17743486d52eac87e7` contains the merged TurboQuant codec in the Intel CPU
plugin. Upstream PR `#35853` added independent key/value algorithm selectors,
`u4` precision, WHT rotation, packed cache storage, per-token norms, and CPU
SDPA encode/score/accumulate dispatch. The public feature request `#35198`
remains open, and the controls are internal rather than a supported public
product surface, so this route remains experimental.

The application accepts only two bounded, independently activated tuples:

- model: pinned IBM Granite 4.1 3B;
- head dimension: exactly 64;
- device: exactly `CPU`;
- attention: non-paged SDPA;
- key/value algorithm: exactly `TURBO`/`TURBO`;
- key/value precision: exactly `u4`/`u4` (TBQ4) or `u3`/`u3` (TBQ3).

QJL, PolarQuant, GPU, PagedAttention, prefill compression, and arbitrary
head dimensions are excluded. The empty patch ledger is deliberate: changing
the released source would be less reproducible than consuming the exact merged
implementation. `Test-OpenVinoTurboQuantPatchClosure.ps1` verifies the clean
commit and every relevant source byte before a build or test is accepted.

The exact source/runtime closure has passed native conformance and activation
proof for TBQ4 and TBQ3 on the accepted Granite model. Application registration
remains bound to the signed manifests and Granite runtime evidence gates.
