# UCL cross-route native closure - R2

Date: 2026-08-28

Status: **integrated and build-complete; native component closure is partial; final product acceptance is blocked by signing and the OpenVINO official-worker boundary**

This is the C0 record for the R2 integration branch. It distinguishes source,
build, managed-test, native-component, packaged-test, and desktop-journey
evidence. It does not turn an environmental or security-policy failure into a
skip or a pass.

## Source identities and preservation

- Branch: `integration/ucl-cross-route-final-validation-r2`
- Frozen base: `f599c358181bd4da44087ab0c64d36d02d23316a`
- Preserved C0 candidate: `e7de3a7017af58f7e5e5f75820ef5c1bf6287d44`
- Preserved C0 candidate tree: `d49f05699c7b287778733ed7306fcb693b9bc0b9`
- C0 archive: `origin/archive/ucl-c0-e7de3a70`
- G1 archive: `origin/archive/ucl-g1-57ab54c5`
- Tested integrated application source: `7b659c09d9cda0bac3d648b05898210daf9c3ac9`
- Tested integrated application tree: `326be11951d0531346812c5918286ba276d5a71e`
- Latest O1 documentation-only integration commit before this handoff:
  `ca846687eb81b8bebbf9e557fc7c312f1fc67111`
- Its tree: `3b6c337491097c02346ab29a98bcd735edc106ec`

The build and native reruns were made from `7b659c09...`. The only subsequent
repository change before this handoff was O1's final documentation correction;
it does not change application, route, test, or packaging inputs. The final
remote documentation tip and tree are also published in the external
`C0-FINAL.json`, avoiding an impossible self-reference in this committed file.
No merge to `main` was performed.

## Worker integration and path audit

### O1 - OpenVINO

- Final remote tip: `origin/validation/ucl-openvino-native-r2@0ed7f3e3b00ccd272ade467aacda295a96980d1d`
- Candidate `b586cb3855514bdb058ad47b5d3d08475170942e` was already
  patch-equivalent in the preserved C0 candidate and was not duplicated.
- O1's R2 delta after that candidate changes only
  `docs/handoffs/2026-08-27-o1-ucl-openvino-native-closure-r2.md`.
- The initial R2 handoff was integrated as `8aa16a85`; its final diagnostic
  correction was integrated as `ca846687`, both with cherry-pick provenance.
- O1 changed no C0-owned UI, package, registration, navigation, onboarding,
  compatibility, or shared optimisation path in R2.

### G1 - GGUF

- Final remote tip: `origin/validation/ucl-gguf-native-r2@3a2a276a9a521e0a232ec5eb5bdc65ab3c5f16f0`
- `57ab54c5d0701f2fcaa8f4d23108e71aaadb12f4` is preserved by the G1
  archive and was already patch-equivalent in C0.
- `00a0aab4` was also already patch-equivalent and was not duplicated.
- The unique G1 commits were integrated, in order and with cherry-pick
  provenance, as `b1f59444`, `6da98794`, `3c1b1f1b`, `6e8d38c0`,
  `0a2bf667`, `2f9efb99`, and `7b659c09`.
- Their paths are limited to the GGUF native adapter, GGUF runtime-closure
  verifier, GGUF adapter/worker-process tests, and the G1 R2 handoff.
- No G1 commit changes shared UI, compatibility, packaging, registration, or
  navigation.

The integration retained every final route patch exactly once. `git cherry`
reported every source G1 commit as patch-equivalent to the integrated branch.

## Verified native inputs

All digests are lowercase SHA-256. Absolute stage and model locations remain in
the external C0 coordination record only.

| Input | Version / identity | Manifest or file SHA-256 | Result |
|---|---|---|---|
| OpenVINO converter | `granite.openvino.converter/1`; Python 3.13.15 | `b975313fccb90250cdf3ba58416b1ea019a93c7889c34a5b425df54df7056983` | closed inventory verified |
| OpenVINO official worker | `openvino.official/1` | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | worker manifest verified |
| OpenVINO TurboQuant worker | `openvino.turboquant/1` | `d4748d69ecacf13b1e1d6756f86df79d3a43e361bc10a0676ca270c9d08b9492` | worker manifest verified |
| TurboQuant runtime | source `8a17657b995fd3b4a52f8484acfcf2bb61214623` | `3b26a537ddfedad6d2f75fd1bf4578703a314f2544f4a81a6acfcade8d330c85` | nested runtime closure verified |
| Controlled OpenVINO fixture | `TinySyntheticV1` | `dc5ef5060a0e8242291d98701ab2b65286169c863a12e573dd1ad7921ac92648` | fixture verified |
| GGUF quantizer | llama.cpp `3f7c29d318e317b63f54c558bc69803963d7d88c`; Release x64 | `be44b38ca5ce66470a657f7d41d99b11c9233a5846f83a71c99b87672021765d` | package verified |
| Raw GGUF runtime stage | `llamasharp-0.27.0-cpu` | `86e537cd5f13d979d8b3856ed964cc041b8e948e70e08e30cdf0754aa0ca74a9` | 52-member detached closure verified |
| Final-build raw GGUF manifest | same runtime build | `6b315cf5bd3710753fb156b3de8e4d3f4cd73f4860f39e764ef642cba7663c03` | final build output verified |
| C0 signed GGUF functional copy | same 52 payload members; solution-owned payloads development-signed | `3f825bf436dac3fcdd93100ae94316bc1da4788116d89e003382e9c05d0f7cb8` | regenerated detached manifest and exact payload mirror verified |
| Hardware llama.cpp probe | `0.27.0-cpu-win-x64` | `cb5b1afd28916c4e0467c884836adcaba0bd76d466fc572854ec402e05c0a25d` | package verified |
| Hardware llmfit | `1.1.9` | `592852e2f19bde606f848a972edefa7060ce78525985b2ac038f2eb1567f379d` | exact three-file package and AMD64 identity verified; executable unsigned |
| Approved Granite model | `granite-4.1-3b-Q4_K_M.gguf`, 2,099,501,664 bytes | `662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29` | exact controlled model verified |

## Final application and test-package builds

Visual Studio Community x64 MSBuild built the application itself in Release,
with package generation enabled, package signing disabled, and every production
native packaging gate enabled. No `GenerateAppxPackageOnBuild=false` or native
packaging bypass was used for final application evidence. A short output root
was used only to stay within the Windows path-length boundary.

| Artifact | Length | SHA-256 | Signature |
|---|---:|---|---|
| Final application MSIX | 551,054,838 | `5ce704fcb7cb59e9c35477b05055bdd7893c0b47777ff6832eb5fd41de2e5396` | `NotSigned` |
| Final application recipe | 6,683,414 | `608155d8d09447944e182159159a86273eda6457f7700e1d9c2e3ca41f3fca65` | n/a |
| Fresh Release x64 UnitTests MSIX | 162,955,882 | `357b2e6b7054fb8d82d29079bd3f3e7f81f6bb60bead79c9fbcaf70998098f8f` | `NotSigned` build input |
| Fresh Release x64 UnitTests recipe | 172,390 | `d586d15e9a95dac6b24b59cf4877e4c540f74df30e0d05f8b5781793b75881a7` | n/a |

The final application build log is 7,887,550 bytes, SHA-256
`51553a0d7f58ef42bb5e3a0e58b39b42c24646b21494236be81b63a67b8e9687`,
with zero reported errors and zero reported warnings. The UnitTests package log
is 26,258 bytes, SHA-256
`e5ec3706a4a84d73b816371689bf8c13d47cd4d01cdff08374f2cd6d31371441`,
with zero errors and 14 existing nullable warnings in ModelImport accessibility
tests.

The essential build invocation was:

```powershell
& $env:VS_X64_MSBUILD $env:APP_PROJECT /restore /m:1 `
  /p:Configuration=Release /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 `
  /p:GenerateAppxPackageOnBuild=true `
  /p:AppxPackageSigningEnabled=false `
  /p:BaseOutputPath=$env:SHORT_OUTPUT_ROOT `
  /p:OpenVinoConverterStageDirectory=$env:OPENVINO_CONVERTER_STAGE `
  /p:OpenVinoConverterManifestSha256=b975313fccb90250cdf3ba58416b1ea019a93c7889c34a5b425df54df7056983 `
  /p:OpenVinoOfficialWorkerStageDirectory=$env:OPENVINO_OFFICIAL_STAGE `
  /p:OpenVinoOfficialWorkerManifestSha256=db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3 `
  /p:OpenVinoTurboQuantWorkerStageDirectory=$env:OPENVINO_TURBOQUANT_STAGE `
  /p:OpenVinoTurboQuantWorkerManifestSha256=d4748d69ecacf13b1e1d6756f86df79d3a43e361bc10a0676ca270c9d08b9492 `
  /p:GgufQuantizerStageDirectory=$env:GGUF_QUANTIZER_STAGE `
  /p:GgufQuantizerManifestSha256=be44b38ca5ce66470a657f7d41d99b11c9233a5846f83a71c99b87672021765d
```

## Managed test evidence

Fresh final-branch reruns used SDK `10.0.301`, Release, x64, MTP native TRX
arguments, explicit minimum discovery counts, and the admitted OpenVINO stages.

| Suite | Total | Passed | Failed | Not executed |
|---|---:|---:|---:|---:|
| GGUF contracts | 12 | 12 | 0 | 0 |
| GGUF transport | 15 | 15 | 0 | 0 |
| GGUF capabilities | 13 | 13 | 0 | 0 |
| GGUF worker | 21 | 21 | 0 | 0 |
| GGUF worker client | 9 | 9 | 0 | 0 |
| OpenVINO contracts | 205 | 205 | 0 | 0 |
| OpenVINO unit, stage-backed | 409 | 409 | 0 | 0 |
| OpenVINO worker client | 13 | 13 | 0 | 0 |
| Model/hardware compatibility | 1,050 | 1,050 | 0 | 0 |

The fresh unsigned GGUF native-adapter rebuild was blocked before discovery by
Custom Policy 1 at `GraniteEdgeAI.GgufRuntime.NativeAdapter.dll` with
`0x800711C7`. This is not a test failure and is not included as green. Before
that rebuild, the integrated managed adapter suite recorded 40 passed and four
controlled-model skips; C0 then exercised the same route through a signed
external functional closure as recorded below.

Representative exact MTP form:

```powershell
dotnet test --project $env:TEST_PROJECT --configuration Release `
  -p:Platform=x64 --no-restore `
  --minimum-expected-tests $env:EXPECTED_TOTAL `
  --results-directory $env:EXTERNAL_RESULTS `
  --report-trx --report-trx-filename $env:TRX_NAME --no-ansi
```

## Native component results

### GGUF

The signed external 52-member closure used only the repository's established
development code-signing identity for solution-owned executables and DLLs.
Vendor/runtime members were not substituted. The detached manifest was
regenerated after signing and verified against a separate exact 52-member
payload mirror. The runnable worker-client layout contains those same members
plus an exact copy of that manifest at its required package-root location; the
extra client copy is not misreported as part of the detached closure.

The focused real-model worker-process test discovered one test and passed it in
26.7 seconds. It verified local runtime loading, two ordinary streamed turns,
bounded stop, explicit close, forced-length completion, and a non-empty
post-length continuation. This closes G1's causal continuation defect at the
native worker boundary. Exact worker, adapter, test-host, and `dotnet test`
process cleanup was zero afterward.

It does not prove a registered desktop journey, GGUF requantization, export,
benchmark, power, or production-signing acceptance.

### OpenVINO

O1's full admitted native worker-process TRX recorded 69 total, 59 passed,
8 failed, and 2 not executed. Focused A/B closure and focused persistent
conversion tests each discovered one test and failed at the same downstream
official-worker boundary. No test was relabelled or skipped.

| Boundary | Result |
|---|---|
| Converter isolation and protocol | 5/5 passed |
| Protocol containment | 34/34 passed |
| TurboQuant worker component | 8/8 passed; two CPU TBQ4/TBQ4 SDPA turns, two runtime dispatches, 22 encoded records, bounded cancellation and cleanup |
| Official worker handshake | `openvino.official/1` hello passed |
| Official CPU session | failed closed with `runtime_integrity_failed` |
| Root cause | the staged GPU plugin loaded Intel-signed `intelocl64.dll` from an ambient Program Files location outside the sealed-stage/System32/WinSxS allow-roots |
| Conversion / persistent optimisation | failed downstream official smoke; no published output |
| Physical GPU | not executed because no approved GPU device input was supplied |
| Cleanup | `openvino_cleanup_valid`; no owned process, pipe, lock, partial, or staging residue; original manifest hashes unchanged |

The ambient Intel DLL was validly Intel-signed, version `2026.6.0.0.0617`,
80,290,432 bytes, SHA-256
`e9f7e229ef83ab27b1902629179c5c121e7ba3c699511c13605260123fd51bcf`.
No contemporaneous Code Integrity event matched this OpenVINO failure. The
correct action is a worker/device-activation correction that avoids eagerly
admitting the GPU plugin during CPU activation. The integrity gate must not be
broadened to accept ambient Program Files modules.

## Packaged acceptance and signing diagnosis

The active Application Control policy is Custom 1, policy ID
`{0283ac0f-fff1-49ae-ada1-8a933130cad6}`. Its active policy file SHA-256 is
`2668895a5b233a80432d00d67251d7b7f52686a3fb13780f4b242c5a1f937a01`.
Events 3033/3077 and .NET `FileLoadException 0x800711C7` prove that unsigned
solution-built DLLs are rejected. A signed application host is accepted and an
unsigned dependency is then rejected, so this is not test-host-only.

The repository's approved signed Hardware Inspection runner used its existing
`CN=GraniteEdgeAI` development certificate, signed the bounded solution-owned
payload, regenerated the llama.cpp probe manifest, repacked and signed the test
MSIX, normally installed it, and obtained non-zero packaged discovery:

| Signed packaged category | Total | Passed | Failed |
|---|---:|---:|---:|
| `HardwareInspectionProcessAcceptance` | 88 | 87 | 1 |

The sole failure was
`SignedPackageCapturesPinnedCpuCapabilitiesOnlyInChildProcess`. Code Integrity
event 3077 records Custom 1 rejecting the signed packaged llama.cpp probe child
executable. The runner removed the package and result file. The acceptance log
is 2,656 bytes with SHA-256
`40ef6e7659ee165c47771b652618416d7d3383f2df22d81d81e032e716e2bc27`.

The nine broader loose-recipe filters - ModelImport, ModelInspection,
HardwareInspection, ModelHardwareCompatibility, ModelOptimization, Onboarding,
GgufRuntime, OpenVino, and CrossRoute - cannot obtain reliable discovery under
the active policy because their freshly rebuilt solution payloads are unsigned.
They are not claimed as run or passed. The only policy-permitted repository
signed path is the closed Hardware Inspection category above.

The production manifest publisher is `CN=Arian`. No matching private
code-signing certificate is available. The trusted private development identity
is `CN=GraniteEdgeAI`, is intentionally scoped to the repository's UnitTests
acceptance flow, and does not match the production publisher. Therefore the
final application MSIX remains unsigned and cannot be registered as a production
candidate. No root certificate, Application Control policy, publisher, or gate
was changed.

## Common product journey and screenshots

The same final production package cannot be registered and launched under the
available approved identity. Consequently C0 could not truthfully execute the
requested picker/drag-drop through inspection, production Hardware Inspection,
compatibility, preference selection, seven-stage optimisation, reinspection,
Chat/export journey for either route. Cancellation/retry, all six preference
bands, Enter/Shift+Enter, and path privacy remain managed/static contract
evidence rather than R2 desktop-journey evidence.

No R2 product screenshots were captured. A same-size parity screenshot pair
would imply a runnable common package and is deliberately not fabricated.

## Duplication, privacy, and cleanup

- Tracked-source inventory contains one production `MainWindow`, ModelImport
  page, ModelInspection page, HardwareInspection page, Compatibility page,
  Optimization page, and Chat page. Debug fixture galleries are separate test
  surfaces, not duplicate production registrations.
- Shared compatibility and optimisation coordination remain in their existing
  shared implementations; no route fork was introduced by O1/G1 integration.
- Picker and Explorer drop still converge on `SubmitInputAsync`; the exact seam
  names `modelInspectionRunId`, `modelInspectionHandoffId`, `modelSha256`,
  `modelLengthBytes`, and `productHardwareRunId` remain present.
- No user name, host name, absolute model/stage/output path, raw hardware name,
  prompt text, generated text, or private certificate material is committed.
- The final test registration was removed. No task-owned application, official
  worker, TurboQuant worker, converter, GGUF worker, native adapter, test host,
  or quantizer process remains.
- Native ownership is returned to `IDLE` only after the final external record is
  published.

## Precise remaining blockers and nonclaims

1. Production package registration and both desktop journeys require an
   approved signer whose subject matches `CN=Arian`, or an already-approved
   production deployment route.
2. The OpenVINO official CPU route requires a worker/device-activation fix that
   prevents the GPU plugin's ambient Intel OCL dependency from entering the
   sealed CPU session.
3. A trusted-signing route accepted by Custom 1 is required for the packaged
   llama.cpp child probe and the remaining packaged filters.
4. Independent-build provenance for official worker B was not established; a
   distinct verified root alone is not that evidence.

No claim is made for all-green native acceptance, production distribution,
common desktop journey, visual parity, screenshots, OpenVINO conversion or
optimisation, physical-GPU execution, GGUF quantization/export, benchmark,
energy, or performance improvement. The integrated source, production build,
managed suites, signed GGUF native continuation, and bounded partial packaged
acceptance are the completed R2 evidence.
