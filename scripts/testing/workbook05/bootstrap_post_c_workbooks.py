from __future__ import annotations

import csv
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable
import argparse
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_DIR = ROOT / 'docs/testing/workbooks/text-templates'
GENERATED_DIR = ROOT / 'docs/testing/workbooks/generated'
DOCS_TESTING = ROOT / 'docs/testing'
WORKBOOKS_DIR = ROOT / 'docs/testing/workbooks'


def table(headers: list[str], rows: Iterable[Iterable[object]]) -> str:
    lines = [
        '| ' + ' | '.join(headers) + ' |',
        '| ' + ' | '.join('---' for _ in headers) + ' |',
    ]
    for row in rows:
        values = [str(value).replace('|', r'\|').replace('\n', '<br>') for value in row]
        lines.append('| ' + ' | '.join(values) + ' |')
    return '\n'.join(lines)


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + '\n', encoding='utf-8', newline='\n')


WORKBOOKS = [
    ('WB-07', 'D1', '07_Workbook_05_D1_Codec_Conformance_Controlled_Workbook_v1.md', '07_Workbook_05_D1_Codec_Conformance_Controlled_Workbook_v1.docx', 'Codec conformance and verified packed-storage decisions'),
    ('WB-08', 'D2', '08_Workbook_05_D2_Diagnostic_KV_Sweep_Controlled_Workbook_v1.md', '08_Workbook_05_D2_Diagnostic_KV_Sweep_Controlled_Workbook_v1.docx', 'Diagnostic ordered K/V capability sweep and memory ranking'),
    ('WB-09', 'E1', '09_Workbook_05_E1_Granite_3B_Frontier_Formal_Controlled_Workbook_v1.md', '09_Workbook_05_E1_Granite_3B_Frontier_Formal_Controlled_Workbook_v1.docx', 'Granite 3B context frontier and matched formal evaluation'),
    ('WB-10', 'E2', '10_Workbook_05_E2_Granite_8B_Feasibility_Controlled_Workbook_v1.md', '10_Workbook_05_E2_Granite_8B_Feasibility_Controlled_Workbook_v1.docx', 'Granite 8B safety and feasibility gate'),
    ('WB-11', 'E3', '11_Workbook_05_E3_Granite_30B_Bounded_Feasibility_Controlled_Workbook_v1.md', '11_Workbook_05_E3_Granite_30B_Bounded_Feasibility_Controlled_Workbook_v1.docx', 'Granite 30B lowest-weight-first bounded feasibility extension'),
    ('WB-12', 'E4', '12_Workbook_05_E4_Cross_Family_Repeatability_Controlled_Workbook_v1.md', '12_Workbook_05_E4_Cross_Family_Repeatability_Controlled_Workbook_v1.docx', 'Asymmetric, cross-family, ablation, repeatability and negative paths'),
    ('WB-13', 'F', '13_Workbook_05_F_Evidence_Closure_Controlled_Workbook_v1.md', '13_Workbook_05_F_Evidence_Closure_Controlled_Workbook_v1.docx', 'Independent evidence ingestion and final workbook closure'),
]

ALLOWED_STATUSES = [
    'Not started',
    'Blocked pending predecessor',
    'Passed',
    'Failed',
    'Blocked',
    'Not applicable',
    'Skipped by frontier',
    'Infrastructure interrupted',
]


def common_header(number: str, title: str, workbook_id: str, filename: str, stage: str, purpose: str) -> str:
    return f"""# {number} {title} v1.0

**Workbook ID:** `{workbook_id}`  
**Controlled filename:** `{filename}`  
**Campaign ID:** `GTQ-WB05-MF-v1`  
**Stage:** `{stage}`  
**Status:** Initialised - no live result is entered  

## Purpose

{purpose}

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

{table(['Control', 'Fixed rule'], [
    ('Canonical source', f'docs/testing/workbooks/text-templates/{filename.replace(".docx", ".md")}'),
    ('Generated working copy', f'docs/testing/workbooks/generated/{filename}'),
    ('Execution index', 'docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv'),
    ('Configuration matrix', 'docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv'),
    ('Evidence register', 'docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv'),
    ('Allowed row states', ', '.join(ALLOWED_STATUSES)),
    ('Live authority', 'None until predecessor gates and an explicit self-hosted dispatch are accepted'),
])}

## Initial revision record

{table(['Version', 'Date', 'Change', 'Affected scope', 'Status'], [
    ('1.0', '2026-08-22', 'Created the first controlled post-C workbook structure.', stage, 'Current - initialised'),
])}
"""


def d1_template() -> str:
    rows = [
        ('RA-D1-01', 'Route A', 'OV-B08', 'Official merged TurboQuant source/API boundary', 'Source and property inventory', 'Not started'),
        ('RA-D1-02', 'Route A', 'OV-B09', 'Official U3/U4 and independent K/V controls', 'Pinned source + property proof', 'Not started'),
        ('RA-D1-03', 'Route A', 'OV-B10', 'Official TurboQuant unit/functional diagnostics', 'Test log + deterministic fixture', 'Not started'),
    ]
    for n in range(1, 13):
        rows.append((f'RB-D1-{n:02d}', 'Route B', f'OVT-A{n:02d}', f'Workbook 05 algorithm-level conformance row OVT-A{n:02d}', 'Expected/actual bytes + numerical/dispatch evidence', 'Blocked pending predecessor'))
    return common_header('07', 'Workbook 05 D1 Codec Conformance Controlled Workbook', 'WB-07', WORKBOOKS[0][3], 'D1',
        'Resolve algorithm-level conformance for every admitted codec before any diagnostic or model benchmark. This implements completion-plan Task R17: round trip, packing/unpacking, norm, record size, finite-value boundaries, NaN/Inf policy, deterministic rotation/projection/codebook behaviour, independent K/V dispatch, activation, fallback, allocation formula, repository tests, and bounded quality/perplexity smoke.') + f"""

[[PAGEBREAK]]

# 1. Predecessor and identity gate

{table(['Field', 'Required record', 'Observed value', 'Status / evidence'], [
    ('C1 asset lock', 'Accepted immutable model/tokenizer/conversion record', '', 'Blocked pending predecessor'),
    ('C2 harness', 'Accepted process, watchdog, retry and checkpoint evidence', '', 'Blocked pending predecessor'),
    ('C3 activation/storage foundation', 'Request/property/dispatch/fallback/storage schemas accepted', '', 'Blocked pending predecessor'),
    ('Route A build identity', 'Exact Runtime and GenAI commits + binary hashes', '', 'Not started'),
    ('Route B disposition', 'Admitted, bounded repair, or evidence-backed blocked', '', 'Not started'),
])}

# 2. Codec inventory and expected representation

Expected bytes are planning values from WB-04/WB-05 and must be corrected only from executable evidence.

{table(['Route', 'Codec', 'Precision/family', 'Planning bytes per head at dim 128', 'Admission', 'Verified bytes', 'Decision'], [
    ('Route A', 'SCALAR', 'U8', '136', 'Pending', '', 'Not started'),
    ('Route A', 'SCALAR', 'U4', '72', 'Pending', '', 'Not started'),
    ('Route A', 'TURBO', 'U4', '72', 'Pending', '', 'Not started'),
    ('Route A', 'TURBO', 'U3', '52', 'Pending', '', 'Not started'),
    ('Route B', 'TBQ4', '4-bit family', '68', 'Pending', '', 'Blocked pending predecessor'),
    ('Route B', 'TBQ3', '3-bit family', '52', 'Pending', '', 'Blocked pending predecessor'),
    ('Route B', 'TBQ4_QJL', 'QJL 4-bit family', '88', 'Pending', '', 'Blocked pending predecessor'),
    ('Route B', 'TBQ3_QJL', 'QJL 3-bit family', '72', 'Pending', '', 'Blocked pending predecessor'),
    ('Route B', 'POLAR4', 'Polar 4-bit family', '~68', 'Pending', '', 'Blocked pending predecessor'),
    ('Route B', 'POLAR3', 'Polar 3-bit family', '~52', 'Pending', '', 'Blocked pending predecessor'),
])}

# 3. Conformance execution register

{table(['Execution ID', 'Route', 'Controlled test ID', 'Test boundary', 'Required evidence', 'Initial status'], rows)}

# 4. Per-codec result entry

{table(['Codec/config', 'Expected K bytes', 'Measured K bytes', 'Expected V bytes', 'Measured V bytes', 'Numerical result/tolerance', 'Dispatch proof', 'Fallback proof', 'Run/artifact/evidence', 'Decision'], [('', '', '', '', '', '', '', '', '', '') for _ in range(12)])}

# 5. D1 acceptance summary

{table(['Codec', 'Conformance', 'Verified storage', 'Dependent rows unblocked?', 'Failure/blocker code', 'Evidence path'], [('', '', '', '', '', '') for _ in range(10)])}

**D1 acceptance rule:** every admitted codec has one conformance decision and verified storage. A failed codec blocks only dependent configurations. No unexplained storage discrepancy may advance to D2 or formal comparison.
"""


def route_a_pairs():
    return [
        ('OV-TQS-01', 'TURBO', 'U4', 'TURBO', 'U4'),
        ('OV-TQS-02', 'TURBO', 'U3', 'TURBO', 'U3'),
        ('OV-TQS-03', 'TURBO', 'U4', 'TURBO', 'U3'),
        ('OV-TQS-04', 'TURBO', 'U3', 'TURBO', 'U4'),
        ('OV-TQS-05', 'TURBO', 'U4', 'SCALAR', 'U8'),
        ('OV-TQS-06', 'SCALAR', 'U8', 'TURBO', 'U4'),
        ('OV-TQS-07', 'TURBO', 'U3', 'SCALAR', 'U8'),
        ('OV-TQS-08', 'SCALAR', 'U8', 'TURBO', 'U3'),
        ('OV-TQS-09', 'TURBO', 'U4', 'SCALAR', 'U4'),
        ('OV-TQS-10', 'SCALAR', 'U4', 'TURBO', 'U4'),
        ('OV-TQS-11', 'TURBO', 'U3', 'SCALAR', 'U4'),
        ('OV-TQS-12', 'SCALAR', 'U4', 'TURBO', 'U3'),
    ]


def route_b_pairs():
    codecs = [
        ('TBQ4', '68'),
        ('TBQ3', '52'),
        ('TBQ4_QJL', '88'),
        ('TBQ3_QJL', '72'),
        ('POLAR4', '~68'),
        ('POLAR3', '~52'),
    ]
    pairs = []
    counter = 1
    for k_name, k_bytes in codecs:
        for v_name, v_bytes in codecs:
            pairs.append((f'OVT-S{counter:02d}', k_name, k_bytes, v_name, v_bytes))
            counter += 1
    return pairs


def d2_template() -> str:
    ra_rows = [(f'D2-RA-{i:02d}', 'Route A', tid, ka, kp, va, vp, 'Blocked pending predecessor') for i, (tid, ka, kp, va, vp) in enumerate(route_a_pairs(), 1)]
    rb_rows = [(f'D2-RB-{i:02d}', 'Route B', tid, k, kb, v, vb, 'Blocked pending predecessor') for i, (tid, k, kb, v, vb) in enumerate(route_b_pairs(), 1)]
    return common_header('08', 'Workbook 05 D2 Diagnostic K/V Sweep Controlled Workbook', 'WB-08', WORKBOOKS[1][3], 'D2',
        'Lock the measured low-memory-to-high-memory execution order and run every admitted ordered K/V pair. This implements Task R18. Rank is recalculated from measured K bytes plus measured V bytes; ties use symmetric first, lower K bytes, lower V bytes, then stable codec name.') + f"""

[[PAGEBREAK]]

# 1. D2 entry gate

{table(['Gate', 'Required state', 'Observed state', 'Evidence'], [
    ('D1 codec conformance', 'Every codec Passed, Failed, Blocked, or Not applicable', '', ''),
    ('Storage measurements', 'Verified K/V bytes for every admitted codec', '', ''),
    ('Diagnostic model', 'Immutable asset lock and successful standard baseline', '', ''),
    ('Harness', 'C2 accepted; watchdog/cooldown/retry enabled', '', ''),
])}

# 2. Route A ordered-pair plan

{table(['Execution ID', 'Route', 'Test ID', 'K algorithm', 'K precision', 'V algorithm', 'V precision', 'Initial status'], ra_rows)}

# 3. Route B 6 x 6 ordered-pair plan

{table(['Execution ID', 'Route', 'Test ID', 'K codec', 'Planning K bytes', 'V codec', 'Planning V bytes', 'Initial status'], rb_rows)}

# 4. Measured capability and rank register

{table(['Test ID', 'Verified K bytes', 'Verified V bytes', 'Total bytes', 'Tie-break key', 'Memory rank', 'Dispatch', 'Fallback', 'Output integrity', 'Run/artifact/evidence', 'Final status'], [('', '', '', '', '', '', '', '', '', '', '') for _ in range(16)])}

[[PAGEBREAK]]

# 5. Blocker propagation

{table(['Blocked codec', 'Root decision/evidence', 'Dependent test IDs', 'Applied status', 'Reason'], [('', '', '', '', '') for _ in range(8)])}

**D2 acceptance rule:** all 48 planned pairs are explicitly `Passed`, `Failed`, `Blocked`, or `Not applicable`; no pair remains unresolved. The execution index receives verified bytes, rank, frontier status, run ID and evidence path.
"""


def config_rows():
    rows = [
        ('RA-SCALAR-U8-SYM', 'Route A', 'SCALAR', 'U8', 'SCALAR', 'U8', '136', '136', 'Formal control'),
        ('RA-SCALAR-U4-SYM', 'Route A', 'SCALAR', 'U4', 'SCALAR', 'U4', '72', '72', 'Formal control'),
        ('RA-TURBO-U4-SYM', 'Route A', 'TURBO', 'U4', 'TURBO', 'U4', '72', '72', 'Decision candidate'),
        ('RA-TURBO-U3-SYM', 'Route A', 'TURBO', 'U3', 'TURBO', 'U3', '52', '52', 'Decision candidate'),
        ('RB-TBQ4-SYM', 'Route B', 'TBQ4', 'codec', 'TBQ4', 'codec', '68', '68', 'Experimental candidate'),
        ('RB-TBQ3-SYM', 'Route B', 'TBQ3', 'codec', 'TBQ3', 'codec', '52', '52', 'Experimental candidate'),
        ('RB-QJL4-SYM', 'Route B', 'TBQ4_QJL', 'codec', 'TBQ4_QJL', 'codec', '88', '88', 'Experimental candidate'),
        ('RB-QJL3-SYM', 'Route B', 'TBQ3_QJL', 'codec', 'TBQ3_QJL', 'codec', '72', '72', 'Experimental candidate'),
        ('RB-POLAR4-SYM', 'Route B', 'POLAR4', 'codec', 'POLAR4', 'codec', '~68', '~68', 'Experimental candidate'),
        ('RB-POLAR3-SYM', 'Route B', 'POLAR3', 'codec', 'POLAR3', 'codec', '~52', '~52', 'Experimental candidate'),
    ]
    return rows


def e1_template() -> str:
    contexts = [512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072]
    frontier_rows = []
    for cfg in config_rows():
        for context in contexts:
            frontier_rows.append((cfg[0], context, 'P5 end-marker retrieval fixture', '0', 'Blocked pending predecessor', '', '', ''))
    formal_rows = [(cfg[0], 'Pilot', 'Excluded', '', '', '', '', '', '') for cfg in config_rows()]
    formal_rows += [(cfg[0], 'Warm-up', 'Excluded', '', '', '', '', '', '') for cfg in config_rows()]
    for repetition in range(1, 4):
        formal_rows += [(cfg[0], f'Measured {repetition}', 'Included', '', '', '', '', '', '') for cfg in config_rows()]
    quality_rows = [(cfg[0], prompt, '', '', '', '', '', '', '') for cfg in config_rows() for prompt in [f'P{i}' for i in range(1, 7)]]
    return common_header('09', 'Workbook 05 E1 Granite 3B Frontier and Formal Controlled Workbook', 'WB-09', WORKBOOKS[2][3], 'E1',
        'Discover the safe Granite 3B context frontier for each admitted symmetric configuration and then collect matched formal performance, memory, quality and perplexity evidence. This combines completion-plan Tasks R19 and R20 while preserving every repetition and keeping frontier discovery separate from formal statistics.') + f"""

[[PAGEBREAK]]

# 1. Immutable experiment identity

{table(['Identity field', 'Required value/evidence', 'Observed value', 'Hash/status'], [
    ('Model repository/revision', 'Accepted Granite 4.1 3B asset lock', '', 'Blocked pending predecessor'),
    ('Converted model', 'Exact IR/config/tokenizer catalogue and hashes', '', ''),
    ('Runtime/GenAI', 'Accepted commits and binary hashes', '', ''),
    ('Prompt set', 'GTQ-PROMPTS-v1', '', ''),
    ('Rubric', 'GTQ-QUALITY-RUBRIC-v1', '', ''),
    ('P5 fixture', 'Frozen end-marker retrieval fixture + hash', '', ''),
    ('Generation controls', 'Frozen seed/sampling/output-token limit', '', ''),
])}

# 2. Symmetric configuration set

{table(['Configuration ID', 'Route', 'K algorithm', 'K precision', 'V algorithm', 'V precision', 'Planning K bytes', 'Planning V bytes', 'Role'], config_rows())}

# 3. Context frontier schedule

Start at 512 tokens, continue through 1,024, 2,048, 4,096, 8,192, 16,384, then double while supported and stable. Retry one potentially transient failure after cooldown. After a repeated failure, record the preceding stable point as the provisional frontier and mark higher points `Skipped by frontier`.

{table(['Configuration', 'Context', 'Fixture', 'Attempt', 'Initial status', 'Exact marker', 'Safety/exit', 'Run/artifact/evidence'], frontier_rows)}

# 4. Frontier summary

{table(['Configuration', 'Last stable context', 'First repeated failure or declared limit', 'Frontier class', 'Peak private MB', 'Minimum available RAM', 'KV MB', 'Evidence'], [('', '', '', '', '', '', '', '') for _ in range(10)])}

[[PAGEBREAK]]

# 5. Formal repetition register

The common formal context must be supported by every compared configuration. One pilot and one warm-up remain visible but excluded; at least three measured repetitions are retained individually.

{table(['Configuration', 'Rep role/no.', 'Statistics', 'Load ms', 'TTFT ms', 'Prompt tok/s', 'TPOT ms', 'Decode tok/s', 'Peak private MB'], formal_rows)}

# 6. Formal system/resource fields

{table(['Configuration/rep', 'Total generation ms', 'Input tokens', 'Output tokens', 'RAM before MB', 'Min available RAM MB', 'RAM after MB', 'K MB', 'V MB', 'CPU mean/peak', 'Device/placement', 'Exit/stability'], [('', '', '', '', '', '', '', '', '', '', '', '') for _ in range(18)])}

# 7. P1-P6 deterministic and rubric evaluation

Five dimensions are mandatory: correctness, instruction following, relevance/completeness, clarity/structure, and safety/faithfulness. Deterministic failure and critical caps cannot be overridden by a high subjective score. Both blinded pairwise presentation orders are retained.

{table(['Configuration', 'Prompt', 'Raw-output SHA-256', 'Deterministic result', 'Correctness', 'Instruction', 'Relevance', 'Clarity', 'Safety'], quality_rows)}

# 8. Quality, perplexity and matched-baseline summary

{table(['Configuration', 'Overall quality /10', 'Matched baseline', 'Quality delta', 'Material degradation?', 'Perplexity', 'Perplexity delta', 'Adjudication/evidence'], [('', '', '', '', '', '', '', '') for _ in range(10)])}

# 9. Formal metric summary

{table(['Configuration', 'Common context', 'Median TTFT (range)', 'Median prompt tok/s (range)', 'Median TPOT (range)', 'Median decode tok/s (range)', 'Median peak private MB (range)', 'K/V/total MB', 'Decision'], [('', '', '', '', '', '', '', '', '') for _ in range(10)])}

**E1 acceptance rule:** every admitted symmetric configuration has a bounded frontier and either matched independently validated formal results or an explicit blocker. No average may hide a failed prompt or a missing repetition.
"""


def e2_template() -> str:
    candidates = [
        ('E2-01', 'RA-TURBO-U3-SYM', 'Lowest-memory Route A candidate'),
        ('E2-02', 'RA-TURBO-U4-SYM', 'Decision-relevant Route A candidate'),
        ('E2-03', 'RA-SCALAR-U4-SYM', 'Aggressive standard control'),
        ('E2-04', 'RA-SCALAR-U8-SYM', 'Practical standard control'),
        ('E2-05', 'RB-TBQ3-SYM', 'Route B candidate if admitted and quality-valid'),
        ('E2-06', 'RB-QJL3-SYM', 'QJL candidate if admitted and quality-valid'),
        ('E2-07', 'RB-POLAR3-SYM', 'Polar candidate if admitted and quality-valid'),
    ]
    run_rows = [(cid, cfg, context, '0', 'Blocked pending predecessor', '', '', '') for cid, cfg, _ in candidates for context in [512, 1024, 2048, 4096, 8192]]
    return common_header('10', 'Workbook 05 E2 Granite 8B Feasibility Controlled Workbook', 'WB-10', WORKBOOKS[3][3], 'E2',
        'Determine whether the controlled 15.7 GiB laptop can use Granite 8B with the standard control and the most decision-relevant compressed candidates. This implements Task R22. Discovery-only marker success is not formal quality evidence.') + f"""

[[PAGEBREAK]]

# 1. Entry and safety gate

{table(['Gate', 'Required state', 'Observed/evidence', 'Decision'], [
    ('Granite 8B immutable asset', 'Exact repository/revision/model/tokenizer/conversion hashes', '', 'Blocked pending predecessor'),
    ('3B evidence', 'E1 accepted and candidate roles known', '', 'Blocked pending predecessor'),
    ('Candidate quality', 'Only quality-valid decision candidates promoted', '', ''),
    ('Machine preflight', 'No conflicting process; accepted RAM/commit/disk thresholds', '', ''),
    ('Harness', 'Same watchdog, retry, cooldown and cleanup as E1', '', ''),
])}

# 2. Candidate promotion register

{table(['Order', 'Configuration', 'Promotion reason', 'D2 memory rank', 'E1 quality decision', 'Promoted?', 'Evidence'], [(cid, cfg, reason, '', '', '', '') for cid, cfg, reason in candidates])}

# 3. 8B frontier attempts

{table(['Candidate', 'Configuration', 'Context', 'Attempt', 'Initial status', 'Marker/output integrity', 'Safety/exit', 'Run/artifact/evidence'], run_rows)}

# 4. Resource and placement record

{table(['Candidate/context', 'Load result', 'Actual device', 'Model placement', 'KV placement', 'Peak private MB', 'Min available RAM MB', 'Max commit %', 'Watchdog class', 'Cleanup'], [('', '', '', '', '', '', '', '', '', '') for _ in range(14)])}

# 5. Feasibility decision

{table(['Configuration', 'Decision: Feasible/Bounded/Blocked', 'Last stable context', 'First repeated failure or limit', 'Quality scope actually tested', 'Performance scope actually tested', 'Evidence'], [('', '', '', '', '', '', '') for _ in range(7)])}

**E2 acceptance rule:** each promoted candidate is classified `Feasible`, `Bounded`, or `Blocked` from real evidence. A discovery-only exact-marker pass must state that full 8B quality and formal performance remain unproven.
"""


def e3_template() -> str:
    candidates = [
        ('E3-CANDIDATE-01', 'Lowest accepted weight precision', 'D2 verified memory rank 1', 'Lowest-weight + lowest-KV first'),
        ('E3-CANDIDATE-02', 'Lowest accepted weight precision', 'D2 verified memory rank 2', 'Next-lowest verified KV candidate'),
        ('E3-CANDIDATE-03', 'Lowest accepted weight precision', 'RA-SCALAR-U4-SYM', 'Standard-cache comparison only if preflight is safe'),
        ('E3-CANDIDATE-04', 'Next accepted weight precision', 'D2 verified memory rank 1', 'Escalation only after candidate 1 completes safely'),
    ]
    attempts = [(cid, context, '0', 'Blocked pending predecessor', '', '', '', '') for cid, _, _, _ in candidates for context in [512, 1024, 2048, 4096]]
    return common_header('11', 'Workbook 05 E3 Granite 30B Bounded Feasibility Controlled Workbook', 'WB-11', WORKBOOKS[4][3], 'E3',
        'Execute the project-owner-approved Granite 30B bounded-feasibility extension in lowest-weight-first order. This extension was approved after the older R19-R23 plan. It is deliberately separate from the formal 3B campaign: its outcome may be Feasible, Bounded, or Blocked, and it cannot authorise formal performance statistics.') + f"""

[[PAGEBREAK]]

# 1. Extension identity and non-claims

{table(['Field', 'Required record', 'Observed value', 'Status'], [
    ('Model family/size', 'IBM Granite 4.1 30B candidate selected by accepted asset process', '', 'Not started'),
    ('Exact model repository/revision', 'Immutable full revision and file catalogue', '', 'Not started'),
    ('Weight candidate', 'Lowest accepted retained format first; no unreviewed format', '', 'Not started'),
    ('KV candidate', 'Derived from D2 verified memory order', '', 'Blocked pending predecessor'),
    ('Formal performance', 'Forbidden in this workbook', 'false', 'Fixed'),
    ('Formal quality', 'Forbidden unless a later separately approved package is created', 'false', 'Fixed'),
])}

# 2. Candidate order

{table(['Candidate ID', 'Weight selection rule', 'KV selection rule', 'Admission rationale', 'Resolved asset/config', 'Status'], [(cid, weight, kv, reason, '', 'Blocked pending predecessor') for cid, weight, kv, reason in candidates])}

# 3. Conservative bounded attempt ladder

Each candidate begins at 512 tokens. Higher points run only after a clean lower point and healthy cooldown. Preflight may block before model load. The same accepted watchdog and descendant cleanup apply.

{table(['Candidate', 'Context', 'Attempt', 'Initial status', 'Preflight', 'Load/generation', 'Safety/cleanup', 'Run/artifact/evidence'], attempts)}

# 4. Resource boundary

{table(['Candidate/context', 'Estimated asset bytes', 'Free disk', 'Available RAM', 'Commit %', 'Peak private MB', 'Min available RAM', 'Stop class', 'Evidence'], [('', '', '', '', '', '', '', '', '') for _ in range(12)])}

# 5. Bounded feasibility decision

{table(['Candidate', 'Decision: Feasible/Bounded/Blocked', 'Last completed boundary', 'First failure/blocker', 'Reason', 'What this does not prove', 'Evidence'], [('', '', '', '', '', 'No formal speed, quality or maximum-context claim', '') for _ in range(4)])}

**E3 acceptance rule:** the candidate order is honoured; no higher-memory candidate runs after a lower candidate establishes an evidence-backed safety block. All rows keep `Formal_Statistics_Allowed=false`.
"""


def e4_template() -> str:
    rows = []
    for tid, title in [
        ('OV-TQ-05', 'Route A TURBO/U4 key / TURBO/U3 value'),
        ('OV-TQ-06', 'Route A TURBO/U3 key / TURBO/U4 value'),
        ('OV-TQ-07', 'Route A Turbo U4 key-only'),
        ('OV-TQ-08', 'Route A Turbo U4 value-only'),
        ('OV-TQ-09', 'Route A Turbo U3 key-only'),
        ('OV-TQ-10', 'Route A Turbo U3 value-only'),
        ('OVT-08', 'Route B scalar U8 key / TBQ4 value'),
        ('OVT-09', 'Route B TBQ4 key / scalar U8 value'),
    ]:
        rows.append((tid, 'Asymmetric/cross-family', title, 'Blocked pending predecessor'))
    for n in range(14, 26):
        rows.append((f'OVT-{n:02d}', 'Asymmetric/cross-family', f'Workbook 05 controlled row OVT-{n:02d}', 'Blocked pending predecessor'))
    for tid in ['OV-TQ-11', 'OV-TQ-12', 'OV-TQ-15', 'OVT-26', 'OVT-27', 'OVT-28', 'OVT-31', 'OVT-34', 'OVT-35', 'OVT-36']:
        rows.append((tid, 'Ablation/repeatability/negative path', f'Controlled row {tid}', 'Blocked pending predecessor'))
    return common_header('12', 'Workbook 05 E4 Cross-Family and Repeatability Controlled Workbook', 'WB-12', WORKBOOKS[5][3], 'E4',
        'Resolve key-only, value-only, scalar/Turbo, QJL/Polar and selected cross-family behaviour, then execute ablations, repeatability, regression and unsupported-path gates. This combines completion-plan Tasks R21 and R23 while keeping blocked codecs visible.') + f"""

[[PAGEBREAK]]

# 1. Entry gate and execution order

{table(['Gate', 'Required state', 'Observed state', 'Evidence'], [
    ('D2 memory order', 'Accepted measured ordering', '', ''),
    ('Constituent codec admission', 'Every codec in a pair admitted and storage-reconciled', '', ''),
    ('3B common context', 'Supported by compared configurations', '', ''),
    ('Identity freeze', 'Same model, request, prompt, rubric and executable hashes where comparison requires it', '', ''),
])}

# 2. Controlled coverage

{table(['Test ID', 'Group', 'Purpose/reference', 'Initial status'], rows)}

# 3. Asymmetric and cross-family result entry

{table(['Test ID/config', 'K codec/precision', 'V codec/precision', 'K query-domain proof', 'V output-domain proof', 'Verified K bytes', 'Verified V bytes', 'Fallback/output result', 'Run/evidence', 'Decision'], [('', '', '', '', '', '', '', '', '', '') for _ in range(16)])}

# 4. Ablation register

{table(['Test/config', 'Control state', 'Candidate state', 'Only variable changed', 'Metric/quality delta', 'Activation/storage unchanged?', 'Decision/evidence'], [('', '', '', '', '', '', '') for _ in range(10)])}

# 5. Repeatability register

{table(['Configuration', 'Execution identity SHA-256', 'Rep 1', 'Rep 2', 'Rep 3', 'Median/range', 'Stability decision', 'Evidence'], [('', '', '', '', '', '', '', '') for _ in range(8)])}

# 6. Negative and unsupported paths

{table(['Path/capability', 'Requested behaviour', 'Expected fail/limit', 'Observed class', 'No-fallback/cleanup proof', 'Affected rows', 'Evidence'], [
    ('GPU request', 'Requested device/placement', 'No GPU claim without proof', '', '', '', ''),
    ('PagedAttention', 'Alternative cache path', 'Not applicable unless pinned source exposes it', '', '', '', ''),
    ('Prefill compression', 'Compress prefill cache', 'Not assumed', '', '', '', ''),
    ('Non-SDPA', 'Alternative attention path', 'Blocked unless explicitly supported', '', '', '', ''),
    ('Unsupported head dimension', 'Non-reviewed head dimension', 'Fail securely', '', '', '', ''),
    ('Corrupt checkpoint/evidence', 'Resume with changed identity', 'Reject', '', '', '', ''),
])}

**E4 acceptance rule:** every required row has a truthful decision and evidence. A blocked constituent codec blocks the combination; it never causes the row to disappear.
"""


def f_template() -> str:
    stage_rows = [(stage, 'Blocked pending predecessor', '', '', '', '') for stage in ['C1','C2','C3','C4','C5','D1','D2','E1','E2','E3','E4']]
    workbook_rows = [(wid, stage, 'Initialised', '', '', '') for wid, stage, _, _, _ in WORKBOOKS]
    return common_header('13', 'Workbook 05 F Evidence and Closure Controlled Workbook', 'WB-13', WORKBOOKS[6][3], 'F',
        'Independently validate and ingest completed stage artifacts, populate every controlled workbook/index field, and produce final bounded conclusions. This implements Tasks R24-R26. Closure is impossible while a required row is unresolved or a formal result lacks independent validation.') + f"""

[[PAGEBREAK]]

# 1. Artifact validation intake

{table(['Stage/batch', 'Workflow run/attempt', 'Artifact ID/name', 'Artifact SHA-256', 'Repository head', 'Manifest validation', 'Schema/cross-record validation', 'Secrets/binary scan', 'Decision/evidence'], [('', '', '', '', '', '', '', '', '') for _ in range(14)])}

# 2. Stage acceptance matrix

{table(['Stage', 'Current state', 'Passed', 'Failed', 'Blocked/skipped', 'Accepted artifact/evidence'], stage_rows)}

# 3. Post-C workbook completion matrix

{table(['Workbook ID', 'Stage', 'Structure state', 'Unresolved controlled fields', 'Generated DOCX SHA-256', 'Completion decision/evidence'], workbook_rows)}

# 4. Execution-index closure

{table(['Stage', 'Total rows', 'Passed', 'Failed', 'Blocked', 'Not applicable', 'Skipped by frontier', 'Infrastructure interrupted', 'Unresolved'], [(stage, '', '', '', '', '', '', '', '') for stage in ['D1','D2','E1','E2','E3','E4','F']])}

# 5. Formal result traceability

{table(['Conclusion/result ID', 'Model/route/config', 'Metric or quality claim', 'Matched baseline', 'Run manifest', 'Artifact digest', 'Workbook/index rows', 'Evidence path', 'Validation status'], [('', '', '', '', '', '', '', '', '') for _ in range(16)])}

# 6. Final bounded conclusions

{table(['Decision dimension', 'Leader/Pareto set', 'Matched evidence', 'Limitations/non-claims'], [
    ('Memory reduction', '', '', ''),
    ('Quality preservation', '', '', ''),
    ('Decode speed', '', '', ''),
    ('Prompt-processing speed', '', '', ''),
    ('TTFT', '', '', ''),
    ('Maximum stable context', '', '', ''),
    ('Stability/repeatability', '', '', ''),
    ('Granite 8B feasibility', '', '', ''),
    ('Granite 30B bounded feasibility', '', '', 'Feasibility only; no automatic formal performance claim'),
    ('Official merged support boundary', '', '', ''),
    ('Experimental support boundary', '', '', ''),
    ('Recommended application role', '', '', ''),
])}

# 7. Final audit checklist

{table(['Audit item', 'Required result', 'Observed result', 'Evidence'], [
    ('All controlled rows resolved', 'No blank/unresolved status', '', ''),
    ('All formal results independently validated', 'Accepted artifact and exact run manifest', '', ''),
    ('Failures/skips remain visible', 'No deleted inconvenient row', '', ''),
    ('Traceability', 'Every conclusion resolves to workbook/index/run/artifact evidence', '', ''),
    ('Payload safety', 'No secret, model, source tree, executable, wheel or archive committed as evidence', '', ''),
    ('Support language', 'No unsupported codec/device/model claim', '', ''),
    ('Generated documents', 'Templates, DOCX files and manifests regenerated and hashed', '', ''),
    ('Review lifecycle', 'All PR threads resolved and explicit project-owner approval obtained', '', ''),
])}

# 8. Closure decision

{table(['Pack status', 'Scientific results authorised?', 'Workbook closure authorised?', 'Decision owner/date', 'Evidence'], [('Initialised', 'false', 'false', '', 'No live post-C result has been accepted')])}

**F acceptance rule:** every controlled row is resolved, every formal result is independently validated, all generated workbooks/manifests are regenerated and hashed, and the final results PR is reviewed. Until then the closure decision remains open.
"""


def make_execution_index() -> list[dict[str, str]]:
    header_defaults = dict(
        Workbook_ID='', Route_ID='', Model_ID='', Model_Scale='', Weight_Format='', Weight_Precision='',
        K_Algorithm='', K_Precision='', V_Algorithm='', V_Precision='', Context_Tokens='', Output_Tokens='',
        Repetition_Role='', Execution_Order='', Predecessor_Gate='', Expected_K_Record_Bytes='',
        Expected_V_Record_Bytes='', Verified_K_Record_Bytes='', Verified_V_Record_Bytes='', Memory_Rank='',
        Frontier_Status='Not started', Formal_Statistics_Allowed='false', Discovery_Only='true', Status='Blocked pending predecessor',
        Blocker_Code='PREDECESSOR_NOT_ACCEPTED', Run_ID='', Artifact_ID='', Evidence_Path='', Notes=''
    )
    rows: list[dict[str, str]] = []
    counter = 1
    def add(stage: str, test_id: str, title: str, **kwargs: str):
        nonlocal counter
        row = dict(header_defaults)
        row.update(kwargs)
        row.update(Execution_Record_ID=f'PC-{counter:04d}', Stage_ID=stage, Test_ID=test_id, Test_Title=title)
        rows.append(row)
        counter += 1
    # D1
    for test_id, title in [('OV-B08','Route A merged source/API boundary'),('OV-B09','Route A U3/U4 independent K/V controls'),('OV-B10','Route A unit/functional diagnostics')]:
        add('D1', test_id, title, Workbook_ID='WB-07', Route_ID='route-a-merged-openvino', Category='Codec Unit/Conformance', Predecessor_Gate='C1-C5')
    for n in range(1,13):
        add('D1', f'OVT-A{n:02d}', f'Route B conformance OVT-A{n:02d}', Workbook_ID='WB-07', Route_ID='route-b-experimental-qjl-polar', Category='Codec Unit/Conformance', Predecessor_Gate='C1-C5')
    # D2 exactly 48
    order = 1
    expected = {'U8':'136','U4':'72','U3':'52','TBQ4':'68','TBQ3':'52','TBQ4_QJL':'88','TBQ3_QJL':'72','POLAR4':'~68','POLAR3':'~52'}
    for tid, ka, kp, va, vp in route_a_pairs():
        add('D2', tid, f'Route A {ka}/{kp} key and {va}/{vp} value', Workbook_ID='WB-08', Route_ID='route-a-merged-openvino', Category='Diagnostic K/V Sweep', K_Algorithm=ka, K_Precision=kp, V_Algorithm=va, V_Precision=vp, Expected_K_Record_Bytes=expected.get(kp,''), Expected_V_Record_Bytes=expected.get(vp,''), Execution_Order=str(order), Predecessor_Gate='D1')
        order += 1
    for tid, k, kb, v, vb in route_b_pairs():
        add('D2', tid, f'Route B {k} key and {v} value', Workbook_ID='WB-08', Route_ID='route-b-experimental-qjl-polar', Category='Diagnostic K/V Sweep', K_Algorithm=k, K_Precision='codec', V_Algorithm=v, V_Precision='codec', Expected_K_Record_Bytes=kb, Expected_V_Record_Bytes=vb, Execution_Order=str(order), Predecessor_Gate='D1')
        order += 1
    # E1 frontier + formal + quality + perplexity
    contexts = [512,1024,2048,4096,8192,16384,32768,65536,131072]
    config_map = {row[0]: row for row in config_rows()}
    for cfg_id, route, ka, kp, va, vp, kb, vb, role in config_rows():
        for context in contexts:
            add('E1', f'{cfg_id}-CTX-{context}', f'Granite 3B frontier {cfg_id} at {context}', Workbook_ID='WB-09', Route_ID='route-a-merged-openvino' if route=='Route A' else 'route-b-experimental-qjl-polar', Category='Granite 3B Frontier', Model_ID='granite-4.1-3b', Model_Scale='3B', Weight_Format='Accepted C1 asset', Weight_Precision='Accepted C1 asset', K_Algorithm=ka, K_Precision=kp, V_Algorithm=va, V_Precision=vp, Context_Tokens=str(context), Output_Tokens='P5 frozen', Predecessor_Gate='D2', Expected_K_Record_Bytes=kb, Expected_V_Record_Bytes=vb, Discovery_Only='true')
        for rep_role, stats in [('Pilot','false'),('Warm-up','false'),('Measured 1','true'),('Measured 2','true'),('Measured 3','true')]:
            add('E1', f'{cfg_id}-FORMAL-{rep_role.upper().replace(" ","-")}', f'Granite 3B formal {cfg_id} {rep_role}', Workbook_ID='WB-09', Route_ID='route-a-merged-openvino' if route=='Route A' else 'route-b-experimental-qjl-polar', Category='Granite 3B Formal', Model_ID='granite-4.1-3b', Model_Scale='3B', Weight_Format='Accepted C1 asset', Weight_Precision='Accepted C1 asset', K_Algorithm=ka, K_Precision=kp, V_Algorithm=va, V_Precision=vp, Context_Tokens='Common accepted context', Repetition_Role=rep_role, Predecessor_Gate='E1 frontier', Expected_K_Record_Bytes=kb, Expected_V_Record_Bytes=vb, Formal_Statistics_Allowed=stats, Discovery_Only='false')
        for prompt in range(1,7):
            add('E1', f'{cfg_id}-P{prompt}', f'Granite 3B quality {cfg_id} P{prompt}', Workbook_ID='WB-09', Route_ID='route-a-merged-openvino' if route=='Route A' else 'route-b-experimental-qjl-polar', Category='Granite 3B Quality', Model_ID='granite-4.1-3b', Model_Scale='3B', K_Algorithm=ka, K_Precision=kp, V_Algorithm=va, V_Precision=vp, Context_Tokens='Common accepted context', Predecessor_Gate='E1 formal', Formal_Statistics_Allowed='true', Discovery_Only='false')
        add('E1', f'{cfg_id}-PPL', f'Granite 3B matched perplexity {cfg_id}', Workbook_ID='WB-09', Route_ID='route-a-merged-openvino' if route=='Route A' else 'route-b-experimental-qjl-polar', Category='Granite 3B Perplexity', Model_ID='granite-4.1-3b', Model_Scale='3B', K_Algorithm=ka, K_Precision=kp, V_Algorithm=va, V_Precision=vp, Predecessor_Gate='E1 formal', Formal_Statistics_Allowed='true', Discovery_Only='false', Notes='Run only when the pinned route has a trustworthy matched procedure')
    # E2
    e2_configs = ['RA-TURBO-U3-SYM','RA-TURBO-U4-SYM','RA-SCALAR-U4-SYM','RA-SCALAR-U8-SYM','RB-TBQ3-SYM','RB-QJL3-SYM','RB-POLAR3-SYM']
    for cfg_id in e2_configs:
        cfg = config_map[cfg_id]
        for context in [512,1024,2048,4096,8192]:
            add('E2', f'{cfg_id}-8B-{context}', f'Granite 8B feasibility {cfg_id} at {context}', Workbook_ID='WB-10', Route_ID='route-a-merged-openvino' if cfg[1]=='Route A' else 'route-b-experimental-qjl-polar', Category='Granite 8B Feasibility', Model_ID='granite-4.1-8b', Model_Scale='8B', Weight_Format='Accepted E2 asset', Weight_Precision='Accepted E2 asset', K_Algorithm=cfg[2], K_Precision=cfg[3], V_Algorithm=cfg[4], V_Precision=cfg[5], Context_Tokens=str(context), Predecessor_Gate='E1', Expected_K_Record_Bytes=cfg[6], Expected_V_Record_Bytes=cfg[7], Discovery_Only='true', Formal_Statistics_Allowed='false')
    # E3
    for candidate in range(1,5):
        for context in [512,1024,2048,4096]:
            add('E3', f'E3-CANDIDATE-{candidate:02d}-{context}', f'Granite 30B bounded candidate {candidate} at {context}', Workbook_ID='WB-11', Route_ID='resolved-from-admitted-route', Category='Granite 30B Bounded Feasibility', Model_ID='granite-4.1-30b', Model_Scale='30B', Weight_Format='Resolved from accepted asset lock', Weight_Precision='Lowest accepted first', K_Algorithm='Resolved from D2 rank', K_Precision='Resolved', V_Algorithm='Resolved from D2 rank', V_Precision='Resolved', Context_Tokens=str(context), Predecessor_Gate='E1/E2 and D2', Discovery_Only='true', Formal_Statistics_Allowed='false', Notes='Feasibility only; no formal speed/quality claim')
    # E4
    e4_ids = ['OV-TQ-05','OV-TQ-06','OV-TQ-07','OV-TQ-08','OV-TQ-09','OV-TQ-10','OVT-08','OVT-09'] + [f'OVT-{n:02d}' for n in range(14,26)] + ['OV-TQ-11','OV-TQ-12','OV-TQ-15','OVT-26','OVT-27','OVT-28','OVT-31','OVT-34','OVT-35','OVT-36']
    for tid in e4_ids:
        add('E4', tid, f'E4 controlled row {tid}', Workbook_ID='WB-12', Route_ID='cross-route-as-defined-by-test-id', Category='Asymmetric/Cross-family/Ablation/Repeatability', Model_ID='granite-4.1-3b or diagnostic', Model_Scale='3B', Predecessor_Gate='D2/E1', Discovery_Only='false', Formal_Statistics_Allowed='true' if tid in ['OV-TQ-15','OVT-31'] else 'false')
    # F stage and workbook closure controls
    for stage in ['C1','C2','C3','C4','C5','D1','D2','E1','E2','E3','E4']:
        add('F', f'F-STAGE-{stage}', f'Validate and accept stage {stage}', Workbook_ID='WB-13', Route_ID='campaign-wide', Category='Evidence Ingestion', Predecessor_Gate=stage, Discovery_Only='false', Formal_Statistics_Allowed='false')
    for wid, stage, _, _, _ in WORKBOOKS:
        add('F', f'F-CLOSE-{wid}', f'Close controlled workbook {wid}', Workbook_ID='WB-13', Route_ID='campaign-wide', Category='Workbook Closure', Predecessor_Gate=stage, Discovery_Only='false', Formal_Statistics_Allowed='false')
    return rows


def write_csv(path: Path, rows: list[dict[str,str]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', newline='', encoding='utf-8') as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator='\n')
        writer.writeheader()
        writer.writerows(rows)



def _trim_blank_rows(path: Path, start_heading: str, next_heading: str, keep: int) -> None:
    """Reduce empty summary rows while preserving a useful handwritten area."""

    lines = path.read_text(encoding="utf-8").splitlines()
    start = next(i for i, line in enumerate(lines) if line.strip() == start_heading)
    end = next(
        i
        for i, line in enumerate(lines[start + 1 :], start + 1)
        if line.strip() == next_heading
    )
    header = next(i for i in range(start, end) if lines[i].strip().startswith("|"))
    data_start = header + 2
    data_end = data_start
    while data_end < end and lines[data_end].strip().startswith("|"):
        data_end += 1
    lines = lines[: data_start + keep] + lines[data_end:]
    path.write_text("\n".join(line.rstrip() for line in lines) + "\n", encoding="utf-8", newline="\n")


def _normalise_templates() -> None:
    """Apply the reviewed page-layout and terminology corrections."""

    _trim_blank_rows(
        TEMPLATE_DIR / WORKBOOKS[3][2],
        "# 4. Resource and placement record",
        "# 5. Feasibility decision",
        7,
    )
    _trim_blank_rows(
        TEMPLATE_DIR / WORKBOOKS[4][2],
        "# 4. Resource boundary",
        "# 5. Bounded feasibility decision",
        4,
    )
    _trim_blank_rows(
        TEMPLATE_DIR / WORKBOOKS[6][2],
        "# 1. Artifact validation intake",
        "# 2. Stage acceptance matrix",
        11,
    )
    _trim_blank_rows(
        TEMPLATE_DIR / WORKBOOKS[6][2],
        "# 5. Formal result traceability",
        "# 6. Final bounded conclusions",
        12,
    )
    for _, _, template, _, _ in WORKBOOKS:
        path = TEMPLATE_DIR / template
        lines = path.read_text(encoding="utf-8").splitlines()
        path.write_text(
            "\n".join(line.rstrip() for line in lines) + "\n",
            encoding="utf-8",
            newline="\n",
        )


def _write_readmes() -> None:
    """Document the canonical/generated boundaries in beginner-readable form."""

    write(
        WORKBOOKS_DIR / "README.md",
        r"""# Controlled Testing Workbooks

The controlled workbook set preserves route order, test identities, planned configurations, evidence references, failure states and bounded conclusions for the Granite–TurboQuant investigation.

## Canonical sources

Git-reviewable workbook content is stored under `text-templates/`. Generated DOCX files under `generated/` are deterministic working/reporting artefacts, not the only authoritative record. The unified workbook catalogue is `Controlled-Workbook-Manifest.csv`.

## Workbook set

| Workbook | Stage/scope | Initial or current role |
| --- | --- | --- |
| WB-01 | Upstream llama.cpp | Dependable GGUF control baseline |
| WB-02 | AtomicBot TurboQuant | Existing TurboQuant comparator |
| WB-03 | animehacker TQ3_0 | Existing TQ3 comparator |
| WB-04 | Official OpenVINO | Route A merged controls and official comparisons |
| WB-05 | Custom OpenVINO | Route B experimental controls and 36-pair matrix |
| WB-06 | Cross-route | Existing route-level comparison |
| WB-07 | D1 | Codec conformance and storage reconciliation |
| WB-08 | D2 | Diagnostic K/V capability sweep and measured memory order |
| WB-09 | E1 | Granite 3B context frontier and formal evaluation |
| WB-10 | E2 | Granite 8B safety and feasibility gate |
| WB-11 | E3 | Granite 30B lowest-weight-first bounded feasibility |
| WB-12 | E4 | Asymmetric, cross-family, ablation and repeatability tests |
| WB-13 | F | Independent evidence ingestion and final closure |

## Post-C shared controls

- `../Workbook-05-Post-C-Execution-Index-v1.csv` — 372 controlled execution and closure records;
- `../Workbook-05-Post-C-Configuration-Matrix-v1.csv` — route/configuration admission and storage controls;
- `../Workbook-05-Post-C-Evidence-Register-v1.csv` — append-only independent evidence intake;
- `../Workbook-05-Post-C-Revision-Register-v1.csv` — WB-07 through WB-13 revision history;
- `Workbook-05-Post-C-Pack-Manifest-v1.json` — pack membership, dependencies and non-claims.

## Generate and validate

```powershell
python -m pip install -r .\scripts\testing\requirements.txt
python .\scripts\testing\workbook05\bootstrap_post_c_workbooks.py --repository-root .
powershell -File .\scripts\testing\Validate-Workbook05-PostC-Workbooks.ps1
```

The workbook pack is only an execution structure until independently validated hardware artifacts populate it. Blank measured fields mean **not measured**, never zero.
""",
    )
    write(
        GENERATED_DIR / "README.md",
        r"""# Generated controlled workbooks

Generate all thirteen controlled Word workbooks from canonical Markdown with:

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py
```

Generate only the repository-safe post-C set, WB-07 through WB-13, with:

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py --post-c-only
```

The generator normalises DOCX member order, ZIP timestamps and document metadata. The post-C gate generates the seven new documents twice and requires byte-identical SHA-256 results. These DOCX files are editable working/reporting artefacts; the Markdown templates, CSV controls and JSON pack manifest remain the Git-reviewable sources of truth.
""",
    )


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _update_controlled_manifest() -> None:
    """Preserve WB-01–WB-06 and append exact WB-07–WB-13 hashes."""

    path = WORKBOOKS_DIR / "Controlled-Workbook-Manifest.csv"
    with path.open(newline="", encoding="utf-8-sig") as handle:
        existing = list(csv.DictReader(handle))
        fields = list(existing[0].keys())
    rows = [row for row in existing if row["Workbook_ID"] in {f"WB-{n:02d}" for n in range(1, 7)}]
    quality = {
        "WB-07": "N/A",
        "WB-08": "N/A",
        "WB-09": "0-10",
        "WB-10": "0-10 where formally promoted",
        "WB-11": "Not authorised",
        "WB-12": "0-10 where applicable",
        "WB-13": "N/A",
    }
    for workbook_id, stage, template, docx, purpose in WORKBOOKS:
        template_path = TEMPLATE_DIR / template
        docx_path = GENERATED_DIR / docx
        template_relative = f"docs/testing/workbooks/text-templates/{template}"
        template_hash = _sha256(template_path)
        rows.append(
            {
                "Workbook_ID": workbook_id,
                "Controlled_File": docx,
                "Canonical_Text_Template": template_relative,
                "Source_File": template_relative,
                "Source_SHA256": template_hash,
                "Canonical_Template_SHA256": template_hash,
                "Last_Validated_DOCX_SHA256": _sha256(docx_path),
                "Status": f"{workbook_id} revision 1.0: initial {stage} controlled structure generated deterministically; no live scientific result entered.",
                "Quality_Scale": quality[workbook_id],
                "Purpose": purpose,
                "Generation_Command": r"python .\scripts\testing\Generate-Controlled-Workbooks.py --post-c-only; powershell -File .\scripts\testing\Validate-Workbook05-PostC-Workbooks.ps1",
                "Revision": "1.0",
            }
        )
    write_csv(path, rows, fields)


def _generate_docx_and_manifest() -> None:
    """Generate exact DOCX files, then bind them into the unified manifest."""

    generator = ROOT / "scripts/testing/Generate-Controlled-Workbooks.py"
    completed = subprocess.run(
        [
            sys.executable,
            str(generator),
            "--repository-root",
            str(ROOT),
            "--post-c-only",
        ],
        cwd=ROOT,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        raise SystemExit(completed.returncode)
    _update_controlled_manifest()

def build():
    templates = {
        WORKBOOKS[0][2]: d1_template(),
        WORKBOOKS[1][2]: d2_template(),
        WORKBOOKS[2][2]: e1_template(),
        WORKBOOKS[3][2]: e2_template(),
        WORKBOOKS[4][2]: e3_template(),
        WORKBOOKS[5][2]: e4_template(),
        WORKBOOKS[6][2]: f_template(),
    }
    for name, content in templates.items():
        write(TEMPLATE_DIR / name, content)
    _normalise_templates()

    execution_rows = make_execution_index()
    for row in execution_rows:
        if row['Stage_ID'] == 'D2':
            row['Execution_Order'] = ''
            row['Memory_Rank'] = ''
        if row['Stage_ID'] == 'E1' and row['K_Algorithm'] == 'TURBO' and row['K_Precision'] == 'U4':
            row['Expected_K_Record_Bytes'] = '72'
        if row['Stage_ID'] == 'E1' and row['V_Algorithm'] == 'TURBO' and row['V_Precision'] == 'U4':
            row['Expected_V_Record_Bytes'] = '72'
        if row['Stage_ID'] == 'E1' and row['Category'] in {'Granite 3B Quality', 'Granite 3B Perplexity'}:
            row['Weight_Format'] = 'Accepted C1 asset'
            row['Weight_Precision'] = 'Accepted C1 asset'
    execution_fields = ['Execution_Record_ID','Stage_ID','Test_ID','Workbook_ID','Route_ID','Category','Test_Title','Model_ID','Model_Scale','Weight_Format','Weight_Precision','K_Algorithm','K_Precision','V_Algorithm','V_Precision','Context_Tokens','Output_Tokens','Repetition_Role','Execution_Order','Predecessor_Gate','Expected_K_Record_Bytes','Expected_V_Record_Bytes','Verified_K_Record_Bytes','Verified_V_Record_Bytes','Memory_Rank','Frontier_Status','Formal_Statistics_Allowed','Discovery_Only','Status','Blocker_Code','Run_ID','Artifact_ID','Evidence_Path','Notes']
    write_csv(DOCS_TESTING / 'Workbook-05-Post-C-Execution-Index-v1.csv', execution_rows, execution_fields)

    config_fields = ['Configuration_ID','Route_ID','Role','Model_Scales','Weight_Format','Weight_Precision','K_Algorithm','K_Precision','V_Algorithm','V_Precision','Planning_K_Record_Bytes','Planning_V_Record_Bytes','Admission_Status','Verified_K_Record_Bytes','Verified_V_Record_Bytes','D2_Memory_Rank','Evidence_Path','Notes']
    config_entries = []
    for cfg in config_rows():
        config_entries.append(dict(Configuration_ID=cfg[0], Route_ID='route-a-merged-openvino' if cfg[1]=='Route A' else 'route-b-experimental-qjl-polar', Role=cfg[8], Model_Scales='3B; 8B where promoted', Weight_Format='Accepted asset lock', Weight_Precision='Accepted asset lock', K_Algorithm=cfg[2], K_Precision=cfg[3], V_Algorithm=cfg[4], V_Precision=cfg[5], Planning_K_Record_Bytes=cfg[6], Planning_V_Record_Bytes=cfg[7], Admission_Status='Blocked pending predecessor', Verified_K_Record_Bytes='', Verified_V_Record_Bytes='', D2_Memory_Rank='', Evidence_Path='', Notes='Planning bytes are expectations until D1 executable evidence'))
    for candidate in range(1,5):
        config_entries.append(dict(Configuration_ID=f'E3-CANDIDATE-{candidate:02d}', Route_ID='resolved-from-admitted-route', Role='30B bounded feasibility', Model_Scales='30B', Weight_Format='Resolved from accepted asset lock', Weight_Precision='Lowest accepted first', K_Algorithm='Resolved from D2 rank', K_Precision='Resolved', V_Algorithm='Resolved from D2 rank', V_Precision='Resolved', Planning_K_Record_Bytes='', Planning_V_Record_Bytes='', Admission_Status='Blocked pending predecessor', Verified_K_Record_Bytes='', Verified_V_Record_Bytes='', D2_Memory_Rank='', Evidence_Path='', Notes='Feasibility only; no formal performance or quality authority'))
    write_csv(DOCS_TESTING / 'Workbook-05-Post-C-Configuration-Matrix-v1.csv', config_entries, config_fields)

    evidence_fields = ['Evidence_Record_ID','Stage_ID','Batch_or_Execution_ID','Workflow_Run_ID','Run_Attempt','Artifact_ID','Artifact_Name','Artifact_SHA256','Repository_Head_SHA','Model_Asset_Lock_SHA256','Configuration_SHA256','Prompt_Set_SHA256','Rubric_SHA256','Manifest_SHA256','Independent_Validation_Status','Evidence_Path','Ingestion_Status','Notes']
    evidence_rows = []
    for idx, stage in enumerate(['D1','D2','E1','E2','E3','E4','F'],1):
        evidence_rows.append(dict(Evidence_Record_ID=f'PC-EV-{idx:03d}', Stage_ID=stage, Batch_or_Execution_ID=f'{stage}-BATCH-001', Workflow_Run_ID='', Run_Attempt='', Artifact_ID='', Artifact_Name='', Artifact_SHA256='', Repository_Head_SHA='', Model_Asset_Lock_SHA256='', Configuration_SHA256='', Prompt_Set_SHA256='', Rubric_SHA256='', Manifest_SHA256='', Independent_Validation_Status='Not started', Evidence_Path='', Ingestion_Status='Not started', Notes='Append-only stage intake row; add rows for further batches'))
    write_csv(DOCS_TESTING / 'Workbook-05-Post-C-Evidence-Register-v1.csv', evidence_rows, evidence_fields)

    revision_fields = ['Record_ID','Workbook_ID','Version','Date','Changed_By','Change_Type','Change_Summary','Reason','Affected_Stage_or_Test_IDs','Change_Reference','Status','Supersedes']
    revision_rows = []
    for idx, (wid, stage, template, docx, purpose) in enumerate(WORKBOOKS,1):
        revision_rows.append(dict(Record_ID=f'PC-WR-{idx:03d}', Workbook_ID=wid, Version='1.0', Date='2026-08-22', Changed_By='Project team', Change_Type='Initial controlled release', Change_Summary=f'Created {stage} controlled workbook: {purpose}.', Reason='Initialise the complete post-C execution and closure structure before live hardware work.', Affected_Stage_or_Test_IDs=stage, Change_Reference='feature/workbook-05-post-c-workbook-pack; draft PR', Status='Current - initialised', Supersedes=''))
    write_csv(DOCS_TESTING / 'Workbook-05-Post-C-Revision-Register-v1.csv', revision_rows, revision_fields)

    pack_manifest = {
        'schema_version': '1.0',
        'campaign_id': 'GTQ-WB05-MF-v1',
        'pack_id': 'GTQ-WB05-POST-C-v1',
        'pack_status': 'Initialised',
        'base_contract_sha': 'fa6606f7c8fa03350fc780f13b5c801c9e0d91dd',
        'scientific_results_authorised': False,
        'workbook_closure_authorised': False,
        'allowed_statuses': ALLOWED_STATUSES,
        'stage_dependencies': {
            'D1': ['C1','C2','C3','C4','C5'],
            'D2': ['D1'],
            'E1': ['D2'],
            'E2': ['E1'],
            'E3': ['D2','E1'],
            'E4': ['D2','E1'],
            'F': ['D1','D2','E1','E2','E3','E4'],
        },
        'shared_controls': {
            'execution_index': 'docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv',
            'configuration_matrix': 'docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv',
            'evidence_register': 'docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv',
            'revision_register': 'docs/testing/Workbook-05-Post-C-Revision-Register-v1.csv',
        },
        'workbooks': [
            {
                'workbook_id': wid,
                'stage_id': stage,
                'template_path': f'docs/testing/workbooks/text-templates/{template}',
                'generated_path': f'docs/testing/workbooks/generated/{docx}',
                'purpose': purpose,
                'required_headings': ['Purpose','Control boundary','Initial revision record'],
                'minimum_table_count': 5,
                'live_authority': False,
            }
            for wid, stage, template, docx, purpose in WORKBOOKS
        ],
    }
    write(WORKBOOKS_DIR / 'Workbook-05-Post-C-Pack-Manifest-v1.json', json.dumps(pack_manifest, indent=2, sort_keys=True))

    plan = """# Workbook 05 Post-C Workbook Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide complete, source-controlled and deterministically generated D1, D2, E1, E2, E3, E4 and F workbooks before live hardware execution.

**Architecture:** Seven canonical Markdown workbooks feed the existing deterministic DOCX generator. A shared execution index, configuration matrix, evidence register and pack manifest provide machine-readable control. A focused validator and two-job hosted workflow generate the pack and validate the exact artifact independently without model or self-hosted execution.

**Tech Stack:** Python 3.12.10, python-docx 1.2.0, CSV/JSON, PowerShell, GitHub Actions.

**Spec:** `docs/superpowers/plans/2026-08-05-workbook-05-completion-checkpoints.md` plus the project-owner-approved E3 Granite 30B bounded-feasibility extension.

## Global Constraints

- No model acquisition, conversion or inference from the workbook workflow.
- No self-hosted runner label in the workbook workflow.
- Canonical Markdown remains the reviewable source of truth.
- Generated DOCX files must be byte-deterministic and visually inspected.
- Existing WB-01 through WB-06 content and IDs remain unchanged.
- A blocked row stays visible.
- E3 cannot authorise formal performance or quality statistics.
- F cannot close while any required row is unresolved or unvalidated.

---

### Task 1: Freeze workbook-pack acceptance tests
- [x] Add focused tests for seven workbooks, stage/index coverage, safe paths, hashes, E3 non-claims and open closure.
- [x] Verify RED because implementation files are absent.

### Task 2: Author canonical workbooks and shared controls
- [x] Add WB-07 through WB-13 Markdown templates.
- [x] Add post-C execution index, configuration matrix, evidence register, revision register and JSON pack manifest.

### Task 3: Extend deterministic generation and validation
- [x] Extend `Generate-Controlled-Workbooks.py` for thirteen templates and a post-C-only mode.
- [x] Add `post_c_workbook_pack.py` and a PowerShell gate.
- [x] Generate twice and compare every new DOCX byte-for-byte.

### Task 4: Generate and visually verify DOCX working copies
- [x] Generate WB-07 through WB-13.
- [x] Render every page to PNG and inspect for clipping, overlap, broken tables and missing glyphs.

### Task 5: Run the complete repository-safe workbook workflow
- [ ] Push the exact pack head.
- [ ] Require hosted producer and independent-validator jobs to pass on the same head.
- [ ] Inspect the retained artifact and validation report.
"""
    write(ROOT / 'docs/superpowers/plans/2026-08-22-workbook-05-post-c-workbook-pack.md', plan)
    _write_readmes()
    _generate_docx_and_manifest()


def main() -> int:
    global ROOT, TEMPLATE_DIR, GENERATED_DIR, DOCS_TESTING, WORKBOOKS_DIR
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository-root', type=Path, default=ROOT)
    args = parser.parse_args()
    ROOT = args.repository_root.resolve()
    TEMPLATE_DIR = ROOT / 'docs/testing/workbooks/text-templates'
    GENERATED_DIR = ROOT / 'docs/testing/workbooks/generated'
    DOCS_TESTING = ROOT / 'docs/testing'
    WORKBOOKS_DIR = ROOT / 'docs/testing/workbooks'
    build()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
