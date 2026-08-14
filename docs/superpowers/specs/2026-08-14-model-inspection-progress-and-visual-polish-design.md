# Model Inspection Progress and Visual Polish Design

| Metadata | Value |
|---|---|
| Status | Approved conversationally; pending written-spec review |
| Date | 2026-08-14 |
| Branch | `refactor/model-inspection-cleanup` |
| Design base | `aecdf32bdae99ad49feb5086b01f7b8ed0e33d29` |
| Product boundary | Model Inspection startup, progress semantics, motion, status glyphs, and all 13 presentation states |
| Prior design | `docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md` |
| Follow-up boundary | Hardware Inspection is a separate feature and begins only after this work is implemented and verified |

## 1. Outcome

Model Inspection will feel immediate, deliberate, and visually coherent without
slowing the inspection worker or inventing progress.

The accepted direction is:

- an immediate truthful `Starting secure inspection…` state after navigation;
- five genuine inspection stages whose Active and Completed events surround
  their actual work;
- a presentation-only, attempt-keyed sequencer that keeps each genuine stage
  visibly active for at least 550 ms;
- one constant-speed Precision Orbit loader for every active stage;
- custom, optically centred vector status glyphs instead of small font glyphs;
- the Measured Checklist progress layout;
- the Balanced Centre completed layout;
- the same layout, icon, spacing, and action polish across warning, conversion,
  incomplete-package, unsupported, invalid, cancelled, and operational-failure
  states;
- immediate cancellation, error, retry, navigation, and reduced-motion
  behavior.

This design intentionally supersedes the prior design's requirements that the
newest progress state always replace intermediate milestones and that no
minimum visible stage duration exist. It retains the stronger underlying
rules: no worker sleep, no fabricated percentage, no fabricated completed
stage, no delayed safety terminal, and no stale-attempt presentation.

## 2. Verified causes

### 2.1 The apparent startup delay is an unrepresented phase

There is no intentional startup sleep.

After the user requests inspection, the shell navigates immediately and the
page initially renders five Waiting rows. `Loaded` starts the attempt, and the
ViewModel immediately publishes `IsRunActive = true` with `Progress = null`.
The presentation currently maps that combination back to `Awaiting inspection`
and five Waiting rows.

Meanwhile, the application must:

1. verify the staged 44-file worker closure and SHA-256 hashes immediately
   before launch;
2. launch the isolated worker process;
3. complete the protocol Hello handshake;
4. send the inspection request;
5. wait for the worker's first Stage 1 event.

This secure preflight can take a few seconds on a Debug package. The UI is busy
but looks idle. The integrity verification remains mandatory and is not cached,
skipped, or weakened by this work.

### 2.2 The loader changes during Stage 2

The active marker currently switches from an indeterminate WinUI
`ProgressRing` to a determinate ring when the native loader reports a genuine
fraction. The visual language and perceived speed therefore change mid-run.
Fraction-only changes are also classified as status changes, causing the
marker animation to restart repeatedly.

### 2.3 Stages 3–5 appear together

The runtime probe currently performs most tokenizer, structure, and integrity
work before the worker reports the later stage boundaries. Stages 3 and 4 then
emit Active and Completed back-to-back, while Stage 5 surrounds only
synchronous mapping and validation. The render coordinator retains only the
newest pending snapshot, so these events can collapse into one visual update.

### 2.4 The visual defects are structural

- Stock `SymbolIcon` glyphs are optically unreliable at the current 12–18 px
  sizes, causing incomplete-looking ticks and off-centre warning marks.
- The progress heading has no deliberate gap before row one.
- Every progress row has a 60 px minimum, including the final row without a
  connector, which creates a dead tail.
- Ready's collapsed disclosure reserves 83 px around a 58 px header.
- Other terminal cards use unnecessary fixed minimum heights.
- Page-level state switches encode inconsistent 22, 24, and 30 px gaps.
- Two-action states occupy columns one and three of a fixed three-column grid.
- Outcome copy can remain left-anchored because its focus target does not
  stretch across the balanced centre column.
- The onboarding footer uses a separate Unicode tick and a different visual
  language.

## 3. Scope

### 3.1 In scope

- Startup/preflight feedback after navigation.
- The five production Model Inspection stages.
- Stage-aware probe and worker progress boundaries.
- UI-only milestone sequencing and cancellation.
- Progress fraction presentation.
- Shared status glyphs and active indicator.
- All 13 existing Model Inspection visual states and four disclosure pairs.
- Desktop and responsive layout at existing breakpoints.
- Light, Dark, High Contrast resources, 200% text behavior, reduced motion,
  keyboard, focus, automation, and live announcements.
- Debug fixture-gallery states and presets needed to inspect these changes.
- Unit, packaged, contract, workflow, documentation, and cleanup-inventory
  updates required by the changed scope.

### 3.2 Out of scope

- Hardware Inspection or Hardware Fit implementation.
- GPU, Vulkan, OpenVINO, TurboQuant, inference, context creation, benchmarking,
  conversion execution, report export, or downstream navigation.
- Weakening worker-manifest verification or process containment.
- Renaming the five approved user-facing stages merely to hide incorrect work
  boundaries.
- Strict Figma pixel claims without exact node PNG references.

Hardware Inspection is the next independent feature. Its implementation plan
may be prepared while this work is underway, but its source changes do not
enter this branch until Model Inspection is complete and verified.

## 4. Truthful progress architecture

### 4.1 Immediate startup state

`IsRunActive = true` with no worker progress is a first-class startup phase,
not an idle phase.

On the first rendered inspection frame, the page shows:

- section title `Inspection progress`;
- summary `Starting secure inspection…`;
- the Precision Orbit active indicator in a dedicated startup presentation;
- all five counted inspection rows still Waiting;
- `0 of 5 checks complete`;
- a single polite announcement: `Model inspection is starting.`

Startup is not a sixth check and does not mark `Check model package` active.
It ends as soon as the first genuine Stage 1 event arrives and has no 550 ms
minimum. If reduced motion is enabled, the same startup text appears with a
static active arc.

The startup presentation is published before worker-manifest verification,
process launch, and handshake can make the screen appear idle. The start path
must yield one UI presentation opportunity without adding a worker delay.

### 4.2 Real stage boundaries

The semantic progress stream brackets actual work:

1. **Check model package** — initial file identity and integrity snapshot.
2. **Read model configuration** — native backend selection, VocabOnly load,
   and configuration reading.
3. **Validate tokenizer and chat setup** — vocabulary, tokenizer smoke, and
   chat-template evidence collection.
4. **Validate model structure** — structural evidence collection, model
   disposal, final integrity snapshot, and comparison.
5. **Confirm core runtime compatibility** — privacy-safe evidence mapping and
   contract validation.

The runtime probe exposes typed internal phase callbacks around these existing
operations. Where the current evidence collector combines Stage 3 and Stage 4
work, it is separated into focused internal collection steps without changing
the resulting evidence contract or native execution policy.

No worker, service, or runtime code uses `Thread.Sleep`, a presentation timer,
or an artificial delay. No percentage is synthesized.

### 4.3 Visible milestone sequencer

A page-owned `ModelInspectionMilestoneSequencer` sits between accepted semantic
snapshots and visual application. It has one responsibility: preserve genuine
stage transitions long enough to be perceived.

It:

- is keyed by page lifetime and attempt generation;
- uses an injected deterministic scheduler;
- gives each genuinely reached Active stage a minimum visible duration of
  exactly 550 ms;
- applies Completed only after that stage's minimum has elapsed;
- queues the next genuine stage in order;
- coalesces fraction-only updates within the current stage;
- lets a naturally long stage remain active without adding another 550 ms;
- lets a normal completed outcome follow the final visible Stage 5 completion;
- never changes the semantic ViewModel snapshot or worker timing.

At most the bounded five-stage sequence plus one pending normal terminal is
retained. A newer attempt invalidates the entire prior queue.

### 4.4 Immediate paths

These events cancel and flush queued visual milestones, then render immediately:

- cancellation requested and trusted Cancelled terminal;
- operational failure;
- Retry or Restart;
- Choose another model;
- page navigation or disposal;
- stale-generation invalidation;
- switching to reduced motion.

Completed model outcomes such as Ready, Ready with warnings, Conversion
required, Incomplete package, Unsupported, and Invalid are normal classified
outcomes. They follow the visible completion sequence; they are not treated as
process failures.

## 5. Motion and status language

### 5.1 Precision Orbit

Every active stage uses the same custom active indicator:

- one 24 px viewbox inside a 30 px effective status surface;
- a quiet neutral track and one primary-blue arc;
- rounded stroke caps;
- constant 1,050 ms linear rotation;
- no determinate/indeterminate mode switch;
- no restart when native percentage changes.

A genuine Stage 2 percentage is displayed as restrained trailing text, such as
`42%`, inside the active row. It does not change the orbit, trigger a live
announcement, or animate the entire marker.

Reduced motion renders the same arc statically. Status text remains the
authoritative meaning.

### 5.2 Shared vector glyphs

One reusable status-glyph control owns custom vector geometry for:

- success: a complete, round-cap check;
- warning: an optically centred triangle and exclamation mark;
- error: a centred cross;
- information: a centred information mark;
- waiting: a stable stage number;
- active: Precision Orbit.

The same geometries are reused at row, summary, banner, and onboarding-footer
sizes. Containers may differ by semantic prominence, but glyph proportions do
not. The old nested 22 px badge inside a 30 px progress shell is removed.

Glyphs are decorative to automation; adjacent visible status text supplies the
accessible name. High Contrast uses system brushes and preserves a visible
outline and focus state.

### 5.3 Existing transition tokens

The existing restrained transitions remain:

- 160 ms status change;
- 180 ms detail and terminal crossfade;
- 240 ms disclosure transition.

Completed glyphs crossfade as complete static shapes. They are not drawn
stroke-by-stroke, preventing an in-between incomplete tick from becoming a
stable visual impression.

## 6. Layout system

### 6.1 Shared rhythm

All states use the same base rhythm:

- 24 px from page header to the first visible card;
- 16 px between visible cards;
- 20–24 px internal card padding;
- hidden cards consume zero layout space;
- collapsed content sizes naturally;
- fixed heights remain only for bounded expanded scroll viewports;
- disclosure expansion does not shift the page header upward.

State-specific magic top margins and unnecessary minimum heights are removed.

### 6.2 Measured Checklist progress card

The accepted progress layout keeps all five rows stable:

- a 14–16 px gap separates `Inspection progress` from row one;
- an exact completed-count chip sits opposite the heading;
- each row uses a compact, equal 48 px rhythm;
- the active row has a subtle blue surface highlight;
- completed, active, and waiting markers occupy the same 30 px surface;
- only the first four rows draw connectors;
- the final row has no extra connector tail or dead bottom space;
- Cancel remains a full-width, 44 px-or-larger target below the list.

Rows never jump between groups while progress advances.

### 6.3 Balanced Centre completed layout

The accepted completed layout preserves the familiar page hierarchy while
making it symmetrical:

- outcome icon, centred copy, and an equal balancing column form the banner;
- the model-card title is centred and the format chip remains right-aligned;
- each metadata cell has equal padding and centres its short label and value;
- all four columns and both rows have equal geometry at desktop width;
- the inspection-summary/disclosure row is centred as one coherent control;
- the result card centres title, helper text, and equal-footprint actions;
- the collapsed disclosure uses its natural header height with no 25 px tail.

Long findings, diagnostics, and disclosure prose remain left-aligned for
readability. Centring applies to short metadata and summary content, not
paragraphs.

### 6.4 Warning and failure states

Every non-success state receives the same polish, not a partial reskin:

- Ready with warnings uses the centred warning glyph/banner and balanced
  finding card in collapsed and expanded forms.
- Conversion required uses centred information/warning geometry, natural card
  height, and a bounded expanded expected-output list.
- Incomplete package, Unsupported model, and Invalid model remove forced empty
  height while preserving readable left-aligned explanations.
- Invalid technical-report expansion remains bounded and scrollable.
- Cancelled and Operational failure use the shared error/recovery glyphs,
  balanced copy, and immediate action availability.
- Action surfaces choose their grid from the number of visible actions: one
  centred action, two equal centred columns, or three equal columns. No empty
  middle column remains.
- Disabled future actions retain clear `Coming later` help and do not appear
  enabled merely for symmetry.

### 6.5 Responsive behavior

The existing breakpoints remain:

- `>= 888 px`: centred 840 px desktop column;
- `600–887 px`: 24 px page margins and two-column metadata;
- `< 600 px`: 16 px margins, one-column metadata, and stacked actions.

Natural height, text wrapping, 200% preview text, and keyboard reachability
take priority over fixed desktop height outside the desktop reference mode.

## 7. Focus, announcements, and error handling

- Immediate startup feedback does not steal focus from the page heading.
- `Model inspection is starting` is announced politely once per attempt.
- Fraction-only updates remain silent.
- Genuine stage changes announce once in semantic order, independent of visual
  dwell bookkeeping.
- Progress updates do not move keyboard focus.
- Disclosure toggles retain focus and their expansion provider semantics.
- Operational failure and cancellation bypass pacing and use existing
  assertive/polite policies as appropriate.
- A normal terminal outcome cannot be overwritten by an older queued stage.
- Retry, navigation, disposal, and motion-setting changes cancel outstanding
  scheduler work before it can mutate UI.
- Worker verification, containment, cleanup, privacy redaction, and model-file
  integrity guarantees remain unchanged.

## 8. Test-first verification

Implementation begins with failing tests for each changed contract.

### 8.1 Startup

- An active attempt with no worker progress renders the startup status and
  active indicator before a held service reports Stage 1.
- Startup is not counted as a sixth check.
- Worker-manifest verification remains mandatory.
- Startup announcement, focus, Cancel state, High Contrast, and reduced-motion
  endpoints are exact.

### 8.2 Semantic stages

- Tests prove every Active/Completed pair surrounds its corresponding runtime
  operation.
- Stage 3 and Stage 4 cannot emit back-to-back after their work has already
  completed.
- Evidence and final classification remain byte/field equivalent for the same
  deterministic probe result.
- Source/contract guards reject worker/service sleeps and fabricated progress.

### 8.3 Sequencing

- An injected manual scheduler proves the exact 550 ms minimum without
  stopwatch-based flakiness.
- A naturally long stage receives no extra delay.
- Bursts retain all five stage transitions in order.
- Fraction updates coalesce within one stage and start no marker transition.
- Normal terminal waits for the final visible milestone.
- cancellation, failure, retry, navigation, disposal, and reduced motion flush
  immediately.
- stale attempt and stale scheduler completions are rejected.

### 8.4 Visual and responsive contracts

- Shared glyph geometry, size variants, brushes, centring, and automation view
  are asserted for progress rows, model checks, disclosures, outcome banners,
  and onboarding footer.
- Layout tests cover all 13 states and four disclosure pairs at 1440, 888,
  887, 600, 599, and 360 px widths.
- Tests assert the heading gap, equal row rhythm, absence of the final dead
  tail, natural collapsed heights, equal visible-card gaps, centred metadata,
  centred outcome copy, and 1/2/3-action layouts.
- Maximum safe text and 200% preview text do not overlap or clip.
- High Contrast uses system resources and all interactive targets remain at
  least 44 by 44 effective pixels.

### 8.5 Packaged and manual acceptance

- The fixture gallery gains an active/no-worker-progress startup fixture and
  updated expected geometry for every affected state.
- Interaction, lifetime, motion, accessibility, and nine-class fixture
  campaigns remain green.
- The real packaged N-001 journey proves semantic order and startup feedback.
- The hosted-equivalent Release filter, protected class map, focused N-001,
  Contracts, cleanup inventory, and Release isolation remain green.
- A manual multi-gigabyte GGUF run validates perceived startup and pacing,
  because the 800-byte N-001 fixture cannot reproduce real hash/load duration.
- No strict Figma pixel claim is made until exact node references exist.

## 9. Acceptance criteria

The work is complete only when:

1. the first inspection frame visibly says `Starting secure inspection…`;
2. no secure worker verification or containment guarantee is weakened;
3. all five stage events surround their real operations;
4. every genuine Active stage is visible for at least 550 ms in normal motion;
5. cancellation, operational failure, retry, navigation, and reduced motion
   remain immediate;
6. one Precision Orbit indicator is visually identical across all stages;
7. genuine percentages never change or restart that orbit;
8. success, warning, error, information, waiting, and footer marks use complete,
   centred vector geometry;
9. the Measured Checklist has deliberate heading separation and no dead tail;
10. the completed page uses Balanced Centre geometry and equal spacing;
11. every warning/failure/conversion/cancelled state receives the same polish;
12. one-, two-, and three-action surfaces are balanced without placeholder
    gaps;
13. focus, announcements, High Contrast, 200% text, reduced motion, and
    responsive behavior retain their guarantees;
14. all changed unit, packaged, contract, workflow, cleanup, and isolation
    gates pass;
15. the normal branch remains directly buildable, deployable, and debuggable in
    Visual Studio.

## 10. Approved decisions

The user selected and approved:

- Approach A: truthful worker boundaries plus a UI-only perceptual sequencer;
- a 550 ms minimum visible duration per genuine stage;
- Precision Orbit status language;
- Balanced Centre completed layout;
- Measured Checklist progress layout;
- equivalent polish for warning and failure pages;
- immediate visual startup feedback;
- Hardware Inspection as the next separate feature after this work.
