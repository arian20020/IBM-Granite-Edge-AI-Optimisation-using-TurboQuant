# Hardware Inspection Expanded Details Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the local Hardware Inspection prototype's limited metadata expansion with a polished seven-stage inspection record and a separately collapsed IT evidence section.

**Architecture:** Keep the approved outcome-family mock as one self-contained HTML companion. Native outer and nested `details` controls own disclosure semantics; a small state-keyed JavaScript presentation model renders seven canonical stage rows and bounded IT evidence into five stable terminal states. Existing unified SVG icon geometry, card selection, action feedback, and reduced-motion behavior remain shared.

**Tech Stack:** HTML5 `details`/`summary`, CSS Grid and custom properties, vanilla JavaScript DOM APIs, SVG, Playwright CLI, PowerShell.

---

## Execution boundary and file map

Execute in the current workspace because the running brainstorming companion at
`http://localhost:56163/` watches this exact workspace. Do not edit or stage any
Model Inspection production source.

The companion directory is intentionally excluded by `.git/info/exclude`.
Never use `git add -f` for it. Browser evidence and file hashes are the
checkpoint mechanism for mock-only tasks.

**Files:**

- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html`
- Reference: `docs/superpowers/specs/2026-08-15-hardware-inspection-expanded-details-design.md`
- Reference only: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Reference only: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`

No production file, fixture catalogue, XAML control, C# view model, hardware
service, or compatibility feature belongs in this plan.

The existing six-card outcome family remains the boundary. Its first card is
the completed-with-warnings terminal example and intentionally contains both
`Completed` and `Completed with note` stage histories; the separate clean
completion screen is outside this file. Do not add a seventh card.

### Task 1: Lock the failing browser contract

**Files:**

- Test target: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html`

- [ ] **Step 1: Confirm the companion serves the approved outcome family**

Run:

```powershell
playwright-cli -s=hardware-expanded-details open http://localhost:56163/
```

Then run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => ({ heading: document.querySelector('h2')?.textContent, details: document.querySelectorAll('details.audit-details').length }))"
```

Expected result:

```json
{
  "heading": "Warnings, recovery, and cancellation - audited family",
  "details": 5
}
```

If the heading differs, make the outcome-family file the newest companion file
with a comment-only `apply_patch`, reload, and repeat. Do not edit another mock
to change server selection.

- [ ] **Step 2: Run the new structure contract and verify it fails**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const terminals=[...document.querySelectorAll('details.audit-details')]; const failures=[]; if(terminals.length!==5) failures.push('expected five terminal disclosures'); terminals.forEach((details,index)=>{ const stages=details.querySelectorAll('.audit-stage-row'); const nested=details.querySelector(':scope .audit-it-details'); if(stages.length!==7) failures.push('state '+(index+1)+' expected seven stages, found '+stages.length); if(!nested) failures.push('state '+(index+1)+' missing IT disclosure'); }); if(document.querySelectorAll('.audit-stage-list[data-scroll-owner=page]').length!==5) failures.push('stage lists do not declare page scroll ownership'); if(!failures.length) throw new Error('Contract unexpectedly passed before implementation'); return failures; })"
```

Expected: a returned failure list containing missing seven-stage rows, missing IT
disclosures, and missing page-scroll ownership.

- [ ] **Step 3: Capture the baseline interaction and icon counts**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => ({ xIcons: document.querySelectorAll('.x-glyph > svg.audit-x-icon').length, completedIcons: document.querySelectorAll('.audit-check-icon').length, actionButtons: document.querySelectorAll('button.audit-button').length, disabledButtons: document.querySelectorAll('button.audit-button:disabled').length, selectedCards: document.querySelectorAll('.audit-state.is-selected').length }))"
```

Expected:

```json
{
  "xIcons": 4,
  "completedIcons": 14,
  "actionButtons": 10,
  "disabledButtons": 1,
  "selectedCards": 1
}
```

- [ ] **Step 4: Record the pre-change worktree status**

Run:

```powershell
git status --short
```

Also fingerprint all tracked staged and unstaged changes:

```powershell
git diff --binary | git hash-object --stdin
git diff --cached --binary | git hash-object --stdin
```

Expected: copy the complete status and both hashes into the execution notes.
This is the baseline used in Task 6 to prove that unrelated tracked production
changes were not introduced or altered; the final status also detects new
untracked paths. If the parallel Model Inspection chat legitimately changes
that baseline during execution, record the new fingerprint and inspect the
changed paths; never revert or absorb its work to make the hashes match.

- [ ] **Step 5: Record the pre-change companion hash**

Run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath .superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html
```

Expected: one SHA-256 value. Copy it into the execution notes; do not commit the
excluded companion file.

### Task 2: Replace the sparse disclosure markup and visual shell

**Files:**

- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html:386-458`
- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html:568-649`

- [ ] **Step 1: Add a state key and an empty render mount to each terminal disclosure**

Replace each existing static `.audit-detail-panel` table with the same semantic
shell, using these state keys in order:

```html
<details class="audit-card audit-details" data-record-key="warnings">
  <summary class="audit-disclosure">
    <span class="audit-disclosure-icon info-glyph" aria-hidden="true">i</span>
    <span class="audit-disclosure-copy">
      <strong>Inspection details</strong>
      <span>Seven checks, resolved differences, and safe support information</span>
    </span>
    <span class="audit-disclosure-action" aria-hidden="true">
      <span class="view-label">Show details</span>
      <span class="hide-label">Hide details</span>
      <svg class="audit-chevron" viewBox="0 0 12 12" aria-hidden="true">
        <path d="M2.5 4.25L6 7.75L9.5 4.25"></path>
      </svg>
    </span>
  </summary>
  <div class="audit-inspection-report"></div>
</details>
```

Use the following exact key and description pairs so the collapsed headers are
state-specific:

| Key | Description |
|---|---|
| `warnings` | `Seven checks, resolved differences, and safe support information` |
| `evidence-failed` | `Seven checks explaining where reliable evidence stopped` |
| `transient-failure` | `Seven checks, failed stage, and safe support information` |
| `repair-required` | `Seven checks, component verification, and IT support information` |
| `cancelled` | `Seven checks showing what completed before cancellation` |

Keep `Stopping safely` without an outer details control.

- [ ] **Step 2: Remove the obsolete metadata-table styles**

Delete `.audit-detail-panel` and `.audit-detail-row` rules. Keep the native
summary reset, header focus style, action-label switch, and chevron rotation.

- [ ] **Step 3: Add the primary report and stage-row styles**

Add the following CSS after `.audit-details[open] .audit-chevron`:

```css
.audit-disclosure {
  min-height: 58px;
  padding: 7px 24px;
}

.audit-disclosure-action {
  min-width: 82px;
  min-height: 44px;
  justify-content: flex-end;
}

.audit-disclosure:hover { background: #f8fbff; }
.audit-disclosure:active { background: #eef5ff; }

.audit-inspection-report {
  display: grid;
  gap: 10px;
  padding: 16px 24px 20px;
  border-top: 1px solid var(--a-line);
  background: #fbfcfe;
}

.audit-record-summary {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 10px 11px;
  border: 1px solid var(--a-line);
  border-radius: 9px;
  background: white;
}

.audit-record-summary-copy > strong {
  display: block;
  color: var(--a-ink);
  font-size: 8.5px;
  line-height: 1.35;
}

.audit-record-summary-copy > span {
  display: block;
  margin-top: 3px;
  color: var(--a-muted);
  font-size: 7px;
  line-height: 1.45;
}

.audit-record-badge {
  min-height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 4px 8px;
  border: 1px solid var(--a-neutral-line);
  border-radius: 999px;
  color: var(--a-neutral-text);
  font-size: 6.5px;
  font-weight: 850;
  line-height: 1.2;
  text-align: center;
  text-transform: uppercase;
}

.audit-stage-list {
  overflow: visible;
  border: 1px solid var(--a-line);
  border-radius: 9px;
  background: white;
}

.audit-stage-row {
  min-height: 48px;
  display: grid;
  grid-template-columns: 22px minmax(0, 1fr) auto;
  gap: 9px;
  align-items: center;
  padding: 8px 10px;
  border-top: 1px solid var(--a-line);
}

.audit-stage-row:first-child { border-top: 0; }

.audit-stage-copy strong {
  display: block;
  color: var(--a-ink);
  font-size: 8px;
  line-height: 1.3;
}

.audit-stage-copy span {
  display: block;
  margin-top: 2px;
  color: var(--a-muted);
  font-size: 7px;
  line-height: 1.4;
}

.audit-stage-status {
  max-width: 82px;
  color: var(--a-muted);
  font-size: 6.5px;
  font-weight: 850;
  line-height: 1.2;
  text-align: right;
  text-transform: uppercase;
}

.audit-stage-row[data-tone="success"] .audit-stage-status { color: var(--a-success-text); }
.audit-stage-row[data-tone="warning"] .audit-stage-status { color: #7a5700; }
.audit-stage-row[data-tone="error"] .audit-stage-status { color: var(--a-error-text); }
```

- [ ] **Step 4: Run a shell-style contract check**

Run:

```powershell
rg -n -e 'data-record-key="warnings"' -e 'data-record-key="evidence-failed"' -e 'data-record-key="transient-failure"' -e 'data-record-key="repair-required"' -e 'data-record-key="cancelled"' -e '\.audit-stage-row' .superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html
```

Expected: all five keys and the new stage-row CSS appear. The browser contract
from Task 1 still fails because rows are not rendered yet.

### Task 3: Render the seven canonical stage records

**Files:**

- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html:656-716`

- [ ] **Step 1: Add the exact canonical stage data before card-selection setup**

Inside the existing IIFE, after `installCompleteIcon` calls and before
`const states`, add:

```js
const stageRecords = {
  warnings: {
    summary: "Inspection completed with two details to review.",
    helper: "A hardware report was created and can continue to compatibility.",
    badge: "Report created",
    rows: [
      ["Starting hardware inspection", "The approved local checking tools were prepared.", "Completed", "success", "check"],
      ["Reading processor information", "Processor information was collected and agreed across the trusted checks.", "Completed", "success", "check"],
      ["Reading system memory", "Two trusted checks differed slightly; the approved Windows value was selected.", "Completed with note", "warning", "warning"],
      ["Detecting graphics hardware", "Graphics information was collected, but a neural processor could not be confirmed.", "Completed with note", "warning", "warning"],
      ["Checking local inference runtimes", "The installed local tools reported the processor and graphics routes they can see.", "Completed", "success", "check"],
      ["Normalising hardware information", "The collected information was compared and resolved using the approved rules.", "Completed", "success", "check"],
      ["Creating the hardware report", "The reliable hardware facts and review notes were recorded.", "Report created", "success", "check"]
    ]
  },
  "evidence-failed": {
    summary: "Installed memory could not be confirmed, so no report was created.",
    helper: "Collected values remain non-actionable and cannot continue to compatibility.",
    badge: "No report",
    rows: [
      ["Starting hardware inspection", "The approved local checking tools were prepared.", "Completed", "success", "check"],
      ["Reading processor information", "Processor information was collected successfully.", "Completed", "success", "check"],
      ["Reading system memory", "The trusted checks disagreed, so installed memory could not be confirmed safely.", "Could not confirm", "error", "x"],
      ["Detecting graphics hardware", "Graphics information was collected for safe diagnostics only.", "Not used in report", "neutral", "dash"],
      ["Checking local inference runtimes", "Runtime visibility was collected for safe diagnostics only.", "Not used in report", "neutral", "dash"],
      ["Normalising hardware information", "The unresolved memory values prevented a reliable hardware snapshot.", "Stopped here", "error", "x"],
      ["Creating the hardware report", "A hardware report was not created from unresolved critical evidence.", "Not started", "neutral", "dash"]
    ]
  },
  "transient-failure": {
    summary: "The inspection stopped while starting the local checking tool.",
    helper: "No hardware conclusion was made and no report was created.",
    badge: "Stopped",
    rows: [
      ["Starting hardware inspection", "The app could not open the approved local checking tool this time.", "Stopped here", "error", "x"],
      ["Reading processor information", "No processor check began.", "Not started", "neutral", "dash"],
      ["Reading system memory", "No memory check began.", "Not started", "neutral", "dash"],
      ["Detecting graphics hardware", "No graphics check began.", "Not started", "neutral", "dash"],
      ["Checking local inference runtimes", "No runtime check began.", "Not started", "neutral", "dash"],
      ["Normalising hardware information", "There was no collected information to combine.", "Not started", "neutral", "dash"],
      ["Creating the hardware report", "No hardware report was created.", "Not started", "neutral", "dash"]
    ]
  },
  "repair-required": {
    summary: "The approved inspection component could not be verified.",
    helper: "The application needs attention; this is not a finding about the computer.",
    badge: "Repair needed",
    rows: [
      ["Starting hardware inspection", "The required local component was missing, changed, damaged, or the wrong version.", "Stopped here", "error", "x"],
      ["Reading processor information", "No processor check began.", "Not started", "neutral", "dash"],
      ["Reading system memory", "No memory check began.", "Not started", "neutral", "dash"],
      ["Detecting graphics hardware", "No graphics check began.", "Not started", "neutral", "dash"],
      ["Checking local inference runtimes", "No runtime check began.", "Not started", "neutral", "dash"],
      ["Normalising hardware information", "There was no trusted information to combine.", "Not started", "neutral", "dash"],
      ["Creating the hardware report", "No hardware report was created.", "Not started", "neutral", "dash"]
    ]
  },
  cancelled: {
    summary: "You cancelled the inspection while graphics information was being read.",
    helper: "No hardware conclusion was made and no report was sent to compatibility.",
    badge: "Cancelled",
    rows: [
      ["Starting hardware inspection", "The approved local checking tools were prepared.", "Completed", "success", "check"],
      ["Reading processor information", "Processor information was collected.", "Completed", "success", "check"],
      ["Reading system memory", "Memory information was collected.", "Completed", "success", "check"],
      ["Detecting graphics hardware", "You stopped the run while this check was active.", "Cancelled here", "neutral", "dash"],
      ["Checking local inference runtimes", "The runtime check did not begin.", "Not started", "neutral", "dash"],
      ["Normalising hardware information", "The partial information was not combined into a report.", "Not started", "neutral", "dash"],
      ["Creating the hardware report", "No hardware report was created.", "Not started", "neutral", "dash"]
    ]
  }
};
```

- [ ] **Step 2: Add a unified SVG factory for stage status marks**

Add:

```js
function createStageIcon(tone, mark) {
  const host = document.createElement("span");
  host.className = "audit-stage-icon";
  host.dataset.tone = tone;
  host.setAttribute("aria-hidden", "true");

  const svg = document.createElementNS(svgNamespace, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("focusable", "false");
  svg.setAttribute("aria-hidden", "true");

  const circle = document.createElementNS(svgNamespace, "circle");
  circle.setAttribute("cx", "12");
  circle.setAttribute("cy", "12");
  circle.setAttribute("r", "11.15");
  circle.setAttribute("class", "audit-stage-ring");
  svg.append(circle);

  if (mark === "check") {
    const path = document.createElementNS(svgNamespace, "path");
    path.setAttribute("d", "M7.45 11.55L10.45 14.55L16.55 8.45");
    path.setAttribute("class", "audit-stage-mark");
    svg.append(path);
  } else if (mark === "x") {
    const path = document.createElementNS(svgNamespace, "path");
    path.setAttribute("d", "M8.75 8.75L15.25 15.25M15.25 8.75L8.75 15.25");
    path.setAttribute("class", "audit-stage-mark");
    svg.append(path);
  } else if (mark === "warning") {
    const path = document.createElementNS(svgNamespace, "path");
    path.setAttribute("d", "M12 7.4V13.1");
    path.setAttribute("class", "audit-stage-mark");
    const dot = document.createElementNS(svgNamespace, "circle");
    dot.setAttribute("cx", "12");
    dot.setAttribute("cy", "16.3");
    dot.setAttribute("r", "1.1");
    dot.setAttribute("class", "audit-stage-mark-dot");
    svg.append(path, dot);
  } else {
    const path = document.createElementNS(svgNamespace, "path");
    path.setAttribute("d", "M8.5 12H15.5");
    path.setAttribute("class", "audit-stage-mark");
    svg.append(path);
  }

  host.append(svg);
  return host;
}
```

Add the matching CSS:

```css
.audit-stage-icon { width: 20px; height: 20px; display: grid; place-items: center; justify-self: center; }
.audit-stage-icon svg { width: 100%; height: 100%; display: block; overflow: visible; shape-rendering: geometricPrecision; }
.audit-stage-ring { fill: var(--a-neutral-bg); stroke: var(--a-neutral-line); stroke-width: 1; vector-effect: non-scaling-stroke; }
.audit-stage-mark { fill: none; stroke: var(--a-neutral-text); stroke-width: 1.5; stroke-linecap: round; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
.audit-stage-mark-dot { fill: var(--a-neutral-text); }
.audit-stage-icon[data-tone="success"] .audit-stage-ring { fill: var(--a-success-bg); stroke: var(--a-success-line); }
.audit-stage-icon[data-tone="success"] .audit-stage-mark { stroke: var(--a-success-text); }
.audit-stage-icon[data-tone="warning"] .audit-stage-ring { fill: var(--a-warn-bg); stroke: var(--a-warn-line); }
.audit-stage-icon[data-tone="warning"] .audit-stage-mark { stroke: #7a5700; }
.audit-stage-icon[data-tone="warning"] .audit-stage-mark-dot { fill: #7a5700; }
.audit-stage-icon[data-tone="error"] .audit-stage-ring { fill: var(--a-error-bg); stroke: var(--a-error-line); }
.audit-stage-icon[data-tone="error"] .audit-stage-mark { stroke: var(--a-error-text); }
```

- [ ] **Step 3: Render the summary and seven rows**

Add:

```js
function appendTextElement(parent, tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  element.textContent = text;
  parent.append(element);
  return element;
}

function renderStageRecord(details, record) {
  const report = details.querySelector(":scope > .audit-inspection-report");
  report.replaceChildren();

  const summary = document.createElement("div");
  summary.className = "audit-record-summary";
  const summaryCopy = document.createElement("div");
  summaryCopy.className = "audit-record-summary-copy";
  appendTextElement(summaryCopy, "strong", "", record.summary);
  appendTextElement(summaryCopy, "span", "", record.helper);
  appendTextElement(summary, "span", "audit-record-badge", record.badge);
  summary.prepend(summaryCopy);

  const list = document.createElement("div");
  list.className = "audit-stage-list";
  list.dataset.scrollOwner = "page";
  list.setAttribute("role", "list");
  list.setAttribute("aria-label", "Seven-stage hardware inspection record");

  record.rows.forEach(([title, detail, status, tone, mark]) => {
    const row = document.createElement("div");
    row.className = "audit-stage-row";
    row.dataset.tone = tone;
    row.setAttribute("role", "listitem");
    row.setAttribute("aria-label", `${title}. ${detail} Status: ${status}.`);
    row.append(createStageIcon(tone, mark));
    const copy = document.createElement("span");
    copy.className = "audit-stage-copy";
    appendTextElement(copy, "strong", "", title);
    appendTextElement(copy, "span", "", detail);
    row.append(copy);
    appendTextElement(row, "span", "audit-stage-status", status);
    list.append(row);
  });

  report.append(summary, list);
}

document.querySelectorAll("details.audit-details[data-record-key]").forEach((details) => {
  const record = stageRecords[details.dataset.recordKey];
  if (!record) throw new Error(`Missing inspection record for ${details.dataset.recordKey}`);
  renderStageRecord(details, record);
});
```

- [ ] **Step 4: Reload and run the seven-stage contract**

Run:

```powershell
playwright-cli -s=hardware-expanded-details reload
```

Then:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const result=[...document.querySelectorAll('details.audit-details')].map(details=>({key:details.dataset.recordKey,rows:details.querySelectorAll('.audit-stage-row').length,scrollOwner:details.querySelector('.audit-stage-list')?.dataset.scrollOwner,labels:[...details.querySelectorAll('.audit-stage-status')].map(node=>node.textContent)})); if(result.some(item=>item.rows!==7||item.scrollOwner!=='page')) throw new Error(JSON.stringify(result)); return result; })"
```

Expected: five records, each with `rows: 7` and `scrollOwner: "page"`.

- [ ] **Step 5: Verify the original X and tick contract still passes**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const xs=[...document.querySelectorAll('.x-glyph')]; const ticks=[...document.querySelectorAll('.check-glyph, .audit-step.done .audit-step-dot')]; const validX=xs.every(host=>host.querySelector(':scope > svg.audit-x-icon circle')&&host.querySelector(':scope > svg.audit-x-icon path')&&getComputedStyle(host,'::before').content==='none'); const validTicks=ticks.every(host=>host.querySelector(':scope > svg.audit-check-icon circle')&&host.querySelector(':scope > svg.audit-check-icon path')&&getComputedStyle(host,'::before').content==='none'); if(xs.length!==4||ticks.length!==14||!validX||!validTicks) throw new Error('Unified icon regression'); return {x:xs.length,ticks:ticks.length}; })"
```

Expected: `{ "x": 4, "ticks": 14 }`.

### Task 4: Add the nested IT evidence disclosure

**Files:**

- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html:656-760`

- [ ] **Step 1: Define bounded illustrative IT records**

Add a `technicalRecords` object next to `stageRecords`:

```js
const technicalRecords = {
  warnings: {
    facts: [
      {
        name: "Installed memory",
        selectedValue: "32 GB installed",
        usedCheck: "Windows system information - 32 GB",
        otherCheck: "Local hardware scanner (LLM Fit) - 31.8 GB",
        resolution: "Confirmed using another trusted Windows check",
        agreement: "Differed within the approved tolerance",
        confidence: "High",
        captured: "Example capture - 15 Aug 2026, 10:42:14 +02:00"
      },
      {
        name: "Neural processor (NPU)",
        selectedValue: "Could not confirm",
        usedCheck: "Windows NPU check - no confirmed device",
        otherCheck: "",
        resolution: "This information could not be checked",
        agreement: "Unavailable",
        confidence: "Unavailable",
        captured: "Example capture - 15 Aug 2026, 10:42:15 +02:00"
      }
    ],
    run: [
      ["Hardware scanner", "Local hardware scanner (LLM Fit) - example version 0.6.1"],
      ["Windows checks", "System information, DXGI, Windows NPU, and Windows Storage - identities recorded when used"],
      ["Local runtime probe", "Local runtime capability check (llama.cpp) - example build b7000"],
      ["Rules", "Example source policy 1.0 - hardware schema 1.0"],
      ["Timing", "Example run started 10:42:12 - report created 10:42:17 (+02:00)"]
    ],
    diagnostic: "The memory difference was resolved safely. NPU presence could not be confirmed, but it did not block the hardware report.",
    reference: "No support code was needed for this completed run."
  },
  "evidence-failed": {
    facts: [
      {
        name: "Installed memory",
        selectedValue: "No safe value selected",
        usedCheck: "Windows system information - 32 GB",
        otherCheck: "Local hardware scanner (LLM Fit) - 24 GB",
        resolution: "The checks disagreed, so no safe value was selected",
        agreement: "Unresolved difference",
        confidence: "Unavailable",
        captured: "Example capture - 15 Aug 2026, 10:47:08 +02:00"
      }
    ],
    run: [
      ["Failed stage", "Normalising hardware information"],
      ["Evidence handling", "Partial values are non-actionable and cannot continue to compatibility"],
      ["Rules", "Example source policy 1.0 - hardware schema 1.0"],
      ["Timing", "Example run started 10:47:02 - stopped 10:47:09 (+02:00)"]
    ],
    diagnostic: "Two trusted memory checks differed too much to choose a safe value. No hardware report was created.",
    reference: "Illustrative support code - EXAMPLE-HW-EVIDENCE-002"
  },
  "transient-failure": {
    facts: [],
    run: [
      ["Failed stage", "Starting hardware inspection"],
      ["Affected tool", "Approved local hardware scanner"],
      ["Result", "No trustworthy hardware evidence was created"],
      ["Rules", "Example source policy 1.0 - hardware schema 1.0"],
      ["Timing", "Example run stopped 15 Aug 2026, 10:51:03 +02:00"]
    ],
    diagnostic: "The checking tool could not start this time. Retrying may clear the temporary application problem.",
    reference: "Illustrative support code - EXAMPLE-HW-START-001"
  },
  "repair-required": {
    facts: [],
    run: [
      ["Failed stage", "Starting hardware inspection"],
      ["Affected component", "Approved local inspection component"],
      ["Verification", "The approved component identity or version could not be confirmed"],
      ["Rules", "Example source policy 1.0 - hardware schema 1.0"],
      ["Timing", "Example verification stopped 15 Aug 2026, 10:55:26 +02:00"]
    ],
    diagnostic: "The application component needs repair. This result does not mean that the computer hardware failed.",
    reference: "Illustrative support code - EXAMPLE-HW-INTEGRITY-001"
  },
  cancelled: {
    facts: [],
    run: [
      ["Cancelled stage", "Detecting graphics hardware"],
      ["Report", "No hardware report was created"],
      ["Partial information", "Used only for this bounded local diagnostic view and not sent to compatibility"],
      ["Rules", "Example cancellation policy 1.0 - hardware schema 1.0"],
      ["Timing", "Example cancellation completed 15 Aug 2026, 11:00:11 +02:00"]
    ],
    diagnostic: "Cancellation is a neutral outcome and does not indicate hardware failure.",
    reference: "No support code was created for this cancelled run."
  }
};
```

- [ ] **Step 2: Add nested-disclosure and evidence styles**

Add:

```css
.audit-it-details {
  overflow: hidden;
  border: 1px solid var(--a-line);
  border-radius: 9px;
  background: white;
}

.audit-it-summary {
  min-height: 46px;
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 8px 10px;
  list-style: none;
  cursor: pointer;
}

.audit-it-summary::-webkit-details-marker { display: none; }
.audit-it-summary::marker { content: ""; }
.audit-it-summary:focus-visible { outline: 3px solid rgba(15, 98, 254, .56); outline-offset: -3px; }
.audit-it-copy > strong { display: block; color: var(--a-ink); font-size: 8px; line-height: 1.3; }
.audit-it-copy > span { display: block; margin-top: 2px; color: var(--a-muted); font-size: 7px; line-height: 1.4; }

.audit-it-toggle {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--a-blue);
  font-size: 6.5px;
  font-weight: 850;
  text-transform: uppercase;
  white-space: nowrap;
}

.audit-it-toggle-label { color: inherit; font: inherit; line-height: inherit; }
.audit-it-details[open] .audit-it-toggle svg { transform: rotate(180deg); }
.audit-it-content { display: grid; gap: 9px; padding: 10px; border-top: 1px solid var(--a-line); background: #f8fafc; }
.audit-preview-note { padding: 7px 9px; border: 1px solid var(--a-blue-line); border-radius: 8px; background: var(--a-blue-soft); color: #344054; font-size: 6.8px; font-weight: 750; line-height: 1.45; }
.audit-it-group { display: grid; gap: 6px; }
.audit-it-group > h5 { margin: 0; color: var(--a-ink); font-size: 7.5px; line-height: 1.35; }
.audit-empty-note { margin: 0; padding: 8px; border: 1px solid var(--a-line); border-radius: 8px; background: white; color: var(--a-muted); font-size: 6.8px; line-height: 1.45; }
.audit-evidence-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; }
.audit-evidence-card { padding: 8px; border: 1px solid var(--a-line); border-radius: 8px; background: white; }
.audit-evidence-card strong { display: block; color: var(--a-ink); font-size: 7.5px; line-height: 1.35; }
.audit-evidence-line { display: grid; grid-template-columns: 68px minmax(0, 1fr); gap: 6px; margin-top: 4px; }
.audit-evidence-line b { color: #344054; font-size: 6.5px; line-height: 1.4; }
.audit-evidence-line span { color: var(--a-muted); font-size: 6.5px; line-height: 1.4; }
.audit-diagnostic-note { padding: 8px 9px; border: 1px solid var(--a-blue-line); border-radius: 8px; background: var(--a-blue-soft); color: #344054; font-size: 6.8px; line-height: 1.45; }
```

- [ ] **Step 3: Render the nested IT disclosure after every stage list**

Add:

```js
function appendEvidenceLine(card, label, value) {
  const line = document.createElement("div");
  line.className = "audit-evidence-line";
  appendTextElement(line, "b", "", label);
  appendTextElement(line, "span", "", value);
  card.append(line);
}

function renderTechnicalDetails(report, technical) {
  const details = document.createElement("details");
  details.className = "audit-it-details";

  const summary = document.createElement("summary");
  summary.className = "audit-it-summary";
  const copy = document.createElement("span");
  copy.className = "audit-it-copy";
  appendTextElement(copy, "strong", "", "Technical information for IT");
  appendTextElement(copy, "span", "", "Sources, versions, timestamps, and safe support codes");
  const toggle = document.createElement("span");
  toggle.className = "audit-it-toggle";
  toggle.setAttribute("aria-hidden", "true");
  appendTextElement(toggle, "span", "audit-it-toggle-label", "Show IT details");
  const chevron = document.createElementNS(svgNamespace, "svg");
  chevron.setAttribute("viewBox", "0 0 12 12");
  chevron.setAttribute("width", "10");
  chevron.setAttribute("height", "10");
  const chevronPath = document.createElementNS(svgNamespace, "path");
  chevronPath.setAttribute("d", "M2.5 4.25L6 7.75L9.5 4.25");
  chevronPath.setAttribute("fill", "none");
  chevronPath.setAttribute("stroke", "currentColor");
  chevronPath.setAttribute("stroke-width", "1.8");
  chevronPath.setAttribute("stroke-linecap", "round");
  chevronPath.setAttribute("stroke-linejoin", "round");
  chevron.append(chevronPath);
  toggle.append(chevron);
  summary.append(copy, toggle);

  const content = document.createElement("div");
  content.className = "audit-it-content";
  appendTextElement(content, "div", "audit-preview-note", "Illustrative preview data - not a reading from this computer.");

  const factGroup = document.createElement("section");
  factGroup.className = "audit-it-group";
  appendTextElement(factGroup, "h5", "", "How hardware facts were confirmed");
  if (technical.facts.length) {
    const grid = document.createElement("div");
    grid.className = "audit-evidence-grid";
    technical.facts.forEach(({ name, selectedValue, usedCheck, otherCheck, resolution, agreement, confidence, captured }) => {
      const card = document.createElement("article");
      card.className = "audit-evidence-card";
      appendTextElement(card, "strong", "", name);
      appendEvidenceLine(card, "Selected value", selectedValue);
      appendEvidenceLine(card, "Used check", usedCheck);
      if (otherCheck) appendEvidenceLine(card, "Other check", otherCheck);
      appendEvidenceLine(card, "Why this value was used", resolution);
      appendEvidenceLine(card, "Agreement", agreement);
      appendEvidenceLine(card, "Confidence", confidence);
      appendEvidenceLine(card, "Captured", captured);
      grid.append(card);
    });
    factGroup.append(grid);
  } else {
    appendTextElement(factGroup, "p", "audit-empty-note", "No canonical hardware fact detail is shown because this run did not create a hardware report.");
  }
  content.append(factGroup);

  const runGroup = document.createElement("section");
  runGroup.className = "audit-it-group";
  appendTextElement(runGroup, "h5", "", "Run and tool information");
  const runCard = document.createElement("article");
  runCard.className = "audit-evidence-card";
  technical.run.forEach(([label, value]) => appendEvidenceLine(runCard, label, value));
  runGroup.append(runCard);
  content.append(runGroup);

  const diagnosticGroup = document.createElement("section");
  diagnosticGroup.className = "audit-it-group";
  appendTextElement(diagnosticGroup, "h5", "", "Warnings and safe diagnostics");
  appendTextElement(diagnosticGroup, "div", "audit-diagnostic-note", technical.diagnostic);
  appendTextElement(diagnosticGroup, "div", "audit-diagnostic-note", technical.reference);
  appendTextElement(diagnosticGroup, "div", "audit-diagnostic-note", "This illustrative inspection runs locally and does not upload hardware information. Raw paths and process output stay hidden.");
  content.append(diagnosticGroup);

  details.append(summary, content);
  report.append(details);
}
```

At the end of `renderStageRecord`, add:

```js
const technical = technicalRecords[details.dataset.recordKey];
if (!technical) throw new Error(`Missing technical record for ${details.dataset.recordKey}`);
renderTechnicalDetails(report, technical);
```

- [ ] **Step 4: Add a toggle listener only for the visible IT label**

After rendering, add:

```js
document.querySelectorAll("details.audit-it-details").forEach((details) => {
  details.addEventListener("toggle", () => {
    const label = details.querySelector(".audit-it-toggle-label");
    if (label) label.textContent = details.open ? "Hide IT details" : "Show IT details";
  });
});
```

Do not intercept native keyboard or pointer behavior.

- [ ] **Step 5: Reload and verify nested semantics and initial state**

Run:

```powershell
playwright-cli -s=hardware-expanded-details reload
```

Then:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const outer=[...document.querySelectorAll('details.audit-details')]; const nested=[...document.querySelectorAll('details.audit-it-details')]; const invalid=document.querySelectorAll('button button, summary button, button summary, a button, button a').length; if(outer.length!==5||nested.length!==5||nested.some(item=>item.open)||invalid) throw new Error(JSON.stringify({outer:outer.length,nested:nested.length,open:nested.filter(item=>item.open).length,invalid})); return {outer:outer.length,nested:nested.length,open:0,invalid}; })"
```

Expected: five outer disclosures, five nested disclosures, zero nested
disclosures open, and zero invalid interactive nesting.

- [ ] **Step 6: Verify the evidence hierarchy is complete and clearly illustrative**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const nested=[...document.querySelectorAll('details.audit-it-details')]; const factCards=[...document.querySelectorAll('.audit-evidence-grid .audit-evidence-card')]; const required=['Selected value','Used check','Why this value was used','Agreement','Confidence','Captured']; const factLabelsValid=factCards.every(card=>{ const labels=[...card.querySelectorAll('.audit-evidence-line b')].map(node=>node.textContent); return required.every(label=>labels.includes(label)); }); const groupsValid=nested.every(details=>{ const headings=[...details.querySelectorAll('.audit-it-group > h5')].map(node=>node.textContent); return ['How hardware facts were confirmed','Run and tool information','Warnings and safe diagnostics'].every(label=>headings.includes(label)); }); const explanationFirst=nested.every(details=>{ const notes=[...details.querySelectorAll('.audit-diagnostic-note')]; return notes.length===3&&!/EXAMPLE-HW-/.test(notes[0].textContent)&&(/EXAMPLE-HW-/.test(notes[1].textContent)||/No support code/.test(notes[1].textContent)); }); const otherChecks=[...document.querySelectorAll('.audit-evidence-line b')].filter(node=>node.textContent==='Other check').length; const result={previewNotes:document.querySelectorAll('.audit-preview-note').length,factCards:factCards.length,otherChecks,factLabelsValid,groupsValid,explanationFirst}; if(result.previewNotes!==5||result.factCards!==3||result.otherChecks!==2||!factLabelsValid||!groupsValid||!explanationFirst) throw new Error(JSON.stringify(result)); return result; })"
```

Expected: five local illustrative-data notices, three fact cards, two explicit
`Other check` rows, all required provenance labels, three named groups per
terminal state, and readable diagnostic explanations before support codes.

### Task 5: Complete responsive, focus, and reduced-motion behavior

**Files:**

- Modify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html:536-562`

- [ ] **Step 1: Add the compact row and evidence layout**

Inside the existing responsive section, add:

```css
@media (max-width: 520px) {
  .audit-disclosure {
    padding-right: 16px;
    padding-left: 16px;
  }

  .audit-inspection-report {
    padding: 14px 16px 16px;
  }

  .audit-stage-row {
    grid-template-columns: 22px minmax(0, 1fr);
  }

  .audit-stage-status {
    grid-column: 2;
    max-width: none;
    text-align: left;
  }

  .audit-record-summary {
    grid-template-columns: minmax(0, 1fr);
  }

  .audit-record-badge {
    justify-self: start;
  }

  .audit-evidence-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .audit-evidence-line {
    grid-template-columns: minmax(0, 1fr);
  }
}
```

- [ ] **Step 2: Extend no-preference transitions without animating height**

Add `.audit-it-summary` and `.audit-it-toggle svg` to the existing
`prefers-reduced-motion: no-preference` transition group. Animate only colour,
background, border, opacity, and chevron rotation. Do not transition `height`,
`max-height`, `grid-template-rows`, or `transform` on the report container.

- [ ] **Step 3: Verify keyboard, pointer, focus, and collapsed-content behavior**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const beforeUrl=page.url(); const selectedBefore=(await page.locator('.audit-state.is-selected .audit-label h3').textContent()).trim(); const outer=page.locator('details.audit-details').first(); const outerSummary=outer.locator(':scope > summary'); await outerSummary.focus(); await page.keyboard.press('Enter'); if(!(await outer.evaluate(node=>node.open))||!(await outerSummary.evaluate(node=>document.activeElement===node))) throw new Error('outer Enter or focus failed'); await page.keyboard.press('Space'); if(await outer.evaluate(node=>node.open)) throw new Error('outer Space failed'); if(!(await outerSummary.evaluate(node=>document.activeElement===node))) throw new Error('outer focus moved'); const collapsedAria=await outer.ariaSnapshot(); const collapsedRects=await outer.locator('.audit-inspection-report').evaluate(node=>node.getClientRects().length); if(collapsedAria.includes('Starting hardware inspection')||collapsedRects!==0) throw new Error('collapsed outer content remained exposed'); await outerSummary.click(); const nested=outer.locator('details.audit-it-details'); const nestedSummary=nested.locator(':scope > summary'); await nestedSummary.focus(); await page.keyboard.press('Enter'); if(!(await nested.evaluate(node=>node.open))||!(await nestedSummary.evaluate(node=>document.activeElement===node))) throw new Error('nested Enter or focus failed'); await page.keyboard.press('Space'); if(await nested.evaluate(node=>node.open)) throw new Error('nested Space failed'); if(!(await nestedSummary.evaluate(node=>document.activeElement===node))) throw new Error('nested focus moved'); await nestedSummary.click(); const selectedAfter=(await page.locator('.audit-state.is-selected .audit-label h3').textContent()).trim(); if(!(await nested.evaluate(node=>node.open))||page.url()!==beforeUrl||selectedAfter!==selectedBefore||await page.locator('.audit-state.is-selected').count()!==1) throw new Error('pointer, URL, or selection identity failed'); return {outerEnter:true,outerSpace:true,nestedEnter:true,nestedSpace:true,pointer:true,focusRetained:true,collapsedHidden:true,urlUnchanged:true,selected: selectedAfter}; }"
```

Expected: Enter, Space, and pointer input work on both native disclosure levels;
focus remains on the activated header; collapsed content has no rendered or
accessible stage rows; URL and selected-card count remain unchanged.

- [ ] **Step 4: Verify action clicks still do not navigate or select another card**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const before=page.url(); const selectedBefore=await page.locator('.audit-state.is-selected').count(); await page.locator('.audit-button:not(:disabled)').first().click(); await page.locator('.audit-toast.is-visible').waitFor(); const selectedAfter=await page.locator('.audit-state.is-selected').count(); if(page.url()!==before||selectedBefore!==1||selectedAfter!==1) throw new Error('action regression'); return {url:page.url(),selected:selectedAfter,message:(await page.locator('.audit-toast').textContent()).trim()}; }"
```

Expected: unchanged URL, one selected card, and a prototype-only feedback
message.

- [ ] **Step 5: Verify card selection and nested-control isolation**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const beforeUrl=page.url(); const first=page.locator('.audit-state').first(); const second=page.locator('.audit-state').nth(1); await second.locator('.audit-state-select').click(); const selected=await page.locator('.audit-state.is-selected').count(); const pressed=await second.locator('.audit-state-select').getAttribute('aria-pressed'); if(selected!==1||pressed!=='true'||!(await second.evaluate(node=>node.classList.contains('is-selected')))) throw new Error('card selection failed'); await first.locator('summary.audit-disclosure').click(); await first.locator('summary.audit-it-summary').click(); const selectedTitle=(await page.locator('.audit-state.is-selected .audit-label h3').textContent()).trim(); if(selectedTitle!=='Essential information missing'||!(await second.evaluate(node=>node.classList.contains('is-selected')))||await first.evaluate(node=>node.classList.contains('is-selected'))||await page.locator('.audit-state.is-selected').count()!==1||page.url()!==beforeUrl) throw new Error('nested IT disclosure changed selection or URL'); return {selected,pressed,selectedTitle,nestedItControlIsolated:true,urlUnchanged:true}; }"
```

Expected: exactly one selected card, its selector exposes `aria-pressed=true`,
and opening a different card's outer and IT disclosures leaves that exact card
selected and does not navigate.

- [ ] **Step 6: Verify reduced motion**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.emulateMedia({reducedMotion:'reduce'}); const result=await page.evaluate(() => ({spinner:getComputedStyle(document.querySelector('.audit-spinner')).animationName,outerChevron:getComputedStyle(document.querySelector('.audit-chevron')).transitionDuration,nestedSummary:getComputedStyle(document.querySelector('.audit-it-summary')).transitionDuration,nestedChevron:getComputedStyle(document.querySelector('.audit-it-toggle svg')).transitionDuration})); await page.emulateMedia({reducedMotion:'no-preference'}); if(result.spinner!=='none'||result.outerChevron!=='0s'||result.nestedSummary!=='0s'||result.nestedChevron!=='0s') throw new Error(JSON.stringify(result)); return result; }"
```

Expected:

```json
{
  "spinner": "none",
  "outerChevron": "0s",
  "nestedSummary": "0s",
  "nestedChevron": "0s"
}
```

### Task 6: Perform visual and content acceptance

**Files:**

- Verify: `.superpowers/brainstorm/1777-1786719024/content/hardware-outcome-recovery-family-v2.html`

- [ ] **Step 1: Run the complete fresh-page contract**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const result=await page.evaluate(() => { const canonical=['Starting hardware inspection','Reading processor information','Reading system memory','Detecting graphics hardware','Checking local inference runtimes','Normalising hardware information','Creating the hardware report']; const outer=[...document.querySelectorAll('details.audit-details')]; const stageRows=[...document.querySelectorAll('.audit-stage-row')]; const nested=[...document.querySelectorAll('details.audit-it-details')]; const natural=[...document.querySelectorAll('.audit-stage-list')].every(node=>getComputedStyle(node).overflowY==='visible'&&getComputedStyle(node).maxHeight==='none'); const orderValid=outer.every(details=>[...details.querySelectorAll('.audit-stage-copy strong')].every((node,index)=>node.textContent===canonical[index])); const descriptions=[...document.querySelectorAll('summary.audit-disclosure .audit-disclosure-copy > span')].map(node=>node.textContent); const stoppingState=[...document.querySelectorAll('.audit-state')].find(state=>state.querySelector('.audit-label h3')?.textContent.trim()==='Stopping safely'); const stopping=stoppingState?.querySelectorAll('details.audit-details').length??-1; const memoryRow=[...document.querySelectorAll('[data-record-key=evidence-failed] .audit-stage-row')].find(row=>row.querySelector('.audit-stage-copy strong')?.textContent==='Reading system memory'); const memoryStatus=memoryRow?.querySelector('.audit-stage-status')?.textContent; const text=document.body.innerText; const unsafePatterns={privatePath:/(?:\b[A-Z]:[\\/]|file:\/\/|\\\\[A-Za-z0-9_.-]+[\\/])/i,commandOrRawError:/\b(?:cmd\.exe|powershell(?:\.exe)?|stack trace|stdout|stderr|HRESULT|System\.[A-Z]\w*Exception)\b|(?:^|\s)--[a-z][\w-]*/im,compatibilityClaim:/\b(?:this (?:model|computer|configuration) is compatible|supported context(?: size)?|recommended context(?: size)?|recommended configuration)\b/i,unsupportedPrivacy:/\b(?:deleted (?:after|within)|encrypted (?:at|in)|logs? (?:are )?retained for|automatically purged)\b/i}; const unsafe=Object.entries(unsafePatterns).filter(([,pattern])=>pattern.test(text)).map(([name])=>name); return {outer:outer.length,stageRows:stageRows.length,nested:nested.length,nestedOpen:nested.filter(node=>node.open).length,natural,orderValid,distinctDescriptions:new Set(descriptions).size,stopping,memoryStatus,buttons:document.querySelectorAll('button.audit-button').length,disabled:document.querySelectorAll('button.audit-button:disabled').length,selected:document.querySelectorAll('.audit-state.is-selected').length,unsafe}; }); if(result.outer!==5||result.stageRows!==35||result.nested!==5||result.nestedOpen!==0||!result.natural||!result.orderValid||result.distinctDescriptions!==5||result.stopping!==0||result.memoryStatus!=='Could not confirm'||result.buttons!==10||result.disabled!==1||result.selected!==1||result.unsafe.length) throw new Error(JSON.stringify(result)); return result; }"
```

Expected: 5 outer disclosures, 35 stage rows, 5 closed IT disclosures,
canonical stage order, five state-specific header descriptions, natural page
scrolling, no stopping-state details, critical memory marked `Could not
confirm`, the 10/1/1 button-disabled-selection baseline, and no unsafe copy
matches.

- [ ] **Step 2: Verify wide layout at 1440 px**

Run:

```powershell
playwright-cli -s=hardware-expanded-details resize 1440 1100
```

Open the first outer disclosure and its IT disclosure and verify the production
header insets and action target:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const outer=page.locator('details.audit-details').first(); await outer.locator(':scope > summary').click(); await outer.locator('details.audit-it-details > summary').click(); return outer.locator(':scope > summary').evaluate(summary=>{ const style=getComputedStyle(summary); const action=summary.querySelector('.audit-disclosure-action').getBoundingClientRect(); const result={paddingLeft:style.paddingLeft,paddingRight:style.paddingRight,height:summary.getBoundingClientRect().height,actionHeight:action.height}; if(result.paddingLeft!=='24px'||result.paddingRight!=='24px'||result.height<58||result.actionHeight<44) throw new Error(JSON.stringify(result)); return result; }); }"
```

Then run:

```powershell
playwright-cli -s=hardware-expanded-details screenshot --filename=.playwright-cli\hardware-details-wide.png --full-page
```

Inspect the screenshot and confirm:

- title, helper, and trailing action do not collide;
- all seven stage rows have centred icons and vertically centred copy;
- status labels align consistently;
- the IT disclosure is visually secondary;
- no row content touches the card edge;
- action buttons remain centred.

- [ ] **Step 3: Verify medium layout at 900 px**

Run:

```powershell
playwright-cli -s=hardware-expanded-details resize 900 1000
```

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.evaluate(() => document.querySelectorAll('details.audit-details, details.audit-it-details').forEach(details=>{ details.open=true; })); const result=await page.evaluate(() => ({horizontalOverflow:document.documentElement.scrollWidth-document.documentElement.clientWidth,visibleStages:[...document.querySelectorAll('.audit-stage-row')].filter(node=>node.getClientRects().length).length,visibleEvidence:[...document.querySelectorAll('.audit-evidence-card')].filter(node=>node.getClientRects().length).length,stageOverflow:[...document.querySelectorAll('.audit-stage-row')].some(node=>node.scrollWidth>node.clientWidth),evidenceOverflow:[...document.querySelectorAll('.audit-evidence-card')].some(node=>node.scrollWidth>node.clientWidth)})); if(result.horizontalOverflow!==0||result.visibleStages!==35||result.visibleEvidence!==8||result.stageOverflow||result.evidenceOverflow) throw new Error(JSON.stringify(result)); return result; }"
```

Expected: all 35 stage rows and all 8 fact/run evidence cards are rendered,
with zero horizontal page overflow and both overflow booleans `false`.

- [ ] **Step 4: Verify compact layout at 480 px**

Run:

```powershell
playwright-cli -s=hardware-expanded-details resize 480 900
```

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.evaluate(() => document.querySelectorAll('details.audit-details, details.audit-it-details').forEach(details=>{ details.open=true; })); const result=await page.evaluate(() => { const rows=[...document.querySelectorAll('.audit-stage-row')]; const statuses=[...document.querySelectorAll('.audit-stage-status')]; const summaries=[...document.querySelectorAll('summary.audit-disclosure')]; return {visibleStages:rows.filter(node=>node.getClientRects().length).length,visibleEvidence:[...document.querySelectorAll('.audit-evidence-card')].filter(node=>node.getClientRects().length).length,statusColumnsValid:statuses.every(node=>getComputedStyle(node).gridColumnStart==='2'),headerInsetsValid:summaries.every(node=>getComputedStyle(node).paddingLeft==='16px'&&getComputedStyle(node).paddingRight==='16px'),horizontalOverflow:document.documentElement.scrollWidth-document.documentElement.clientWidth,stageOverflow:rows.some(node=>node.scrollWidth>node.clientWidth)}; }); if(result.visibleStages!==35||result.visibleEvidence!==8||!result.statusColumnsValid||!result.headerInsetsValid||result.horizontalOverflow!==0||result.stageOverflow) throw new Error(JSON.stringify(result)); return result; }"
```

Expected: all terminal content is rendered; every status starts in grid column
2; every header has 16 px side insets; and there is no horizontal overflow.

- [ ] **Step 5: Verify genuine 200% text scaling**

Use a 720 px viewport, expand every terminal record, then double the computed
font size of every element that owns visible text. This tests text scaling
without mistaking a narrow viewport for larger text:

```powershell
playwright-cli -s=hardware-expanded-details resize 720 900
```

Then run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => { await page.reload(); const result=await page.evaluate(() => { document.querySelectorAll('details.audit-details, details.audit-it-details').forEach(details=>{ details.open=true; }); const textOwners=[...document.body.querySelectorAll('*')].filter(node=>[...node.childNodes].some(child=>child.nodeType===Node.TEXT_NODE&&child.textContent.trim())&&parseFloat(getComputedStyle(node).fontSize)>0); const original=textOwners.map(node=>parseFloat(getComputedStyle(node).fontSize)); textOwners.forEach((node,index)=>{ node.style.fontSize=(original[index]*2)+'px'; }); const scaled=textOwners.every((node,index)=>parseFloat(getComputedStyle(node).fontSize)>=original[index]*1.99); const rows=[...document.querySelectorAll('.audit-stage-row')]; const evidence=[...document.querySelectorAll('.audit-evidence-card')]; return {scaledTextNodes:textOwners.length,scaled,visibleStages:rows.filter(node=>node.getClientRects().length).length,visibleEvidence:evidence.filter(node=>node.getClientRects().length).length,horizontalOverflow:document.documentElement.scrollWidth-document.documentElement.clientWidth,stageOverflow:rows.some(node=>node.scrollWidth>node.clientWidth),evidenceOverflow:evidence.some(node=>node.scrollWidth>node.clientWidth),actionOverflow:[...document.querySelectorAll('.audit-actions')].some(node=>node.scrollWidth>node.clientWidth)}; }); if(!result.scaled||result.scaledTextNodes<100||result.visibleStages!==35||result.visibleEvidence!==8||result.horizontalOverflow!==0||result.stageOverflow||result.evidenceOverflow||result.actionOverflow) throw new Error(JSON.stringify(result)); return result; }"
```

Capture the 200% text layout:

```powershell
playwright-cli -s=hardware-expanded-details screenshot --filename=.playwright-cli\hardware-details-200-percent.png --full-page
```

Inspect that labels wrap without clipping, the status remains associated with
its stage, and buttons remain centred and readable.

- [ ] **Step 6: Re-run exact unified-icon geometry checks**

Run:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const xs=[...document.querySelectorAll('.x-glyph')]; const ticks=[...document.querySelectorAll('.check-glyph, .audit-step.done .audit-step-dot')]; const stages=[...document.querySelectorAll('.audit-stage-icon')]; const hosts=[...xs,...ticks,...stages]; const deltas=hosts.map(host=>{ const svg=host.querySelector(':scope > svg'); if(!svg) return Infinity; const a=host.getBoundingClientRect(); const b=svg.getBoundingClientRect(); return Math.max(Math.abs((a.left+a.width/2)-(b.left+b.width/2)),Math.abs((a.top+a.height/2)-(b.top+b.height/2))); }); const exactX=xs.every(host=>{ const svg=host.querySelector(':scope > svg.audit-x-icon'); const circle=svg?.querySelector('circle'); const path=svg?.querySelector('path'); return circle?.getAttribute('cx')==='12'&&circle?.getAttribute('cy')==='12'&&path?.getAttribute('d')==='M8.75 8.75L15.25 15.25M15.25 8.75L8.75 15.25'; }); const exactTicks=ticks.every(host=>{ const svg=host.querySelector(':scope > svg.audit-check-icon'); const circle=svg?.querySelector('circle'); const path=svg?.querySelector('path'); return circle?.getAttribute('cx')==='12'&&circle?.getAttribute('cy')==='12'&&path?.getAttribute('d')==='M7.45 11.55L10.45 14.55L16.55 8.45'; }); const stageRings=[...document.querySelectorAll('.audit-stage-icon .audit-stage-ring')]; const stageChecks=[...document.querySelectorAll('.audit-stage-icon[data-tone=success] .audit-stage-mark')]; const exactStages=stageRings.every(circle=>circle.getAttribute('cx')==='12'&&circle.getAttribute('cy')==='12')&&stageChecks.every(path=>path.getAttribute('d')==='M7.45 11.55L10.45 14.55L16.55 8.45'); const pseudoFree=hosts.every(host=>getComputedStyle(host,'::before').content==='none'); const result={x:xs.length,ticks:ticks.length,stages:stages.length,stageChecks:stageChecks.length,maxDelta:Math.max(...deltas),exactX,exactTicks,exactStages,pseudoFree}; if(result.x!==4||result.ticks!==14||result.stages!==35||result.stageChecks!==10||result.maxDelta>0.01||!exactX||!exactTicks||!exactStages||!pseudoFree) throw new Error(JSON.stringify(result)); return result; })"
```

Expected: 4 X hosts, 14 original tick hosts, 35 stage hosts, 10 stage
completion ticks, a maximum host/SVG centre delta of at most 0.01 px, exact
ring/mark geometry, and no competing pseudo-element glyphs.

- [ ] **Step 7: Reset the live preview to its initial state**

Run:

```powershell
playwright-cli -s=hardware-expanded-details resize 1440 1100
```

Then:

```powershell
playwright-cli -s=hardware-expanded-details reload
```

Verify the initial state:

```powershell
playwright-cli -s=hardware-expanded-details run-code "async page => page.evaluate(() => { const result={selected:document.querySelectorAll('.audit-state.is-selected').length,firstSelected:document.querySelector('.audit-state')?.classList.contains('is-selected'),outerOpen:document.querySelectorAll('details.audit-details[open]').length,nestedOpen:document.querySelectorAll('details.audit-it-details[open]').length}; if(result.selected!==1||!result.firstSelected||result.outerOpen!==0||result.nestedOpen!==0) throw new Error(JSON.stringify(result)); return result; })"
```

- [ ] **Step 8: Record the final hash and confirm production files are untouched**

Run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath .superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html
```

Run:

```powershell
git status --short
git diff --binary | git hash-object --stdin
git diff --cached --binary | git hash-object --stdin
```

Expected: the companion hash differs from Task 1. If no parallel change
occurred, the complete status and both tracked-diff fingerprints exactly match
the Task 1 baseline. If the Model Inspection chat advanced concurrently,
inspect and record that delta without modifying or reverting it. In either
case, no Hardware Inspection production source appears, and the ignored
companion does not appear. Do not force-add the companion file.

## Final handoff checklist

- [ ] Live URL remains `http://localhost:56163/`.
- [ ] User can select cards, open both detail levels, and press prototype action buttons.
- [ ] Five terminal states each render seven truthful canonical stages.
- [ ] `Stopping safely` exposes no premature stable record.
- [ ] IT details are secondary, closed by default, and contain bounded illustrative evidence.
- [ ] The stage list uses natural page scrolling.
- [ ] Wide, medium, compact, genuine 200%-text, keyboard, reduced-motion, and icon-centre checks pass.
- [ ] No Model Inspection production file was edited, staged, or committed.
- [ ] The excluded companion file was not force-added to Git.
