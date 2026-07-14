# Assumption Register

**Document ID:** REG-ASM-001  
**Version:** 0.1  
**Status:** Draft structure — entries require review  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Pending content-population review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This register records statements currently treated as true for planning, design, implementation or evaluation but which require evidence before they can be relied upon as confirmed project facts.

The existing draft assumption `A-001` has been moved from the mixed register into this dedicated register without changing its meaning. Missing fields remain explicitly pending until the content review.

## Outcome vocabulary

| Outcome | Meaning |
|---|---|
| Pending | The assumption has not yet been adequately checked. |
| Confirmed | The assumption is supported by sufficient reviewable evidence for the stated scope. |
| Rejected | Evidence shows the assumption is false or unreliable. |
| Superseded | A later assumption or controlled decision replaces this record. |

## Assumption records

| Assumption ID | Assumption | Source | Importance / dependency | Validation method | Evidence | Owner | Outcome | Consequence if false | Related IDs | Date raised | Last reviewed | Next review |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| A-001 | Raw experiment evidence can be recovered. | Previous mixed risk/assumption register; original source review pending. | The project depends on recoverable raw outputs, manifests and checksums for reproducibility and final evaluation claims. | Run an evidence audit covering required campaigns, raw-output locations, manifests, checksums and independent backup or explicit gaps. | Evidence manifest and recovery index; exact links pending. | Arian B | Pending | Repeat the minimum critical runs where possible and record any irrecoverable evidence gap as a limitation. | `G-M07`; `PD-03`; `EP-020` | 2026-07-14 | 2026-07-14 | Pending |

## Entry rule

An assumption is not confirmed merely because it appears reasonable or is written in a planning document.

Each assumption must identify:

- why the project depends on it;
- how it will be checked;
- the evidence reviewed;
- the consequence if it is false;
- a clear Pending, Confirmed, Rejected or Superseded outcome;
- an owner and review dates.
