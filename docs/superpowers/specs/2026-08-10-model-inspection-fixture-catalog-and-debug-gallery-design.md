# Model Inspection Fixture Catalogue and Debug Gallery Design

| Metadata | Value |
|---|---|
| Status | Approved for implementation planning |
| Date | 2026-08-10 |
| Branch | `test/model-inspection-completeness-gate` |
| Verified base commit | `fdbf1cfba54d77fcb6d6a79ef41ac25971450780` |
| Product boundary | Model Inspection deterministic fixtures, Debug-only gallery, and fixture verification |
| Runtime boundary | Synthetic fixture sessions only; the existing x64 GGUF worker journey remains separate |
| Related visual design | `docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md` |
| Related completion roadmap | `docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md` |

---

## 1. Outcome

Model Inspection will have one canonical, reviewable fixture catalogue that
drives both automated verification and a Debug-only in-app fixture gallery.
The catalogue will cover every screen and meaningful interaction currently
representable by the Model Inspection UI contract, including progress,
terminal outcomes, disclosure pairs, cancellation, retry, stale-event
rejection, targeted operational failures and approved layout/accessibility
stress cases.

Every fixture will be a versioned JSON descriptor with an independently
declared expected screen contract. A fixture that targets a failure or
lifecycle hazard must name that condition in its filename. The build will fail
when required coverage is absent or when a descriptor, filename, transition or
observed screen disagrees with its contract.

The gallery will render the real Model Inspection page, presentation factory,
render coordinator and controls. It will not duplicate the UI in a mock screen
and will not start a worker, read a GGUF file, access the network or execute a
future product action.

## 2. Verified baseline and problem

The current tree already provides:

- the 13-state `ModelInspectionFigmaState` inventory;
- deterministic synthetic presentation construction in test code;
- loaded WinUI semantic and geometry coverage for all 13 states;
- four collapsed/expanded disclosure pairs;
- the five ordered progress stages;
- page lifetime, retry, cancellation, stale-callback, announcement and motion
  tests;
- one real end-to-end GGUF fixture, N-001, which reaches `Ready` through the
  production worker path.

The current tree does not provide one operator-loadable catalogue. Synthetic
states are assembled in C# test helpers, quick-scan/parser fixtures do not
represent screens, and there is no Debug gallery. The classifier currently
emits only `Ready` and `ReadyWithWarnings`; other valid presentation outcomes
are therefore constructible in tests but not selectable with checked-in GGUF
files. There is also no real GGUF fixture that deterministically produces
`ReadyWithWarnings`.

This design closes the deterministic fixture and inspection gap. It does not
pretend that every synthetic presentation is a production classifier outcome
or a real worker journey.

## 3. Goals

1. Give developers and designers a visible Debug-only `Fixture gallery`
   button from which every scenario can be inspected without a model file.
2. Replace scattered synthetic setup with one canonical JSON catalogue shared
   by the gallery and automated tests.
3. Make the targeted screen, failure or lifecycle hazard obvious from every
   fixture filename.
4. Require expected outputs that are independent of fixture inputs, so a
   production mapping defect cannot make both input and expectation wrong in
   the same way.
5. Exercise the real page, presentation factory, coordinator, controls,
   disclosure behavior, focus and accessibility semantics.
6. Fail closed on malformed, incomplete, unsafe, ambiguous or stale fixture
   data.
7. Keep all fixture assets, gallery UI, synthetic services and synthetic
   commands out of Release application builds and packages.
8. Preserve N-001 as the separate real-worker boundary.

## 4. Non-goals and explicit nonclaims

- The catalogue does not change classifier policy or make currently
  unreachable outcomes reachable in production.
- It does not add or modify the worker protocol, GGUF parser, runtime policy or
  native execution.
- It does not create GGUF files for arbitrary terminal states.
- It does not execute conversion, Hardware Fit, report export, chat,
  configuration or any other `Coming later` action.
- It does not introduce an application-wide localization system. The current
  default-English presentation remains the initial fixture copy contract.
- It does not use real user files, paths, model contents or identities.
- It does not fabricate the missing 13 exact Figma node-export PNGs,
  reference manifest, masks or strict pixel evidence.
- Theme, text-scale and motion presets are labelled simulations. They are not
  evidence of real OS High Contrast, real 200% text scale, Narrator or a
  controlled rasterizer campaign.
- "Complete" means complete for the current finite UI and lifecycle contract,
  not a combinatorial product of every arbitrary field value.

## 5. Chosen approach

### 5.1 Decision

Use a JSON descriptor catalogue, a shared non-UI loader/validator, a
Debug-only adapter and gallery, and automated tests that consume the same
descriptors.

This was selected because it is human-reviewable, satisfies filename-based
failure discovery, provides one source for manual and automated inspection,
and keeps expected outputs independent from production presentation logic.

### 5.2 Alternatives not selected

**C#-only scenarios** would provide compile-time typing but would be harder to
review, would not satisfy the filename requirement cleanly, and would keep the
catalogue coupled to test helper code.

**A separate fixture application** would isolate fixtures strongly but would
duplicate shell/navigation behavior and make the catalogue less convenient to
open while visually reviewing the real app.

## 6. Architecture

### 6.1 Components

The feature has six bounded components:

1. **Fixture descriptors** are the canonical JSON files under
   `tests/TestFixtures/ModelInspectionScenarios/`. The adjacent authoritative
   files are `model-inspection-fixture.schema.json` and
   `model-inspection-fixture-coverage-policy.json`.
2. **Catalogue loader and validator** form a small UI-independent Debug
   component. Consumers provide streams or a directory; the component performs
   no package discovery, UI work or product I/O. It is compiled into the Debug
   app and Debug packaged-test campaign, not referenced by the Release app.
3. **Fixture adapter** exists only in Debug x64 application builds. It maps a
   validated descriptor into existing Model Inspection request, progress,
   execution-result and command contracts.
4. **Fixture session** provides a deterministic injected
   `IModelInspectionService`. It emits only the descriptor's declared events
   and owns cancellation/retry barriers. It never starts the worker.
5. **Fixture gallery** is a Debug-only WinUI page. Its Frame navigates a
   Debug-only host page, which creates and owns one real injected
   `ModelInspectionPage`.
6. **Screen contract observer** reads the presentation and loaded visual tree
   without consulting the descriptor input. It returns the actual semantic,
   interaction and accessibility contract for comparison with `expected`.

Each component has one direction of dependency:

```text
JSON descriptors
  -> non-UI loader/validator
  -> Debug fixture adapter/session
  -> existing ModelInspectionPage and production presentation pipeline
  -> independent screen contract observer
  -> expected-contract comparison
```

Production presentation code never depends on fixture code.

### 6.2 Existing seams reused

The design uses existing seams rather than adding a second renderer:

- `ModelInspectionPage(IModelInspectionService)` for a controlled service;
- `ModelInspectionViewSnapshot` and `ModelInspectionRenderCoordinator` for
  keyed semantic updates and stale-event rejection;
- `ModelInspectionPresentationFactory.Create(...)` for screen mapping;
- the existing outcome, model, content, disclosure and action controls;
- existing page navigation retirement and `ChooseAnotherModelRequested`;
- injected motion settings and animation-driver seams for normal/reduced
  motion fixture behavior.

`Frame.Navigate(typeof(ModelInspectionPage), ...)` is not used for fixtures:
WinUI would invoke the public parameterless constructor and compose the
production service. Instead, implementation extracts the request/coordinator
activation body from `OnNavigatedTo` into one private page-owned
`ActivateRequest(...)` path. Production `OnNavigatedTo` calls that path
unchanged. A Debug-only `CreateForFixture(...)` factory constructs the page
through its injected-service constructor and calls the same activation path
exactly once. The gallery Frame navigates only the Debug fixture host page,
which places that already injected real page in its content.

The Debug factory requires a `DebugModelInspectionService` instance and has no
fallback to the public production constructor. Tests prove fixture selection
causes zero production service composition, worker creation and worker process
starts. Production `OnNavigatedFrom` and a Debug-only `RetireForFixture()` seam
both call one page-owned `RetirePageLifetime()` path. Before replacing or
unloading the nested page, the fixture host calls `RetireForFixture()`.
Host navigation-away and `Unloaded` share an idempotent exactly-once guard, so
activation and retirement each occur once for one fixture page even when both
host callbacks fire.

The gallery must not directly assign four card presentations as a shortcut.
A scenario is valid only when it can travel through the page-owned render
lifetime used by production.

### 6.3 Debug and Release boundary

The gallery is available only when both conditions are true:

- configuration is `Debug`;
- platform is `x64`.

A dedicated build constant owns that boundary. Debug-only source, XAML,
project references and JSON `Content` items are conditionally included. The
ordinary Release app project graph and package must contain none of the
following:

- fixture gallery page or navigation entry;
- fixture JSON or schema assets;
- fixture adapter or fixture session;
- synthetic command/service path;
- fixture-specific labels, resource keys or package files.

The Release onboarding shell XAML and visual tree remain unchanged by this
feature. The complete entry surface, including any host element and the
visible `Fixture gallery` button, is conditionally compiled only inside the
Debug x64 boundary. Contract tests inspect both project evaluation and built
package contents. A source-only `#if` around a button while shipping a host,
fixture assets or XAML is insufficient.

## 7. Descriptor and filename contract

### 7.1 Filename

Every scenario filename has this exact shape:

```text
MI-<three-digit-stable-id>-<target-condition>[-<variant>].fixture.json
```

Rules:

- IDs are unique and never reused for a different scenario.
- `MI-` is the fixed uppercase prefix; every slug after `MI-NNN-` uses
  lowercase ASCII kebab case.
- `id` inside the document exactly matches the filename ID.
- `targetCondition` exactly matches a contiguous filename slug.
- When present, `variant` also exactly matches a contiguous filename slug.
- A failure fixture names the targeted failure, not a generic `error`.
- A lifecycle fixture names the rejected or retired event.
- Renaming an established ID or target condition is a reviewed contract
  change, not an incidental cleanup.

The parser removes the exact `.fixture.json` suffix, reads the uppercase
`MI-NNN` token, and then matches the remaining lowercase slug against
`targetCondition` plus optional `variant`. No case folding, alternative prefix,
extra suffix or inferred word split is accepted.

Examples:

```text
MI-001-inspection-progress-initial.fixture.json
MI-008-incomplete-package-missing-tokenizer.fixture.json
MI-040-operational-failure-worker-timeout.fixture.json
MI-035-retry-stale-result-rejected.fixture.json
```

### 7.2 Schema

The catalogue has a versioned JSON Schema for tooling and an authoritative
fail-closed .NET validator. `System.Text.Json` uses strict unmapped-member,
enum, nullability, length and numeric-bound handling. The loader rejects an
unknown `schemaVersion`; it does not silently upgrade or repair documents.

Every fixture declares these top-level fields:

| Field | Purpose |
|---|---|
| `$schema` | Repository-relative schema reference |
| `schemaVersion` | Exact supported schema version |
| `id` | Stable `MI-NNN` identity |
| `targetCondition` | Screen, failure or lifecycle slug required in filename |
| `variant` | Optional paired/stress variant, also required in filename |
| `title` | Short gallery label |
| `category` | `screen`, `progress`, `lifecycle`, `failure` or `stress` |
| `coverage` | Required state/stage/outcome/interaction/stress tags |
| `input` | Synthetic request, snapshot, progress, result/failure and event sequence |
| `expected` | Independent complete screen contract |
| `presetExpectations` | Required per-preset layout/accessibility checks and overrides |
| `interactions` | Allowed user action and expected next fixture/step |
| `presets` | Required visual environment presets |

Unknown properties are errors at every object depth. A target condition may be
shared only by explicitly linked variants, such as collapsed and expanded
members of one disclosure owner.

### 7.3 Input contract

`input` may contain only values needed by existing application contracts:

- an invented safe model display name and relative display filename;
- validated quick-scan metadata;
- an initial snapshot;
- ordered progress events;
- one completed outcome, cancellation or operational failure;
- retry-attempt event sequences;
- deterministic command availability;
- barriers that release declared stale events after retry or retirement.

It may not contain an absolute path, URI, environment identity, secret,
machine/user name, arbitrary code, raw GGUF bytes, network location, control
characters or bidirectional formatting characters.

`ModelInspectionRequest` still requires an absolute validated path. The Debug
adapter constructs that value from a fixed, non-user, non-environment fixture
root plus the validated relative display filename. That synthetic path never
comes from JSON, is never opened, logged or displayed, and cannot be selected
by the user.

### 7.4 Expected screen contract

`expected` is mandatory and is never generated from `input`. It declares:

- exact `ModelInspectionFigmaState` and geometry profile;
- visibility and ownership of outcome, model, content and action regions;
- outcome kind, badge, title and supporting copy;
- model fields, status chip, checks and disclosure state;
- content mode, findings/report/progress rows and scroll ownership;
- action order, label, visibility, enabled state and accessible help;
- five inspection-stage content rows and current aggregate shell-bound footer status;
- expected focus target;
- accessible names, control types and live-region settings;
- announcement text and exact announcement count;
- stable row/control identities where the lifecycle requires retention.

`presetExpectations` is mandatory for every preset selected by the coverage
policy. It does not replace `expected`; it declares the exact
environment-specific checks or overrides described in section 8.5.

Copy expectations contain both a stable `copyKey` and approved default-English
text. `copyKey` uses a production resource identifier when one exists;
otherwise it is a fixture contract key mapped independently to the current
presentation copy. The initial gallery renders the application's current
default-English presentation. A later localization change must add locale
resolution without weakening the canonical default-English contract.

### 7.5 Interaction contract

An interaction entry names exactly one supported action:

- expand or collapse the current disclosure;
- Cancel;
- Retry;
- Restart after cancellation;
- Choose another model;
- Reset the fixture.

It points to an expected fixture/step identity and declares expected focus,
announcement, footer and lifetime effects. Actions omitted from the descriptor
are unavailable. Disabled `Coming later` controls remain visible where the
production presentation requires them, but are never executable.

## 8. Required catalogue coverage

Completeness is defined by a versioned coverage policy, not by an unexplained
test count. Tests enumerate descriptors and production enums, then require the
following matrix.

### 8.1 Canonical screen fixtures

All 13 Figma states are present as directly loadable fixtures:

| State | Required target condition |
|---|---|
| 01 Inspection progress | initial waiting state |
| 02 Ready collapsed | clean compatible model |
| 03 Ready expanded | clean compatible model details |
| 04 Ready with warnings collapsed | exact approved `MI-WARN-CHAT-TEMPLATE-MISSING` warning |
| 05 Ready with warnings expanded | details for that same approved warning |
| 06 Conversion required collapsed | verified incompatible conversion route |
| 07 Conversion required expanded | expected conversion output details |
| 08 Incomplete package | missing tokenizer/package member |
| 09 Unsupported | unsupported model architecture |
| 10 Invalid collapsed | corrupt or contradictory validated model evidence |
| 11 Invalid expanded | technical report for the same invalid evidence |
| 12 Cancelled | user-requested cooperative cancellation |
| 13 Operational failure | worker unavailable/start failure |

Collapsed/expanded members of a pair use the same outcome owner and retained
rows. Their interaction entries point to each other. Expanded Figma states are
disclosure variants of the same outcome, not invented outcome enum values.

### 8.2 Progress fixtures

State 01 additionally requires:

- one initial `0 of 5` fixture with all rows waiting;
- one `Active` and one `Completed` fixture for each exact stage:
  `CheckModelPackage`, `ReadModelConfiguration`,
  `ValidateTokenizerAndChatSetup`, `ValidateModelStructure`, and
  `ConfirmCoreRuntimeCompatibility`;
- one cancellation-requested fixture with Cancel disabled and no fabricated
  terminal state;
- representative valid `Warning`, `Failed` and `Cancelled` stage-status
  sequences, each followed only by its truthful permitted terminal path;
- one fractionless active update and one bounded fractional active update,
  proving that fraction-only changes update progress without repeating a
  polite announcement.

The expected rows must encode the truthful completed count and allow at most
one active stage.

### 8.3 Lifecycle fixtures

The catalogue requires deterministic sequences for:

- cancellation requested to cooperative cancellation;
- forced or unconfirmed cancellation mapped to OperationalFailure rather than
  the forbidden non-cooperative Cancelled result;
- retry after cancellation;
- retry after operational failure;
- stale progress rejected after retry;
- stale result rejected after retry;
- stale motion completion rejected after retry/retirement;
- stale announcement rejected after retry/retirement;
- Choose another model retiring the current page;
- switching gallery fixtures retiring the old fixture session.

Each targeted hazard appears in its filename.

### 8.4 Failure variants

At minimum, operational-failure coverage distinguishes:

- worker unavailable/start failure;
- worker timeout;
- worker crash or early exit;
- malformed/invalid worker response.

Outcome validation also includes missing-package-member, unsupported
architecture, corrupt/contradictory model evidence and missing optional
metadata variants. These are synthetic presentation inputs and do not broaden
production classifier policy. `ReadyWithWarnings` uses exactly one approved
`MI-WARN-CHAT-TEMPLATE-MISSING` finding; arbitrary warning codes, counts or copy
are rejected rather than coerced into states 04/05.

### 8.5 Stress coverage

Data-changing stress descriptors cover:

- a maximum-length safe model display name;
- missing optional metadata with truthful `Not reported` output;
- the maximum approved bounded finding/report/check rows and scrolling;
- long but bounded copy-keyed detail text.

Unsafe-path, identity, control-character and bidi cases are generated only as
ephemeral negative mutations. They must fail validation and are never retained
as gallery data or echoed in diagnostics.

Environment presets cover:

- desktop, medium and narrow widths;
- Light and Dark resources;
- High-Contrast resource preview;
- 100% and 200% text preview;
- normal and reduced motion.

The policy defines a curated pairwise matrix plus mandatory canonical desktop
checks. It does not run a meaningless full Cartesian product. Any new Figma
state, outcome, progress stage, supported interaction or preset must update the
policy and add coverage in the same change.

Each required preset has substantive acceptance checks in
`presetExpectations`:

- expected responsive layout profile and content-column bounds;
- no clipped, overlapping or unreachable required content;
- declared wrapping versus truncation for every long-text role;
- correct bounded-scroll owner and preserved row identity;
- at least 44 by 44 effective pointer targets for interactive controls;
- unchanged logical reading/tab order and valid focus target;
- resolved Light, Dark or High-Contrast-preview semantic brushes without
  meaning that depends on color alone;
- at 200% preview, natural reflow with no fixed-height text clipping;
- in reduced motion, zero animation starts and final geometry/semantics equal
  to normal motion after completion.

Width and text assertions use the existing approved responsive breakpoints and
geometry profiles. A preset that is merely selectable or visibly labelled but
does not satisfy these observations fails the fixture gate.

## 9. Debug gallery behavior

### 9.1 Entry and layout

The Debug onboarding shell displays a visible `Fixture gallery` button. The
gallery uses a split layout:

- a searchable/filterable scenario list on the left;
- the real Model Inspection surface on the right;
- a header showing filename, stable ID, targeted condition and category;
- a fixed `Synthetic fixture` provenance badge and, where verified, a
  separate read-only `Real-worker coverage: N-001` link;
- preset controls for width, resources, text and motion;
- an interaction panel showing only actions declared by the fixture;
- Reset, which creates a fresh fixture lifetime.

There is no arbitrary file picker. Only the canonical Debug catalogue packaged
as application resources can be loaded. Reading those fixed package resources
is permitted; user-selected, model, network, temporary and external filesystem
access is forbidden.

### 9.2 Session flow

Selecting a fixture performs this sequence:

1. retire and dispose the prior page and fixture session;
2. validate the selected descriptor again at the trust boundary;
3. create a fresh request, attempt identity, deterministic service and motion
   settings;
4. navigate a new Debug fixture host in the gallery Frame and let its
   `CreateForFixture(...)` factory install the injected real
   `ModelInspectionPage` through the shared activation path;
5. release the fixture's declared service events through dispatcher barriers;
6. compare the observed loaded screen with `expected` in development builds;
7. show a concise pass/failure diagnostic beside the filename.

Switching fixtures must detach callbacks, cancel motion, clear retained
outgoing layers and reject every callback owned by the old lifetime. A fixture
may not reuse another fixture's ViewModel, coordinator, service or commands.

### 9.3 Safe interactions

Expand/collapse, Cancel, Retry, Restart and Choose another use the page's real
interaction path. The deterministic service and gallery host provide the
declared results. No interaction opens a file, starts a process, writes a
report, invokes a future feature or changes the production shell state.

The preset labels explicitly include `Preview` for High Contrast and 200% text
scale. The gallery does not display a compliance badge or imply controlled OS
evidence.

## 10. Validation and error handling

Catalogue loading is atomic. One invalid descriptor invalidates the complete
catalogue and the gallery renders no scenario. A diagnostic identifies the
filename, JSON path and violated rule without echoing unsafe input.

The loader rejects:

- duplicate IDs or filenames, and unpaired duplicate target conditions;
- filename/document identity disagreement;
- missing required coverage;
- unknown fields, versions, enum values or actions;
- contradictory stage/status/completed-count combinations;
- a progress and terminal result that are both current;
- invalid disclosure ownership or transition targets;
- an expectation copied or derived at runtime from input;
- unsafe, absolute, identity-bearing or unbounded text;
- duplicate JSON object properties, including escaped duplicates;
- non-finite or out-of-range numeric values.

An unsupported interaction is a deterministic error and produces no state
change. A stale event is recorded as rejected evidence and cannot update the
screen, focus, footer, announcement count or gallery selection.

There is no fallback scenario, best-effort parsing or silent defaulting.

## 11. Verification strategy

Implementation follows test-driven development. The permanent gates are:

### 11.1 Catalogue contracts

- JSON schema and strict loader tests;
- filename/ID/target-condition tests;
- duplicate-property and privacy mutation tests;
- exact coverage-policy tests against production enum inventories;
- copy-key/default-English consistency tests;
- Debug inclusion and Release exclusion project/package tests.

### 11.2 Mapping and screen contracts

For every descriptor:

- construct the input without consulting `expected`;
- run the real presentation factory and coordinator;
- load the real WinUI controls;
- independently observe the screen;
- compare every declared expected field;
- verify stable control/row identity where required.

A mutation that changes one input mapping, visible region, action, accessible
name, announcement count or footer status must fail at least one fixture.

### 11.3 Interaction and lifetime tests

- all four disclosure round trips;
- Cancel, Retry, Restart and Choose another;
- reset and fixture-to-fixture switching;
- host back/forward replacement, gallery close and window/app close;
- selecting an invalid fixture after a valid page is active, proving the
  active page retires before the atomic catalogue error is shown;
- stale progress/result/motion/announcement callbacks;
- focus retention and retirement;
- exactly one shared inner-page retirement on every exit route, leaving no
  service lifetime, dispatcher callback, motion batch, disclosure operation,
  focus request or live-region callback;
- simultaneous navigation-away and Unloaded signals, proving the idempotent
  guard calls `RetireForFixture()` exactly once;
- normal/reduced-motion final-state equivalence;
- no duplicate announcements on hide/show or fraction-only progress.

### 11.4 Gallery tests

A serialized Debug x64 packaged campaign verifies:

- visible gallery entry;
- search and category filtering;
- filename and target-condition display;
- fixture selection and atomic failure UI;
- preset application and honest preview labels;
- every required preset expectation, including reflow, clipping, scrolling,
  target size, focus/order, brush resolution and reduced-motion parity;
- Reset and clean retirement;
- host navigation, gallery close and window close each retiring the nested
  page exactly once;
- active-page replacement by a catalogue-validation failure, plus a
  navigation-away/Unloaded double signal, with exact-one retirement evidence;
- zero worker/fixture-process launches and zero user/model/external
  filesystem or network access; fixed application-package resource reads are
  the only allowed fixture I/O.

### 11.5 Regression boundaries

- the ordinary Release packaged suite remains green;
- the N-001 real GGUF worker journey remains green and separate;
- current presentation, layout, motion, accessibility, workflow, privacy and
  cleanup contracts remain green;
- source/inventory ledgers include every new durable file;
- Release build/package inspection finds zero fixture/gallery artifacts.

## 12. Coverage report

Tests generate
`docs/evidence/testing/Model-Inspection-Fixture-Catalog.md` deterministically
from the validated descriptors plus a contract-verified external-evidence
join. The checked-in report must byte-match the generator output and contains,
in stable ID order:

- filename and ID;
- targeted condition;
- category and coverage tags;
- initial and expected screen state;
- supported interactions and expected destinations;
- required presets;
- automated semantic/render/lifetime verification status;
- whether separately verified real-worker coverage exists for the scenario.

Every gallery-loadable descriptor remains a `synthetic deterministic fixture`.
The coverage policy may attach `externalEvidenceLinks` only when a contract
test proves the named real fixture and journey test exist and still assert the
claimed state. N-001 is the only current real-worker source and is linked to
the Ready collapsed/expanded fixtures; N-001 itself is not copied into JSON or
made gallery-loadable. The report must not imply that synthetic
ReadyWithWarnings, ConversionRequired, IncompletePackage, Unsupported or
Invalid descriptors were classified by a real GGUF worker.

## 13. Definition of done

The feature is complete when:

1. the required catalogue matrix is present and every filename follows the
   targeted-condition contract;
2. all descriptors pass strict schema, semantic, privacy and completeness
   validation;
3. every fixture's observed loaded screen matches its independent expected
   contract;
4. every declared interaction and lifecycle sequence passes;
5. the Debug x64 gallery exposes and cleanly switches all fixtures;
6. gallery presets work and retain their explicit simulation labels;
7. Release project/package checks prove complete fixture isolation;
8. N-001 and all relevant regressions remain green;
9. the generated coverage report is complete and truthful;
10. no exact Figma PNG, controlled OS or Narrator evidence is fabricated or
    claimed.

The unavailable strict Figma reference bundle remains a separate blocker. The
stable `MI-NNN` identities provide the future join key for authentic node
exports, manifests and pixel evidence when those inputs become available.
