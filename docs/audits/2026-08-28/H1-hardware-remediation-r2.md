# H1 Hardware remediation R2

## Disposition

The H1-owned implementation is complete and verified at commit
`1273808c31d4025adc0f494349ecdac188c250a9` with tree
`03eead0ca570535831e064efc5771a5e35993916`. It is based on the authoritative
C0 integration commit `a5ef3558334e50587889140dafba194853938765`
(tree `90c34ab009b744d7b00866fb93e8dbc86363f1b2`), whose frozen ancestor is
`4748fe04f19afdf6b27c4c12502b84db325e7294`
(tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`).

The lane is reported as mixed only because two package-wide checks stop in
other owners' files: the shared Debug x64 application composition includes a
Hardware Inspection gallery XBF, and the Model Inspection package-boundary
test first encounters an unresolved GGUF runtime expression. H1 did not edit
shared composition/navigation or GGUF runtime packaging to conceal either
finding. The H1 packaging target itself has no unresolved item expression and
contributes only its verified closed inventory.

## Implemented remediation

- Introduced distinct public value types for total physical memory, currently
  available memory, proportional safety reserve, and executable model budget.
  Hardware and fresh-resource factories now require the correct role type, and
  the evaluation result retains all four roles without allowing raw-byte
  substitution at the compatibility boundary.
- Replaced decimal reserve arithmetic with a deterministic integer-only
  calculator. It uses a widened product, rounds the proportional term upward,
  clamps the reserve to the observed available pool, and proves the invariant
  `available = reserve + executable` even at `ulong.MaxValue`.
- Added a stable snapshot identity containing its UUID and a lowercase SHA-256
  over a tagged, length-prefixed canonical form. It includes bounded hardware
  facts and validated evidence tokens while excluding unrestricted provider
  names, device labels, build strings, operating-system text, usernames,
  hostnames, and paths. GGUF and OpenVINO compatibility projections preserve
  this exact digest, and actionable handoffs require snapshot/run UUID equality.
- Repaired llama.cpp probe packaging so the manifest verifier emits the exact
  verified member paths consumed by MSBuild. The former unresolved
  `@(_HardwareInspectionLlamaCppProbePublishedFiles)` package item is absent;
  no post-verification wildcard can add content.
- Preserved the existing bounded process execution, cancellation, timeout,
  job/child cleanup, manifest verification, AMD64 validation, trusted-signing
  boundary, and fail-closed hash behavior. No asset or tool was downloaded and
  no security control, certificate, or hash policy was changed.

## RED-GREEN evidence

The behavioral changes were driven by failing checks before implementation:

- the low-memory calculation exposed an unbounded fixed reserve;
- a default total-memory value crossed the public compatibility factory;
- snapshot identity tests initially failed to compile because no stable
  identity existed;
- the packaging contract found the unresolved H1 item expression and gallery
  exposure.

The first three are green in the committed implementation. The H1 target's
packaging defect is green; the remaining gallery is an inherited shared Debug
x64 composition item and is listed below as a C0 blocker.

## Verification

| Gate | Result |
| --- | ---: |
| Hardware Inspection Foundation | 202/202 passed |
| llama.cpp probe/tool/fake-tool | 22/22 passed |
| Model/hardware compatibility, including typed memory and budget boundaries | 1060/1060 passed |
| Hardware packaging and trust PowerShell contracts | 10/10 passed |
| Debug x64 application build with H1 production probe packaging | succeeded, 0 warnings, 0 errors |
| Production probe manifest | verified; 10 controlled files, 0 duplicate names, 0 forbidden H1 package files |
| Debug x64 static evaluation | 203 items, 0 unresolved expressions; 1 shared H1 gallery item and 4 inherited audit/reference matches |
| Release x64, Debug x86, Debug AnyCPU static evaluation | 148 items each, 0 unresolved expressions, 0 H1 galleries; 2 inherited audit/reference matches each |
| Model Inspection package boundary | 0/1; stopped on GGUF runtime `@(_GgufRuntimePublishedFiles)` outside H1 ownership |
| Subject and worktree `git diff --check` | passed |
| Changed-production privacy scan | 0 private-pattern hits |

The committed test total is 1,295 discovered/executed: 1,294 passed and one
failed outside H1 ownership. The two deliberate private-looking strings found
in changed test code are synthetic privacy fixtures proving they do not affect
the snapshot digest; no production change contains such a value.

The Windows App SDK unit-test executable compiled but its packaged GUI launcher
detached without producing a TRX on this host. No test result is claimed for
that launch. The same changed contracts compile in the successful Debug x64
application/unit build, while the headless Foundation, probe, and compatibility
suites above executed normally.

## Native phase

H1 acquired the single native slot atomically and ran only the already-built,
manifest-verified packaged probe. An initial bookkeeping attempt observed a
transient non-zero pre-existing process count and was discarded. The
authoritative uncontended rerun began with zero Granite Edge AI processes:

| Command | Exit | stdout bytes | stderr bytes |
| --- | ---: | ---: | ---: |
| `identity --format json-v1` | 0 | 366 | 0 |
| `capabilities --format json-v1` | 0 | 148 | 0 |

Both commands completed within the 30-second bound. The post-run process count
was zero, cleanup was verified, and the shared lock was released. Raw provider
JSON remains outside the repository and is represented only by a SHA-256 in
the evidence manifest.

## Blockers and non-claims

1. C0-owned Debug x64 composition still declares
   `Features/HardwareInspection/DebugFixtures/HardwareInspectionFixtureGalleryPage.xaml`,
   producing its XBF in the loose application output. Removing that shared
   page/composition item is intentionally not an H1 change.
2. The cross-lane Model Inspection boundary test cannot reach later assertions
   because `GgufRuntime.WorkerPackaging.targets` contains unresolved
   `@(_GgufRuntimePublishedFiles)`. That target is outside H1 ownership.

No clean package-wide claim is made for those inherited items. H1 does claim
that its probe package is manifest-exact, flat, duplicate-free, path-private,
and free of audit/reference/evidence/debug content. No native model load,
OpenVINO behavior change, hosted CI result, visual validation, Application
Control bypass, or shared onboarding/composition change is claimed.

The companion manifest is
`docs/audits/2026-08-28/evidence/H1-hardware-evidence-v1.json`; its stable output
kinds are `hardwareSnapshot`, `availableMemory`, and `safetyBudget`.
