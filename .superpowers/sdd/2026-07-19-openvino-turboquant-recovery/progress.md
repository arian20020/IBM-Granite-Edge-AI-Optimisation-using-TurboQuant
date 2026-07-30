# SDD ledger — plan: docs/superpowers/plans/2026-07-19-openvino-turboquant-recovery.md

Reconstructed 2026-07-30 from the active branch history and retained evidence after conversation compaction.
Task 1: complete (prior branch history; WB-04 execution status corrected and revision-controlled)
Task 2: complete (portable clean-source replay and exact source identity retained)
Task 3: complete (standalone TBQ3/TBQ4 codec patch and native tests retained)
Task 4: complete (independent K/V controls and activation telemetry retained)
Task 5: complete (CPU stateful SDPA TurboQuant storage integration retained)
Task 6: complete (build 00edae3b verified; 208/208 relevant native-test executions passed)
Task 7: in progress (load-proven U8 and U4 artifacts complete; FP16 and suitable-host 8B artifacts unresolved)
Task 8: in progress (governed runtime campaigns and matrix binding implemented; expected-rejection evidence controller pending review)
Task 9: in progress (formal 3B measurements not yet launched)
Task 10: in progress (quality capture/adjudication infrastructure implemented; P1-P6 execution not yet launched)
Task 11: pending (8B execution requires a host with at least 24 GiB available RAM)
Task 12: pending
Task 8: audit finding (2026-07-30) — matrix must be frozen before any formal campaign because campaign identity hashes raw matrix and all runtime-controller sources.
Task 10: audit finding (2026-07-30) — capture API is hash-bound but lacks a governed real OpenVINO adapter; legacy CLI/adjudicator schemas are not admissibly connected.
Task 8: expected-rejection controller complete pending review (commit e943b26; 13 exact probes, byte-identical dated evidence).
Task 9: fix round 1/5 in progress (review found frozen scalar/attention/host fields not all consumed pre-launch in commit a3ebc02).
Task 9: fix round 1/5 (3 addressed, 0 open; commit a557ec0).
Task 9: matrix-freeze subtask complete (commits a3ebc02..a557ec0, scoped re-review clean).
Task 8: expected-rejection fix round 1/5 in progress (review required create-only validated publication).
Task 8: expected-rejection fix round 1/5 (2 addressed, 0 open; commit c4cbc15).
Task 8: expected-rejection subtask complete (commits e943b26..c4cbc15, scoped re-review clean).
Task 10: governed quality-capture design/plan committed at 7be5941; implementation pending.
Task 10 / quality adapter Task 1: implementation complete pending review (commit a6b5133; 9 worker tests and 68 related tests passed).
Task 10 / quality adapter Task 1: fix round 1/5 in progress (review found P6-failure seventh-call, normalized-spec, forbidden-key/type, and atomic-test gaps).
Task 10 / quality adapter Task 1: fix round 1/5 (4 addressed, 1 open; commit 0f3696b — nested stateful Mapping remains).
Task 10 / quality adapter Task 1: fix round 2/5 in progress (deep detached input snapshot).
Task 10 / quality adapter Task 1: fix round 2/5 (1 addressed, 0 open; commit 6ba9bb8).
Task 10 / quality adapter Task 1: complete (commits a6b5133..6ba9bb8, scoped re-review clean; 27 focused and 86 related tests reported green).
Task 10 / measured-summary producer alignment: complete (commit f7c4295; scoped review clean; 53 related tests reported green).
Task 10 / quality adapter Task 2: implementation commit 4d78406; review found 1 Critical and 3 Important findings.
Task 10 / quality adapter Task 2: fix round 1/5 (original 4 findings addressed; commits 38066b3; review found 1 new Important receipt-chain finding).
Task 10 / quality adapter Task 2: fix round 2/5 (receipt-chain finding addressed; commit 80f5943).
Task 10 / quality adapter Task 2: complete (commits 4d78406..80f5943, final scoped re-review clean; 116 related tests reported green).
