# EXP-TV-COMP-001 — Blocked Feasibility Run

The controlled run ended **BLOCKED** at the real Granite embedding gate. The pinned `ibm-granite/granite-embedding-small-english-r2` revision uses `ModernBertModel`; the locked OpenVINO 2026.3 conversion stack rejected that architecture for feature extraction. A direct conversion attempt also failed during ModernBERT tracing.

TurboVec 1.0.0 was pinned to commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` (MIT). Its published wheel ran 479 upstream Python tests: 321 passed, 1 failed on Windows long-path atomic persistence, and 157 were skipped. The local PDF extraction gate passed 5/5 tests. Rust gates were unexecuted because Rust 1.89 was absent and installation was not approved.

The matched Exact/TQ2/TQ3/TQ4 campaign did not execute. Therefore this evidence makes no claim about retrieval quality, latency, memory, storage, application integration, or production readiness. The next valid action is to approve and lock a supported ModernBERT-to-OpenVINO conversion path, then start a new append-only run.
