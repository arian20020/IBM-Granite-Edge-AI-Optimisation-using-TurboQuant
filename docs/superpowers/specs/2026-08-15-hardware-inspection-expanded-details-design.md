# Hardware Inspection Expanded Details Design

## 1. Status and scope

| Item | Decision |
|---|---|
| Date | 2026-08-15 |
| Approval | Approved in conversation by the user |
| Immediate deliverable | Refine the local Hardware Inspection visual companion |
| Production boundary | No WinUI Model Inspection or Hardware Inspection production files change in this refinement |
| Audience | General healthcare and education workers, with a secondary IT-support path |
| Requirements source | `Granite_Edge_AI_Approved_Hardware_Inspection_Architecture_Block_2.docx` |
| UI reference | Model Inspection's disclosure header and title-detail-status row grammar |

This design replaces the prototype's short four-row metadata expansion with a
layered inspection record. It is deliberately more informative than the current
prototype while remaining understandable without hardware or diagnostic
expertise.

## 2. Outcome

Selecting `Show details` opens two levels of information:

1. a plain-language seven-stage inspection record for any user; and
2. a separately collapsed `Technical information for IT` disclosure for
   source evidence, conflicts, versions, timestamps, confidence, and safe
   diagnostics.

The first level follows Model Inspection's strongest visual pattern: a clear
summary followed by repeated rows containing a centred status icon, a stage
name, one explanatory sentence, and a written status. The design improves on
the current Model Inspection implementation by allowing the page to scroll
naturally instead of placing the ordinary inspection record inside a cramped
nested scroll viewport.

## 3. Alternatives considered

### 3.1 Selected: layered inspection record

The seven-stage record is immediately visible after expansion. Technical
evidence remains available behind a second, clearly labelled disclosure.

This balances consistency, transparency, and suitability for non-specialists.

### 3.2 Rejected: one full technical report

Showing stages, facts, sources, conflicts, versions, timestamps, and diagnostic
codes at the same level would be complete but visually dense. It would make the
primary expansion feel designed for administrators rather than healthcare or
education workers.

### 3.3 Rejected: tabs or two columns

A `Summary / Technical` switcher or two-column dashboard would look compact on
wide screens, but it adds another interaction model and hides information
again. It would also diverge from Model Inspection and reflow less cleanly at
narrow widths.

## 4. Component structure

### 4.1 Collapsed disclosure header

The whole header is one native expand/collapse control.

- Title: `Inspection details`
- Description: state-specific, for example `Seven checks and safe support
  information`
- Trailing action: one `Show details` or `Hide details` label, never a repeated
  title plus a second uppercase action
- Chevron: fixed vector geometry in a 44 px interaction target
- Minimum production height: 58 px
- Wide/medium content inset: 24 px
- Compact content inset: 16 px
- Visible pointer, hover, pressed, and focus states

### 4.2 Plain-language result summary

The expanded content starts with a concise state summary rather than repeating
the existing outcome card.

Examples:

- `Inspection completed with two details to review.`
- `Installed memory could not be confirmed, so no report was created.`
- `The inspection stopped while starting the local checking tool.`
- `You cancelled the inspection while graphics information was being read.`

The summary also states whether a hardware report was created. It does not
claim model compatibility, a supported context size, or a recommended
configuration.

### 4.3 Seven-stage inspection record

The record always uses these canonical stages and this order:

1. Starting hardware inspection
2. Reading processor information
3. Reading system memory
4. Detecting graphics hardware
5. Checking local inference runtimes
6. Normalising hardware information
7. Creating the hardware report

Each row contains:

- one unified SVG status circle and glyph;
- a bold stage name;
- one plain-language explanatory sentence;
- a written trailing status;
- an optional safe time only when the real record supplies one.

Approved ordinary-user status text is:

- `Completed`
- `Completed with note`
- `Could not check`
- `Could not confirm`
- `Stopped here`
- `Cancelled here`
- `Not started`
- `Not used in report`
- `Report created`

Colour and icons reinforce status but never replace written status. Rows use
the same title-detail-status hierarchy as Model Inspection. They are separated
by quiet one-pixel rules and retain generous vertical centring.

The stage list has no fixed-height internal scrollbar. The expanded card and
page grow naturally so all seven stages can be reviewed without discovering a
second scroll region.

### 4.4 Technical information for IT

A second native disclosure appears after the seven-stage record and is closed
by default.

- Title: `Technical information for IT`
- Description: `Sources, versions, timestamps, and safe support codes`
- It remains available for completed, warning, failed, and cancelled terminal
  outcomes.
- It is not exposed while `Stopping safely` is still waiting for cleanup to
  reach a stable terminal state.

When opened, it contains three bounded groups.

#### A. How hardware facts were confirmed

Each relevant hardware fact presents the selected canonical value first. Its
supporting evidence may then show:

- `Used check` - the selected source;
- `Other check` - another candidate source, when relevant;
- `Why this value was used` - the authority or fallback decision in plain
  language;
- `Agreement` - agreed, differed within tolerance, or unresolved;
- `Confidence` - separate from source identity;
- `Captured` - the real evidence timestamp when available.

Competing candidate values are supporting evidence, not alternative canonical
answers. The UI never averages conflicting values or silently selects the
larger value.

Technical resolution states are translated before display:

| Contract meaning | Display text |
|---|---|
| `PrimaryAccepted` | `Confirmed by the preferred check` |
| `PrimaryAcceptedWithConflict` | `Confirmed, but another check reported something different` |
| `SecondaryFallback` | `Confirmed using another trusted Windows check` |
| `Unavailable` | `This information could not be checked` |
| `ConflictUnresolved` | `The checks disagreed, so no safe value was selected` |

#### B. Run and tool information

Only information present in the real inspection record is displayed:

- local hardware scanner identity and version, with `LLM Fit` secondary to the
  plain-language name;
- Windows System, DXGI, Windows NPU, and Windows Storage sources when used;
- pinned local runtime capability probe identity and version, with `llama.cpp`
  shown secondarily;
- source-authority policy and schema versions;
- run start, completion, report-creation, and source-capture times;
- freshness of available-memory evidence when supplied.

#### C. Warnings and safe diagnostics

The UI shows the readable explanation before the stable technical code. It may
show the affected stage, provider, component, and safe resolution. It never
shows raw process output, command lines, private paths, or unrestricted native
error text.

The final note states that the inspection ran locally and did not upload
hardware information. It does not invent guarantees about deletion timing,
log retention, encryption, or other behavior absent from the approved plan.

## 5. State-specific behavior

### 5.1 Completed with warnings

All reached stages remain visible. A non-critical stage may read `Completed
with note` or `Could not check`, and the report-creation stage reads `Report
created`. The IT section explains fallbacks and source disagreements without
turning them into hardware failure.

### 5.2 Critical evidence missing

The critical stage reads `Could not confirm`. Later stages distinguish
`Not started` from work that ran but was `Not used in report`. No report-created
status appears, and the summary explains why compatibility cannot continue.

### 5.3 Temporary operational failure

The first affected stage reads `Stopped here`; later stages read `Not started`.
The wording states that the checking tool failed, not the computer. The IT
section may expose a safe reference code and failed stage.

### 5.4 Application repair required

The starting stage explains that the approved local component could not be
verified. Retry is not presented as the primary recovery when repair is
required. IT information may show the safe component identity and verification
result.

### 5.5 Stopping safely

This remains a temporary cleanup state. It does not expose a final record or IT
details until child-process cleanup is confirmed.

### 5.6 Cancelled

Completed stages retain `Completed`. The interrupted stage reads `Cancelled
here`; later stages read `Not started`. No hardware conclusion or compatibility
handoff is implied.

## 6. Responsive and visual behavior

- The detail report remains one column at all widths.
- Stage rows use icon, copy, and trailing-status columns at wide and medium
  widths.
- At compact widths the status moves beneath the explanation while staying
  associated with the same row.
- No fixed outer height is used to align neighbouring prototype cards.
- Text wraps without horizontal scrolling at 200% text scale.
- Unified SVG X and tick icons remain geometrically centred at every rendered
  size.
- The nested IT disclosure is visually quieter than the primary inspection
  record and cannot compete with the result summary.

## 7. Interaction and accessibility

- Both disclosure headers use native expand/collapse semantics.
- Enter, Space, and pointer input toggle the intended disclosure.
- Focus remains on the activated header.
- The collapsed region leaves layout, hit testing, keyboard navigation, and
  the accessibility tree.
- Expanded state is programmatically exposed.
- Every stage has an accessible name containing its stage, explanation, and
  written status.
- Status is never colour-only.
- Focus rings meet the existing high-visibility prototype treatment.
- Expansion motion is subtle and omitted under reduced motion.
- Opening the IT disclosure does not navigate or change the selected outcome
  card.

## 8. Prototype data flow

The local prototype uses state-specific illustrative records, but its structure
mirrors future production ownership:

1. terminal outcome selects the plain-language summary;
2. ordered phase records populate the seven-stage list;
3. the canonical hardware snapshot supplies selected fact values;
4. the evidence manifest supplies sources, conflicts, confidence, policy,
   versions, and timestamps;
5. safe diagnostics supply bounded codes and explanations;
6. the presentation layer translates contract terms into approved user-facing
   language.

Illustrative prototype values must not be described as live machine evidence.

## 9. Verification and acceptance

The prototype refinement is accepted when:

1. every terminal disclosure contains exactly seven ordered stage rows;
2. the row anatomy visibly follows Model Inspection while using natural page
   scrolling;
3. every stage has an icon, title, explanation, and written status;
4. the IT disclosure is present and collapsed by default for stable terminal
   states;
5. `Stopping safely` exposes no premature final record;
6. completed, warning, failed, repair-required, and cancelled examples use
   truthful state-specific stage histories;
7. pointer, Enter, and Space operate both disclosure levels;
8. action buttons still provide in-page prototype feedback without navigation;
9. reduced-motion mode removes disclosure transitions and spinner motion;
10. no internal vertical scrollbar appears in the ordinary stage record;
11. no raw path, command output, unrestricted error text, or unsupported
    privacy promise appears;
12. no copy claims model compatibility or configuration suitability;
13. X and tick centring regressions remain covered;
14. layouts are visually reviewed at wide, medium, and compact widths.

## 10. Out of scope

- Production WinUI implementation
- Hardware inspection service or worker implementation
- Live evidence collection
- Compatibility analysis
- Export, copy-to-clipboard, or support-ticket actions
- Changing Model Inspection's approved disclosure
- Inventing retention, deletion, encryption, or upload behavior

## 11. Locked decisions

- Use the layered inspection-record approach.
- Show the seven-stage plain-language record first.
- Place source-level evidence behind `Technical information for IT`.
- Preserve the Model Inspection row grammar while avoiding a cramped nested
  scrollbar.
- Keep all normal-user language understandable without acronyms.
- Keep technical evidence available, bounded, and truthful.
- Refine only the local prototype in the immediate implementation.
