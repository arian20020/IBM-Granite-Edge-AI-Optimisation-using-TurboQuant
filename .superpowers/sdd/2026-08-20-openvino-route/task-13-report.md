# Task 13 report: standard OpenVINO optimization

## Outcome

Implemented a closed, CPU-only standard optimization pipeline with independently
published FP16, INT8, and INT4 OpenVINO packages. Runtime KV-cache optimization
is represented separately from persistent weights and disposable compiled caches.
The only non-default KV-cache candidate is exact `u8`, and it is exposed only with
real official-worker construction and generation evidence.

The four registered objectives are exact candidates:

- Automatic: INT8 weights, released-default KV cache.
- Quality: FP16 weights, released-default KV cache.
- Balanced: INT8 weights, `u8` KV cache.
- Efficiency: INT4 weights, `u8` KV cache.

No GGUF or llama.cpp quantization vocabulary enters these contracts. FP16 cannot
be claimed as restored from an already compressed source.

## Implementation and safety

- The sealed offline converter now has exact `convert` and `optimize` operations.
- NNCF produces fresh INT8/INT4 IR weights; FP16 produces a fresh compressed FP16
  IR. The three controlled artifacts have strictly descending weight-file sizes.
- Optimization uses a locked source snapshot, fresh sibling staging, native
  validation, official CPU smoke, sanitized provenance, atomic publication, and
  independent published-package reinspection.
- Old conversion/optimization provenance is not copied into a derived artifact;
  the new optimization provenance records the source manifest identity and binds
  every output byte using an exact output manifest.
- Provenance rejects malformed types, unknown fields, unregistered candidates,
  altered files, and any optimizer version outside the exact pinned closure.
- Runtime startup carries exact requested KV precision. The official worker applies
  the same property map to validation and generation pipelines and reports actual
  precision only after construction succeeds. The managed conversation validator
  binds requested and actual evidence.
- Generation success and the quality disposition are separate. The deterministic
  quality gate checks exact bounded token accounting, prompt accounting, failure
  absence, valid Unicode, and unsafe control characters independently of the
  generation-completed disposition.
- Compiled-cache identities bind runtime, plugin, device, driver, model, and
  configuration identities; caches are explicitly disposable and are not model
  artifacts.

## Red/green evidence

- RED: missing runtime contract types and session-start evidence failed compile.
- GREEN: focused runtime contract tests passed 2/2.
- RED: INT8 NNCF rejected `all_layers`; fixed with the supported exact INT8 mode.
- RED: NNCF carriage-return progress corrupted the JSONL channel; library stdout is
  now suppressed only during the operation and restored for protocol events.
- RED: one-token decoded text was not a sufficient independent quality sample.
- GREEN: the explicit bounded token-accounting quality rubric passed its focused
  unit test and the real three-candidate service campaign.

## Fresh verification

- Independent official native closures C and D: 7/7 C++ targets passed in each.
- Closed .NET protocol contracts: 162 passed, 0 failed.
- OpenVINO unit suite against official Stage C: 228 passed, 0 failed, 0 skipped.
- Worker-client unit suite: 13 passed, 0 failed.
- Protected official/converter/optimization campaign: 13 passed, 0 failed, with
  the pre-existing physical Intel GPU case skipped because no authorized GPU was
  supplied.
- Real registered-candidate service campaign: 1/1 passed in 1m44s. It converted a
  controlled Granite source once, then independently optimized, validated, loaded,
  generated, published, and reinspected FP16/default, INT8/u8, and INT4/u8 packages.
- Full protocol and process cleanup checks found no official worker or Python
  process residue.
- Release x64 application build passed with 0 errors; the known missing-profile
  `NETSDK1198` warning remains unchanged.
- `git diff --check`: clean apart from repository line-ending notices.

## Bound evidence

- Official Stage C manifest SHA-256:
  `6daf8376e15f35f552bd6072d581617d5dae1829056a7baf1f04e17ae4866a08`
- Official Stage D manifest SHA-256:
  `c87a5d6522912fe56b9d091f71ee2eb801e1d1c879a7963b216c61d815d94421`
- Converter Stage P manifest SHA-256:
  `b975313fccb90250cdf3ba58416b1ea019a93c7889c34a5b425df54df7056983`

Review and execution were performed inline per user direction; no subagent was
used.
