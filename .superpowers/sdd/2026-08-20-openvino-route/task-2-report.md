# Task 2 — Closed OpenVINO Contracts

## Scope

Created the independently buildable `GraniteEdgeAI.OpenVino.Contracts` project and attached the existing Task 1 contract-test shell to it. The closed source-generated JSON boundary covers four commands, nine events, exact official/TurboQuant identities, bounded ordered sessions, the complete support-code taxonomy, and the exact path-minimized `ModelInspectionHandoffV2` schema.

`ModelInspectionHandoffV2` serializes exactly these six fields in canonical UTF-8 order: `schemaVersion`, `modelInspectionHandoffId`, `modelInspectionRunId`, `outcome`, `modelSha256`, and `modelLengthBytes`. Its model hash and length are explicitly documented and validated as the identity of `openvino_model.bin`; no path, hardware, diagnostic, or extra metadata field exists on the type.

## RED evidence

Initial focused command (before the production contract project/types existed):

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests|FullyQualifiedName~ProtocolSequenceTests|FullyQualifiedName~SupportCodeTests"
```

Observed output: MSB9008 reported that `shared\GraniteEdgeAI.OpenVino.Contracts\GraniteEdgeAI.OpenVino.Contracts.csproj` did not exist, followed by `CS0246` for the missing `StartSessionCommand` type. Exit code: `1`.

Two targeted regression RED runs then demonstrated production breaks before their minimal fixes:

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests|FullyQualifiedName~ProtocolSequenceTests|FullyQualifiedName~SupportCodeTests"
```

Observed output: `HandoffParsingRejectsNoncanonicalPropertyOrderInsteadOfTreatingEquivalentJsonAsTheCanonicalHandoff` and `SequenceRejectsOperationTextSplitAcrossTurnsInsteadOfResettingTheFourMiBOperationBudget` both failed because no exception was thrown. Exit code: `2`.

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests"
```

Observed output: `PromptValidationRejectsInvalidUtf16InsteadOfLeakingAnEncoderDiagnostic` failed because `EncoderFallbackException` escaped instead of the typed `OpenVinoProtocolException`. Exit code: `2`.

## GREEN evidence

Focused required command:

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests|FullyQualifiedName~ProtocolSequenceTests|FullyQualifiedName~SupportCodeTests"
```

Observed output: `total: 21`, `failed: 0`, `succeeded: 21`, `skipped: 0`. Exit code: `0`.

Full required project command:

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj
```

Observed output: `total: 27`, `failed: 0`, `succeeded: 27`, `skipped: 0`. Exit code: `0`.

`git diff --check` also completed without whitespace errors (Git emitted only the repository's LF-to-CRLF informational warning for the existing test project file).

## Self-review

- Closed type switches reject unregistered commands/events and support codes.
- Strict UTF-8 parsing rejects malformed text; duplicate/unknown/case-mismatched members, comments, trailing commas, deep JSON, and overlong lines become typed failures.
- Session validation rejects stale UUIDs, impossible ordering, noncontiguous token sequences, terminal reuse, 33rd turns, and operation text exceeding 4 MiB across turns.
- Handoff validation rejects noncanonical JSON, malformed/uppercase/non-v4 identities, non-eligible outcomes, invalid hashes/lengths, paths, and fields outside the exact six-field allowlist.
- No solution/app/shared-registry/UI/worker/client/Task 1 lock or verifier file was modified.

## Known external condition

The pre-existing ModelInspection cleanup-inventory failure was not touched, per task instruction. It is outside this independently passing new-contract project.

## Fix round 1 — command/event sequencing and independent wire literals

### Coverage added

- `ProtocolSequenceTests.cs` now drives `OpenVinoConversationValidator` with both commands and events. It proves hello-first, exactly one `StartSessionCommand` or `StartInspectionCommand`, session-start binding, prompt-to-generation binding, one active turn, stale session/turn rejection, stop/cancel legality, terminal immutability, 32 turns, cumulative 4 MiB operation text, and the separate inspection start/terminal path.
- `ProtocolJsonTests.cs` now validates every command and event against independently hand-written UTF-8 JSON literals, including the discriminator and every serialized field in canonical order.
- `SupportCodeTests.cs` now owns a hand-written 24-row enum-to-wire-value table; it does not call production conversion code to compute expectations.

### RED evidence

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolSequenceTests"
```

Observed output before the unified validator existed: `CS0246` for missing `OpenVinoConversationValidator`. Exit code: `1`.

The independent support-code table was also mutation-proved: with only the `RuntimeTimedOut` production mapping temporarily changed to `runtime_timeout`, the following command failed exactly on the hand-written expected `runtime_timed_out` literal, then the production mapping was restored.

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~SupportCodeTests"
```

Observed output: `FailureSerializationEmitsEveryFixedSupportCodeInsteadOfAnArbitraryDiagnosticString` failed; expected `"supportCode":"runtime_timed_out"`, actual `"supportCode":"runtime_timeout"`. Exit code: `2`.

### GREEN evidence

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests|FullyQualifiedName~ProtocolSequenceTests|FullyQualifiedName~SupportCodeTests"
```

Observed output: `total: 23`, `failed: 0`, `succeeded: 23`, `skipped: 0`. Exit code: `0`.

```powershell
dotnet test tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj
```

Observed output: `total: 29`, `failed: 0`, `succeeded: 29`, `skipped: 0`. Exit code: `0`.

## Task 7 approved atomic v1 contract baseline

Before Task 7 native implementation, the root explicitly approved a narrow
Task 2/4 correction because the earlier unpublished feature-branch wire could
not represent an arbitrary protected package selection, native parse evidence,
or a graceful session close. The producer and every consumer changed atomically
on `feature/openvino-route`; no released or external compatibility consumer
exists. For that reason the authoritative identifier remains
`openvino.official/1` rather than manufacturing a version migration for a
contract that has not shipped.

The authoritative v1 baseline now includes:

- `startInspection`: inspection UUID plus protected-stdin absolute package path,
  package-manifest SHA-256, model SHA-256, and model length.
- `startSession`: session/inspection UUIDs, the same package/model identity,
  requested device, and bounded generation limits.
- Atomic `hello`: protocol ID and exact Runtime/GenAI/Tokenizers build identities
  plus the worker-manifest digest.
- Ordered inspection started/progress/terminal events, with successful native
  parse flags and evidence bound to the Hello build identity.
- Session start evidence, explicit graceful `closeSession`, and authoritative
  prompt/generated counts with a closed completed/stopped disposition.

Task 7 review tests additionally prove the hand-written atomic Hello literal,
reject a Hello build-evidence mismatch before startup acceptance, and reject a
terminal count of prompt 63 plus generated 2 under a 64-token context without
overflow-prone addition. Final Release contract verification is 60/60.
