# Project engineering tools

**Status:** Living source-adjacent documentation  
**Current branch:** `feature/model-inspection`

## Purpose

This folder contains isolated engineering utilities used to prove feasibility,
collect evidence or support development without becoming part of the shipped
WinUI application by accident.

A tool belongs here when it has a different lifecycle from the application and
should be built or executed explicitly.

## Current tools

- [Model Inspection LLamaSharp feasibility spike](./ModelInspection.LlamaSharpSpike/README.md)

## Boundary rules

- A tool project is not an application feature merely because it is in the same
  repository.
- Native or experimental dependencies should remain here until their safety,
  compatibility and packaging gates pass.
- Tool output must distinguish local exploratory artifacts from controlled
  formal evidence.
- Tools must not write to model files unless that behavior is explicitly
  designed, tested and approved. The LLamaSharp spike is read-only.
- A successful tool experiment does not automatically prove the WinUI
  application integration.
- The application project should reference a tool only through deliberately
  extracted production contracts or libraries, never by referencing a console
  executable project directly.

## Source-of-truth order

```text
1. Tool source and executable tests
2. Tool README
3. Approved ADR and design specification
4. Historical discussion or exploratory notes
```
