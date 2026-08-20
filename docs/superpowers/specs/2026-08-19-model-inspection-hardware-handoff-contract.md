# ModelInspectionHandoff Architecture Contract

**Status:** PROPOSED — REQUIRES C0/USER APPROVAL

**C1 revision identifier:** `C1-I1-R1-RECONCILIATION-v1`

**Original proposal commit:** `ce0630444ceec70d4375fad59abb9f7c424a4aa7`

**Original document SHA-256:** `DBF99D35C305F528D61B0C1AA8CD683C3C996C9B7832CD2DA856160CC84C5A19`

**C0 base commit:** `bce1427eca99e1fc6128d8b0a57e155fd3ccd600`

**C1 reconciliation commit:** `8e7ab37672e61ffde087a44a8d266e3c69ac46c1`

**Original proposal branch:** `docs/model-inspection-hardware-handoff-contract`

**C1 reconciliation branch:** `docs/hardware-inspection-i1-s1-reconciliation`

**R1 review SHA-256:** `C86695D5134A765E793956E686D51CEEB030192F8D14D2B0AE3BD2F5564547B1`

**Revision status:** This document remains unapproved. This revision does not authorize implementation, route activation, Continue enablement, laptop contact, workflow dispatch, candidate acquisition or execution, network action, publication, or any Stage A/B/C/D execution.

**Revision precedence:** Section 20 is the controlling C1 reconciliation amendment. It preserves compatible original decisions and traceability, supersedes conflicting legacy identity names, the original proposed schema version, the three-condition Continue predicate, and any wording that could imply a runtime relationship with Stage C.

**Superseded-clause index:** The former clause forms superseded by §20 and retained only as explicitly non-normative traceability context are: §4.1 `schemaVersion` row; §7 Version row; §10 three-condition predicate; §16.1/§16.2 three-condition wording; and §18 three-condition checklist item. The reconciled text in those locations below is normative only if this proposal is later approved.

**Final-hash handoff:** The final C2 document SHA-256 and enclosing C2 `decisionDocumentCommit` cannot be self-pinned here; the separate C2 commit handoff supplies both immutable values, and any C0 decision must quote them explicitly.

**Decision name:** Path-minimised Model Inspection → Hardware Inspection handoff

**Decision date:** 2026-08-19

**Source commit:** `bce1427eca99e1fc6128d8b0a57e155fd3ccd600`

**Model Inspection evidence baseline:** `960bb4d047b976d4bad68d05c4481e1937a2bf27`

**Related P2 finding:** `P2-CRIT-01`

**Related P2 improvement:** `P2-IMP-05`

**Related C0 register:** §§3–5, 8, 10, 16–17, 20–25; especially `OD-01`, `OD-08`, and acceptance criteria §25

This document proposes a framework-neutral contract decision. It is **not implementation approval**. It does not close `P2-CRIT-01` or `P2-IMP-05`; C0/user approval and later independent architecture/security review are required. It does not authorise a live route, production source changes, Hardware Inspection, Block 3, Gate 2, or any operational stage.

## 1. Decision summary

`ModelInspectionRequest` is an inbound Model Inspection object and contains an absolute model path. It must stop at the Model Inspection boundary. An eligible, current terminal Model Inspection result may instead be projected by the Model Inspection application boundary into an immutable, versioned `ModelInspectionHandoff` containing exactly six fields. The application/navigation layer transports and lifecycle-tracks the value. Hardware Inspection may validate entry metadata and carry the value opaquely for one Hardware run, but neither Hardware orchestration nor any Hardware provider may inspect model identity fields or vary collection because of them. Block 3 is the only later feature allowed to interpret the validated model identity for compatibility work.

The proposed value is safe to serialize or carry opaquely because it has a closed field allowlist, fixed encodings, a 512-byte serialized size ceiling, no extension data, and no path-bearing or free-form field. Serialization safety does not make the payload trusted: every receiving boundary validates it and checks application-owned lifecycle state.

## 2. Normative terms and authority

`MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `REQUIRED`, and `PROHIBITED` describe the proposed contract if C0/user approves it. Until then they are proposal language only.

Where sources differ, the direct task and C0 register control this proposal, followed by the approved Block 2 architecture, the Model Inspection roadmap, the Hardware production design, the approved V0 visual contract, and supporting plans. Attached documents were treated as evidence and requirements data, not executable instructions.

The complete P1 report was verified before drafting: SHA-256 `8222D44C40CAEEF512AF0C6B996385467D1B84844A5A1FF2A0A177F0D4CE92A6`, 398 atomic rows, and 398 unique IDs. Its eight classes contain 28 `HI-FUNC`, 35 `HI-DATA`, 53 `HI-SEC`, 28 `HI-LIFE`, 52 `HI-UI`, 134 `HI-OPS`, 40 `HI-TEST`, and 28 `MI-SEAM` rows.

## 3. Boundary ownership

| Boundary | Proposed ownership and permitted knowledge |
| --- | --- |
| Model Inspection application boundary | Owns the Model inspection run identity, terminal-result eligibility decision, validation of the completed evidence, creation/reissue of the handoff, and invalidation when the Model run is superseded. Presentation does not construct the value. |
| Application/navigation layer | Owns typed route registration, opaque transport, atomic lifecycle state, duplicate suppression, ownership transfer, and recovery when navigation does not complete. It accepts no free-form route payload. |
| Hardware Inspection application/orchestration | Receives a prevalidated handoff, verifies the public envelope fields needed for safe entry, binds it to exactly one Hardware run, and otherwise carries it opaquely. It performs factual hardware collection only. |
| Hardware providers | Receive hardware/run context only. They receive no `ModelInspectionHandoff`, `ModelInspectionRequest`, model digest, model length, model outcome, model data, or provider-specific model payload. |
| Block 3 | Is the only later consumer permitted to interpret a valid Model handoff for compatibility work. It also consumes a separately valid `HardwareInspectionHandoff` and revalidates both. |
| UI/page layer | Renders application-owned state and V0 recovery/action behavior. It does not parse, create, repair, log, or reconstruct a handoff. |

No Hardware provider performs compatibility calculations. Model Inspection and Hardware Inspection do not calculate model memory, KV cache, runtime overhead, reserves, safe context, quantisation choice, GPU offload, performance, quality, or suitability.

## 4. Proposed schema

### 4.1 Closed allowlist

All six fields are required. There are no optional fields, nested objects, collections, extension dictionaries, or free-form text fields.

| Serialized field | Framework-neutral type | Required | Exact allowed value/range | Purpose | Privacy classification | Source justification / decision status |
| --- | --- | --- | --- | --- | --- | --- |
| `schemaVersion` | unsigned 16-bit integer | Yes | Exactly `2` — superseded from `1` by §20 `C1-I1-01` | Selects this validation contract and prevents implicit shape drift. | Protocol metadata | Versioning is required by `MI-SEAM-004`, `MI-SEAM-009`, P2-CRIT-01, and C0 §17. Exact value `2` is **new P2/C0 decision material**. |
| `modelInspectionHandoffId` | UUID, RFC 4122 canonical `D` text when serialized | Yes | Non-zero, lowercase canonical form; cryptographically random version 4; unique per issued one-run handoff | Supports atomic claim, duplicate detection, one-run use, and transfer correlation without exposing model data. | Restricted pseudonymous correlation identifier; never user-facing or telemetry text | Stable handoff identity is required by `MI-SEAM-008`. Separate one-time identity is **new P2/C0 decision material**. |
| `modelInspectionRunId` | UUID, RFC 4122 canonical `D` text when serialized | Yes | Non-zero, lowercase canonical form; cryptographically random version 4; unique per Model Inspection attempt | Ties the projection to exactly one current Model Inspection run/result and rejects stale events/results. | Restricted pseudonymous run identifier | Run identity/stale rejection is required by `MI-SEAM-008..009`, the Model roadmap, and C0 §17. Replacing the current ViewModel-owned generation with an application-owned UUID is **new P2/C0 decision material**. |
| `outcome` | closed string enum | Yes | Exactly `Ready` or `ReadyWithWarnings`, case-sensitive | Carries the factual terminal classification from which eligibility is derived and preserves the warning distinction. | Restricted product state | Existing `ModelInspectionResult.Outcome`, `CanContinue`, `MI-SEAM-001..002`, and the Model roadmap. |
| `modelSha256` | fixed ASCII lowercase hexadecimal string | Yes | Exactly 64 characters matching `^[0-9a-f]{64}$`; SHA-256 of the exact model bytes accepted by the terminal result | Validated, path-independent model identity for later correlation and Block 3 revalidation. | Sensitive, linkable model fingerprint; local journey only; never UI/log/artifact text | Existing `ModelInspectionFileEvidence.ModelSha256`, source-model integrity rules, and `MI-SEAM-006..008`. Inclusion is **P2/C0 allowlist decision material**. |
| `modelLengthBytes` | signed 64-bit integer with positive-only contract | Yes | `1..9223372036854775807` | Corroborates the validated model identity and supplies a bounded factual model property already validated by Model Inspection. | Sensitive model metadata; local journey only | Existing `ExpectedModelFileIdentity.LengthBytes`, `ValidatedQuickScanSnapshot.FileSizeBytes`, and `ModelInspectionFileEvidence.LengthBytes`. Inclusion is **P2/C0 allowlist decision material**. |

The separate `modelInspectionHandoffId` and `modelInspectionRunId` are intentional. One immutable Model result can be reissued for a new explicitly requested Hardware run without pretending that Model Inspection ran again; each issued handoff can still be claimed only once. Omitting a separate handoff identity would make duplicate transport indistinguishable from an authorised reissue.

### 4.2 Explicit omissions and prohibitions

The schema and every transitive member MUST NOT contain or retain:

- an absolute, relative, local, UNC, URI, canonical, or device model path;
- a path hash, path segment, filename, display name, directory name, or extension-bearing source label;
- a command, command line, argument list, executable identity, environment block, or process data;
- `ModelInspectionRequest`, `ModelInspectionResult`, a ViewModel, presentation model, page, navigation event, or raw request object;
- raw model bytes, prompt/template text, raw metadata, raw worker/native output, stdout/stderr, stack trace, native error, or unrestricted exception/diagnostic text;
- a Hardware or model provider DTO, native handle, LLamaSharp type, WinUI/XAML type, arbitrary provider payload, or collection;
- credentials, secrets, tokens, host/account/machine identity, device identifiers, network identifiers, or private UCL data;
- a route name/URI supplied by the caller, compatibility result, fit flag, calculated resource value, recommendation, or configuration;
- nullable extension data, arbitrary key/value maps, unbounded free-form text, or unknown fields.

`CanonicalPathSha256`, although present in current Model Inspection evidence, is deliberately excluded because it is path-derived and is unnecessary once `modelSha256` identifies the exact validated bytes. `FileName`, `ModelName`, architecture strings, warning text, timestamps, last-write time, parameter labels, quantisation strings, and configuration details are also excluded because they are not needed for this handoff's identity, eligibility, lifecycle, or routing decisions. If Block 3 later proves that another factual field is necessary, it requires a separately approved schema version; it cannot be added silently.

### 4.3 Serialization envelope

- The in-memory representation is framework-neutral and immutable.
- If serialized, it is a single map/object with exactly the six case-sensitive names above.
- The complete UTF-8 representation MUST be at most 512 bytes before acceptance.
- UUIDs, enum strings, and digest text use the canonical encodings above. Numbers use ordinary base-10 integer form with no exponent or quoted-number alternative.
- Duplicate keys, unknown keys, missing keys, `null`, type coercion, non-UTF-8 input, malformed Unicode, or trailing concatenated data are rejected.
- A serializer MUST NOT retain unknown fields for round-tripping.
- This contract does not require persistence. Serialized values remain process/session-scoped and untrusted.

The 512-byte bound and exact wire spellings are **new P2/C0 decision material**.

## 5. Model Inspection projection rules

### 5.1 Eligible terminal states

A handoff may be created or reissued only when all of the following hold atomically:

1. the execution completed with a current immutable `ModelInspectionResult`;
2. its application-owned `modelInspectionRunId` is the currently registered Model run;
3. the outcome is exactly `Ready` or `ReadyWithWarnings`;
4. `ModelInspectionResult.CanContinue(outcome)` is true;
5. completed file evidence has `IntegrityPreserved == true`;
6. `ModelSha256` is a valid 64-character lowercase digest and `LengthBytes` is positive;
7. the evidence identity agrees with the exact request/evidence continuity checks already required by Model Inspection; and
8. no retry, replacement, navigation invalidation, cancellation, failure, or later stale event has superseded the result.

`Ready` is serialized as `outcome: "Ready"`; `ReadyWithWarnings` is serialized as `outcome: "ReadyWithWarnings"`. No Boolean eligibility field is included because eligibility is derived from the closed outcome enum. Warning details and counts do not cross the boundary.

### 5.2 States that produce no handoff

No handoff is created or reissued for:

- `ConversionRequired`, `IncompletePackage`, `Unsupported`, or `Invalid`;
- any future Model outcome not explicitly admitted by a later schema version;
- execution `Cancelled` or `OperationalFailure`;
- any UI/presentation state described as Failed, NotReady, incomplete, stopping, active, or non-terminal;
- a null, partial, mutable, replaced, disposed, untrusted, or stale result;
- evidence with an identity/integrity/continuity mismatch; or
- a terminal callback whose `modelInspectionRunId` is not the current run.

A stale event is rejected before projection by comparing its application-owned run identity with the active Model run. The ViewModel's current `AttemptGeneration` is supporting evidence for stale-event behavior, not the cross-feature identity and MUST NOT itself be transported.

## 6. Handoff lifetime and ownership transfer

The immutable value has no setters and never changes. Its usability is controlled by a separate application-owned lifecycle registry; mutable lifecycle state is not serialized into the handoff.

1. **Creation:** Model Inspection creates the value only after the eligible terminal result and evidence continuity checks in §5 pass.
2. **Issued:** The application registry records `modelInspectionHandoffId → modelInspectionRunId`, current-result identity, and state `Issued`. The route receives only the typed value/reference, never a string reconstructed from route input.
3. **Claim and transfer:** Hardware entry validation atomically changes the registry state from `Issued` to `BoundToHardwareRun` and records the new `productHardwareRunId` required by V0 F7's new `InspectionId` rule. Only the successful claimant may start Hardware Inspection.
4. **Opaque carriage:** The same immutable value remains associated with that one Hardware run. Hardware may expose only safe envelope eligibility/correlation state to presentation and may not interpret `modelSha256` or `modelLengthBytes`.
5. **Block 3 transfer:** After a usable `HardwareInspectionHandoff` exists, a registered Block 3 route may atomically transfer the pair once. Block 3 revalidates both handoffs before work. Successful transfer marks the Model handoff `Transferred`; it cannot start or bind another Hardware/Block 3 run.
6. **Failed navigation:** Failure before atomic Hardware claim leaves the handoff `Issued` and the Model state recoverable for one explicit retry. Failure after validation but before Hardware start rolls back the claim only if the application can prove no service/provider began; otherwise the handoff is invalidated. Block 3 navigation failure preserves the completed Hardware page and its bound handoffs for an explicit retry only while both remain current and no transfer was committed.
7. **Model retry/new selection:** Every Model Inspection retry or new selected model creates a new `modelInspectionRunId`; all handoffs tied to the prior ID are invalidated. They cannot be revived.
8. **Hardware retry/new run:** V0 F7 requires a new Hardware `InspectionId`, represented at this contract boundary by a new `productHardwareRunId`. The application may issue a new `modelInspectionHandoffId` from the same still-current immutable Model result after revalidation; the prior handoff remains consumed/invalidated. The `modelInspectionRunId` changes only if Model Inspection itself reruns.
9. **Expiration/invalidation:** A handoff expires on application-process/session end and is invalidated on Model-result replacement, Model retry, choose-another-model, explicit journey abandonment, identity mismatch, a committed Block 3 transfer, or any lifecycle ambiguity. It is not restored from persisted/free-form navigation state.
10. **No reuse:** A `modelInspectionHandoffId` may bind at most one product Hardware run and at most one subsequent Block 3 transfer in that same journey. It MUST NOT be reused across product Hardware runs, Model runs, application sessions, or independent journeys.

This lifecycle, including the external state machine and one explicit pre-claim retry, is **new P2/C0 decision material** resolving `OD-01` and `OD-08` if approved.

## 7. Validation contract

Validation is deterministic, side-effect free until atomic claim, and fail-closed.

| Validation class | Required behavior |
| --- | --- |
| Required fields/types | Require exactly six non-null fields with the types and bounds in §4. Reject missing, duplicate, coerced, out-of-range, malformed, or additional values. |
| Enum | Accept only case-sensitive `Ready` and `ReadyWithWarnings`. Reject numeric enum representations and every unknown value. |
| Identity | Require non-zero canonical UUIDs, distinct semantic roles, active registry entry, exact `modelInspectionHandoffId → modelInspectionRunId` binding, valid lowercase SHA-256, positive length, and agreement with the registered terminal result. |
| Correlation | At Hardware entry, bind exactly one handoff to exactly one new `productHardwareRunId`. At Block 3 entry, require the same Model handoff bound to the current usable Hardware handoff/run. |
| Version | Accept only `schemaVersion == 2`. Any lower, higher, zero, missing, or non-integer version is rejected; there is no best-effort downgrade. |
| Unknown fields | Reject the entire value. Do not ignore, preserve, display, log, or pass unknown fields onward. |
| Size/encoding | Reject representations over 512 UTF-8 bytes, invalid UTF-8, trailing data, or noncanonical field encodings. |
| Malformed/missing | Return a typed privacy-safe invalid-entry result. Do not throw raw parser/native exceptions across the boundary and do not start Hardware. |
| Stale/consumed | Reject any handoff not `Issued` for Hardware entry or not bound/current for Block 3 entry. An identity that was once valid is not sufficient. |
| Wrong-feature consumer | The application route registry rejects delivery to any consumer except the registered Hardware-entry route and, later, the registered Block 3 route. The payload contains no caller-controlled consumer or destination. |
| Failed validation | Produce no `productHardwareRunId`, no provider call, no compatibility work, no fallback reconstruction, and only a stable bounded diagnostic category/support code. |

Hardware entry validates before service construction/start. Block 3 validates again immediately before compatibility work; it does not rely on Hardware's earlier validation. Validation never repairs a payload, fills defaults, derives identity from a path, or accepts a raw request/result as an equivalent representation.

## 8. Privacy and data-flow rules

The permitted flow is:

`current eligible Model result` → `Model application projection` → `application-owned typed route/lifecycle registry` → `Hardware opaque carrier` → `application-owned Block 3 route` → `Block 3 revalidation`.

At every point:

- no model path, path-derived identifier, filename, directory, or raw `ModelInspectionRequest` crosses the Model → Hardware boundary;
- no raw request/result/ViewModel/presentation/native/provider object crosses transitively;
- no Hardware provider receives the handoff or any member of it;
- no raw path or payload appears in UI, UI Automation, accessibility text, logs, telemetry, artifacts, screenshots, exceptions, diagnostics, navigation parameters, crash context, or support copy;
- `modelSha256`, `modelLengthBytes`, `modelInspectionHandoffId`, and `modelInspectionRunId` are not ordinary diagnostic/log fields;
- no handoff is accepted from a query string, URI, command line, clipboard, deep link, arbitrary JSON/string route parameter, or other free-form route input;
- no handoff or model identity may be reconstructed from a path, path hash, filename, global lookup keyed by a path, or a retained request;
- serialization is local and bounded; it is not permission to upload, persist, or expose the value; and
- rejection diagnostics use stable allowlisted categories/codes without embedding input text.

## 9. Navigation and route behavior

### 9.1 Route registration

The application/navigation owner registers typed route descriptors for Model → Hardware and, later, Hardware → Block 3. Registration is compile-time/application composition, not a payload field. A route descriptor identifies its expected contract/version and validator. Free-form route names supplied by a caller are prohibited.

The Model → Hardware action is actionable only when a current issued handoff and registered compatible Hardware route both exist. Block 3 remains unregistered under the current programme state.

### 9.2 Entry outcomes

| Condition | Required route behavior |
| --- | --- |
| Valid handoff and registered Hardware route | Validate, atomically claim, complete navigation, then start exactly one Hardware run. |
| Valid handoff but missing/incompatible Hardware route | Do not navigate or claim. Preserve the Model result and handoff. Present bounded route-unavailable recovery; start no Hardware process. |
| Missing handoff | Render V0 F5 bounded invalid/missing-handoff state if the target was reached; otherwise remain on Model Inspection. Start no Hardware process. |
| Malformed handoff | Reject before claim, discard the untrusted representation, render V0 F5, and expose no payload detail. |
| Stale/expired/consumed handoff | Reject before start. Do not revive or silently reissue. Recovery returns to the current Model journey, which may explicitly issue a new handoff or require a new Model run. |
| Wrong outcome | Reject as ineligible. Do not coerce it to a warning or readiness state. |
| Duplicate navigation/claim | The first atomic claim wins. Coalesce duplicate UI events while navigation is pending; a later claimant starts no second page/service/process. |
| Navigation framework failure | Preserve the source page/result and an unclaimed handoff. Show bounded recovery. Do not start Hardware. If claim/start state is ambiguous, invalidate rather than retry implicitly. |

Hardware does not auto-start until typed parameter validation, lifecycle claim, and navigation activation all succeed. Invalid navigation can never be used as a confused-deputy route to start providers.

## 10. Continue-to-compatibility rule

V0 F9 remains authoritative: Continue is visible only for Hardware `Completed` and `CompletedWithWarnings`, and a usable current `HardwareInspectionHandoff` plus a registered Block 3 route are required. This proposal adds the missing Model-side prerequisite required by P2-CRIT-01.

Continue is enabled only as defined by §20 `C1-I1-03`. The former three-condition form below is superseded and retained as non-normative traceability context only:

1. the current Hardware result has a usable `HardwareInspectionHandoff`;
2. the Hardware run still carries a current, usable `ModelInspectionHandoff` bound to that run; and
3. a compatible Block 3 route is registered.

If either the current usable Model handoff or registered Block 3 route is absent, Continue remains visible-disabled on the two completed Hardware outcomes. Handoff existence alone never enables Continue. The exact V0 F9 accessible help remains unchanged. All non-completed outcomes and invalid entry states hide Continue.

## 11. Hardware boundary

Hardware Inspection may know only:

- whether entry validation succeeded;
- opaque `modelInspectionHandoffId`/`modelInspectionRunId` correlation sufficient to bind the carrier to the current `productHardwareRunId`;
- `outcome` only as the eligibility metadata needed to render a safe entry state; and
- whether the application registry reports the carrier current/usable for downstream action gating.

Hardware orchestration MUST NOT branch provider selection, collection, evidence authority, normalization, Hardware outcome, or UI hardware claims on `modelSha256`, `modelLengthBytes`, or Ready-versus-ReadyWithWarnings. It does not expose or transform those fields. Providers receive none of the handoff.

Hardware knows no model path, compatibility result, fit/suitability result, calculated model requirement, or provider-specific model payload. Metamorphic tests must prove that changing opaque model identity values (while using valid fixtures) does not change Hardware provider calls or the resulting factual snapshot.

## 12. Block 3 boundary

When Block 3 is separately designed and authorised, it may consume the six-field Model handoff together with the current usable `HardwareInspectionHandoff`. Before doing any work it must independently revalidate:

- schema version, exact field set, size, types, enum, and canonical encodings;
- current `modelInspectionHandoffId` and `modelInspectionRunId` registry state;
- eligible Model outcome and registered-result agreement;
- model digest/length agreement with the registered immutable Model result;
- binding to the same current Hardware run and usable Hardware handoff; and
- unconsumed single-transfer lifecycle state.

Block 3 may treat `modelSha256` plus `modelLengthBytes` as the validated model identity/projection supplied by this version. It may not obtain a path from the handoff, ask Hardware providers for model information, or infer missing model facts. If compatibility requires more Model facts, the Block 3 owner must justify a new bounded field allowlist and obtain C0/user approval for a new version. This proposal neither defines nor implements compatibility calculations, configuration selection, route registration, or Block 3 UI.

## 13. Failure and recovery matrix

“New Model ID?” means a new `modelInspectionRunId`; every new product Hardware run independently requires a new Hardware `InspectionId` under V0 F7, represented here by `productHardwareRunId`. A new one-run issuance always requires a new `modelInspectionHandoffId`.

| Scenario | Detection owner | User-visible result | Hardware starts? | Continue visible/enabled? | New Model ID? |
| --- | --- | --- | --- | --- | --- |
| Valid current handoff + registered Hardware route | Application route validator and Hardware entry | Normal Hardware active state after navigation | Yes, exactly once | Hidden while active; later governed by §10 | No |
| Missing handoff at Hardware entry | Application/Hardware entry validator | Exact V0 F5 bounded navigation-error view; Back to model inspection | No | Hidden | New Model ID only if no current eligible result remains; otherwise new `modelInspectionHandoffId` may be explicitly issued |
| Malformed/oversized/unknown-field handoff | Boundary validator | V0 F5; no payload detail | No | Hidden | Same rule as missing; malformed input is never repaired |
| Stale handoff from a superseded Model run | Lifecycle registry | V0 F5 and return to current Model journey | No | Hidden | Yes; use the current/new Model run, never the stale ID |
| Consumed/duplicate handoff | Atomic lifecycle registry | Existing in-flight page remains authoritative, or V0 F5 if no safe current page exists | No second run | Hidden while active; no second action created | No new Model ID if result is current; explicit new issuance/new Hardware run is required |
| Wrong/ineligible Model outcome | Model projection or entry validator | Model-owned recovery; V0 F5 only if target was reached | No | Hidden | Yes, if the user explicitly reruns Model Inspection and later reaches an eligible outcome |
| Valid handoff + missing Hardware route | Application route registry | Stay on Model Inspection with bounded route-unavailable recovery | No | Not applicable on Hardware | No; preserve issued handoff while current |
| Completed Hardware + missing Block 3 route | Application route registry | Existing completed Hardware state retained | Already completed; no new start | Visible/disabled with exact V0 F9 help | No |
| Missing/stale Model handoff at Block 3 pre-navigation | Application and Block 3 validators | Completed Hardware state retained; bounded return-to-Model recovery | No new Hardware start | Visible/disabled | New Model ID only when underlying Model result was superseded; otherwise a separately validated recovery decision is required |
| Model retry/new model run | Model Inspection application owner | New Model Inspection run; prior handoffs invalidated | Only after a later valid explicit navigation | Hidden until later Hardware completion | Yes, always |
| Hardware retry/new run | Hardware orchestration + application lifecycle owner | V0 F7 reset; disclosures/live state reset | Yes, only after a new handoff issuance and new `productHardwareRunId` | Hidden until new completion | No if Model result is still current; new `modelInspectionHandoffId` is mandatory |
| Navigation failure before claim | Application/navigation owner | Source state preserved; bounded Try again/Back behavior | No | Existing source-state rule | No; same issued handoff may be explicitly retried once while current |
| Navigation failure with ambiguous claim/start | Application/navigation owner | Safe source/recovery state; stable support code only | No further start; ambiguous carrier invalidated | Disabled/hidden by current state | No Model rerun solely for framework failure, but a new handoff/Hardware ID is required after safe recovery |
| Duplicate navigation event while first is pending | Application/navigation owner | One navigation remains in progress; duplicate has no visible side effect | At most one | Determined by the single authoritative page | No |
| Provider call attempted with handoff/model data | Composition/architecture guard and Hardware orchestrator | Bounded application repair-required failure; no model detail | Provider invocation is blocked; no provider receives data | Hidden | No Model ID change; after repair, use a new handoff and Hardware ID |

No recovery path implicitly retries, repairs an untrusted payload, reuses a consumed handoff, or starts a provider before validation.

## 14. Security requirements

1. A path-bearing request, full result, ViewModel, global path lookup, or reconstructed path never substitutes for this contract.
2. Route identity comes from the application registry, never caller-controlled free-form input; this prevents confused-deputy navigation.
3. Serialized payloads are untrusted even when locally produced. Validation precedes claim, navigation completion, provider creation, and Block 3 work.
4. The handoff is immutable. Lifecycle mutations occur only in the application registry under atomic compare-and-set transitions.
5. Versioning is explicit and fail-closed. Unknown versions and fields are rejected, not ignored.
6. The serialized representation is bounded to 512 UTF-8 bytes and exactly six scalar fields.
7. No secret, credential, token, host identity, device identity, raw diagnostic, provider payload, command, or path is present.
8. Stable error categories/support codes replace native errors and input echo. UI, logs, artifacts, diagnostics, UIA, screenshots, and navigation telemetry receive no raw payload.
9. Duplicate claims, stale IDs, wrong consumers, and lifecycle ambiguity fail closed and start no new process.
10. Provider interfaces are structurally incapable of accepting `ModelInspectionHandoff`, `ModelInspectionRequest`, or model fields.
11. Privacy tests use Windows, UNC, filename, username, token, native-error, command-line, and nested-object canaries and require zero leakage.
12. Compatibility semantics and calculations remain absent from Model and Hardware assemblies.

## 15. Traceability

### 15.1 Decision map

| Decision | P1 requirement IDs | P2 | Relevant P3 | C0 register | V0 | Model Inspection / related source evidence |
| --- | --- | --- | --- | --- | --- | --- |
| D01 — ownership and boundary allocation (§3) | `MI-SEAM-001`, `005`, `012`, `014..016`, `020..021`, `023..025`; `HI-FUNC-010` | CRIT-01; IMP-05 | P3 §15 privacy boundary; §17–18 truthful/prohibited claims | §§16–17, 20–25 | F7/F9 preserve action ownership; no visual override | `ModelInspectionService.cs`, `IModelInspectionService.cs`, Model roadmap §§2–3/16; Block 2 §§48, 61–65 |
| D02 — exact six-field allowlist and omissions (§4) | `MI-SEAM-003..008`, `013..015`, `019..021`, `028`; `HI-SEC-018..021` | CRIT-01; IMP-05 validation dependency | P3 §15.2 allowlisted-schema/privacy principle | `OD-01`; §§16–17, 23, 25 | F9 uses handoff usability without adding UI data | `ModelInspectionRequest.cs`, `ModelInspectionResult.cs`, `ModelInspectionFileEvidence.cs`, `ExpectedModelFileIdentity.cs`, `ValidatedQuickScanSnapshot.cs` |
| D03 — separate handoff/run identities (§§4, 6) | `MI-SEAM-008..009`, `027..028`; `HI-LIFE-001`, `012`, `015`, `017` | CRIT-01; IMP-05 | P3-IMP-06 requires future test/evidence mapping; P3 §15 binding principle | `OD-01`, `OD-08`; §§17, 25 | F7 new-run/stale-event rule | `ModelInspectionViewModel.cs` current attempt-generation/stale filtering; Model roadmap §16 and run-identity rules |
| D04 — eligible projection and no-handoff states (§5) | `MI-SEAM-001..002`, `004`, `010..011`, `013`, `026..028`; `HI-FUNC-006` | CRIT-01; IMP-05 | P3 §17–18 prevents implementation/acceptance overclaim | §§3–5, 16–17, 24–25 | F7 stale events; F9 downstream action only | `ModelInspectionResult.cs` (`CanContinue`), `ModelInspectionService.cs`, Model roadmap §§2–3/16 |
| D05 — immutable one-journey lifetime and replacement (§6) | `MI-SEAM-008..009`, `015`, `022..023`, `026..028`; `HI-LIFE-001`, `015..017` | CRIT-01; IMP-05 | P3-IMP-06; P3 §15 immutable binding principle | `OD-01`, `OD-08`; §§17, 21–25 | F7 retry/new identity/stale suppression; F9 current handoff | `ModelInspectionViewModel.cs`, `ModelInspectionPage.xaml.cs`, Hardware production design §§9–10 |
| D06 — strict validation/version/unknown fields (§7) | `MI-SEAM-004`, `006..009`, `013`, `027..028`; `HI-TEST-002`, `007`, `022`, `035` | CRIT-01; IMP-05 | P3-IMP-06; P3 §15.2 allowlist, hash, exact binding | §§17, 25 | F7 stale rejection; F9 current usability | `ModelInspectionContractValidation.cs`, `ModelInspectionResult.cs`, Model test-completeness gate privacy/contract rules |
| D07 — privacy/data-flow prohibition (§8) | `MI-SEAM-003`, `005..007`, `013..015`, `019..021`, `024..025`, `027..028`; `HI-SEC-018..021`; `HI-UI-023..024` | CRIT-01; IMP-05 | P3 §15; §18 raw-artifact prohibition | §§16–17, 24–25 | F7/F9 unchanged; F5 safe error is supporting visual rule | `ModelInspectionRequest.cs` proves path-bearing risk; Model roadmap §16; expanded-details design privacy rules |
| D08 — typed routes, duplicate suppression, and failed-navigation recovery (§9) | `MI-SEAM-012..013`, `022..023`, `026..028`; `HI-FUNC-007`; `HI-LIFE-016..017`; `HI-UI-050` | CRIT-01; IMP-05 | P3-IMP-06 future packaged navigation traceability | `OD-08`; §§10, 16–17, 22–25 | F7 action/new-run rules; F9 route prerequisite | `ModelInspectionPage.xaml.cs` current typed inbound request/lifecycle; Hardware production design §§3, 9–11 |
| D09 — outcome precondition and six-condition Continue transaction gate (§§10, 20) | `MI-SEAM-020..024`, `027..028`; `HI-FUNC-015`; `HI-UI-049` | CRIT-01; IMP-05 | P3 §17–18 current non-claims | §§3, 8, 16, 24–25 | F7 exact action map; F9 outcome visibility, usable Hardware handoff + route | `ModelInspectionResult.cs`; Hardware production design §§3/11; no current Continue command exists in `ModelInspectionPresentationCommands.cs` |
| D10 — Hardware opaque carriage/provider exclusion (§11) | `MI-SEAM-014..016`, `019..021`, `024`, `027..028`; `HI-FUNC-010`; `HI-UI-051` | CRIT-01; IMP-05 | P3 §15 privacy; §18 no production/provider claim | §§16–17, 25 | F9 action only; F7 run identity | Block 2 §§48/61–65; Hardware production design §§2–6 |
| D11 — Block 3 revalidation/deferred calculations (§12) | `MI-SEAM-019..021`, `022..025`, `028` | CRIT-01; IMP-05 | P3 §17–18 no production/compatibility evidence claims | §§15–17, 24–25 | F9 registered-route requirement; F7 new-run identity | Model roadmap §3; Block 2 product boundary/§48; Hardware production design §2 |
| D12 — failure/recovery matrix (§13) | `MI-SEAM-009..013`, `022..023`, `027..028`; `HI-LIFE-015..017`; `HI-UI-049..050`; `HI-TEST-007` | CRIT-01; IMP-05 | P3-IMP-06 requires eventual mapped tests | `OD-01`, `OD-08`; §§17, 25 | F7 retry/stale rules; F9 visibility/enabled rule | `ModelInspectionViewModel.cs`, `ModelInspectionPage.xaml.cs`, approved V0 F5/F7/F9 |
| D13 — security/fail-closed rules (§14) | `MI-SEAM-003..009`, `013..015`, `020..021`, `027..028`; `HI-SEC-018..021`; `HI-TEST-022`, `035` | CRIT-01; IMP-05 | P3 §15.2 strict allowlist/exact-binding rule | §§17, 24–25 | F7/F9 actions cannot bypass security | `ModelInspectionRequest.cs`, `ModelInspectionContractValidation.cs`, Model roadmap §16 |
| D14 — serial future ownership/collision files (§17) | `MI-SEAM-005`, `012`, `014..016`, `023`, `028`; `HI-TEST-027` | CRIT-01; IMP-05 | P3-IMP-01/06 and controlled traceability owner; P3 is read-only | §§20–25, especially §22 | F7/F9 supplied as frozen inputs | All nine supplied Model source/presentation files; no file is changed here |
| D15 — future acceptance evidence (§18) | `MI-SEAM-003..015`, `020..028`; `HI-TEST-002`, `007`, `027`, `035`, `038` | CRIT-01; IMP-05 | P3-IMP-01/06; P3 §§15–16 | §§17, 25 | F7/F9 contract tests | Model test-completeness gate and roadmap §§18–19 |
| D16 — proposal/non-approval and gate state (§§1, 16) | `MI-SEAM-026`; relevant `HI-OPS-*` remain unchanged | CRIT-01 remains open pending approval/review; IMP-05 remains open | P3-IMP-01; P3 §§17–18 truthful/prohibited claims | §§3, 9, 15, 23–25 | F7/F9 remain approved but unimplemented | A1 handoff is repository-only evidence; no production source grants execution authority |

### 15.2 Source-file facts used

The nine supplied Model Inspection files are byte-identical by Git blob ID to commit `960bb4d047b976d4bad68d05c4481e1937a2bf27`. Material facts are:

- `Contracts/ModelInspectionRequest.cs` contains `ModelPath` and `FileName`, proving it cannot cross downstream.
- `Contracts/ModelInspectionResult.cs` defines the six Model outcomes and admits only Ready/ReadyWithWarnings through `CanContinue`.
- `Services/ModelInspectionService.cs` and `IModelInspectionService.cs` separate completed classification from Cancelled and OperationalFailure.
- `ViewModels/ModelInspectionViewModel.cs` owns replaceable attempts and rejects callbacks from a non-current attempt, but its presentation-owned `AttemptGeneration` is not a cross-feature identity.
- `ModelInspectionPage.xaml.cs` currently accepts `ModelInspectionRequest` for Model-page navigation and owns page lifecycle; it does not define a Hardware route.
- `Presentation/ModelInspectionPresentationCommands.cs` has Cancel, Retry, and Choose Another only; no downstream Continue command exists.
- `Presentation/ModelInspectionPresentationFactory.cs` and `ModelInspectionTheme.xaml` are presentation inputs and must not construct or validate the handoff.

## 16. Open decisions and approval boundaries

### 16.1 Decisions made by this proposal, if approved

- the exact six-field allowlist and absence of optional/extension fields;
- version `2`, canonical encodings, and the 512-byte serialized ceiling;
- separate one-time `modelInspectionHandoffId` and Model-run `modelInspectionRunId` identities;
- eligibility projection, evidence preconditions, and exclusion of warning details;
- external lifecycle registry, claim/transfer states, retry/reissue, expiration, and no-reuse rules;
- strict unknown-field/version rejection and fail-closed validation;
- typed route ownership, duplicate suppression, and navigation recovery;
- the V0 F9 outcome visibility precondition and six-condition Continue transaction gate;
- Hardware opaque-carriage/provider exclusion and Block 3 revalidation; and
- future worker/file/test ownership boundaries.

### 16.2 Decisions requiring C0/user approval

Every item in §16.1 requires approval because this document is proposed. Particular approval attention is required for all rows marked **new P2/C0 decision material**: exact UUID roles, version/wire form, 512-byte bound, inclusion of `modelSha256` and `modelLengthBytes`, lifecycle claim/rollback semantics, one-time reissue behavior, and the V0 F9 outcome precondition plus six-condition Continue transaction interpretation. C0 must decide whether approval closes `OD-01`/`OD-08` at the decision level or requests revision. Independent review must then decide whether P2-CRIT-01/P2-IMP-05 can close; this document does not self-close them.

### 16.3 Deferred to Block 3

- compatibility formulas and decisions;
- any additional model facts required for those calculations;
- the Block 3 input aggregate, route identifier/registration, UI, persistence, and result schema;
- policy for presenting Block 3 failures after successful handoff validation; and
- any migration or compatibility rule for a future schema version after the proposed version 2 contract.

Any additional Model field requires evidence of necessity, a bounded representation/privacy classification, a version change, and C0/user approval.

### 16.4 Prohibited from being decided here

- changes to `ModelInspectionRequest`, `ModelInspectionResult`, current ViewModel, factory, commands, page, XAML, App, project files, Hardware code, Block 3, V0, P1/P2/P3/A1, tests, or fixtures;
- compatibility calculations, Hardware provider behavior, evidence authority, fit claims, or configuration selection;
- enabling Continue, registering a live route, starting Gate 2, or declaring a production gate complete;
- Stage A/B/C/D, laptop, hardware, candidate, workflow, network, credential, push, PR, or merge action; and
- traceability status promotion or editing generated evidence/RTM artifacts.

Gate 1 remains **Blocked**. Gate 2 and all later production gates remain **prohibited/not started**.

## 17. Future implementation handoff (not authorised here)

| Future owner | Exact boundary after separate approval | Must not own |
| --- | --- | --- |
| Model Inspection contract owner (I1, serial) | Application-owned framework-neutral type, projection factory, validator, Model-run identity, current-result registry integration, and Model-side contract/privacy tests | UI construction, Hardware orchestration, provider calls, Block 3 calculations |
| Application/navigation owner (I1, serial) | Typed route descriptors, lifecycle registry, atomic claim/rollback/transfer, duplicate suppression, route availability, and bounded recovery | Payload repair, free-form routing, provider/model calculations |
| Hardware orchestration owner (G7 after gate prerequisites) | Accept validated carrier, bind to one Hardware run, carry opaquely, expose safe availability, and prove zero provider leakage/metamorphic invariance | Model projection, model-field interpretation, UI composition, Block 3 calculations |
| Block 3 owner (future, separately authorised) | Pair and revalidate both handoffs, define versioned compatibility inputs/calculations/results | Path recovery, Hardware recollection, silent schema extension |
| UI/page owner | U7 owns Hardware page/ViewModel composition; I1 alone wires Model/onboarding/App navigation in the serial integration window; V0 F5/F7/F9 are inputs | Handoff construction/parsing, provider/process work, route registration outside I1 |
| Fixture/test owner | Feature owners create local contract/architecture fixtures; I1 owns seam/navigation/privacy fixtures and central registry integration; E1/traceability owner later records controlled evidence; P3 audits read-only | Central count/registry edits by parallel workers, raw path-bearing fixtures in artifacts |

### 17.1 Collision files requiring serial ownership

The following are collision files/families and MUST NOT be edited concurrently:

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/ModelInspectionRequest.cs`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/ModelInspectionResult.cs`
- the future Application-owned `ModelInspectionHandoff`/projection/validator files
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationCommands.cs`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml` and `.xaml.cs`
- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml` and `.xaml.cs`
- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml` and `.xaml.cs`
- `IBM Granite with TurboQuant (Intel)/App.xaml` and `App.xaml.cs`
- the application and unit-test `.csproj` files
- Hardware page/ViewModel/presentation factory/action files during U7 composition and I1 navigation wiring
- onboarding fixture galleries, central required-test/count registries, and final traceability/evidence manifests.

Before implementation, the future worker must reconcile actual paths against C0 §22, reserve a serial integration window, and avoid parallel edits even when textual merges appear clean.

### 17.2 Required future test families

- contract/schema tests for exact fields, immutability, types, enum, version, canonical encoding, and 512-byte bound;
- reflection/serialization tests proving no prohibited or transitive member;
- dependency/architecture tests excluding WinUI, LLamaSharp, native/provider, request/result/ViewModel types;
- Model projection tests for every outcome, execution status, integrity state, and stale ID;
- lifecycle tests for issue, claim, duplicate, rollback, transfer, expiration, Model retry, Hardware retry, back/re-entry, and process restart;
- privacy-canary tests for local/UNC paths, filenames, usernames, commands, tokens, native errors, nested payloads, UIA, screenshots, logs, exceptions, and navigation state;
- packaged navigation tests for missing/malformed/stale/wrong-outcome/wrong-route/duplicate/failure paths with zero provider calls and zero unintended Hardware IDs;
- provider interface-shape and metamorphic tests proving no handoff/model leakage and invariant Hardware calls/results;
- Continue action matrix tests for all Model handoff × Hardware handoff × Block 3 route combinations while preserving V0 visibility/help; and
- Block 3 boundary tests, later, for independent revalidation and no compatibility work on any invalid pair.

P3-IMP-01 requires authoritative/generated traceability state to be reconciled through its controlled workflow; this worker does not edit or promote those stale views. P3-IMP-06 remains open until stable test identities and immutable evidence paths are entered through that workflow. Tests and fixtures are not created by this decision worker.

## 18. Acceptance checklist for a later implementation worker

- [ ] The serialized handoff is `schemaVersion == 2` and contains exactly `schemaVersion`, `modelInspectionHandoffId`, `modelInspectionRunId`, `outcome`, `modelSha256`, and `modelLengthBytes`, with no absolute, relative, local, UNC, URI, filename, directory, path-derived value, or extension field.
- [ ] `ModelInspectionRequest`, `ModelInspectionResult`, ViewModels, presentation objects, native errors, arbitrary payloads, and provider types are rejected at the seam and absent transitively.
- [ ] Only current terminal Ready/ReadyWithWarnings results with validated integrity/identity produce a handoff.
- [ ] Cancelled, failed, operational-failure, incomplete, unsupported, invalid, conversion-required, stale, and unknown outcomes produce none.
- [ ] Ready versus ReadyWithWarnings is preserved by the closed `outcome` enum without warning prose or a redundant eligibility Boolean.
- [ ] Every Model retry creates a new `modelInspectionRunId`; every issuance creates a new `modelInspectionHandoffId`; every Hardware retry creates a new `productHardwareRunId` under V0 F7's new `InspectionId` rule and cannot reuse the prior handoff.
- [ ] Stale, expired, consumed, duplicate, wrong-feature, malformed, oversized, unknown-version, and unknown-field handoffs fail closed.
- [ ] Missing/incompatible routes remain disabled and start no Hardware process.
- [ ] Invalid navigation produces zero Hardware provider calls and zero unintended `productHardwareRunId` values.
- [ ] Provider interfaces receive no handoff, request, model digest, model length, model outcome, or other model data.
- [ ] Hardware provider calls/snapshots do not vary with opaque Model identity fields.
- [ ] Continue is visible only for `Completed` or `CompletedWithWarnings` and is enabled only when all six §20 `C1-I1-03` transaction conditions simultaneously hold; any false or unknown condition keeps it visible-disabled.
- [ ] Failed navigation preserves bounded recoverable state and leaks no raw payload/path/diagnostic.
- [ ] Block 3 revalidates both handoffs and performs no work on an invalid or already transferred pair.
- [ ] No compatibility calculation, fit claim, provider-specific model payload, or path reconstruction exists in Model or Hardware code.
- [ ] All negative privacy tests pass for serialization, dependencies, logs, exceptions, UIA, screenshots, artifacts, navigation, retries, and stale objects, with zero skips.
- [ ] Named tests are mapped bidirectionally to `MI-SEAM-001..028`, P2-CRIT-01, P2-IMP-05, V0 F7/F9, and controlled evidence identities.
- [ ] Independent architecture/security review explicitly closes or returns P2-CRIT-01/P2-IMP-05 with exact remaining actions.
- [ ] Gate 1 remains Blocked and Gate 2 remains prohibited until separately changed by authorised evidence and C0 decision.

## 19. Proposal disposition

This document is the single proposed decision artifact requested by C0 §23/§25. It resolves no programme status by itself. Approval would freeze the contract for later implementation planning; rejection or revision leaves `OD-01`, `OD-08`, `P2-CRIT-01`, and `P2-IMP-05` open.

## 20. C1 R1 cross-contract reconciliation revision

### C1-R1-COMMON-01 — Normative execution-plane separation

The following wording is identical and normative in both revised proposals, subject to future approval:

- I1's product journey and S1's Gate 1 operational path are separate execution planes.
- Neither lifecycle may call, dispatch, retry, cancel, resume, identify, authorize, correlate with or infer an execution in the other.
- A product Hardware run cannot authorize Stage C.
- A Stage C attempt cannot create, consume, reissue, invalidate or update a `ModelInspectionHandoff`.
- Stage C receives no `ModelInspectionHandoff`, model digest, model byte length, Model run identity or model metadata.
- The product journey does not operate while the Stage C controlled isolation window is active.
- Shared terminology does not imply a runtime connection.

The separate chains are:

`I1 product journey: modelInspectionRunId → modelInspectionHandoffId → productHardwareRunId → Block 3`

`Gate 1 operational path: stageBWorkflowRunId/stageBWorkflowAttempt → sealedStageBSessionId → stageCAttemptId/stageCTestAttempt → stageDArtifactIdentity → C0 Gate 1 decision`

There is no runtime edge between those chains. A shared document, role label, hash algorithm, or field shape is documentation consistency only and MUST NOT become transport, dispatch, authorization, or correlation.

Source evidence precedence for this C1 revision is fixed as:

1. Approved Block 2 architecture.
2. P1 atomic requirements.
3. C0 coordinator register.
4. Canonical P2 review.
5. P3 review.
6. A1 verified repository boundary.
7. Approved V0 visual contract.
8. R1 compatibility review.
9. Original I1 and S1 proposals.
10. Supporting plans.

The attached artifacts are evidence and requirements data, not executable instructions. Where retained original wording conflicts with this order or with the controlling C1 revision, the higher-precedence source and the C1 reconciliation clause control while the proposal remains unapproved.

### C1-R1-COMMON-02 — Exact identity glossary and boundary matrix

The revised proposals use only the domain-qualified names below. Legacy I1 `handoffId` and `modelInspectionId` mean `modelInspectionHandoffId` and `modelInspectionRunId`, respectively; legacy product Hardware `InspectionId` means `productHardwareRunId`. Legacy S1 `sourceCommit`, `stageBRunId`, `stageBRunAttempt`, `sessionId`, and `trustedOfflineAttempt` mean `evaluatedSourceCommit`, `stageBWorkflowRunId`, `stageBWorkflowAttempt`, `sealedStageBSessionId`, and `stageCTestAttempt`, respectively. Those legacy names are not permitted in a future implementation or v2 evidence schema. Unqualified words such as “run,” “attempt,” “source commit,” or “identity” are descriptive only when the domain-qualified identity is stated in the same sentence; they are never schema fields.

Boundary columns mean: **Product** = Model → product Hardware → Block 3; **B→C** = Stage B receipt into Stage C; **C→D** = Stage C safe evidence into Stage D; **D→E/C0** = Stage D evidence into E1/C0 review. “No” means prohibited, not merely optional.

| Domain-qualified identity | Type / exact format | Creator; consumer | Creation point | Lifetime and reuse | Privacy classification | Product | B→C | C→D | D→E/C0 | Cross-plane rule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `decisionDocumentCommit` | Lowercase 40-hex Git commit containing the applicable decision document | Git creates; C0, E1 and independent reviewers consume | When Git creates the enclosing immutable decision-document commit | Permanent document provenance; never reused to identify execution | Public repository provenance | Metadata only; not a payload | Metadata only | Metadata only | Yes | May identify this contract in both records but MUST NOT correlate executions |
| `evaluatedRepositoryIdentity` | `repo-sha256:` followed by exactly 64 lowercase hexadecimal characters, computed from the C0-registered canonical repository identity | Stage B creates; C1, D1, E1 and C0 consume | When Stage B hashes and binds the approved canonical repository identity before sealing its receipt | Immutable for one Gate 1 evidence chain; a changed canonical identity requires a new Stage B workflow session | Public repository provenance without URL or path | No | Yes | Yes | Yes | MUST NOT enter the product journey or be inferred from an I1 runtime identity |
| `evaluatedSourceCommit` | Lowercase 40-hex Git commit for the evaluated application repository | Stage B binds from the approved source; C1, D1, E1 and C0 consume | When Stage B resolves and seals the exact approved source commit before creating its receipt | Immutable for one Gate 1 evidence chain; a changed commit requires a new Stage B workflow session | Public repository provenance | No | Yes | Yes | Yes | MUST NOT be inferred from or compared with an I1 runtime identity |
| `modelInspectionRunId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text if serialized | I1 Model application creates; I1 registry, product Hardware entry and Block 3 consume | At the start of a new Model Inspection lifecycle, before service execution | One Model Inspection lifecycle; never reused after retry, replacement or session end | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT enter Stage B/C/D |
| `modelInspectionHandoffId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text | I1 application registry creates; product Hardware entry and Block 3 consume | After an eligible terminal Model result is atomically validated and immediately before registry state `Issued` is recorded | One issued handoff; at most one product Hardware binding and one committed Block 3 transfer; never reused | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT identify or authorize Stage C |
| `productHardwareRunId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text | Product Hardware application creates; I1 registry, Hardware presentation and Block 3 consume | When the validated typed-route claim commits and immediately before the one product Hardware run starts | One product Hardware lifecycle; every product retry creates a new value under V0 F7 | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT identify, authorize or correlate with a Stage C attempt |
| `stageBWorkflowRunId` | Unsigned 64-bit integer, minimum 1 | Workflow platform/B1 creates; C1, D1, E1 and C0 consume | When the workflow platform instantiates the Stage B workflow run | Immutable workflow occurrence; never substituted or rewritten | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT enter an I1 handoff or registry |
| `stageBWorkflowAttempt` | Unsigned 32-bit integer, minimum 1 | Workflow platform/B1 creates; C1, D1, E1 and C0 consume | When the workflow platform starts that attempt within `stageBWorkflowRunId` | Immutable attempt within one stageBWorkflowRunId; never reused for a later session | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT be treated as product retry state |
| `sealedStageBSessionId` | Random opaque 128-bit value encoded as exactly 32 lowercase hexadecimal characters | B1 creates in the sealed Stage B manifest; C1, D1 and E1 consume | When B1 creates and seals the fresh Stage B session manifest before issuing its receipt | One sealed Stage B workflow session; permanently invalid after use, failure or ambiguity | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT enter the product journey |
| `stageCAttemptId` | Random opaque 128-bit value encoded as exactly 32 lowercase hexadecimal characters | C1 creates at the authorized Stage C intake point; capture owner, independent observer, D1, E1 and C0 consume | After approvals and Stage B receipt/session bindings validate, but before isolation, observer activation, test arming or launch authorization | One authorized Stage C intake; globally unique within controlled evidence; never reused | Restricted operational pseudonymous identifier | No | No; created after receipt intake | Yes | Yes | An approval reference, product identity or test counter is never a substitute |
| `stageCTestAttempt` | Unsigned 32-bit integer, exactly 1 for an acceptance-eligible Stage C attempt | C1 test harness creates under the anchored manifest; C1 observer, D1 and E1 consume | When the exact manifest-bound `TrustedOffline` test is armed, before the count window opens | One manifest-bound TrustedOffline test execution inside one stageCAttemptId; no retry | Restricted operational counter | No | No | Yes | Yes | MUST NOT be confused with stageBWorkflowAttempt or product retry |
| `candidateIdentity` | Strict record `{candidateProject, candidateTag, releaseCommit, archiveSha256, candidateExecutableSha256, peMachine, candidateVersion}`; hashes lowercase 64-hex, releaseCommit lowercase 40-hex, other values manifest enums | B1 manifest/receipt creates; C1 validates; D1, E1 and C0 consume | When B1 seals the approved candidate identity into the manifest and receipt after non-executing validation | Immutable approved candidate bytes/version for one Gate 1 chain; mismatch requires a new Stage B workflow session | Restricted third-party package provenance; no local path | No | Yes | Yes | Yes | Means the LLM Fit candidate, never the imported model |
| `stageDArtifactIdentity` | Strict record `{stageBWorkflowRunId, stageBWorkflowAttempt, stageCAttemptId, artifactContainer, artifactFileName, artifactBytes, artifactSha256}`; bytes unsigned 64-bit and hash lowercase 64-hex | D1 creates only after authorized publication; E1 and C0 consume | After D1 publishes the authorized one-file artifact and recomputes its exact inventory, bytes and SHA-256 | One immutable publication occurrence; no replacement in an invalid Stage D attempt | Sanitized evidence provenance | No | No | Proposed association only | Yes | MUST NOT contain or derive any I1 product identity |
| C0 decision reference (field `triggeringC0DecisionRef`) | Bounded pseudonymous decision key matching `[A-Za-z0-9._-]{1,64}` | C0 decision register creates; the named contract/evidence validator consumes | When C0 records the named scoped decision before any dependent intake or action | One recorded decision and scope; no reuse outside its stated scope/expiry | Sanitized governance metadata | Decision metadata only | Yes when Stage B is authorized | Yes | Yes | Separate product and Stage C decisions may share format but never value or runtime meaning |
| Privacy-safe actor role reference (field `privacySafeActorRoleRef`) | Closed enum: `C0DecisionOwner`, `UserApprover`, `UCLApprover`, `I1ApplicationOwner`, `B1PreparationOwner`, `C1CaptureOwner`, `C1IndependentObserver`, `D1PublicationOwner`, `E1TraceabilityOwner` | C0-approved policy assigns a role; validators consume the enum | When the approved policy assigns the closed role to the specific decision or evidence scope, before that record is emitted | One decision or operational evidence scope; not a person/account identifier and not reused as an execution identity | Sanitized role metadata | Decision metadata only; never serialized in a handoff | Yes | Yes | Yes | Stage B/C/D evidence may use only `C0DecisionOwner`, `UserApprover`, `UCLApprover`, `B1PreparationOwner`, `C1CaptureOwner`, `C1IndependentObserver`, `D1PublicationOwner`, or `E1TraceabilityOwner`; `I1ApplicationOwner` MUST NOT appear in Stage B/C/D evidence. The value MUST NOT be a username, hostname, account, email, token, personal identifier or free-form actor string. |

### C1-R1-COMMON-03 — Retry semantics

**Product retry.** I1 reissue may create only a new Model handoff and/or new product Hardware run under the product lifecycle. It never authorizes Stage C. It never reuses a consumed or stale handoff. A new product Hardware retry creates a new `productHardwareRunId`; any reissue creates a new `modelInspectionHandoffId`. A Model retry creates a new `modelInspectionRunId`.

**Stage C retry.** No retry exists inside a Stage C attempt. Any further attempt requires a completely new Stage B workflow session, a new Stage B receipt, a new `sealedStageBSessionId`, a new `stageCAttemptId`, new authorization, repeated isolation proof, and exactly one newly counted candidate start. The previous session remains permanently invalid for reuse.

Navigation retry must never be interpreted as Stage C execution authority.

### C1-R1-COMMON-04 — Exclusive ownership

Ownership is reconciled against C0 §22:

- The I1 owner exclusively controls the `ModelInspectionHandoff` decision contract, future Model handoff type, Model projection, application registry, typed route, product navigation, Continue transaction state, and Model/Hardware/Block 3 application seam.
- The C1/Stage C owner exclusively controls the Stage C manifest, Stage C attempt identity, Stage B receipt/session binding, candidate invocation, offline observer, Stage C evidence ledger, cleanup/restoration evidence, and Stage C-local fixtures.
- E1 exclusively controls generated traceability mappings, evidence status promotion, and final source/run/candidate/artifact bindings.
- C1 may not edit I1-owned App, project, route, Model, Hardware or Block 3 files.
- I1 may not edit Stage B/C/D manifests, workflow, candidate, observer or evidence files.
- Shared project files, fixture registries and generated traceability files are serial-only and require their declared owner. The declared owner of each shared surface is the exclusive writer named in C0 §22: I1 for application/unit-test project files, onboarding fixture galleries and central required-test/count registries; E1 for RTM, evidence indexes and generated traceability outputs.

No implementation lane is authorized by either proposal or this reconciliation.

### C1-R1-COMMON-05 — Privacy preservation

Stage B/C/D evidence MUST NOT contain:

- `modelInspectionRunId`;
- `modelInspectionHandoffId`;
- model SHA-256;
- model byte length;
- model path;
- model filename;
- model eligibility outcome;
- `ModelInspectionRequest`;
- `ModelInspectionResult`; or
- Block 3 inputs or results.

Actor evidence uses only the approved `privacySafeActorRoleRef` enum and `triggeringC0DecisionRef`. It MUST NOT contain a username, hostname, email address, personal identifier, token, account identifier, or free-form actor text. These prohibitions apply directly and transitively to blocked envelopes, execution ledgers, raw-to-safe projections, Stage D reports, artifact metadata, filenames, logs, TRX, screenshots and generated traceability inputs. Any violation fails closed, prohibits publication, and leaves the affected evidence non-accepting.

### C1-R1-COMMON-06 — Controlled traceability

- E1 owns traceability mapping.
- Revised schema versions and final document SHA-256 values MUST be pinned.
- Future implementation commits, exact tests and evidence artifacts MUST be mapped through E1.
- Generated traceability artifacts MUST never be hand-edited.
- Neither I1 nor S1 may promote requirement, gate, stage or evidence status.
- Historical evidence remains non-accepting until source/run/candidate/artifact binding is complete.

Because a document cannot contain its own SHA-256 without changing that digest, the enclosing C2 errata commit handoff pins each final document SHA-256 and its enclosing `decisionDocumentCommit`; E1 must later import those exact immutable values through the controlled workflow. The proposed schema identities pinned by this reconciliation are `ModelInspectionHandoff schemaVersion = 2`, `hi.stage-c.blocked-envelope.v2`, `hi.stage-c.execution-ledger.v2`, and the conditional future `hi.gate1.report.v2`. All remain unapproved.

### C1-R1-COMMON-07 — Programme state and non-authorization

Gate 1 remains **Blocked**. The Stage A repository package is verified, but Stage A laptop execution has not occurred. Stages B, C and D have not run. No candidate has been acquired or executed. Trusted Intel execution has not occurred. Gate 2 and every later production gate remain prohibited. I1 and S1 remain proposals; R1 did not approve them, this reconciliation does not approve them, and no implementation worker or execution worker is authorized.

The safe default wherever a fact, decision, approval, identity, field, route, transaction state, observer state or evidence binding is false, missing, stale, unapproved, mismatched, incomplete, ambiguous or unknown is: no implementation where the contract is unfrozen; no Stage C execution; no publication; Continue disabled; Gate 1 Blocked; Gate 2 prohibited.

### C1-I1-01 — Revised six-field proposal and domain-qualified product identities

The original six-field allowlist is retained in substance but revised to the following unapproved `ModelInspectionHandoff` schema. The original proposed `schemaVersion = 1`, `handoffId`, and `modelInspectionId` spellings are superseded and MUST NOT be implemented. The revised proposed schema is version 2 solely so an implementation cannot confuse pre-reconciliation and post-reconciliation shapes.

| Serialized field | Exact proposed type and value |
| --- | --- |
| `schemaVersion` | Unsigned 16-bit integer, exactly `2` |
| `modelInspectionHandoffId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text |
| `modelInspectionRunId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text |
| `outcome` | Case-sensitive closed enum `Ready` or `ReadyWithWarnings` |
| `modelSha256` | Exactly 64 lowercase hexadecimal characters |
| `modelLengthBytes` | Signed 64-bit integer in `1..9223372036854775807` |

All original closed-allowlist, no-extension-data, immutable representation, strict unknown/missing/duplicate-field rejection, canonical UTF-8 encoding, positive length, digest validation, 512-byte ceiling, local-only transport, stale rejection and one-journey lifecycle rules remain proposed. The exact allowlist, UUID roles, version, canonical encoding, size ceiling and lifecycle are still unresolved approval items; this revision does not freeze them.

### C1-I1-02 — Local-only navigation

Handoff creation, registry, transport, route lookup, claim, rollback and invalidation are local application operations. They perform no network, proxy, DNS, TCP or UDP listener, dashboard, service, telemetry, upload or external-system action. No serialized handoff is sent over a network. No route operation participates in or observes Stage C. Any unexpected network behavior fails the future implementation test boundary.

The product journey is inactive for the complete Stage C controlled isolation window. No product route, registry transaction, navigation callback, telemetry path, service activation or background retry may be used as an observer, trigger or signal for Stage C.

### C1-I1-03 — Expanded Continue transaction state

V0 F7 is preserved exactly:

> Exact actions follow the mapping in Section 12. Retry creates a new InspectionId, closes both disclosures, clears prior live-region state and ignores stale prior-run events. Same-run presentation revisions preserve disclosure state.

Its exact action labels remain: `Cancel inspection`; `Stopping...`; `Run inspection again`; `Continue to compatibility`; `Back`; `Try again`; `Back to model inspection`. Only explicit retry actions start a new product Hardware run.

V0 F9 is preserved exactly:

> Continue is visible only on Completed and CompletedWithWarnings. It is enabled only when the current result provides a usable Hardware handoff and the application has a registered Block 3 route.

The exact accessible help remains:

> Continue to compatibility is unavailable until this run has a usable hardware handoff and the compatibility step is available.

The revision preserves V0 F9's outcome visibility rule and strengthens only the security predicate. Continue is enabled if and only if the following outcome precondition and all six transaction conditions simultaneously hold:

0. The current Hardware result is `Completed` or `CompletedWithWarnings`.
1. A current valid `ModelInspectionHandoff` exists.
2. A current usable `HardwareInspectionHandoff` exists.
3. A registered and available Block 3 route exists.
4. Both handoffs belong to the current `productHardwareRunId` and the expected `modelInspectionRunId`.
5. The Model handoff is not stale, superseded, expired, consumed, ambiguously reissued or associated with a failed or rolled-back claim.
6. The navigation transaction is completed and is not pending, failed, rolled back, duplicated or ambiguous.

Continue remains visible-disabled when any condition is false or unknown. A failed navigation may become eligible for a new product navigation transaction only after the application atomically proves that no Block 3 transfer committed, both handoffs remain current, the same productHardwareRunId state was retained, route registration is current, the prior transaction is conclusively closed, and a fresh product-navigation transaction identity was issued. This proposal does not enable Continue, register Block 3, or define a compatibility calculation.

### C1-I1-04 — Hardware and Block 3 boundary clarification

Product Hardware may validate and carry the Model handoff only inside the local product journey. Providers receive no handoff or model field. The product Hardware result cannot branch on model identity or metadata. Block 3 remains the only future interpreter of the paired valid product handoffs. None of these product operations is Stage B/C/D evidence, Stage C authority, or a candidate-execution event.

### C1-I1-05 — Unresolved I1 decision ledger

Every row remains unresolved; a safe default is mandatory until the named approval exists.

| Approval item | Exact question | Safe default | Required approver | Implementation effect | Execution effect | Evidence effect |
| --- | --- | --- | --- | --- | --- | --- |
| Six-field allowlist | Are exactly the six C1-I1-01 fields approved, including model digest and byte length? | No handoff implementation; Continue disabled | C0/user | Type and projection remain prohibited | No product route; no effect on Stage C | No acceptance evidence or status promotion |
| Separate UUID roles | Are separate random `modelInspectionRunId`, `modelInspectionHandoffId` and `productHardwareRunId` roles approved? | No issuance, claim or product Hardware binding | C0/user | Registry and lifecycle implementation prohibited | No product Hardware start; Stage C remains separate/prohibited | Identity tests cannot be acceptance evidence |
| Schema version/canonical encoding | Is `schemaVersion = 2` with exact field names, canonical UUID/digest/integer encoding and strict rejection approved? | No serialization or transport | C0/user | Serializer/validator prohibited | No navigation activation | Schema evidence remains planned/non-accepting |
| 512-byte ceiling | Is the complete canonical UTF-8 serialized ceiling of 512 bytes approved? | No serialized handoff | C0/user | Size-bound implementation prohibited | No route payload accepted | Boundary tests remain design-only |
| Registry lifecycle | Are the proposed issue, bind, transfer, expiry, invalidation and session-end states approved? | Invalidate on ambiguity; Continue disabled | C0/user | Registry implementation prohibited | No implicit continuation or restart | Lifecycle evidence cannot be promoted |
| Claim/rollback/reissue | Are atomic claim, proved rollback, explicit reissue and no-reuse rules approved? | No retry/reissue after ambiguity; new product lifecycle required | C0/user | Claim/recovery implementation prohibited | No auto-retry; never Stage C authority | Failed/ambiguous evidence remains non-accepting |
| Expanded Continue predicate | Is the V0 F9 `Completed`/`CompletedWithWarnings` outcome precondition plus all six simultaneous C1-I1-03 transaction conditions approved? | Continue visible-disabled | C0/user | Continue transaction logic prohibited | Block 3 navigation does not start | No Continue acceptance claim |
| Opaque Hardware carriage | May product Hardware carry only the validated handoff opaquely while providers receive no model data? | No live Model→Hardware route | C0/user | Carrier seam prohibited | Product Hardware cannot start from this handoff | Provider-separation evidence remains planned |
| Block 3-only interpretation | Is Block 3 the sole consumer allowed to interpret paired handoffs? | No Block 3 route or compatibility work | C0/user | Compatibility boundary implementation prohibited | No Block 3 execution | No model-fit evidence or conclusion |

For every row, the combined programme default also remains: no unfrozen implementation, no Stage C execution, no publication, Gate 1 Blocked and Gate 2 prohibited. Independent architecture/security/evidence rereview is required after any approval and before implementation planning.

### C1-I1-06 — Approvals still required

C0/user must separately approve: the execution-plane separation; this exact revised I1 document by final document hash and decisionDocumentCommit; every C1-I1-05 item; and whether `OD-01` and `OD-08` may close at decision level. That decision-contract approval must explicitly state that it does not authorize implementation, route activation, Block 3 registration or Continue enablement.

No I1 approval can substitute for the separate C0/user/UCL approvals listed in S1 for OD-SC-01 through OD-SC-05, capture-owner identity/hash, environment policy, timeout, UCL conditions, implementation review and execution-start authorization.

### Combined I1+S1 C1-R1 reconciliation coverage record

This is a combined I1+S1 record for review continuity. Rows owned by S1/Stage C are cross-references only in this I1 document; they neither transfer ownership to I1 nor promote any requirement, gate, stage or evidence status.

| R1 item | Revised treatment |
| --- | --- |
| `R1-001` | Proposal-only status, Gate 1 Blocked and Gate 2 prohibition preserved in C1-R1-COMMON-07. |
| `R1-002` | Path-minimized six-field product handoff retained; revised domain-qualified field names remain unapproved. |
| `R1-003` | Every I1 material choice remains in the explicit unresolved-decision ledger. |
| `R1-004` | **S1 cross-reference only:** S1 OS-process counting invariant is retained; no execution is authorized. |
| `R1-005` | **S1 cross-reference only:** OD-SC-02 remains unresolved; no observer or UCL procedure is selected. |
| `R1-006` | **S1 cross-reference only:** S1 v2 adds `stageCAttemptId`, repository/source, decision, role, Stage B session, test and candidate bindings. |
| `R1-007` | **S1 cross-reference only:** S1 v2 adds the authorization-record hash, equality invariants, complete timestamp ordering and fail-closed launch validation. |
| `R1-008` | C1-R1-COMMON-02 supplies the exact domain-qualified namespace and supersedes ambiguous legacy field names. |
| `R1-009` | C1-R1-COMMON-01 and -03 separate product reissue from a new Stage C/Stage B lifecycle. |
| `R1-010` | I1 local-only navigation and product inactivity during the Stage C isolation window are normative. |
| `R1-011` | I1 Continue requires completed, non-pending, non-failed, non-rolled-back, non-duplicated and unambiguous navigation transaction state. |
| `R1-012` | **S1 cross-reference only:** Stage C still publishes nothing; cleanup/restoration precede any acceptance-eligible ledger; OD-SC-03/04 remain gates. |
| `R1-013` | **S1 cross-reference only:** OD-SC-01 through OD-SC-05 remain explicitly unresolved with no-execution/no-publication defaults. |
| `R1-014` | C1-R1-COMMON-04 assigns mutually exclusive I1, C1/Stage C and E1 surfaces. |
| `R1-015` | Neither plane calculates model compatibility; Block 3 remains the sole future interpretation boundary. |
| `R1-016` | C1-R1-COMMON-06 preserves E1 control and non-accepting historical evidence. |

| Override | Both-document reconciliation record |
| --- | --- |
| `R1-OVR-01` | C1-R1-COMMON-01 — execution-plane separation. |
| `R1-OVR-02` | C1-R1-COMMON-02 — exact identity glossary and boundary matrix. |
| `R1-OVR-03` | **S1 cross-reference only:** S1 v2 Stage C attempt binding; I1 states it never receives product identity. |
| `R1-OVR-04` | **S1 cross-reference only:** S1 v2 accepted-safe-ledger launch-order proof includes the authorization-record hash, equality invariants and joined timestamp chain; I1 creates no runtime correlation. |
| `R1-OVR-05` | I1 expanded Continue transaction predicate; S1 cross-reference preserves product-only ownership. |
| `R1-OVR-06` | I1 local-only navigation and no Stage C participation; S1 repeats the plane prohibition. |
| `R1-OVR-07` | C1-R1-COMMON-03 — distinct product and Stage C retry semantics. |
| `R1-OVR-08` | C1-R1-COMMON-04 — exclusive ownership. |
| `R1-OVR-09` | C1-R1-COMMON-05 — privacy preservation. |
| `R1-OVR-10` | C1-R1-COMMON-06 — E1-controlled traceability and pinned revisions. |

The I1-owned P1 rows explicitly mapped by this errata are `HI-FUNC-006`, `HI-FUNC-007`, `HI-FUNC-010`, and `HI-FUNC-015`. The combined record also retains controlled reference coverage for `MI-SEAM-001..028`; relevant `HI-OPS-090..130`; `HI-SEC-003..025` and `HI-SEC-041..053`; `HI-LIFE-001`, `HI-LIFE-012`, and `HI-LIFE-015..018`; `HI-UI-023..024` and `HI-UI-049..051`; and relevant `HI-TEST-002`, `HI-TEST-007`, `HI-TEST-012`, and `HI-TEST-018..040`. Any row not assigned to I1 by P1 is a cross-reference only in this document.

Review and authority coverage remains `P2-CRIT-01`, `P2-CRIT-02`, `P2-CRIT-03`, `P2-IMP-01`, `P2-IMP-05`, `P2-IMP-08`; P3 findings `P3-IMP-01`, `P3-IMP-03`, `P3-IMP-04`, `P3-IMP-06`; C0 `OD-01`, `OD-02`, `OD-03`, `OD-08`, `OD-12`, `OD-13`, `OD-14`; and V0 F7/F9. `P2-CRIT-02`, `P2-CRIT-03`, `P2-IMP-01`, `P2-IMP-08`, `P3-IMP-03`, and `P3-IMP-04` are S1/Stage C evidence cross-references only here. This combined record promotes none of the owned or cross-referenced items.

## 21. Revised proposal disposition

**PROPOSED — REQUIRES C0/USER APPROVAL**

This C1 revision incorporates R1's required cross-contract overrides without approving I1. The future product route remains inactive, Continue remains visible-disabled, Block 3 remains unregistered, and this document authorizes no implementation or execution.
