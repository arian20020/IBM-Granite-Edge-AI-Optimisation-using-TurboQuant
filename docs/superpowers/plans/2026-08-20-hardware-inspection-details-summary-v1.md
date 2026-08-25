# Hardware Inspection Details and Summary v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render canonical hardware facts and the approved two-level terminal details disclosure without exposing raw evidence.

**Architecture:** Pure presentation records carry already-sanitised fact tiles, seven detail rows, and grouped IT items. A summary factory maps `HardwareSnapshot` to distinct processor/memory/graphics/storage/runtime/source values. Hardware-owned controls render those models and preserve or reset disclosure state only when explicitly requested.

**Tech Stack:** C# 12, WinUI 3 `Expander`, packaged MSTest UI tests.

---

### Task 1: RED models and controls

- Test distinct installed/usable/available memory and dedicated/shared graphics formatting.
- Test no path/model/compatibility fields appear.
- Test seven detail rows, outer and IT disclosures closed initially, same-run preservation, new-run reset, exact headings, report badge, and absence of nested scrolling.

### Task 2: Presentation models and summary factory

- Add immutable `HardwareFactPresentation`, `HardwareSummaryPresentation`, `HardwareInspectionDetailRow`, `HardwareInspectionTechnicalItem`, `HardwareInspectionTechnicalGroup`, and `HardwareInspectionDetailsState`.
- Add `HardwareSummaryPresentationFactory` using invariant binary-size formatting and distinct labels; copy every input collection.

### Task 3: WinUI controls

- Add `HardwareInspectionSummaryCard.xaml(.cs)` with `This computer`, fact groups, wrapped values, and Hardware theme tokens.
- Add `HardwareInspectionDetailsCard.xaml(.cs)` with outer `Inspection details`, exactly seven row items, nested `Technical information for IT`, native expanders, no internal ScrollViewer, and explicit preserve/reset behavior.

### Task 4: Verify

- Run focused and full packaged tests, Python contract/theme tests, source assertion that Details XAML contains no `ScrollViewer`, and `git diff --check`.
- Commit only the plan, models/factory, two controls, and tests.
