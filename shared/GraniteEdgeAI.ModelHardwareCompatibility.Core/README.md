# Model/hardware compatibility core

**Status:** Byte arithmetic, planning context, phase composition, safety policy, fit
classification, candidate vocabulary and the GGUF resource estimator implemented and tested.
Support matrix, candidate generation, mode selection, orchestration, screens and owner adapters
remain later gates.
**Last reviewed:** 2026-08-20

## Purpose

This pure `net8.0` project answers one question: given what we know about a model and what we know
about this computer, which configurations could run safely, and how much memory would each need?

```text
Model Inspection handoff  ─┐
                           ├─→  GraniteEdgeAI.ModelHardwareCompatibility.Core  ─→  selected plan
Hardware Inspection handoff┘
```

It selects a plan. It does not collect hardware, parse models, run converters, run inference or
optimise artifacts.

## Owned responsibilities

- checked byte arithmetic with typed unknowns;
- planning-context resolution;
- per-component resource estimation and per-pool peak composition;
- versioned safety and estimator policies with explicit provenance;
- safe-budget comparison and fit classification;
- complete candidate configurations with a deterministic fingerprint.

## Forbidden responsibilities

- WinUI, XAML, pages, ViewModels or navigation;
- any reference to Hardware Inspection or Model Inspection production types;
- Windows-only APIs, memory collection or device enumeration;
- any path, filename, model name, hostname, credential or raw tool output.

## Why the numbers are conservative

A false-safe answer crashes the user's machine; a false-unsafe answer is an inconvenience. So every
margin is added to the requirement and never subtracted, equality counts as fitting only after all
mandatory margins are included, and any unknown input collapses a candidate to `NotEstablished`
rather than defaulting to zero.

Both policies currently ship `Provisional` provenance: their values are documented defaults, not
measurements. Every estimate built on them records the limitation. Moving either policy to
`Calibrated` is a code change today, not a data change: both are hardcoded C# factories with
private constructors, and no JSON (or other externally-loadable) asset exists yet. `Calibrated`
itself has no factory. See "Known follow-up, recorded not hidden" in the GGUF resource estimator
plan for what would need to exist first.
