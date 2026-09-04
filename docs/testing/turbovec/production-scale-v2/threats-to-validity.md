# Threats to validity and independent review

- **Scale:** only 30 chunks completed; fixed metadata dominates storage and timing.
- **Host contamination:** eight higher-scale readiness windows exceeded CPU and/or RAM-stability limits. They were rejected, not averaged into results.
- **Thermals/GPU:** thermal sensors and GPU counters were unavailable through the safe host APIs used. The route was CPU-only.
- **Machine scope:** results come from one Windows laptop and cannot establish cross-device performance.
- **Dataset realism:** data are deterministic, diverse and non-private, but generated rather than a licensed production document collection.
- **PDF linkage:** extraction fixtures and retrieval corpora are separate; mixed-PDF retrieval usefulness is not established.
- **Evaluation scope:** relevance labels are independent of candidate output, but generated-answer quality and human usability were not measured.
- **Memory noise:** process working-set increments at 30 vectors are near allocator/measurement noise.
- **Toolchain:** strict Clippy fails on 38 pinned-upstream warnings; one upstream Windows long-path test fails under the recorded host policy.
- **Order bias:** a rotating schedule reduced order bias, but repetition 5 necessarily repeats one order in a five-repetition/four-format design.

Review found no hidden candidate failure, favourable-run removal, corpus duplication, ground-truth leakage, unequal embedding input, storage-field mismatch, warm/cold conflation, threshold change, unsupported product claim, privacy exposure or production integration. The unresolved external-load restriction is material and determines `BLOCKED`.
