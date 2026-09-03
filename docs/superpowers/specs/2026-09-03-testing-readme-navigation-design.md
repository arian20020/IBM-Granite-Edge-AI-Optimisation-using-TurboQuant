# Beginner-Friendly Testing README Hierarchy Design

**Date:** 3 September 2026  
**Target branch:** `main`  
**Target worktree:** `C:\Users\Student\Granite-Main-Merge-2026-09-02`  
**Status:** Awaiting implementation approval

## Problem

The testing material is complete but spread across several large directory trees. Existing README files explain many important areas, but coverage is uneven. A new reader can still struggle to tell the difference between a protocol, a manifest, raw evidence, a processed result, a publication package and the scripts that connect them.

The repository needs a progressive README hierarchy. A reader should be able to start at a broad folder, follow links downward, and understand each meaningful folder before reaching its files.

## Goal

Make the testing workspace understandable to a beginner without changing any test, result or program behaviour.

Each in-scope README will explain:

1. what its folder is for;
2. where the folder fits in the testing process;
3. where a new reader should go next;
4. what each immediate child folder contains;
5. what each immediate file records or performs;
6. which material is authoritative, generated, editable, historical or raw;
7. important warnings and common interpretation mistakes; and
8. how to return to the parent guide or continue to the next useful guide.

## Documentation model

The READMEs will use progressive disclosure. A parent README explains its immediate children. Each child README then explains its own children and files. This avoids copying the full repository tree into every document while still giving a continuous explanation from the testing root down to the files.

The main navigation flow is:

```text
docs/testing
  -> final-results
     -> numbered route
        -> data, evidence, reports, reproduction or validation
           -> individual files

scripts/testing
  -> command, campaign, reporting, test or tool group
     -> individual scripts

experiments
  -> protocols, manifests, raw results, processed results, figures or scripts
     -> route or experiment family
        -> evidence files or grouped generated runs
```

## In-scope directories

### 1. Curated testing documentation

Cover `docs/testing` and its meaningful child directories, including:

- `cleanup`
- `failure-evidence`
- `final-results`
- `legacy`
- `manual`
- `plans`
- `runbooks`
- `source-material`
- `strategies`
- `test-reports`
- `ux-evaluation`
- `workbook05`
- `workbooks`

Existing README files will be expanded only where needed. Their useful instructions, status statements and evidence boundaries will be preserved.

### 2. Final-results publication tree

Cover every directory below `docs/testing/final-results`, including:

- the six numbered packages;
- each package's `data`, `evidence`, `reports`, `reproduction` and `validation` folders;
- nested failure, source, protocol, quality, script and system folders;
- the top-level `catalog`, `standards`, `schemas` and `validation` folders.

Every folder in this tree is a curated publication surface, so every directory receives a README. Route-level guides retain their exact accounting and limitations. Lower-level guides explain each file and identify the correct source of truth.

### 3. Supported testing scripts

Cover `scripts/testing` and every meaningful source directory below it:

- `campaigns` and its route modules;
- `cli`;
- `examples`;
- `reporting`;
- `tests`, including `acceptance`, `integration`, `unit` and `fixtures`;
- `tools`;
- `turbovec`; and
- `workbook05`, including `phase3`.

Each README explains what the scripts do, whether they are read-only or write outputs, their important inputs and outputs, and the safer high-level entry point when one exists. It does not duplicate full command help or source code.

### 4. Meaningful experiment directories

Cover the semantic layers under `experiments`, including:

- `figures`;
- `granite_turboquant_intel` and its category/route navigation folders;
- `manifests`;
- `patches`;
- `processed-results`;
- `prompts`;
- `protocols`;
- `raw-results`;
- `rubrics`; and
- `scripts`.

Route and experiment-family directories are included. Generated run-instance directories are explained by their parent README as a naming pattern rather than receiving repetitive README files of their own.

## Explicit exclusions

Do not add READMEs to:

- `__pycache__`, compiler caches or temporary directories;
- individual `sample-*`, `warmup` or `pilot` directories;
- individual attempt/run-ID directories;
- date/timestamp batch directories whose only purpose is to hold one generated run;
- copied third-party repositories, model directories or build outputs;
- recovery folders or unrelated application-development directories.

These exclusions prevent documentation files from altering raw-evidence directories or appearing to be part of captured experimental output. Their parent README must still explain what the excluded folders contain and how their names are structured.

## README content standard

Use simple English and short sentences. Define technical terms the first time they appear. Prefer concrete guidance such as “Use this CSV when you need one row per attempted configuration” over descriptions such as “contains analytics data.”

Each README uses only the sections that help its folder:

```markdown
# Folder name

One short explanation of the folder.

## Start here

The first file or child folder a beginner should open.

## How this folder fits into testing

Its place in the evidence flow.

## Folders

Every immediate semantic child folder and its purpose.

## Files

Every immediate file, its format, purpose, authority and normal reader.

## How to use this folder

Common read, reproduce or validation tasks.

## Important boundaries

Warnings about editing, generated files, failed results and unsupported comparisons.

## Related guides

Links to the parent and next useful README files.
```

Empty sections will be omitted. A folder containing many generated siblings may use a pattern table, but all distinct file types and semantic groups must be explained.

## File explanation rules

Descriptions must be based on the actual file, its header/schema, existing documentation or the script's command interface. They must not be guessed from the filename alone.

Common formats will be explained in beginner terms:

- CSV: a table that can be opened in Excel or analysed by a script;
- JSON: structured machine-readable evidence or configuration;
- Markdown: a readable source document;
- DOCX/XLSX/PDF: review or handoff versions, with the authoritative source identified;
- log/TXT: captured diagnostic evidence, not a summary conclusion;
- Python/PowerShell: executable tooling, with mutation and safety behaviour stated;
- SHA-256 manifest: checksums used to detect changed files.

README files must clearly distinguish:

- raw observations from processed summaries;
- passed, failed, blocked and unavailable attempts;
- source files from generated reports;
- a reproducible command from proof that the command was run;
- within-route comparisons from unsupported cross-route rankings; and
- repository/harness testing from application usability or accessibility testing.

## Preservation rules

- Change only `README.md` files and the approved design/plan documents.
- Do not alter test data, scripts, logs, workbooks, reports, manifests or generated artefacts.
- Do not remove useful content from an existing README.
- Preserve exact campaign totals, route names, release identifiers and limitations.
- Do not describe a planned, blocked or unavailable test as completed.
- Do not place new files inside raw run-instance directories.
- Preserve the clean starting state of the `main` worktree apart from the documentation change.

## Implementation approach

1. Build a deterministic inventory of in-scope directories and immediate contents.
2. Classify each directory as navigation, protocol, source input, raw evidence, processed evidence, publication, tooling or validation.
3. Read the files needed to write accurate descriptions, including schemas, headers, script help and existing guides.
4. Improve existing READMEs without discarding their useful content.
5. Add missing READMEs from the closest suitable content pattern.
6. Link parents and children using repository-relative links.
7. Review the complete hierarchy from a beginner's perspective.

## Verification gates

The work is complete only when all of these checks pass:

1. Every in-scope directory contains `README.md`.
2. Every immediate semantic child folder is explained or covered by an explicit generated-folder pattern.
3. Every immediate non-README file is named and explained, or belongs to a clearly documented homogeneous generated group.
4. Every relative Markdown link resolves to an existing repository path.
5. Campaign totals and route statuses match the canonical final-results catalogues.
6. Existing README facts and safety boundaries remain present unless replaced by a clearer equivalent.
7. `git diff` contains only README files plus the approved design and implementation-plan documents.
8. No README is added inside an excluded cache, sample, attempt, timestamp or raw run-instance directory.
9. A final wording review removes unexplained jargon, vague descriptions and unsupported claims.

## Expected result

A beginner can start at `docs/testing/README.md`, `scripts/testing/README.md` or `experiments/README.md`, understand the purpose of the area, follow the documented folder path, and know which file to open for a result, command, protocol, failure, validation record or source artefact. Experienced readers retain direct access to the same authoritative files without an extra documentation layer changing the evidence.
