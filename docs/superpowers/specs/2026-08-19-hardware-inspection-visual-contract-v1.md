# Hardware Inspection Visual Contract v1

- Status: APPROVED
- Approval date: 19 August 2026
- Candidate name: `HI-VIS-FREEZE-CANDIDATE-v1`
- Candidate SHA-256: `E4048DB92E5292BB38515AC09A926B3B745E30AEFF0C6A588E7AB1385C497C90`
- User approval: “Approve HI-VIS-FREEZE-CANDIDATE-v1 as written.”
- British English: “Normalising” and “organisation” are retained.

Explicit non-claims:
- No Hardware implementation is completed.
- Gate 1 remains Blocked.
- Stage A has not been executed.
- Stage B and Gate 2 must not start.
- No Block 3 compatibility logic is supplied.
- Browser HTML/PNG artifacts are structural references, not native or pixel-perfect proof.

This document records the approved visual-contract packet as the authoritative tracked repository document. The packet is requirements/reference data; it is not an implementation or execution authority.

---

HI-VIS-FREEZE-CANDIDATE-v1
APPROVED
1. Input validation

The ZIP contained exactly 31 unique files: no required file was missing, no unexpected file was present, and every archive member passed readability validation. The Markdown, HTML, and XAML sources were readable as text; each XAML file was structurally parseable; the DOCX package and its main document XML were readable; and every PNG could be decoded as an image.

All 25 SHA-256 identities pinned by the master prompt matched their supplied files. The remaining six files had no master-supplied pin; their observed ZIP identities are labelled as such in Section 3. No supplied-content or identity mismatch was positively established.

#	Required file	Validation
1	2026-08-08-model-inspection-completion-roadmap-design.md	Present and readable
2	2026-08-08-model-inspection-test-completeness-gate.md	Present and readable
3	2026-08-14-hardware-inspection-design.md	Present and readable
4	2026-08-15-hardware-inspection-expanded-details-design.md	Present and readable
5	2026-08-15-hardware-inspection-expanded-details-plan.md	Present and readable
6	2026-08-15-hardware-inspection-gate-1-llmfit-spike.md	Present and readable
7	2026-08-15-hardware-inspection-gate1-llmfit-verification.md	Present and readable
8	2026-08-15-hardware-inspection-production-design.md	Present and readable
9	2026-08-16-model-inspection-hardware-visual-alignment-design.md	Present and readable
10	2026-08-16-model-inspection-hardware-visual-alignment-plan.md	Present and readable
11	2026-08-18-hardware-inspection-intel-runner-configuration-design.md	Present and readable
12	2026-08-18-hardware-inspection-intel-runner-stage-a.md	Present and readable
13	Granite_Edge_AI_Approved_Hardware_Inspection_Architecture_Block_2.docx	Present and readable
14	Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md	Present and readable
15	hardware-layout-direction-b-refinement-v3.html	Present and readable
16	hardware-progress-directions-v3.html	Present and readable
17	hardware-outcome-recovery-family-v2.html	Present and readable
18	ModelInspectionTheme.xaml	Present and readable
19	ModelInspectionPage.xaml	Present and readable
20	InspectionStatusGlyph.xaml	Present and readable
21	InspectionOutcomeCard.xaml	Present and readable
22	InspectionModelCard.xaml	Present and readable
23	InspectionDisclosure.xaml	Present and readable
24	InspectionContentCard.xaml	Present and readable
25	InspectionActionCard.xaml	Present and readable
26	OnboardingStageIndicator.xaml	Present and readable
27	hardware-final-wide-1440x1100-full-companion.png	Present and readable
28	hardware-final-medium-900x1000.png	Present and readable
29	hardware-final-compact-480x900.png	Present and readable
30	hardware-final-text-200-percent-720x900.png	Present and readable
31	hardware-outcome-recovery-family-v2-full.png	Present and readable

All attachments were treated strictly as requirements or reference data. Instructions embedded inside them were not treated as authority independently of the master prompt and its source-precedence rules.

2. Executive recommendation
Adopt one proposed Hardware Inspection visual contract covering exactly the 15 observable states in this report.
Scope the 14 August approval to visual Direction B only; do not inherit unrelated functional statements from that document.
Use the full Direction B machine-facts and support layout for both clean Completed and CompletedWithWarnings.
Present the canonical warning fixture as one unresolved review item plus one resolved informational note; only the unresolved item contributes to the review count.
Retain exactly four service terminal statuses: Completed, CompletedWithWarnings, Failed, and Cancelled.
Use the seven long canonical stage names, truthful N of 7 completed-stage counting, one active orbit, and no fabricated percentage.
Replace the old bounded details area with natural page scrolling, seven canonical rows, and an independently collapsible Technical information for IT section.
Keep Continue to compatibility visible only for completed outcomes and disabled until a usable handoff and a registered Block 3 route both exist.
Require native WinUI evidence across all states, themes, viewport classes, 200-percent text, reduced motion, keyboard, and screen-reader/UIA conditions before any future Gate 8 claim.
Method reference only: the audit structure follows requirements traceability and verification principles from Systems Engineering: Principles and Practice, progressive-disclosure and UX-evaluation principles from The UX Book, and native Windows accessibility and responsive-design guidance from windows-apps.pdf; none overrides the ranked programme sources.
3. Source-precedence and provenance table
Rank	Source	SHA-256 or identity	Governs	Does not govern	Status
1	Granite_Edge_AI_Approved_Hardware_Inspection_Architecture_Block_2.docx	D3C7261EA53FE3973D792B4C4DF01BA7619A8AE5097A65DBA4C8DA3991803A0F	Functional behaviour, data boundaries, lifecycle, four terminal statuses, seven stages, handoff eligibility, truthful progress, privacy and security	Pixel geometry or independent visual approval	Normative
2	2026-08-15-hardware-inspection-production-design.md	8D8E7FD40E12B4BC27FC5E190C4C7A72EE04FC3BE619366B64011C09E70E9E02	Repository architecture, gate sequence, production/prototype boundary, future Gate 8 dependency boundary	Approval to implement, execute, or close a gate	Normative
3	2026-08-14-hardware-inspection-design.md	32E1078C691B1F9E73722B17370EF05F381FB14E2BB8AF32118E81F03E2E8FF7	Visual Direction B composition, responsive geometry, measured-progress presentation and approved broad visual direction	Its unrelated provider, architecture, gate or functional statements; old short labels; old details treatment	Scoped normative
4	2026-08-15-hardware-inspection-expanded-details-design.md	EAFD6F7EEFFF92F6DA792F134EFBAD609D513FD4B9E8A8659BAE06265F216458	Inspection details, seven rows, natural page scrolling, nested IT disclosure, allowed written statuses, disclosure behaviour	Production implementation evidence or provider behaviour	Scoped normative
4	2026-08-15-hardware-inspection-expanded-details-plan.md	559BAC96273693B9C4196C88ACBE31DB2B032554EA58E7D38C452A539C68FF62	Illustrative terminal records, stage sentences, IT group examples and bounded diagnostic examples	Hard-coded fixture values, implementation permission, final warning count	Supporting reference
5	hardware-layout-direction-b-refinement-v3.html	6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B	Completed/warning composition, machine-facts hierarchy, support-card order and scoped geometry	Native WinUI proof, browser behaviour as production behaviour, warning semantics where recovery v2 is later or narrower	Scoped normative
5	hardware-progress-directions-v3.html	EDA670DDB8E6F3628D3F900B0F8A47FED19D51B763A90EF9C691132BF401E8DF	Progress Direction B, active hierarchy, one active orbit, row-state treatment and count presentation	Real provider progress, percentages, service timing or native implementation proof	Scoped normative
5	hardware-outcome-recovery-family-v2.html	24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286	Warning meaning, failed presentation classes, stopping, cancellation and recovery grammar	Additional service statuses, browser toast/navigation behaviour, Hardware facts layout for completed outcomes	Scoped normative
6	2026-08-16-model-inspection-hardware-visual-alignment-design.md	25A2D8F80C09778D1D8E2D7C95EFE6D44A20FE835B9C7601444C5A7154C446E7	Shared visual grammar, spacing, card hierarchy, focus/disclosure principles and accessibility alignment	Hardware semantics, copy, fixtures, controls or behaviour copied from Model Inspection	Scoped normative
6	2026-08-16-model-inspection-hardware-visual-alignment-plan.md	58409F0E547C9422D3A05994492F4EE429D1E42ED671C44F56EE4E450D31BC75	Supporting geometry and alignment rationale	Production implementation, ownership or semantic control sharing	Supporting reference
6	ModelInspectionTheme.xaml	EB19C9E1E9F4659280504BAC4F5275D73E26AE019EC8D8A1421282F4446A1B2B	Current semantic-theme and geometry grammar at reference commit 960bb4d047b976d4bad68d05c4481e1937a2bf27	Hardware-specific colours, copy, semantics or a mandate to reuse a control	Current grammar
6	ModelInspectionPage.xaml	8064E4979FD1BD6A85F1B20908AD26C8FF8E10AE2EF9CDCD84F682175781D7A5	Single-page scroll structure, centred column and reading-order reference	Hardware page implementation or state semantics	Current grammar
6	InspectionStatusGlyph.xaml	6A6090876B7CE5546EFACA764F1615907F409AB274B8802B49E9066264D34C90	Current centred-vector and non-colour-only glyph grammar	Hardware status mapping or shared-control requirement	Current grammar
6	InspectionOutcomeCard.xaml	C484EF4991528C2455EE076C93278D5F2AC544C921266CDE375A4ADADE8592F2	Outcome hierarchy, semantic tone, heading and live-region reference	Hardware outcome copy or service classification	Current grammar
6	InspectionModelCard.xaml	99BCDE16FBBAB5FD295741226976BBE7A1A02989FE83C6600D33F3F3852947E7	Card and fact-layout reference only	Importing model facts or Model Inspection semantics into Hardware Inspection	Current grammar
6	InspectionDisclosure.xaml	019E3B625FA9F5D39CFB2A2E001406F491D7A39842D275C6C277E9DBAAB703A8	Focusable disclosure-header, chevron, keyboard and collapsed-tree grammar	A requirement to share the implementation or Model-specific disclosure copy	Current grammar
6	InspectionContentCard.xaml	BD9ADDC2D6053B2D19481118105AF22D27B9D81264B5FC9FE3D1FA2A0022121F	Current content-card surface and spacing grammar	Hardware content or evidence fields	Current grammar
6	InspectionActionCard.xaml	63F1E929E8F8766A04D35CC6CF6156584D7D5664A7CC1D4ABB67C17533F47D2B	Action alignment, minimum target, help-text and responsive-action grammar	Hardware action availability or navigation rules	Current grammar
6	OnboardingStageIndicator.xaml	No master pin supplied. Observed ZIP identity: 2119779B8C638BA1E389FFEFC5AF6108F31B24CB5F52A0C237844A989A442E31	Current production-shell journey wording and step order	Hardware inspection status, progress or service lifecycle	Current grammar
6	2026-08-08-model-inspection-completion-roadmap-design.md	02EB2B3471E29A2721157FB9C3E425469D16A9A45AFAEC2AF911D2A817C2476F	Boundary of the preceding Model Inspection journey and visual-reference context	Hardware semantics, provider work or a production Hardware page	Boundary only
6	2026-08-08-model-inspection-test-completeness-gate.md	24AA1DE8EBB3370022A1894ED9EF707457F21EBD94E5C33EEFAFAEF629D9137E	Boundary against importing Model Inspection evidence or claiming equivalent Hardware testing	Hardware visual acceptance or Gate 8 evidence	Boundary only
7	hardware-final-wide-1440x1100-full-companion.png	823EB9FB084EDCDFC7E2506066CE168804F5C38685148156B4D26ECA376645F9	Structural visual comparison at the named wide capture condition	Native rendering, pixel authority or implementation proof	Render evidence
7	hardware-final-medium-900x1000.png	A5879CBEE9E887651088A3510816AC618C6F3B05E7128F517C46A30B81E0D96F	Structural medium-width reflow evidence	Native rendering or acceptance proof	Render evidence
7	hardware-final-compact-480x900.png	34D1AC514F570A015381BE46ADD8AF2E76833264681C5E87C9E519413F6DF36C	Structural compact stacking and wrapping evidence	Native rendering or acceptance proof	Render evidence
7	hardware-final-text-200-percent-720x900.png	F0E5CFD96761D77D5CF51A204A29836E2339AD50102244B51D7F74C2956BEB19	Structural evidence that the prototype design can reflow under enlarged text	Proof of native Windows 200-percent text behaviour	Render evidence
7	hardware-outcome-recovery-family-v2-full.png	No master pin supplied. Observed ZIP identity: 85006ED1DB97E1BF68BD723586DA2A5B70B8F463E4C8AC9A1600BAB98D1D7518	Overview of the recovery-family composition	Native evidence, complete lower-page capture or authority over written requirements	Render evidence
8	2026-08-15-hardware-inspection-gate-1-llmfit-spike.md	No master pin supplied. Observed ZIP identity: 2B1DBBBE63626AD439513AB10119599228B2D3A3D84C17694D370A4BA6B9BE71	Gate 1 and candidate non-claim boundary	Hardware UI appearance or permission to run a candidate	Boundary only
8	2026-08-15-hardware-inspection-gate1-llmfit-verification.md	BCC9D23E8213C789A6DF627AD64FDD57A6F73D2AE7C617CEE475DE1777E41224	Retained Gate 1 result: Blocked; no trusted Intel run or Gate 2 transition	Hardware UI appearance or implementation approval	Boundary only
8	2026-08-18-hardware-inspection-intel-runner-configuration-design.md	No master pin supplied. Observed ZIP identity: 44916A51C7D4E1856B73564C8CE23E9B4EE60B20067C67A2EDEFF8C482E13CFE	Runner configuration and execution stop boundary	Visual contract, permission to configure or execute the laptop	Boundary only
8	2026-08-18-hardware-inspection-intel-runner-stage-a.md	No master pin supplied. Observed ZIP identity: 644EFAD3F99CC56F97C4130BE8DD176734713ED2D1DA07CCA0925C9E9C4C923A	Stage A non-claim and sequencing boundary	Permission to run Stage A, Gate 1 closure or UI implementation	Boundary only
8	Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md	No master pin supplied. Observed ZIP identity: 90E99F38D2410E216055891508C1F816C494B90202D7046481EB69683CBCE91A	Operational boundary reference only	Permission to execute, configure, register or handle credentials	Boundary only
4. Obsolete and rejected source treatments
Treatment	Proposed disposition	Reason
Direction A and Direction C visual alternatives	Obsolete/non-normative	Direction B is the only approved 14 August visual direction.
Compact findings-only warning layout as the whole completed screen	Rejected	F2 requires the full Direction B This computer, Local AI tools, and Information sources composition for CompletedWithWarnings.
Old five-row details treatment	Rejected	The later expanded-details design requires exactly seven canonical rows.
Bounded 172–240 px ordinary-record details viewport	Rejected	Ordinary records use natural page scrolling; a nested main scroll region would harm reachability and enlarged-text behaviour.
Old disclosure title How the inspection checked this computer	Replaced	The later title is Inspection details.
Old disclosure actions View details or browser-only variants	Replaced	The proposed contract uses Show details and Hide details.
Short stages such as Starting, Reading memory, Organising results, and Creating report	Replaced	All surfaces use the seven long canonical stage names.
Warning/failure v1 copy or layout	Obsolete/non-normative	Recovery v2 is the later scoped source.
Intermediate model-aligned Hardware revision	Obsolete/non-normative	The approved alignment design and exact 960bb4d… XAML snapshots govern visual grammar only.
Two unresolved warnings in the canonical warning fixture	Rejected	The memory difference is resolved informationally; only the NPU item remains unresolved.
Hard-coded warning count	Rejected	Counts derive from presentation-model review dispositions.
No compatibility report will be created from this run.	Rejected	The correct Hardware sentence is No hardware report will be created from this run.
A fifth Stopping terminal status	Rejected	Stopping is temporary; Cancelled is published only after child-tree cleanup.
Three separate failed service statuses	Rejected	Critical-evidence, transient, and repair-required are presentation classifications beneath service status Failed.
Browser toast for Continue	Rejected	Production Continue requires a registered route and usable handoff; no prototype toast substitutes for navigation.
Prototype button actions that remain on the same HTML page	Rejected as production behaviour	HTML is visual intent, not the native interaction implementation.
Old prototype footer labels such as Compatibility and Ready	Replaced	The current shell uses Check hardware fit, Configure model, and Ready to chat.
Pre-amendment details screenshots	Obsolete/non-normative	Later details wording and natural-scroll requirements override them.
Failed or incomplete 200-percent prototype captures	Obsolete/non-normative	Only the supplied post-amendment render remains supporting evidence, and it still does not prove native behaviour.
PNG dimensions or browser pixels as a native metric oracle	Rejected	They support manual structural comparison only.
Pixel-perfect equivalence claim	Rejected	No authoritative Figma extraction or native implementation capture exists.
Tool failure translated into bad hardware	Rejected	Operational failure and hardware facts remain separate.
Combining dedicated and shared graphics memory	Rejected	They remain distinct values.
Treating installed, usable, and available-now memory as one number	Rejected	They remain separate facts with separate provenance and freshness.
5. F1–F10 approved freeze table
ID	Exact approved frozen value	Exact user-facing copy where applicable	Source basis	Alternatives rejected	Implementation consequence	Approval status
F1	The 14 August approval means visual Direction B only, subordinate to the Block 2 DOCX and all later scoped amendments. It does not approve unrelated functional, provider, gate, architecture or evidence statements in the 14 August document.	Not user-facing.	Block 2 DOCX; production design; 14 August metadata and precedence rule	Treating the whole 14 August document as approved	A later record must identify the visual scope explicitly and keep behaviour traceable to the DOCX.	Resolved — Approved
F2	Both Completed and CompletedWithWarnings use the complete Direction B composition: outcome card; dominant This computer card; right-column Local AI tools then Information sources; full-width details and actions. Recovery v2 supplies warning, failure and cancellation meaning.	This computer; Local AI tools; Information sources	Direction B HTML; recovery v2; F2 instruction	Compact warning-only page; different layout for warnings	One completed-page composition varies by outcome tone and review content rather than by removing machine facts.	Resolved — Approved
F3	The canonical warning fixture contains exactly one unresolved review item and one resolved informational note. The unresolved item is the NPU check; the resolved note is the memory-source difference. Only items with presentation disposition RequiresReview contribute to the review count.	1 detail needs review; Neural processor (NPU); Could not check; Memory information; Resolved	Recovery v2 rule; Direction B; expanded-details records	Two unresolved warnings; hard-coded count; counting every amber row	The count is computed from the current presentation model. A Completed with note row can be informational or review-requiring; its review disposition controls the count.	Resolved — Approved
F4	Clean Completed uses the same full composition without warning/review language. It includes all computer facts, tool/source cards, a neutral Block 2 limitation, report-created summary, seven rows, complete IT groups, Run inspection again, and visible Continue subject to F9.	This computer’s hardware information is ready.
Inspection complete
Your hardware information is ready
The hardware report was created from the reliable information collected on this device.
This report records hardware facts only. It does not decide whether the selected model will run.
Inspection completed with no details to review.
Report created	Block 2 Completed; Direction B; expanded-details design	Reusing warning copy; removing support/provenance; making suitability claims	Exact facts and IT records are defined in Sections 8, 10 and 11. Continue is currently disabled because Block 3 is unavailable.	Resolved — Approved
F5	Invalid or missing handoff renders a bounded navigation-error view. It starts no service or hardware process, exposes no raw handoff or model path, makes no hardware conclusion, offers no Continue and has one recovery action.	Hardware inspection could not open safely.
Navigation problem
Hardware inspection could not open
The information needed to begin this page was missing or could not be read safely. No inspection was started. Return to model inspection and try the journey again.
Back to model inspection	14 August bounded-navigation rule; Block 2 validation/privacy	Blank page; automatic retry; showing payload/path; starting a run anyway	Default focus lands on Back to model inspection; one assertive announcement is made; no InspectionId is created.	Resolved — Approved
F6	Cancellation request immediately changes Active to Stopping. Stopping remains until contained child-tree cleanup completes; it shows neither report nor details and is not a service terminal status.	Your cancellation request was received.
Stopping inspection
Closing the inspection safely...
No hardware report will be created from this run.
Stopping...	Block 2 cancellation lifecycle; recovery v2; F6 instruction	Compatibility-report wording; publishing Cancelled before cleanup; retaining details	Cancel is removed immediately, a disabled stopping action is shown, and Cancelled appears only after cleanup confirmation.	Resolved — Approved
F7	Exact actions follow the mapping in Section 12. Retry creates a new InspectionId, closes both disclosures, clears prior live-region state and ignores stale prior-run events. Same-run presentation revisions preserve disclosure state.	Cancel inspection; Stopping...; Run inspection again; Continue to compatibility; Back; Try again; Back to model inspection	Master action map; Block 2 stale-event rules; disclosure design	Reusing an InspectionId; resetting disclosure on same-run updates; retry on repair-required failure	Action availability is state- and classification-driven. Only explicit retry actions start a new run.	Resolved — Approved
F8	All active and details surfaces use the seven long canonical stage names and exact copy matrix in Section 7. The count is completed workflow stages, not seven independent hardware devices or seven provider calls.	See Section 7; no placeholders or abbreviations.	Block 2 stage set; progress Direction B; expanded-details design	Short stage names; percentage from stage count; provider-level progress invention	Initial frame is stage 1 Active with 0 of 7; later rows Waiting. A genuine stage has 550 ms minimum visible pacing except under reduced motion, without delaying cancellation.	Resolved — Approved
F9	Continue is visible only on Completed and CompletedWithWarnings. It is enabled only when the current result provides a usable Hardware handoff and the application has a registered Block 3 route.	Continue to compatibility
Accessible help: Continue to compatibility is unavailable until this run has a usable hardware handoff and the compatibility step is available.	Block 2 handoff boundary; production design; F9 instruction	Prototype toast; hidden disabled reason; compatibility calculations in Block 2	Current candidate condition is visible-disabled. P4 defines no model-fit, memory, context, offload or performance calculation.	Resolved — Approved
F10	The authority manifest is the 31-row table in Section 3. The 25 supplied pins are exact; six unpinned identities are observed-only and gain no authority from hashing. Future native acceptance uses the matrix in Section 17.	Not user-facing.	Master hash list; source precedence; PNG boundary	Treating unpinned hashes as approvals; pixel-perfect screenshot oracle	Native WinUI captures—not HTML or PNGs—must establish future visual evidence. Comparison with prototypes is structural and manual.	Resolved — Approved
6. Fifteen-state screen matrix

The page title is always Hardware inspection. The shell footer remains:

MODEL SETUP · STEP 3 OF 5
Choose model
Inspect model
Check hardware fit
Configure model
Ready to chat
State	Service status	Page subtitle	Kicker	Title	Body	Main content	Details availability	Primary action	Secondary action	Default focus	Announcement	Continue state
1. Bounded invalid-or-missing-handoff navigation error	No service run; no terminal status	Hardware inspection could not open safely.	Navigation problem	Hardware inspection could not open	The information needed to begin this page was missing or could not be read safely. No inspection was started. Return to model inspection and try the journey again.	One bounded semantic error card; no hardware facts, payload, report or process state	Unavailable	Back to model inspection	None	Back to model inspection	Hardware inspection could not open. No inspection was started. Back to model inspection is available.	Hidden
2. Active: Starting hardware inspection	Non-terminal, running	Checking this computer. Hardware information stays on this device.	Inspection in progress	Starting hardware inspection	Preparing the approved local inspection tools and a safe run context.	Active summary; 0 of 7; checks complete; stage 1 Active; stages 2–7 Waiting; exactly one orbit	Unavailable	Cancel inspection	None	Page heading on initial entry; later no focus movement	Starting hardware inspection. Preparing the approved local inspection tools and a safe run context. State: Active.	Hidden
3. Active: Reading processor information	Non-terminal, running	Active subtitle	Inspection in progress	Reading processor information	Reading the processor name, architecture, core count, thread count, and supported instruction information.	Stage 1 Complete; stage 2 Active; stages 3–7 Waiting; 1 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Reading processor information. Reading the processor name, architecture, core count, thread count, and supported instruction information. State: Active.	Hidden
4. Active: Reading system memory	Non-terminal, running	Active subtitle	Inspection in progress	Reading system memory	Reading installed, Windows-usable, and currently available memory as separate values.	Stages 1–2 Complete; stage 3 Active; stages 4–7 Waiting; 2 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Reading system memory. Reading installed, Windows-usable, and currently available memory as separate values. State: Active.	Hidden
5. Active: Detecting graphics hardware	Non-terminal, running	Active subtitle	Inspection in progress	Detecting graphics hardware	Checking graphics devices and keeping dedicated and shared memory separate.	Stages 1–3 Complete; stage 4 Active; stages 5–7 Waiting; 3 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Detecting graphics hardware. Checking graphics devices and keeping dedicated and shared memory separate. State: Active.	Hidden
6. Active: Checking local inference runtimes	Non-terminal, running	Active subtitle	Inspection in progress	Checking local inference runtimes	Checking which processor and graphics routes the installed local inference runtime can see.	Stages 1–4 Complete; stage 5 Active; stages 6–7 Waiting; 4 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Checking local inference runtimes. Checking which processor and graphics routes the installed local inference runtime can see. State: Active.	Hidden
7. Active: Normalising hardware information	Non-terminal, running	Active subtitle	Inspection in progress	Normalising hardware information	Comparing trusted evidence and selecting safe hardware values under the approved rules.	Stages 1–5 Complete; stage 6 Active; stage 7 Waiting; 5 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Normalising hardware information. Comparing trusted evidence and selecting safe hardware values under the approved rules. State: Active.	Hidden
8. Active: Creating the hardware report	Non-terminal, running	Active subtitle	Inspection in progress	Creating the hardware report	Recording the reliable hardware facts, provenance, and review notes in the hardware report.	Stages 1–6 Complete; stage 7 Active; 6 of 7	Unavailable	Cancel inspection	None	Existing focus preserved	Creating the hardware report. Recording the reliable hardware facts, provenance, and review notes in the hardware report. State: Active.	Hidden
9. Stopping safely after cancellation is requested	No terminal status yet; cancellation requested	Your cancellation request was received.	Stopping inspection	Closing the inspection safely...	No hardware report will be created from this run.	Neutral stopping card only; no progress rows, facts, report or details	Unavailable	Stopping... disabled	None	Stopping title	Stopping hardware inspection. No hardware report will be created from this run.	Hidden
10. Clean Completed	Completed	This computer’s hardware information is ready.	Inspection complete	Your hardware information is ready	The hardware report was created from the reliable information collected on this device.	Full Direction B composition; no review block; neutral Block 2 limitation; report summary; seven rows; IT disclosure	Available; both disclosures initially closed	Continue to compatibility, visible-disabled until F9 conditions hold	Run inspection again	Outcome title	Hardware inspection completed. A hardware report was created.	Visible; disabled under current programme state
11. CompletedWithWarnings	CompletedWithWarnings	Your computer's hardware information is ready to review.	Inspection complete · Review recommended	Your hardware information is ready	The neural processor check could not be confirmed. A small memory-source difference was resolved safely. The hardware report was still created.	Full Direction B composition; one unresolved NPU review item; one resolved memory note; report summary; seven rows; IT disclosure	Available; both disclosures initially closed	Continue to compatibility, visible-disabled until F9 conditions hold	Run inspection again	Outcome title	Hardware inspection completed with one detail to review. A hardware report was created.	Visible; disabled under current programme state
12. Failed — critical evidence missing	Failed	The inspection finished, but the report is incomplete.	Inspection incomplete - Cannot continue	Essential hardware information is missing	Installed memory could not be confirmed reliably. This does not mean the computer is unsuitable.	Incomplete-result card; confirmed processor fact; unresolved memory; remaining evidence non-actionable; no report	Available	Run inspection again only when the classification is retryable; otherwise Back	Back when retry is available; otherwise none	Outcome title	Hardware inspection failed because essential hardware information could not be confirmed. No hardware conclusion was made.	Hidden
13. Failed — transient operation problem	Failed	The inspection could not collect hardware information this time.	Inspection problem - No hardware conclusion	The inspection could not start	The app could not open the tool it needs to check this computer. This might be temporary.	Temporary-problem card, bounded recovery guidance and safe details	Available	Try again	Back	Try again	Hardware inspection could not start. Try again is available.	Hidden
14. Failed — application repair required	Failed	The application needs attention before it can check this computer.	Application problem - Cannot continue	The inspection tool could not be verified	The app cannot safely use a required local component. No conclusion has been made about this computer.	Repair-required card and safe IT support information; no retry	Available	Back to model inspection	None	Back to model inspection	The inspection tool could not be verified. Back to model inspection is available.	Hidden
15. Cancelled	Cancelled	The inspection has stopped.	Inspection cancelled	No hardware report was created	You stopped the inspection. This does not indicate a problem with the computer.	Neutral cancellation card; partial-stage history only; no actionable snapshot or report	Available	Run inspection again	Back	Outcome title	Hardware inspection cancelled. No hardware report was created.	Hidden
7. Seven-stage progress copy matrix

N of 7 counts completed stages in the ordered inspection workflow. It does not claim that the seven stages are seven independent hardware checks, devices, providers or measurements.

A completed count advances only after a genuine stage-completed event. Page-level percentage is absent unless a provider supplies a real, bounded fraction for its own current operation.

Stage	Active title	Plain-language active explanation	Completed-row sentence	Waiting-row sentence	Accessible row names
1	Starting hardware inspection	Preparing the approved local inspection tools and a safe run context.	The approved local inspection tools were prepared.	Waiting for the inspection to begin.	Active — Starting hardware inspection. Preparing the approved local inspection tools and a safe run context. State: Active.
Complete — Starting hardware inspection. The approved local inspection tools were prepared. State: Complete.
Waiting — Starting hardware inspection. Waiting for the inspection to begin. State: Waiting.
2	Reading processor information	Reading the processor name, architecture, core count, thread count, and supported instruction information.	Processor information was collected.	Starts after the local inspection tools are ready.	Active — Reading processor information. Reading the processor name, architecture, core count, thread count, and supported instruction information. State: Active.
Complete — Reading processor information. Processor information was collected. State: Complete.
Waiting — Reading processor information. Starts after the local inspection tools are ready. State: Waiting.
3	Reading system memory	Reading installed, Windows-usable, and currently available memory as separate values.	Installed, usable, and currently available memory were collected separately.	Starts after processor information is read.	Active — Reading system memory. Reading installed, Windows-usable, and currently available memory as separate values. State: Active.
Complete — Reading system memory. Installed, usable, and currently available memory were collected separately. State: Complete.
Waiting — Reading system memory. Starts after processor information is read. State: Waiting.
4	Detecting graphics hardware	Checking graphics devices and keeping dedicated and shared memory separate.	Graphics hardware and separate dedicated and shared memory values were collected.	Starts after system memory is read.	Active — Detecting graphics hardware. Checking graphics devices and keeping dedicated and shared memory separate. State: Active.
Complete — Detecting graphics hardware. Graphics hardware and separate dedicated and shared memory values were collected. State: Complete.
Waiting — Detecting graphics hardware. Starts after system memory is read. State: Waiting.
5	Checking local inference runtimes	Checking which processor and graphics routes the installed local inference runtime can see.	The installed local tools reported the processor and graphics routes they can see.	Starts after graphics hardware is detected.	Active — Checking local inference runtimes. Checking which processor and graphics routes the installed local inference runtime can see. State: Active.
Complete — Checking local inference runtimes. The installed local tools reported the processor and graphics routes they can see. State: Complete.
Waiting — Checking local inference runtimes. Starts after graphics hardware is detected. State: Waiting.
6	Normalising hardware information	Comparing trusted evidence and selecting safe hardware values under the approved rules.	The collected information was compared and resolved using the approved rules.	Starts after the available hardware information is collected.	Active — Normalising hardware information. Comparing trusted evidence and selecting safe hardware values under the approved rules. State: Active.
Complete — Normalising hardware information. The collected information was compared and resolved using the approved rules. State: Complete.
Waiting — Normalising hardware information. Starts after the available hardware information is collected. State: Waiting.
7	Creating the hardware report	Recording the reliable hardware facts, provenance, and review notes in the hardware report.	The reliable hardware facts and review notes were recorded.	Starts after the hardware information is normalised.	Active — Creating the hardware report. Recording the reliable hardware facts, provenance, and review notes in the hardware report. State: Active.
Complete — Creating the hardware report. The reliable hardware facts and review notes were recorded. State: Complete.
Waiting — Creating the hardware report. Starts after the hardware information is normalised. State: Waiting.

Additional proposed progress rules:

The first rendered frame is stage 1 Active, 0 of 7, with stages 2–7 Waiting.
Exactly one row has the orbit and Active.
Completed rows use a tick and Complete.
Future rows use their stage number and Waiting.
A genuine stage remains visibly presented for at least 550 ms, except when reduced motion is enabled.
The pacing rule never delays cancellation, never invents a stage that did not occur, and never prolongs an inaccurate state after a terminal result.
Reduced motion removes orbit rotation, disclosure transitions and state-transition animation. It does not remove the Active label or alter lifecycle semantics.
8. Completed and warning facts matrix

All hardware values below are illustrative prototype values only. They are not readings from a supplied machine and are not requirements that a production result must contain those particular values.

Area or field	Clean Completed	CompletedWithWarnings	Contract rule
Page subtitle	This computer’s hardware information is ready.	Your computer's hardware information is ready to review.	Clean copy contains no review language.
Outcome kicker	Inspection complete	Inspection complete · Review recommended	Status is also conveyed by icon, text and accessible name—not colour alone.
Outcome title	Your hardware information is ready	Your hardware information is ready	Same information hierarchy; warning tone does not replace machine facts.
Outcome body	The hardware report was created from the reliable information collected on this device.	The neural processor check could not be confirmed. A small memory-source difference was resolved safely. The hardware report was still created.	Neither outcome states that the selected model is compatible or will run.
Review counter	No review counter	1 detail needs review	Value derives from unresolved presentation items, not a hard-coded literal.
Neutral limitation	This report records hardware facts only. It does not decide whether the selected model will run.	Same neutral limitation after the review block	Retains Block 2 limitations without warning language on clean Completed.
Main facts card title	This computer	This computer	Dominant card in both outcomes.
Main facts helper	A detailed overview of this computer's hardware and available resources.	Same	Helper remains factual.
Processor model	[Illustrative] Intel Core Ultra 7 155H	Same illustrative value	Display safe model name only.
Processor facts	[Illustrative] 16 cores · 22 threads · 64-bit	Same illustrative values	Architecture, core and thread facts remain distinct from suitability.
Installed memory	[Illustrative] 32 GB installed	Same selected illustrative value	Installed memory is not replaced by usable or available memory.
OS-usable memory	[Illustrative] 31.6 GB usable	Same illustrative value	Presented separately.
Available-now memory	[Illustrative] 18.4 GB available now	Same illustrative value, with freshness retained in IT details	Dynamic value has its own capture time/freshness.
Graphics name	[Illustrative] Intel Arc Graphics	Same illustrative value	One or more adapters may be represented; software adapters must be identified safely.
Dedicated graphics memory	[Illustrative] 128 MB dedicated	Same illustrative value	Never added to shared memory.
Shared graphics memory	[Illustrative] Up to 16 GB shared memory	Same illustrative value	Label must make sharing clear.
Storage available	[Illustrative] 412 GB available	Same illustrative value	Capacity is scoped to the checked Windows drive.
Storage helper	[Illustrative] On the checked Windows drive	Same	No full path or unsafe volume identifier.
Review item	Not shown	Neural processor (NPU)
The inspection could not confirm whether one is present. This does not mean one is missing.
Could not check	This is the single unresolved item and contributes one to the review count.
Resolved note	Not shown	Memory information
Two sources differed slightly. The report safely used the Windows value.
Resolved	Informational only; contributes zero to the unresolved review count.
Reliable-core note	Not shown	Core hardware information
Processor, memory, graphics, and storage are reliable.
Ready	Explains why a report exists without claiming model compatibility.
Support card 1 title	Local AI tools	Local AI tools	Right-column first card.
Support card 1 helper	What the installed local inference tool reported it can see.	Same	Describes visibility, not performance or compatibility.
Tool row 1	[Illustrative] Intel graphics / Seen by the installed AI tool / Found	Same	Found means visible to the tool, not proven suitable.
Tool row 2	[Illustrative] Processor / Seen by the installed AI tool / Found	Same	Same limitation.
Support card 2 title	Information sources	Information sources	Always follows Local AI tools.
Support card 2 helper	The trusted local checks used to confirm this report.	Same	Sources, not raw provider output.
Source row 1	[Illustrative] Hardware scanner / Primary cross-platform inventory / Used	Same	Scanner name/version appears in IT details.
Source row 2	[Illustrative] Windows checks / Memory, graphics, NPU, and storage validation / Checked	Same	Specific check identities appear only when actually used.
Details summary	Inspection completed with no details to review.	Inspection completed with one detail to review and one resolved informational note.	Summary follows current presentation data.
Details badge	Report created	Report created	Badge requires an actionable report.
Actions	Run inspection again; Continue to compatibility visible under F9	Same	Continue is currently disabled.
Footer	Current five-step shell journey	Same	Hardware is step 3 of 5.

The warning count must be derived as follows at presentation-contract level:

An item with disposition RequiresReview contributes one.
An item with disposition ResolvedInformational contributes zero.
A visual status such as Completed with note does not, by itself, decide whether the item is unresolved.
The canonical warning fixture therefore has UnresolvedReviewCount = 1 and ResolvedInformationCount = 1.
9. Failure, stopping, cancellation, and navigation-error recovery matrix
State	Exact user-facing outcome copy	Retryability	Actions	Why actions are present or absent	Report/details
Invalid or missing handoff	P4 proposed subtitle: Hardware inspection could not open safely.
P4 proposed kicker: Navigation problem
P4 proposed title: Hardware inspection could not open
P4 proposed body: The information needed to begin this page was missing or could not be read safely. No inspection was started. Return to model inspection and try the journey again.	No run exists to retry on this page	Back to model inspection only	The user must re-enter through a valid, already-validated Model Inspection handoff. Starting locally would bypass the entry boundary.	No report; no details; no process started
Stopping	Your cancellation request was received.
Stopping inspection
Closing the inspection safely...
No hardware report will be created from this run.	Not applicable while cleanup continues	Disabled Stopping...	Prevents a second cancellation or retry while contained child processes are still being closed.	No report and no details
Failed — critical evidence missing	The inspection finished, but the report is incomplete.
Inspection incomplete - Cannot continue
Essential hardware information is missing
Installed memory could not be confirmed reliably. This does not mean the computer is unsuitable.	Driven by the bounded failure classification; no UI inference	Back; add Run inspection again only when the classification explicitly states that a new run may resolve the cause	Critical evidence blocks an actionable snapshot. Retry must not be offered for a known non-retryable cause.	No report; details available with non-actionable evidence
Failed — transient operation problem	The inspection could not collect hardware information this time.
Inspection problem - No hardware conclusion
The inspection could not start
The app could not open the tool it needs to check this computer. This might be temporary.	Retryable	Try again; Back	A fresh run may clear a temporary application or Windows condition. Copy does not blame the computer.	No report; safe details available
Failed — application repair required	The application needs attention before it can check this computer.
Application problem - Cannot continue
The inspection tool could not be verified
The app cannot safely use a required local component. No conclusion has been made about this computer.	Not retryable from Hardware Inspection	Back to model inspection only	Repeating the same run cannot safely repair a missing, changed, damaged or wrong-version component.	No report; safe IT repair information available
Cancelled	The inspection has stopped.
Inspection cancelled
No hardware report was created
You stopped the inspection. This does not indicate a problem with the computer.	Retryable by explicit user choice	Run inspection again; Back	A cancellation is neutral. A new run is safe after cleanup has completed.	No report; completed/partial stage history and safe IT information available
Clean Completed	See Sections 6 and 8	Retry available	Continue to compatibility under F9; Run inspection again	Completed result provides an actionable report, but route availability remains separate.	Report and both disclosures available
CompletedWithWarnings	See Sections 6 and 8	Retry available	Continue to compatibility under F9; Run inspection again	Warning does not invalidate the report; retry is optional, not presented as a required fix.	Report and both disclosures available
10. Inspection details contract
Outer disclosure
Title: Inspection details
Collapsed action: Show details
Expanded action: Hide details
Starts closed for every new run.
Remains independently operable from the nested IT disclosure.
Opening or closing it changes no outcome, service state, evidence or navigation.
Focus remains on its header after pointer, Enter or Space activation.
When collapsed, its content leaves layout, pointer hit-testing, keyboard tab order and the accessibility tree.
Ordinary records use the page’s natural vertical scroll. No 172–240 px nested record viewport is permitted.
State-specific summary, helper and badge
State	Disclosure helper under title	Outer summary	Summary helper	Badge
Clean Completed	Seven completed stages and safe support information	Inspection completed with no details to review.	A hardware report was created from the reliable information collected in all seven stages.	Report created
CompletedWithWarnings	Seven stages, one review item, one resolved note, and safe support information	Inspection completed with one detail to review and one resolved informational note.	A hardware report was created. One unresolved item and one resolved note are recorded below.	Report created
Failed — critical evidence	Seven stages explaining where reliable evidence stopped	Installed memory could not be confirmed, so no report was created.	Collected values remain non-actionable and cannot continue to compatibility.	No report
Failed — transient	Seven stages, failed stage, and safe support information	The inspection stopped while starting the local checking tool.	No hardware conclusion was made and no report was created.	Stopped
Failed — repair required	Seven stages, component verification, and IT support information	The approved inspection component could not be verified.	The application needs attention; this is not a finding about the computer.	Repair needed
Cancelled	Seven stages showing what completed before cancellation	You cancelled the inspection while graphics information was being read.	No hardware conclusion was made and no report was sent to compatibility.	Cancelled
Active, Stopping or navigation error	No disclosure	No summary	No helper	No badge
Exact seven-row terminal records
Canonical stage	Clean Completed	CompletedWithWarnings	Failed — critical evidence	Failed — transient	Failed — repair required	Cancelled
Starting hardware inspection	The approved local inspection tools were prepared. — Completed	The approved local checking tools were prepared. — Completed	The approved local checking tools were prepared. — Completed	The app could not open the approved local checking tool this time. — Stopped here	The required local component was missing, changed, damaged, or the wrong version. — Stopped here	The approved local checking tools were prepared. — Completed
Reading processor information	Processor information was collected and confirmed by the trusted checks. — Completed	Processor information was collected and agreed across the trusted checks. — Completed	Processor information was collected successfully. — Completed	No processor check began. — Not started	No processor check began. — Not started	Processor information was collected. — Completed
Reading system memory	Installed, usable, and currently available memory were collected and confirmed separately. — Completed	Two trusted checks differed slightly; the approved Windows value was selected. — Completed with note	The trusted checks disagreed, so installed memory could not be confirmed safely. — Could not confirm	No memory check began. — Not started	No memory check began. — Not started	Memory information was collected. — Completed
Detecting graphics hardware	Graphics information was collected with dedicated and shared memory kept separate. — Completed	Graphics information was collected, but a neural processor could not be confirmed. — Completed with note	Graphics information was collected for safe diagnostics only. — Not used in report	No graphics check began. — Not started	No graphics check began. — Not started	You stopped the run while this check was active. — Cancelled here
Checking local inference runtimes	The installed local tools reported the processor and graphics routes they can see. — Completed	The installed local tools reported the processor and graphics routes they can see. — Completed	Runtime visibility was collected for safe diagnostics only. — Not used in report	No runtime check began. — Not started	No runtime check began. — Not started	The runtime check did not begin. — Not started
Normalising hardware information	The collected information was compared and resolved using the approved rules. — Completed	The collected information was compared and resolved using the approved rules. — Completed	The unresolved memory values prevented a reliable hardware snapshot. — Stopped here	There was no collected information to combine. — Not started	There was no trusted information to combine. — Not started	The partial information was not combined into a report. — Not started
Creating the hardware report	The reliable hardware facts and provenance were recorded. — Report created	The reliable hardware facts and review notes were recorded. — Report created	A hardware report was not created from unresolved critical evidence. — Not started	No hardware report was created. — Not started	No hardware report was created. — Not started	No hardware report was created. — Not started

Allowed written row statuses are exactly:

Completed
Completed with note
Could not check
Could not confirm
Stopped here
Cancelled here
Not started
Not used in report
Report created

Completed with note is not automatically an unresolved warning. In the canonical warning fixture, the memory row is resolved informationally while the NPU issue remains the single unresolved review item.

11. Technical information for IT contract
Disclosure behaviour
Title: Technical information for IT
Helper: Sources, versions, timestamps, and safe support codes
Collapsed action: Show IT details
Expanded action: Hide IT details
The nested disclosure is shown only inside expanded Inspection details.
It starts closed on a new run, independently of the outer disclosure.
It preserves its state during same-run presentation revisions.
It does not alter the outcome, evidence, action availability or navigation.
Collapsed content leaves layout, pointer hit-testing, tab order and the accessibility tree.
Group and field order
Order	Group	Required field order
1	How hardware facts were confirmed	Fact name; Selected value; Used check; optional Other check; Why this value was used; Agreement; Confidence; Captured
2	Run and tool information	Hardware scanner; Windows system-information check; DXGI graphics check; Windows NPU check; Windows Storage check; local runtime probe; source-policy version; hardware-schema version; safe run reference when permitted; run started; fact-capture timing; available-memory freshness; report created/completed
3	Warnings and safe diagnostics	Plain-language explanation; affected stage; affected provider/component when safe; resolution; safe support code when one exists; final local/no-upload note

Approved resolution wording:

Internal resolution meaning	User-facing IT wording
Primary accepted	Confirmed by the preferred check
Primary accepted with conflict	Confirmed, but another check reported something different
Secondary fallback	Confirmed using another trusted Windows check
Unavailable	This information could not be checked
Conflict unresolved	The checks disagreed, so no safe value was selected
Clean Completed illustrative fact records

The following values define a complete illustrative clean fixture, not required machine values.

Fact name	Selected value	Used check	Other check	Why this value was used	Agreement	Confidence	Captured
Processor	[Illustrative] Intel Core Ultra 7 155H	[Illustrative] Local hardware scanner — matching processor record	[Illustrative] Windows system information — matching processor record	Confirmed by the preferred check	[Illustrative] Agreed	[Illustrative] High	[Illustrative timestamp]
Processor architecture	[Illustrative] 64-bit	[Illustrative] Windows system information	[Illustrative] Local hardware scanner	Confirmed by the preferred check	[Illustrative] Agreed	[Illustrative] High	[Illustrative timestamp]
Installed memory	[Illustrative] 32 GB installed	[Illustrative] Windows system information — 32 GB	[Illustrative] Local hardware scanner — 32 GB	Confirmed by the preferred check	[Illustrative] Agreed	[Illustrative] High	[Illustrative timestamp]
Windows-usable memory	[Illustrative] 31.6 GB usable	[Illustrative] Windows memory check	Omitted when no independent safe comparison exists	Confirmed by the preferred check	[Illustrative] Available from one authoritative field source	[Illustrative] High	[Illustrative timestamp]
Memory available now	[Illustrative] 18.4 GB available now	[Illustrative] Windows memory check	Omitted	Confirmed by the preferred check	[Illustrative] Dynamic reading	[Illustrative] High at capture time	[Illustrative timestamp and freshness]
Graphics adapter	[Illustrative] Intel Arc Graphics	[Illustrative] DXGI graphics check	[Illustrative] Local hardware scanner — matching adapter	Confirmed by the preferred check	[Illustrative] Agreed	[Illustrative] High	[Illustrative timestamp]
Dedicated graphics memory	[Illustrative] 128 MB dedicated	[Illustrative] DXGI graphics check	Omitted	Confirmed by the preferred check	[Illustrative] Kept separate from shared memory	[Illustrative] High	[Illustrative timestamp]
Shared graphics memory	[Illustrative] Up to 16 GB shared memory	[Illustrative] DXGI graphics check	Omitted	Confirmed by the preferred check	[Illustrative] Kept separate from dedicated memory	[Illustrative] High	[Illustrative timestamp]
Neural processor	[Illustrative] No neural processor detected	[Illustrative] Windows NPU check	[Illustrative] Second trusted Windows enumeration, when available	Confirmed by the preferred check	[Illustrative] Agreed	[Illustrative] High	[Illustrative timestamp]
Storage	[Illustrative] 412 GB available on the checked Windows drive	[Illustrative] Windows Storage check	Omitted	Confirmed by the preferred check	[Illustrative] Current at capture time	[Illustrative] High	[Illustrative timestamp]

A confirmed NotPresent neural-processor result is a hardware fact and does not create a warning. DetectionUnavailable means the check could not establish presence or absence and must not be displayed as Not present.

Clean Completed illustrative run and tool records
Field	Illustrative display value
Hardware scanner	[Illustrative] Local hardware scanner (LLM Fit) — approved identity and example version
Windows system-information check	[Illustrative] Windows system information — identity and version recorded when used
DXGI graphics check	[Illustrative] DXGI — identity and version recorded when used
Windows NPU check	[Illustrative] Windows NPU check — identity recorded when used
Windows Storage check	[Illustrative] Windows Storage — identity recorded when used
Local runtime probe	[Illustrative] Local runtime capability check (llama.cpp) — example build identifier
Source policy	[Illustrative] Source policy 1.0
Hardware schema	[Illustrative] Hardware schema 1.0
Safe run reference	[Illustrative bounded app-generated reference]
Run started	[Illustrative localised date, time and UTC offset]
Last stable fact captured	[Illustrative localised date, time and UTC offset]
Available-memory freshness	[Illustrative] Captured 2 seconds before report creation
Report created	[Illustrative localised date, time and UTC offset]
Completion	[Illustrative] Completed
Warning fixture illustrative IT records
Fact	Record
Installed memory	Selected value: [Illustrative] 32 GB installed
Used check: [Illustrative] Windows system information — 32 GB
Other check: [Illustrative] Local hardware scanner (LLM Fit) — 31.8 GB
Why this value was used: Confirmed using another trusted Windows check
Agreement: [Illustrative] Differed within the approved tolerance
Confidence: [Illustrative] High
Captured: [Illustrative timestamp]
Neural processor (NPU)	Selected value: Could not confirm
Used check: [Illustrative] Windows NPU check — no confirmed device
Why this value was used: This information could not be checked
Agreement: Unavailable
Confidence: Unavailable
Captured: [Illustrative timestamp]
Warning diagnostic	The memory difference was resolved safely. NPU presence could not be confirmed, but it did not block the hardware report.
Support reference	No support code was needed for this completed run.
Failure and cancellation applicability
State	Hardware-fact group	Run/tool group	Warning/diagnostic group
Clean Completed	Full canonical fact records	Full	No warnings or safe support codes were recorded for this run.
CompletedWithWarnings	Canonical facts plus retained competing observation where safe	Full	One resolved memory explanation; one NPU review explanation; no support code required in the canonical fixture
Failed — critical evidence	Only bounded evidence needed to explain the unresolved critical fact; no actionable snapshot	Failed stage, evidence handling, rules and timing	Plain explanation plus bounded support code
Failed — transient	No canonical hardware fact detail is shown because this run did not create a hardware report.	Failed stage, affected tool, result, rules and timing	Temporary-operation explanation plus bounded support code
Failed — repair required	Same empty-fact message	Failed stage, affected component, verification, rules and timing	Repair explanation plus bounded support code
Cancelled	Same empty-fact message	Cancelled stage, report state, partial-information treatment, rules and timing	Neutral cancellation explanation; normally no support code
Active, Stopping, navigation error	Not shown	Not shown	Not shown
Sanitisation rules
Display only allowlisted, typed presentation fields.
Never expose a model path, working directory, executable path, command line, environment value, raw JSON, stdout, stderr or native exception.
Never expose hostname, account name, machine serial, MAC address, CPU/device serial, unrestricted device interface identifier, secret, token or credential.
Use safe display labels for storage; never show a full filesystem path.
Explain a diagnostic in plain language before showing a stable support code.
A displayed support code must be app-generated, non-secret, allowlisted and bounded; the proposed display limit is 64 characters.
Provider and component names must be approved display identities, not raw filenames or process arguments.
Versions and timestamps appear only when genuinely captured.
Available-now memory always carries capture time or freshness.
Missing optional values are omitted or written as unavailable; they are not replaced with zero.
Dedicated and shared graphics memory remain separate in every record.
Competing observations may appear only when needed to explain a safe resolution or unresolved conflict.
Long values wrap; they are not horizontally clipped or silently truncated.
Final local-processing note: This hardware inspection ran locally and did not upload hardware information.
12. Action and navigation contract
State or classification	Action	Visibility	Enabled rule	Exact disabled/help behaviour	Destination or effect
Invalid/missing handoff	Back to model inspection	Visible	Enabled	None	Returns to the safe preceding journey; starts no run
Active	Cancel inspection	Visible and centred	Enabled until first activation	After activation, immediately replaced by Stopping state	Requests cancellation of the current run and contained child tree
Stopping	Stopping...	Visible and centred	Disabled	P4 proposed accessible name: Stopping hardware inspection	No action; remains until cleanup completes
Clean Completed	Continue to compatibility	Visible	Enabled only when usable handoff and registered Block 3 route both exist	Continue to compatibility is unavailable until this run has a usable hardware handoff and the compatibility step is available.	Navigates through the registered route with the current immutable handoff
Clean Completed	Run inspection again	Visible	Enabled	None	Creates a new run
CompletedWithWarnings	Continue to compatibility	Visible	Same two-condition rule	Same exact help	Same route/handoff boundary
CompletedWithWarnings	Run inspection again	Visible	Enabled	None	Creates a new run
Failed — critical evidence	Back	Visible	Enabled	None	Returns to the preceding safe page
Failed — critical evidence	Run inspection again	Visible only when explicitly retryable	Enabled when shown	Hidden rather than disabled when non-retryable	Creates a new run
Failed — transient	Try again	Visible	Enabled	None	Creates a new run
Failed — transient	Back	Visible	Enabled	None	Returns to the preceding safe page
Failed — repair required	Back to model inspection	Visible	Enabled	None	Returns to Model Inspection; no same-page retry
Cancelled	Run inspection again	Visible	Enabled after cleanup has completed	None	Creates a new run
Cancelled	Back	Visible	Enabled	None	Returns to the preceding safe page

New-run rules:

Every retry or rerun creates a new InspectionId.
Sequence tracking restarts for the new run.
Outer and nested disclosures start closed.
Live-region deduplication state resets.
Prior outcome, facts, review count and actionability are cleared before new-run events are accepted.
Events bearing an earlier InspectionId, a stale sequence, or arriving after that earlier run’s terminal result are ignored.
Same-run display revisions preserve the outer and nested disclosure states.
Same-run revisions do not repeat an already-announced terminal outcome.
Duplicate page-loaded events during the same page lifetime must not start duplicate inspections.

Block 3 boundary:

The Hardware page does not calculate model fit, memory reserve, context length, GPU offload, KV-cache configuration, TurboQuant settings, expected speed, expected quality or a will run result.
A prototype toast is not acceptable production behaviour.
While Continue is disabled, pointer, Enter and Space produce no navigation.
Its unavailable reason must be exposed through accessible help text, not only a visual tooltip.
Enabling Continue in a future implementation requires both a usable Hardware handoff and a real registered route; neither condition may be assumed from the button’s presence.
13. Responsive layout matrix

Global layout contract:

Centred page scroll column, maximum width 840 px.
Top spacing 28 px; bottom spacing 32 px.
Major vertical gaps 16 px.
Wide and medium horizontal gutters 24 px.
Compact horizontal gutters 16 px.
Cards use approximately 12 px corner radius and a 1 px semantic border.
This computer card padding is 24 px, reducing to 16 px on compact layouts.
Internal section gaps are 16 px; fact-tile gaps are 12 px.
Typography preserves the supplied hierarchy at approximately 32 / 18 / 14 / 12 / 10 without treating browser pixels as a native guarantee.
Interactive targets are at least 44 × 44 effective pixels.
No fixed page or card height, dead footer space, horizontal overflow, clipped text, or nested main scroll region.
The footer follows the main content in the natural page flow.
Condition	Width rule	Proposed composition	Fact tiles and actions	Acceptance requirement
Wide	888 px and above	Centred 840 px column. Completed outcomes use two columns: dominant This computer on the left; Local AI tools then Information sources on the right. Details and actions span the content width.	Fact tiles may use two columns. Actions may share a centred row when labels fit without compression.	Reading order remains page heading, outcome, computer facts, tools, sources, details, actions, footer. No excessive empty footer region.
Medium	600–887 px	Cards stack in the same semantic order: This computer, Local AI tools, Information sources, details, actions. Gutters remain 24 px.	Fact tiles use two columns only while each tile remains readable; otherwise they flow to one. Actions wrap or stack without changing priority.	No horizontal clipping or fixed-width support column.
Compact	Below 600 px	Single-column page with 16 px gutters and compact card padding. All status text wraps below or beside its icon without collision.	Fact tiles are one column. Actions stack to full available width while retaining at least 44 px height.	No horizontal scroll. Long stage names, memory labels and action text remain fully visible.
200-percent text	Native capture target 720 × 900 at 200-percent text	Natural page scrolling; layout reflows according to effective available width rather than preserving browser geometry.	Fact labels, values, status badges, disclosure labels and actions wrap. No fixed-height card or nested ordinary-record viewport.	Every heading, fact, stage, disclosure, IT record, action and footer item remains reachable. No horizontal clipping, overlap or inaccessible off-screen action.

The HTML/PNG evidence suggests the intended hierarchy can stack at medium, compact and enlarged-text conditions, but it is not native acceptance evidence.

14. Theme, accessibility, focus, live-region, and motion matrix
Area	Proposed acceptance contract
Light theme	Every surface, border, foreground, focus indicator and status tone resolves through semantic resources. No Hardware control depends on literal white, black, green, amber, red or blue values.
Dark theme	Same semantic hierarchy and status meaning as Light. Text, secondary text, borders, disabled controls and focus indicators remain distinguishable.
Windows High Contrast	Windows/semantic High Contrast resources override decorative tones. Borders, glyphs, text, focus and disabled state remain visible without relying on background tint.
Status meaning	Every status has an icon or vector mark, written status and accessible name. Colour is supplementary only.
Keyboard order	Tab order follows visual/read order: page recovery action where applicable; disclosure header; nested disclosure header when available; terminal actions; then any remaining shell targets. Hidden disclosure content is absent from tab order.
Focus visibility	Every interactive target has a visible semantic focus indicator in Light, Dark and High Contrast. Focus is not communicated by a subtle colour change alone.
Disclosure operation	Pointer, Enter and Space toggle each disclosure. Focus stays on the activated header. The header exposes correct button/disclosure role, name and expanded/collapsed state.
Independent disclosures	Opening the outer disclosure does not open the nested disclosure. Toggling either does not move focus, navigate or alter outcome.
Initial focus	Invalid handoff: Back to model inspection. First active frame: page heading. Clean/warning/critical/cancelled: outcome title. Transient: Try again. Repair-required: Back to model inspection.
Active-stage focus	Stage changes never steal focus from Cancel, a disclosure, or another current control.
Active announcements	A polite live region announces only the changed active-stage accessible name. It does not repeat the page title, all seven rows or the privacy subtitle on every stage.
First active announcement	Starting hardware inspection. Preparing the approved local inspection tools and a safe run context. State: Active.
Stopping announcement	Stopping hardware inspection. No hardware report will be created from this run.
Terminal announcement	Announced once per run. Clean/warning/cancelled and critical failure move focus to the outcome heading. Transient and repair-required move focus to the first meaningful recovery action.
Duplicate announcement prevention	Same-run content corrections update the visual presentation without repeating an unchanged terminal announcement. New runs clear the deduplication state.
Disabled Continue	Accessible name remains Continue to compatibility; disabled state is exposed; accessible help gives the exact F9 reason.
Target size	Buttons, disclosure headers, compact icon buttons and other interactive areas have at least 44 × 44 effective pixels.
Heading structure	Page title is the top heading. Outcome, major cards, Inspection details, IT groups and recovery headings use a logical descending hierarchy without skipped meaning.
Long and translated strings	Labels wrap naturally. No ellipsis hides essential action, status, warning, stage or evidence meaning. Cards and rows grow vertically.
Screen reader row names	Each stage name includes canonical stage, explanation and state. Visual status text is not redundantly announced multiple times.
Reduced motion	Orbit rotation, chevron transition and state-transition animation are disabled. A static orbit glyph plus Active remains. The 550 ms visual pacing requirement is bypassed.
Standard motion	Exactly one active orbit rotates. No decorative motion appears elsewhere. Cancellation is never delayed to complete an animation.
UI Automation	Expanded/collapsed state, disabled state, heading level, accessible help, live-region setting and action names must be inspectable through UIA.
Footer accessibility	Current step is exposed as Check hardware fit, step 3 of 5; prior and future labels remain readable but are not falsely announced as completed if the shell state does not support that claim.
15. Privacy and non-claims checklist

Each item is a proposed future acceptance criterion and remains unchecked.

 No model path, directory, command, process argument, stdout, stderr, native error or raw payload is displayed.
 No hostname, account name, serial number, MAC address, unrestricted device identifier, token, secret or credential is displayed.
 Safe support information is allowlisted, bounded and explained in plain language.
 Hardware information remains local and no UI copy claims an upload occurred.
 The UI makes no unsupported claim about retention, deletion, encryption or telemetry.
 Installed, Windows-usable and available-now memory are displayed as separate facts.
 Available-now memory includes genuine capture time or freshness.
 Dedicated, dedicated-system and shared-system graphics memory are not merged into an invented total.
 NotPresent and DetectionUnavailable are never conflated.
 Tool failure is described as an application/operation condition, not as bad hardware.
 A failed or cancelled run exposes no actionable snapshot or handoff.
 A completed report does not state that the selected model is compatible, suitable, fast enough or guaranteed to run.
 The page does not recommend context length, GPU layers, KV-cache format, TurboQuant mode, backend or memory reserve.
 The page does not state or imply a measured performance result.
 N of 7 is described as completed workflow stages, not seven independent hardware checks.
 No page-level percentage is created from stage count.
 Warning count is derived from unresolved presentation items.
 A resolved memory difference is visibly informational and does not increment the unresolved count.
 Continue is disabled unless both handoff and route preconditions are genuinely satisfied.
 HTML and PNG prototypes are not described as native implementation or test evidence.
 No pixel-perfect claim is made.
 No Gate 1, Gate 2, Stage B, Stage C, Stage D or Gate 8 completion is claimed.
 No production Hardware Inspection UI is claimed to exist.
 Worker A1’s frozen Stage A security package at dc70e8e323e5e35708aecd83c654d2763300dc04 is not reopened or reassessed.
16. Downstream ownership and collision map

This table describes future ownership areas only. It grants no person or worker permission to implement, modify, test, merge or record anything.

Future ownership area	Proposed boundary	Principal collision risks	Required coordination boundary
Presentation and copy	Exact page strings, state hierarchy, warning count, icons, user-facing statuses and accessibility names in this candidate	Functional copy drifting into compatibility conclusions; source-approved wording being silently changed	Later V0 record may transcribe only explicitly approved values; C0 coordinates any override
Theme tokens	Hardware-specific semantic aliases built from the current Model grammar	Literal colours; modifying Model resources; theme divergence; High Contrast failure	Model XAML is reference grammar only; shared-token changes require separate ownership
Progress control	Seven rows, one orbit, count, live announcement, reduced motion and cancellation transition	Fake percentage; multiple active indicators; short labels; pacing that delays cancel	Service lifecycle remains owned outside P4; control consumes typed presentation state only
Completed and recovery controls	Outcome, machine facts, support cards, warning/recovery families and their visual tones	Treating recovery subclasses as service statuses; compacting warning page; blaming hardware for tool failure	Terminal status and retryability come from approved application contracts
Details control	Outer disclosure, seven rows, natural scroll and status rendering	Reintroducing nested ordinary-record scroll; status/count coupling; importing Model semantics	Expanded-details design governs; implementation ownership remains unassigned
Technical IT control	Nested disclosure, groups, field order, safe diagnostics and sanitisation	Raw provider output; unsafe identifiers; unbounded values; false provenance	Security/architecture review owners retain data-boundary decisions
Action control	Exact labels, visibility, disabled reason and keyboard/UIA semantics	Prototype toast; enabled Continue without route; retry with reused run ID	Route registration and handoff eligibility remain outside P4
Page composer	State selection, reading order, responsive layout, focus entry and footer placement	Starting duplicate runs; retaining stale state; footer dead space; horizontal overflow	Application lifecycle and navigation contracts must be supplied by their owners
Shared Model/Onboarding integration	Visual alignment and current five-step shell journey	Copying Model fixtures/semantics; modifying shared controls without collision review	Reference commit 960bb4d… is grammar only; shell integration requires separate coordination
QA and evidence	Native capture matrix, UIA evidence, keyboard, screen reader, theme, motion and long-string evidence	Treating browser screenshots as proof; claiming Gate 8 before upstream gates	Evidence ownership remains with future QA/evidence lanes and C0; P4 supplies criteria only
17. Future Gate 8 native visual acceptance checklist

Gate 8 cannot be claimed until upstream gates, production implementation, trusted dependencies and any required navigation route are satisfied.

Dependency conditions
 Gate 1 has been closed by its authorised owner using accepted trusted evidence.
 Gate 2 and later prerequisite gates have been opened and completed through their authorised processes.
 A production Hardware Inspection UI exists under separate implementation authority.
 The production UI consumes approved typed presentation contracts rather than browser fixture behaviour.
 Any enabled Continue action has a usable Hardware handoff and registered Block 3 route.
 No acceptance evidence depends on reopening Worker A1’s frozen Stage A package.
Native capture matrix
 Capture every one of the 15 observable states at 1440 × 1100.
 Capture every one of the 15 observable states at 900 × 1000.
 Capture every one of the 15 observable states at 480 × 900.
 Capture every one of the 15 observable states at 720 × 900 with 200-percent text.
 Repeat the complete state/size matrix in Light, Dark and Windows High Contrast.
 The base native still-image matrix therefore covers 15 states × 4 capture conditions × 3 themes.
 Capture both disclosures closed and expanded where applicable, without replacing the required closed-default state.
 Capture the warning state with exactly one unresolved review item and one resolved informational note.
 Capture the clean Completed state with no warning/review wording.
 Capture Continue in its current visible-disabled condition with its accessible reason.
 When Block 3 later exists, separately capture the genuinely enabled Continue condition.
Responsive and visual checks
 Wide completed outcomes use two columns in the required reading order.
 Below 888 px, cards stack This computer, Local AI tools, then Information sources.
 Fact tiles flow from two columns to one without clipping.
 Compact actions remain at least 44 px high and do not overflow.
 At 200-percent text, every fact, stage, disclosure, IT record, action and footer item is reachable.
 No horizontal scroll appears.
 No fixed-height card clips content.
 No nested ordinary-record scrollbar appears.
 No dead footer space is created by fixed page sizing.
 Long translated or pseudo-localised strings wrap without collision.
 Dedicated and shared graphics memory remain visibly separate.
 Installed, usable and available-now memory remain visibly separate.
Theme and High Contrast checks
 Light theme uses semantic resources and preserves hierarchy.
 Dark theme uses semantic resources and preserves hierarchy.
 Windows High Contrast exposes every border, glyph, focus indicator, status and disabled state.
 No status depends on colour alone.
 Disabled Continue remains distinguishable without appearing enabled.
Keyboard and focus checks
 Complete every state’s available journey using keyboard only.
 Pointer, Enter and Space operate both disclosures.
 Focus remains on the disclosure header after toggling.
 Hidden disclosure content leaves tab order.
 Active-stage changes do not steal focus.
 Terminal focus lands on the state-specific heading or recovery action defined in Section 14.
 Retry produces a new-run presentation with both disclosures closed.
Screen reader and UIA checks
 Page and outcome headings expose correct levels.
 Every stage row exposes stage, explanation and state.
 Expanded/collapsed state is correct through UIA.
 Disabled Continue exposes the F9 reason.
 Active-stage changes are announced politely and once.
 Terminal result is announced once.
 Same-run revisions do not duplicate terminal announcements.
 New runs reset live-region state.
 Collapsed content is absent from the accessibility tree.
 Icons and decorative chevrons do not create duplicate speech.
Motion checks
 Standard motion shows exactly one active orbit.
 Reduced motion removes orbit rotation and transition animation.
 Reduced motion retains a visible and accessible Active state.
 Cancellation is not delayed by pacing or animation.
 Stopping remains until real cleanup completes.
Evidence interpretation
 Native captures are taken from the authorised WinUI build, not an HTML recreation.
 HTML and PNG comparisons are documented as structural/manual comparisons.
 No pixel-perfect statement is made.
 Any visual difference is assessed against semantic hierarchy, layout contract, focus, wrapping and accessibility—not browser-pixel identity.
 Gate 8 remains unclaimed until all upstream dependencies and acceptance evidence are satisfied.
18. Conflict and referral ledger
ID	Conflict	Proposed resolution	Confidence	Needs user approval	Out-of-scope owner
C-01	14 August metadata can be read as approving more than the visual direction	Limit its authority to visual Direction B only	High	Yes	User/C0
C-02	Direction B warning composition versus compact recovery warning card	Use full Direction B facts/support composition; recovery v2 supplies warning meaning	High	Yes	User/C0
C-03	One-warning versus two-warning wording	One unresolved NPU review item plus one resolved memory note	High	Yes	User/C0
C-04	Clean Completed lacks a complete approved copy set	Adopt the exact P4-proposed clean copy and records in this candidate	Medium	Yes	User/C0
C-05	Invalid-handoff behaviour exists, but complete visual copy does not	Adopt the bounded P4-proposed navigation-error contract	High	Yes	User/C0
C-06	Stopping prototype says compatibility report	Replace only that sentence with No hardware report will be created from this run.	High	Yes	User/C0
C-07	Prototype Continue produces page-local behaviour	Require usable handoff plus registered route; no toast	High	Yes	Block 3/navigation owner
C-08	Older sources use short stage labels	Use long canonical names everywhere	High	Yes	User/C0
C-09	Older details use five rows and bounded scrolling	Use seven rows and natural page scrolling	High	Yes	User/C0
C-10	Prototype footers use obsolete journey labels	Use current OnboardingStageIndicator.xaml labels	High	Yes	Shared shell owner
C-11	Recovery variants might be mistaken for separate service statuses	Keep all three beneath Failed; keep Stopping non-terminal	High	Yes	Application-contract owner
C-12	Stage Completed with note appears for both resolved and unresolved matters	Derive review count from explicit review disposition, not written row status	High	Yes	Presentation-model owner
C-13	PNGs show browser rendering and partial/full-page combinations	Use them only for structural comparison	High	Yes	Future QA/evidence owner
C-14	Exact native colours and pixel geometry are not authoritative	Use semantic tokens and approximate hierarchy; validate natively	High	Yes	Theme/QA owners
C-15	Exact provider, NPU enumeration and evidence-authority implementation is outside visual scope	Display only approved typed outcomes; refer provider decisions outward	High	No visual override needed	P1/P2 and later implementation gates
C-16	Gate 1 source is retained but Gate 1 remains Blocked	Preserve the stop/non-claim boundary; do not enable later work from this packet	High	No visual override needed	C0/Gate 1 owner
C-17	Stage A repository package exists at frozen commit, but no execution is authorised	Do not reopen A1’s security package or treat its existence as run evidence	High	No visual override needed	A1/C0
C-18	Native Gate 8 evidence does not exist	Treat Section 17 as future acceptance criteria only	High	Yes	Future QA/evidence owner
C-19	Exact Block 3 calculations and model-fit wording do not exist in P4 scope	Keep Continue gated and make no compatibility calculation	High	No visual override needed	Future Block 3 owner
C-20	Same-run revisions versus new-run disclosure reset can be conflated	Same-run preserves disclosure state; new run resets both disclosures and live state	High	Yes	Presentation/application integration owner

Out-of-scope referral — single listing: Gate 1 remains Blocked and no trusted Intel execution, candidate acceptance, runner configuration, Stage B, Gate 2 or later gate is claimed. Exact provider authority, NPU detection implementation, process containment, evidence retention and security decisions remain with their existing architecture/security owners. Block 3 route construction and compatibility calculations remain outside P4. Worker A1’s frozen security package at commit dc70e8e323e5e35708aecd83c654d2763300dc04 remains unopened and unassessed by this audit.

19. Single bundled approval checklist
F1 — Resolved — Approved: Approve visual Direction B as the 14 August design’s only authority.
F2 — Resolved — Approved: Approve the full Direction B machine-facts/support composition for clean Completed and CompletedWithWarnings.
F3 — Resolved — Approved: Approve one unresolved review item plus one resolved informational note, with a derived count.
F4 — Resolved — Approved: Approve the proposed clean-Completed copy, facts, actions, seven rows and IT records.
F5 — Resolved — Approved: Approve the proposed bounded invalid/missing-handoff screen and recovery copy.
F6 — Resolved — Approved: Approve the stopping lifecycle and exact Hardware-report sentence.
F7 — Resolved — Approved: Approve the exact action map, new-run reset and stale-event rules.
F8 — Resolved — Approved: Approve the seven-stage copy matrix, truthful counter and 550 ms pacing rule.
F9 — Resolved — Approved: Approve visible-disabled Continue until both usable handoff and registered Block 3 route exist.
F10 — Resolved — Approved: Approve the provenance manifest and future native-capture oracle.

A later V0-record worker may transcribe only the exact values, copy, state matrix, stage matrix, layout criteria, accessibility criteria, provenance identities, conflict resolutions and acceptance checklist that the user explicitly approves from this packet. It may record the proposal’s approved scope and source precedence.

It may not edit a supplied source; alter or implement repository files; write XAML, C#, tests, fixtures, scripts, workflows, plans, RTMs or evidence; broaden the 14 August authority; change copy without an explicit override; resolve Block 3 calculations; reopen A1’s security package; claim implementation or testing; or declare any gate, stage, runner or hardware execution complete.
