# Model Inspection Figma Fidelity and Motion Design

| Metadata | Value |
|---|---|
| Status | Approved specification; implementation plan ready |
| Date | 2026-08-09 |
| Branch | `test/model-inspection-completeness-gate` |
| Verified base commit | `e5e3f6cfaa0744fab32aa57eafca750faf1f1876` |
| Product boundary | Model Inspection presentation, disclosure, motion, accessibility and visual verification |
| Runtime boundary | Existing x64 GGUF, CPU-only, LLamaSharp/llama.cpp VocabOnly inspection path |
| Figma file | `gAmBX1DYh71hqxHVqiivus` (`Granite Edge AI`) |
| Figma board | `142:2148` (`model-inspection-complete-ordered-board-v2 1`) |
| Supplied SVG evidence | 4624 x 5836 flattened board; SHA-256 `8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB` |
| Implementation plan | `docs/superpowers/plans/2026-08-09-model-inspection-figma-fidelity-and-motion.md` |

---

## 1. Executive summary

The connected Model Inspection journey is functionally complete for the scoped
x64 GGUF llama.cpp path, but its current presentation does not match the
approved Figma board and its stage changes can appear abrupt or laggy.

This design replaces the current snapshot-heavy presentation behavior with a
Figma-faithful, state-driven WinUI presentation system. It preserves the
existing worker, process containment, application service, evidence mapping,
classification, retry, cancellation and navigation behavior.

The completed work will:

- reproduce all 13 Figma Model Inspection frames;
- expose the four designed collapsed/expanded disclosure pairs;
- show truthful model and inspection evidence instead of hard-coded sample
  metadata;
- retain one stable visual tree while inspection progress changes;
- coalesce redundant UI refreshes and update only changed sections;
- add short composited transitions without delaying or fabricating worker
  progress;
- honor Windows reduced-motion, high-contrast and text-scaling settings;
- preserve keyboard, Narrator, focus, privacy and stale-attempt guarantees;
- keep unimplemented downstream actions visible in their Figma positions but
  disabled with clear `Coming later` help text.

The design deliberately does not add OpenVINO, TurboQuant, Vulkan, GPU,
full inference, context creation, conversion execution, Hardware Fit,
performance benchmarking or quality benchmarking.

---

## 2. Verified current-state findings

The redesign is based on direct inspection of the current application, Figma
nodes and the user-supplied SVG.

### 2.1 The missing details control already exists but is unreachable

The current control set already contains:

- `InspectionModelCard` detailed mode and `InspectionDetailsExpander`;
- `InspectionContentCard` findings/details `Expander`;
- inspection-check, finding, report and progress-row templates;
- outcome and action-card variants.

The current presentation factory nevertheless forces every model card into
compact mode and does not populate detailed model fields, inspection checks,
disclosure summaries or expanded rows. Ready content is explicitly hidden.
Consequently the user cannot reach the details presentation visible in Figma.

The control implementation also resets expansion whenever a new presentation
object is assigned, so merely enabling detailed mode would not be sufficient.

### 2.2 The lag is presentation churn, not worker computation alone

The page currently rebuilds and reassigns all four card presentations for each
ViewModel property or command-state notification. A run start can produce four
full refreshes and a terminal result can produce five. Every progress message
creates a new five-row collection and causes synchronous binding, template and
layout work inside a `ScrollViewer`/`StackPanel` hierarchy.

The worker truthfully emits some later stages in quick succession. Those
events are individually posted to the UI queue, where they can accumulate
behind repeated full-page XAML work. The resulting visual catch-up can look
like a worker delay even though the primary defect is presentation churn.

### 2.3 No motion system exists today

Current visual-state changes use transitions disabled. The page and controls
contain no inspection-specific `VisualTransition`, storyboard, composition
animation or shared motion token. Visibility and height changes therefore
happen immediately and force abrupt remeasurement.

### 2.4 The Figma source is an ordered 13-state desktop system

The supplied SVG contains 13 exact 1440 x 1024 frames on a 4624 x 5836 board.
It is fully flattened into vector paths, so it preserves geometry and colors
but not font or motion metadata. Direct Figma design context confirms the type
family is Inter with Regular and Bold faces.

The Figma/SVG pair provides state geometry but no authoritative motion timing
or easing. Motion values in this specification are therefore application
tokens chosen for a smooth WinUI implementation; they are not attributed to
Figma.

---

## 3. Scope and non-scope

### 3.1 In scope

- The Model Inspection page at standard desktop size and responsive sizes.
- All 13 Figma visual states.
- Ready, warning, conversion and invalid disclosure interactions.
- Existing live five-stage progress.
- Existing Cancel, Retry and Choose another model behavior.
- Visible but disabled downstream buttons where their features do not exist.
- Presentation mapping from existing validated request, progress, result,
  evidence and finding contracts.
- Render coalescing and stable child identity.
- WinUI motion and reduced-motion equivalence.
- Keyboard, focus, Narrator, high contrast and text scaling.
- Automated semantic, layout, visual-regression and packaged journey tests.
- Documentation, workflow test registration, cleanup inventory and test floors
  required by changed scope.

### 3.2 Explicitly out of scope

- OpenVINO or OpenVINO GenAI execution.
- TurboQuant execution or evaluation.
- Vulkan, CUDA, GPU offload or non-CPU backend work.
- Full model inference, context creation or prompt execution.
- Performance, memory, throughput, latency or quality benchmarking.
- Hardware Fit implementation.
- Conversion execution or conversion-format selection.
- Technical-report file export.
- Chat, Configure or Ready-to-chat implementation.
- New model-format picker support.
- New worker protocol or native runtime policy.
- Synthetic progress, artificial worker delays or guessed evidence.

### 3.3 Downstream-action policy

The Figma layout remains visually intact. Buttons for Hardware Fit, conversion
execution/selection and technical-report export remain in their designed
positions but are disabled. Their accessible help text states that the feature
is coming later. A disabled downstream button must never silently no-op.

This does not disable the four approved inline disclosure pairs. In particular,
state 10's `View technical report` disclosure opens state 11's bounded,
privacy-safe inline diagnostic rows. Only a separate full-report/export action
remains downstream.

Existing working actions remain active:

- Cancel inspection while a run is active;
- Retry inspection after cancellation or operational failure;
- Restart inspection after trusted cancellation;
- Choose another model;
- Locate missing file may reuse the existing return-to-model-selection route;
- expand or collapse available details.

---

## 4. Authoritative Figma state inventory

| # | Figma node | State | Disclosure | Action policy |
|---:|---|---|---|---|
| 01 | `142:2151` | Inspection progress shell (initial, running or cancel-requested) | none | Cancel active only while the run is active and cancellation has not already been requested |
| 02 | `142:2213` | Ready | collapsed | Choose another active; report and Hardware Fit disabled |
| 03 | `142:2280` | Ready - inspection details expanded | expanded | same actions as 02 |
| 04 | `142:2403` | Ready with warnings | collapsed | Choose another active; report and Hardware Fit disabled |
| 05 | `142:2476` | Ready with warnings - full details expanded | expanded | same actions as 04 |
| 06 | `142:2599` | Conversion required | collapsed | conversion actions disabled |
| 07 | `142:2664` | Conversion required - expected output expanded | expanded | conversion actions disabled |
| 08 | `142:2787` | Incomplete package | none | Locate missing file returns to selection; technical-details action disabled |
| 09 | `142:2851` | Unsupported model | none | Choose another active; technical-details action disabled |
| 10 | `142:2910` | Invalid model | report collapsed | Choose another active; report export disabled |
| 11 | `142:2973` | Invalid model - technical report expanded | report expanded | same actions as 10 |
| 12 | `142:3096` | Inspection cancelled | none | Choose another and Restart active |
| 13 | `142:3154` | Inspection operational failure | none | Retry and Choose another active; full-report action disabled |

Every state is directly constructible and testable. Production may select a
state only when existing validated evidence or an existing trusted execution
terminal proves it. The presentation layer does not broaden classifier policy.
Unknown, contradictory or incomplete data fails closed to the existing
operational-failure path rather than selecting a model outcome speculatively.

---

## 5. Figma layout and design tokens

### 5.1 Standard desktop canvas

The visual-regression reference size is a 1440 x 1024 page client area at 100%
display and text scale in Light theme.

At that size:

- the main content column is exactly 840 px wide;
- the column is centered at x = 300 through x = 1140;
- the five footer step centers are x = 360, 540, 720, 900 and 1080;
- footer centers are separated by 180 px;
- footer status nodes are 36 x 36;
- standard buttons are 46 px high;
- parent cards use 24 px horizontal inner insets for 792 px nested rows;
- the standard action surface is 840 x 140;
- collapsed nested finding/check rows are normally 72 px high;
- compact rows in expanded disclosure areas are normally 58 px high.

Frame-specific vertical geometry follows the Figma nodes. The important
collapsed-to-expanded deltas are:

| Pair | Banner | Main details card | Action surface |
|---|---|---|---|
| Ready 02 -> 03 | y 160 -> 144 | y 258, h 304 -> y 242, h 470 | y 592 -> 734 |
| Warnings 04 -> 05 | y 160 -> 144 | y 368, h 232 -> y 352, h 380 | y 624 -> 754 |
| Conversion 06 -> 07 | y 160 -> 144 | y 368, h 232 -> y 352, h 365 | y 624 -> 739 |
| Invalid 10 -> 11 | y 160 -> 144 | y 368, h 248 -> y 352, h 380 | y 640 -> 754 |

Expanded detail lists use a bounded scroll viewport, a visible right-side
scrollbar when needed, a bottom fade and `Scroll for more` affordance. Content
must not expand the page indefinitely.

### 5.2 Color tokens

One Model Inspection resource dictionary owns the palette. Controls do not
duplicate literal colors.

| Purpose | Values |
|---|---|
| Primary text | `#101828` |
| Secondary text | `#344054`, `#475467`, `#667085` |
| Muted text | `#7A8797`, `#98A2B3` |
| Primary blue | `#0F62FE` |
| Blue surface/border | `#EEF5FF`, `#BDD3FF`, `#C9D9F2` |
| Success surface/text/border | `#E9F7F1`, `#067A57`, `#0B3B2F`, `#A5D8C4` |
| Warning surface/text/border | `#FFF6E0`, `#B7791F`, `#9A6700`, `#604200`, `#EEC86F` |
| Error surface/text/border | `#FFF0EF`, `#B42318`, `#7A271A`, `#EDB3AD` |
| Neutral surfaces | `#FFFFFF`, `#F7F9FC`, `#F8FAFC` |
| Borders | `#E8EDF3`, `#C9D5E3`, `#D7E0EA`, `#BFC9D6` |

System high-contrast brushes supersede this palette when high contrast is
active. Meaning never depends on color alone.

### 5.3 Typography

Inter is packaged with the application under its approved open-source license.
Only required faces are included. A central font-family resource is used by
the inspection subtree.

| Role | Size | Weight |
|---|---:|---:|
| Page title | 32 px | 700 |
| Section/model title | 18 px | 700 |
| Body and button text | 14 px | 400 or 700 by Figma role |
| Helper and metadata text | 12 px | 400 |
| Uppercase chips/footer labels | 10 px | 700 |

Text uses natural wrapping at increased text scale. No fixed card height may
clip required text. Standard-scale visual fidelity and accessibility-scale
layout are separate acceptance modes.

---

## 6. Presentation architecture

### 6.1 Stable page shell

`ModelInspectionPage` retains one visual tree for the lifetime of one
navigation attempt:

```text
ModelInspectionPage
  Header
  Figma 840px content host
    Outcome region
    Model overview region
    Inspection content/disclosure region
    Action region
  Onboarding step footer
```

Existing controls and row templates are reused where they match the Figma
structure, but their layout, token use and state wiring may be refactored. The
implementation must not replace the page with a single custom-drawn canvas.

The four high-level regions remain accessible XAML controls. Their instances
are stable across progress changes. Visibility, content and visual state
change in place.

### 6.2 Screen state

One internal presentation type describes the complete screen:

- immutable render key;
- visual state identity;
- outcome banner presentation;
- model overview fields and status chip;
- progress/findings/report content;
- disclosure availability, summary and expansion state;
- action labels, commands, enabled state and accessible help;
- footer status;
- live-region announcement text.

The presentation factory remains deterministic and side-effect free, but it is
part of the WinUI presentation layer. Existing DTOs may continue to use small
WinUI value types such as `Visibility` and `Symbol`. The factory must not create
XAML controls, access a Dispatcher, perform I/O or inspect
worker/protocol/native types. The immutable ViewModel snapshot in the next
section remains UI-independent and contains only application contract values.

### 6.3 Observable view snapshot and render key

The page cannot safely own stale-attempt suppression by reading separate,
unversioned ViewModel properties. The ViewModel therefore exposes one internal,
UI-independent immutable view snapshot containing:

- `AttemptGeneration`;
- `PresentationRevision`;
- active/cancellation-requested state;
- current progress or terminal result;
- the invariant that progress and terminal result are never both current.

`AttemptGeneration` is the existing private, monotonically increasing attempt
identity surfaced only through this application snapshot. It is not a worker
request ID and is never presented, logged or serialized. It increments when a
new attempt is published and when lifecycle invalidation must make all prior
callbacks stale.

`PresentationRevision` increments for every accepted semantic change within
the generation, including progress, cancellation-requested state and terminal
retirement. The pair forms an immutable `ModelInspectionRenderKey`.

The ViewModel replaces the complete snapshot under its existing state lock and
then raises one snapshot-change notification. The page reads that one snapshot
and never reconstructs a render from separately sampled `Progress`, `Result`
and `IsRunActive` values.

Every scheduled semantic render, semantic animation completion and live-region
callback captures its render key. It may apply only when all of the following
still match:

- the current page instance and navigation lifetime;
- the ViewModel's current attempt generation;
- the current presentation revision;
- the render coordinator's latest accepted key.

This makes stale suppression deterministic across same-ViewModel Retry as well
as progress-to-terminal transitions within one attempt.

### 6.4 Stable progress rows

The five ordered stage-row objects are created once per attempt and retained:

1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm runtime support

Progress updates mutate the corresponding UI-owned row state or replace only
that row's value through an observable keyed collection. The content host,
template and other four rows remain attached.

At most one stage may be visually active. Cross-field validation between stage
ordinal, stage status and completed count remains enforced before presentation.

### 6.5 Render coordinator

An internal UI-thread render coordinator owns presentation application.

It:

- accepts ViewModel property and command-state notifications;
- schedules at most one pending dispatcher render;
- captures the newest immutable view snapshot and render key;
- rejects stale callbacks before scheduling, before applying and at animation
  or live-region completion;
- compares each high-level region with its last applied value;
- updates only regions that changed;
- retains expansion and focus state when the outcome identity is unchanged;
- retires progress before applying a terminal state;
- never delays the worker, changes a result or invents progress.

If Active and Completed for one stage arrive before the next compositor frame,
the coordinator applies the truthful latest Completed state with its completion
transition. It does not display a fabricated minimum-duration Active state.

Command `CanExecute` changes update only the relevant action presentation.
They do not rebuild the model, content or outcome regions.

### 6.6 Disclosure ownership

Expansion is UI interaction state, not inspection evidence. It is owned by the
current page/attempt and keyed by the current visual outcome.

- New attempt: reset to the Figma collapsed state.
- Progress update within the same attempt/outcome: preserve expansion.
- Repeated Hide -> show of the same terminal state within one attempt: do not
  announce the terminal outcome again.
- Retry: reset expansion before the new run.
- Navigation away: discard expansion with the retired page.
- Outcome identity changes: reset to that outcome's collapsed state.

The presentation assignment path must not unconditionally set `IsExpanded`
to false.

Disclosure interaction has a separate page-owned `InteractionRevision` because
Expand/Collapse can change without a ViewModel snapshot revision. Each toggle
increments that revision, cancels or retargets the prior disclosure animation,
and captures a combined key:

```text
ModelInspectionVisualOperationKey
  = ModelInspectionRenderKey + InteractionRevision
```

Disclosure animation completion may apply only when the page lifetime, render
key and interaction revision all still match. A new attempt, outcome change,
navigation-away or reduced-motion setting change increments the interaction
revision and cancels the prior operation. Rapid Expand -> Collapse and Collapse
-> Expand therefore cannot be overwritten by an older 240 ms completion.

---

## 7. Truthful model and inspection details

Figma sample content illustrates layout; it is not production data.

### 7.1 Model overview mapping

| Figma field | Production source | Missing-value behavior |
|---|---|---|
| Model name | safe display projection of completed configuration name, then quick-scan model name, then final filename without `.gguf` | `Not reported` when every candidate is unavailable or unsafe; never use a full path |
| Publisher | approved evidence only | `Not reported`; never infer IBM from a model name |
| Format | validated configuration/quick-scan format | fail closed if inconsistent |
| Quantisation | validated quick-scan label or an explicitly approved file-type mapping | `Not reported`; never display an unexplained numeric file type as a marketing label |
| Parameters | validated quick-scan label, otherwise formatted evidence parameter count | `Not reported` |
| Model type | approved evidence only | `Not reported`; chat-template presence does not prove instruction tuning |
| Declared context | validated configuration/quick-scan context | `Not reported` |
| File size | completed file evidence length, or validated request length while running | culture-aware B/KB/MB/GB display; no path |

If two available sources are required to agree by the current mapper, the
presentation consumes only the already-validated result. It does not relax
consistency checks.

### 7.2 Inspection check rows

Completed check rows use only facts established by validated progress and
evidence. Examples:

- GGUF version and package/file-boundary validation;
- architecture, layer count and quantisation when present;
- tokenizer smoke result and bounded chat-template presence;
- successful structure stage without inventing tensor names or counts;
- exact approved CPU/VocabOnly runtime profile and selected final library name;
- source-integrity preservation and inspection duration.

Absent details use concise neutral text. The UI does not fabricate the exact
sample values from the Figma board.

### 7.3 Findings and diagnostics

User-facing findings come from the existing classified result. Diagnostic
rows use only stable, privacy-safe codes and summaries. They never include:

- full model paths;
- canonical path digests presented as paths;
- raw chat-template text;
- exception chains or stack traces;
- command lines;
- raw stdout/stderr;
- user or machine names.

Unexpected/unknown finding codes fail closed instead of receiving a guessed
tone or action.

### 7.4 Bounded display-text policy

Contract validation proves structural evidence; it does not make arbitrary
GGUF metadata safe or practical to render. Every evidence-derived string passes
through one deterministic `ModelInspectionDisplayTextPolicy` before entering a
TextBlock, tooltip, automation name/help text or live-region announcement.

The policy:

- rejects a model-name candidate longer than 160 UTF-16 code units before any
  normalization or scan;
- uses separate explicit caps of 96 code units for compact metadata labels and
  512 for bounded finding/detail prose;
- rejects malformed surrogate content, control characters,
  format/bidirectional controls, line/paragraph separators, private-use and
  unassigned content;
- normalizes only accepted input to Unicode Form C and rechecks the applicable
  cap and disallowed categories;
- trims outer spaces and collapses repeated ordinary U+0020 spaces;
- rejects model-name candidates containing `/` or `\`, a drive-root prefix,
  or another fully qualified path form;
- never truncates an unsafe value into a potentially misleading or partially
  private value;
- never echoes a rejected value into diagnostics.

Model-name fallback order is:

1. completed configuration model name when safe;
2. validated quick-scan model name when safe;
3. the validated final filename with the `.gguf` suffix removed when that
   candidate also passes the 160-unit display policy;
4. fixed text `Not reported`.

International letters, numbers and ordinary punctuation remain supported when
they do not trigger the unsafe categories above. Unsafe optional metadata uses
`Not reported`. Unsafe required finding/detail text maps to a fixed generic
privacy-safe description for its already-known outcome/code; it never changes
the classified outcome.

---

## 8. State and interaction behavior

### 8.1 Run lifecycle

```text
Navigated with validated request
  -> state 01 initial variant (0/5, no active stage, Cancel disabled)
  -> Loaded starts one attempt
  -> state 01 running variant
  -> trusted Completed -> states 02/04/06/08/09/10 as classified
  -> trusted Cancelled -> state 12
  -> OperationalFailure -> state 13
```

The initial variant uses the exact state 01 geometry. It displays `0 of 5
checks complete`, all five checks waiting, no active spinner and disabled
Cancel. It does not claim that worker execution has started. `Loaded` changes
that same stable visual tree in place; no additional pre-Loaded visual state is
permitted.

State 03, 05, 07 and 11 are interaction variants of their collapsed terminal
states, not new inspection results.

Cancel disables immediately after invocation, but the UI remains in a running
state until the service returns a trusted terminal. A forced or unknown
failure never appears as safe cancellation.

Retry publishes a new private attempt identity before cancelling/retiring the
old attempt. Late progress, results, animation completions and live-region
callbacks from the old attempt are ignored.

Choose another model invalidates the current attempt before raising navigation
and uses the existing fresh Model Import route. Retired pages and requests do
not remain in the Frame back stack.

### 8.2 Focus behavior

- Page entry places focus on the page heading or first meaningful progress
  element according to existing shell behavior.
- Progress updates do not steal focus.
- Expanding details retains focus on the disclosure button.
- Collapsing details retains focus on the same button.
- A terminal state moves focus only when required to expose an assertive error;
  ordinary Ready completion announces without arbitrary focus movement.
- Disabled future buttons remain discoverable by screen readers only when the
  chosen WinUI accessibility pattern can communicate their label and reason;
  otherwise adjacent help text supplies the reason.
- Retry and Choose another preserve normal tab order.

---

## 9. Motion and render behavior

### 9.1 Motion tokens

| Token | Duration | Use |
|---|---:|---|
| `InspectionMotionFast` | 160 ms | status icon and small row-state changes |
| `InspectionMotionStandard` | 180 ms | active detail and terminal crossfade |
| `InspectionMotionDisclosure` | 240 ms | disclosure reveal, chevron and repositioning |

The default easing is WinUI cubic ease-out. No spring, bounce or motion that
implies progress beyond actual state is used.

### 9.2 Stage transition

- status glyph/ring crossfades over 160 ms;
- newly visible active detail moves from Y +8 px to Y 0 while fading in over
  180 ms;
- completion removes the active spinner and reveals the terminal glyph without
  rebuilding the row;
- subsequent real events may retarget an in-flight animation smoothly;
- there is no fixed sleep or minimum visible stage duration.

### 9.3 Disclosure transition

- chevron rotates between down and up;
- the fixed-height detail viewport is revealed with clipping/opacity;
- the parent card interpolates to the approved target geometry;
- following content repositions through a theme/composition transition;
- scroll position resets only when a new outcome/attempt owns the disclosure;
- repeated expansion does not reconstruct its rows.

### 9.4 Terminal transition

The progress surface and terminal layout crossfade over 180 ms. The terminal
state is not applied until the current attempt is retired from progress so a
queued callback cannot overwrite the result.

### 9.5 Reduced motion

When Windows animations are disabled:

- all semantic state changes still occur;
- durations become zero;
- no information is available only through movement;
- live-region behavior, focus and layout are identical;
- tests verify that no animation is started.

---

## 10. Responsive and accessibility design

### 10.1 Responsive breakpoints

- Client width >= 888 px: exact centered 840 px Figma column.
- Client width 600-887 px: 24 px side margins, two-column metadata grid,
  wrapping action buttons.
- Client width < 600 px: 16 px side margins, one-column metadata grid,
  vertically stacked actions and a semantically complete footer treatment.

The standard 1440 x 1024 mode is the pixel-fidelity target. Responsive modes
preserve hierarchy and content rather than scaling the desktop canvas.

### 10.2 Accessibility requirements

- Interactions are at least 44 x 44 effective pixels; standard buttons remain
  46 px high.
- Every disclosure exposes expanded/collapsed state and supports Enter/Space.
- One polite progress live region announces meaningful stage changes.
- One assertive outcome live region announces each visible terminal state,
  including successful completion, exactly once per attempt.
- Nested/conflicting live regions are prohibited.
- Terminal announcement identity is `(AttemptGeneration, terminal outcome)`.
  Visibility/disclosure changes alone do not reset it; a new attempt or page
  lifetime does, so Retry can announce the same outcome again.
- Status always includes icon and text, never color alone.
- High contrast uses system brushes and retains visible focus.
- 200% text scale produces no clipped required text or inaccessible action.
- Tooltips/help text explain every disabled future action.
- Expanded bounded lists remain keyboard-scrollable and expose row names and
  statuses through automation peers.

---

## 11. Verification strategy

### 11.1 Presentation and state tests

Direct tests cover:

- all 13 visual state identities;
- exact banner tone, status chip, content mode, disclosure and actions;
- all four collapsed/expanded pairs;
- truthful model overview mapping and every missing-value fallback;
- safe display projection for valid international names plus rejection of
  Windows/UNC/POSIX paths, URLs, controls, newlines, bidi/format controls,
  malformed Unicode and oversized metadata;
- every supported finding/check/report row;
- disabled downstream buttons and accessible help;
- fail-closed undefined enums, unknown codes and contradictory data;
- no technical/private text leakage.

### 11.2 Render-coordinator tests

Deterministic tests prove:

- one pending dispatcher render despite a burst of notifications;
- atomic view snapshots and exact render-key progression across initial,
  progress, cancel-requested, terminal, Retry and Deactivate transitions;
- unchanged high-level controls receive no new presentation assignment;
- all five progress-row instances retain identity;
- the newest truthful state wins within one dispatcher frame;
- ordered completed counts and terminal state are preserved;
- stale attempt progress/result/animation callbacks are ignored;
- a progress animation or live announcement from an older revision of the
  current attempt cannot complete after terminal retirement;
- disclosure and focus state survive same-outcome updates;
- retry/new outcome resets disclosure exactly once.
- page-owned interaction revisions reject stale rapid Expand/Collapse,
  Collapse/Expand, outcome-change and navigation-away animation completions.

No correctness assertion depends on a machine-speed stopwatch threshold.

### 11.3 Motion tests

Tests verify:

- the exact 160/180/240 ms token values;
- icon opacity and active-detail translation endpoints;
- chevron endpoints and disclosure clip/reposition behavior;
- terminal crossfade endpoints;
- animation retargeting does not apply stale state;
- disclosure animation completion requires both the current render key and the
  current page interaction revision;
- reduced-motion mode starts no animation and preserves the same final state.
- same-attempt Hide -> show does not repeat an assertive terminal announcement,
  while a new-attempt Retry that reaches the same outcome announces once.

### 11.4 Rendered layout and visual regression

At 1440 x 1024, 100% scale, Light theme:

- anchor bounds are asserted to within one physical pixel;
- solid fill and border samples must match the Figma palette exactly;
- font family, size, weight, alignment and wrapping bounds are asserted;
- screenshots are retained for all 13 states;
- Figma reference comparisons allow platform text-antialiasing differences but
  do not tolerate shifted card geometry, incorrect color regions, missing
  controls or incorrect expanded height;
- dynamic model text regions use canonical deterministic fixtures.

The exact reference images are exported from the node IDs in section 4. The
user-supplied SVG may be used as an independent board/geometry reference.

### 11.5 Packaged journey tests

The packaged WinUI boundary proves:

- production `Loaded` automatically starts exactly one run;
- the real N-001 packaged worker journey reaches all five ordered stages and
  Ready;
- Ready details expand/collapse and remain accessible;
- Cancel, retry, navigation away and stale callbacks remain correct;
- fake-service fixtures render every remaining terminal state;
- keyboard focus, automation announcements, high contrast, text scale and
  reduced motion satisfy their contracts;
- no worker process remains after completion or cancellation.

### 11.6 Manual Visual Studio acceptance

Documentation includes a short Debug checklist that lets the user:

1. start the packaged x64 application;
2. select a valid GGUF model;
3. observe smooth factual stage progression;
4. inspect Ready details;
5. exercise Cancel, Retry and Choose another model;
6. enable Windows reduced motion and repeat;
7. verify no downstream future action claims to work.

---

## 12. Expected implementation boundaries

The implementation plan may refine exact filenames after repository inspection,
but ownership remains:

- `Features/ModelInspection/Presentation`: state selection, model/detail/action
  presentation, bounded display-text policy and design tokens;
- `Features/ModelInspection/Controls`: stable visual states, expanders,
  templates, automation peers and motion hooks;
- `Features/ModelInspection/ViewModels`: notification batching surface only if
  needed without moving WinUI into the ViewModel;
- `ModelInspectionPage`: UI-thread render coordination, focus, lifecycle and
  application of changed regions;
- `Features/Onboarding`: footer integration only if exact Figma geometry cannot
  be achieved through existing composition; navigation semantics remain fixed;
- packaged/unit tests: state, visual-tree, motion, accessibility and real
  journey verification;
- workflow/register/docs/inventory: exact test identities, floors, evidence and
  source registration.

The worker, worker client, protocol, native runtime and GGUF probe are not
modified for visual fidelity unless a separately reproduced functional defect
is found. Such a defect must be reported and scoped independently rather than
hidden inside UI work.

---

## 13. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Screenshot-perfect desktop code becomes inaccessible when resized | exact desktop layout plus explicit responsive modes; never scale one fixed canvas |
| Figma sample metadata is mistaken for evidence | centralized mapping table and `Not reported` fallbacks |
| Motion hides stale-attempt bugs | attempt identity checked before scheduling and again at animation completion |
| Fast real stages visually flash | stable rows, coalesced frame application and retargetable completion transitions; no fake delays |
| Expander reassignments collapse user state | page-owned disclosure state and changed-region rendering |
| Raw screenshot comparison flakes on ClearType | strict geometry/color/token assertions with bounded text-antialiasing tolerance |
| Disabled Figma actions appear broken | explicit disabled styling plus accessible `Coming later` reason |
| New font asset increases package/legal surface | include only required Inter faces and its license; verify package manifest and notices |
| More presentation states weaken privacy | use existing safe contracts only and add visible-text privacy sentinels |
| UI work changes model outcome policy | presentation consumes classifier output; no presentation-owned inference |

---

## 14. Definition of done

This redesign is complete only when:

1. all 13 Figma states and four disclosure pairs are implemented;
2. the 1440 x 1024 rendered layout matches the approved Figma geometry,
   palette and typography under the standard test environment;
3. genuine model data replaces Figma sample values without fabrication;
4. the existing real packaged GGUF journey still completes through all five
   ordered stages;
5. progress updates retain the visual tree and no longer cause repeated
   whole-page reconstruction;
6. the approved motion tokens, terminal transition and disclosure behavior are
   present;
7. reduced motion produces an equivalent instant state;
8. expansion, focus, keyboard, Narrator, high contrast and text scaling pass;
9. Cancel, Retry, Choose another and navigation cleanup remain correct;
10. downstream actions remain visibly disabled and make no completion claim;
11. no privacy, model-integrity, worker-containment or orphan-process guarantee
    regresses;
12. unit, packaged, contract, inventory and workflow-guard suites pass with
    reconciled current floors and required identities;
13. manual Visual Studio Debug instructions are updated and reproducible;
14. a final review finds no unsupported OpenVINO, TurboQuant, GPU, inference,
    context-creation or benchmarking expansion.

---

## 15. Approved conversational decisions

The user explicitly approved:

- Approach A: a Figma-faithful state system rather than a superficial reskin or
  fixed custom-drawn replica;
- rendering downstream future actions in place but disabled with clear
  `Coming later` treatment;
- the happy-path progress, Ready and expanded-Ready visual/motion direction;
- the complete evidence-based 13-state outcome system;
- the stable-tree, batched-render, responsive and accessible architecture;
- the acceptance strategy covering layout, motion, all states, packaged N-001,
  privacy and Visual Studio Debug verification.

No open product decision remains. The user gave written approval to this
specification and authority to proceed without another approval pause on
2026-08-09. Implementation follows the separately reviewed task plan derived
from this specification.
