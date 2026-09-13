# Machine readiness and formal-block ledger

Every attempt used a five-minute idle period followed by at least a requested 60-second observation. Attempt `readiness-scale-30-001` was rejected because measured timestamp coverage was 59.647851 seconds; a regression-first correction ensured later windows cover the full duration. Attempts 001-006 at scale 1,000 used one-second sampling. Attempts 007-008 used three-second sampling to reduce measurement overhead; thresholds did not change.

| Attempt | Ready | Coverage s | Mean CPU % | RAM variation % | Minimum available bytes | Classification |
|---|---|---:|---:|---:|---:|---|
| scale-30-001 | No | 59.648 | 7.51 | 1.95 | 6,503,784,448 | short window |
| scale-30-002 | Yes | 60.330 | 7.47 | 1.28 | 6,589,911,040 | admitted |
| scale-1000-001 | No | 60.365 | 10.83 | 0.97 | 6,534,037,504 | CPU |
| scale-1000-002 | No | 60.348 | 11.62 | 2.34 | 6,496,002,048 | CPU |
| scale-1000-003 | No | 60.458 | 26.89 | 24.01 | 5,219,106,816 | CPU and RAM |
| scale-1000-004 | No | 60.564 | 11.83 | 1.63 | 5,256,216,576 | CPU |
| scale-1000-005 | No | 60.424 | 14.17 | 7.35 | 4,815,601,664 | CPU and RAM |
| scale-1000-006 | No | 60.420 | 11.13 | 1.53 | 5,224,656,896 | CPU |
| scale-1000-007 | No | 60.012 | 22.69 | 14.58 | 5,070,745,600 | CPU and RAM |
| scale-1000-008 | No | 60.279 | 27.16 | 7.16 | 4,350,181,376 | CPU and RAM |

Captured top-process snapshots repeatedly included Code, Microsoft Defender, System, idle MSBuild `dotnet` nodes, and occasionally VBCSCompiler/project-tooling. Attempt 003 captured an active compilation burst. No unrelated process was killed, no security control was disabled, and no failed admission was presented as a candidate result. All raw samples, declared conditions and decisions remain in their named evidence directories.
