# Hardware Inspection Terminal Cards v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render approved outcome/recovery copy and exact state-driven actions as reusable Hardware-owned WinUI cards.

**Architecture:** `HardwareInspectionOutcomeCard` renders immutable top-level presentation state and selects only semantic visual tones. `HardwareInspectionActionCard` creates buttons solely from the state's approved action list and raises a typed action event; it contains no navigation or run logic.

**Tech Stack:** WinUI 3, C# 12, packaged MSTest UI tests.

---

### Task 1: RED UI contracts

- Add UI tests for success, warning, failure, stopping, and invalid copy/tone; reject Active state.
- Add UI tests for exact action labels/order/enabled state, disabled Continue help, typed invocation, and replacement without stale buttons.
- Build and capture missing-control RED.

### Task 2: Outcome card

- Create `HardwareInspectionOutcomeCard.xaml(.cs)` with theme-local 840 px semantic card, 44 px status glyph, wrapped approved text, accessible announcement, and neutral/success/warning/error visual states.
- Accept every non-Active presentation kind and reject Active.

### Task 3: Action card

- Create `HardwareInspectionActionCard.xaml(.cs)` with centred, wrapping, >=44 px buttons, exact labels/help, enabled state, and `ActionRequested` carrying `HardwareInspectionActionKind`.
- Never add hidden actions, invent a default action, navigate, or start a run.

### Task 4: Verify

- Run focused tests, all packaged UnitTests, Hardware Python contracts, and `git diff --check`.
- Commit exact control/test/plan scope and preserve downstream page/navigation ownership.
