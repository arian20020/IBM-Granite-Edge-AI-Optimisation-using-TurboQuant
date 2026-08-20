# Retrieval

Retrieval finds relevant chunks for a query. A document is divided into bounded chunks, each chunk receives a stable identifier, and an embedding represents the chunk for vector search. At query time the same embedding route represents the question. The index returns ranked chunk identifiers. The benchmark maps those identifiers to source judgements without placing raw document content into its result file.

## Exact baseline

The float32 route is the matched exact baseline for this controlled evaluation. Candidate rankings are compared with the baseline rankings for the same ordered query set and the same top-k. Recall at k is the overlap between the two top-k identifier sets divided by k. Matching query counts and stable unique identifiers prevent results from unrelated runs from being combined accidentally.

## Judged relevance

MRR uses the reciprocal rank of the first candidate item judged relevant. Hit at k records whether any judged relevant item appears in the candidate top-k. A query with an explicitly empty relevant_sources list is a no-supported-answer case: its MRR and Hit contribution is zero. The system never invents relevance for such a query, even if a returned distractor contains a familiar word.

## Cold and warm timing

Cold vector-search timing is recorded separately from warm vector-search timing. Each summary contains a sample count, minimum, conventional median, nearest-rank p95, and maximum in seconds. Release evidence requires at least five warm samples. Embedding duration is not silently mixed into vector-search duration, and a zero baseline median fails closed because a meaningful latency ratio cannot be calculated.

## Storage comparison

Persisted candidate bytes are divided by the matched float32 vector bytes to produce a size ratio. Both byte counts must be positive. The ratio compares equivalent vector data rather than an entire application folder or an unrelated cache. A four-bit TurboVec candidate passes the storage criterion only when its persisted size ratio is at most one quarter of the float32 vector bytes.

## Simultaneous gate

The retrieval gate requires Recall at 10 of at least 0.85, an MRR ratio of at least 0.90, a Hit at 5 delta of at least minus 0.05, a size ratio no greater than 0.25, and a median vector-search latency ratio no greater than 1.0. Boundary equality passes. All criteria must pass simultaneously, and invalid, infinite, or contradictory evidence fails closed.

## Paraphrase and distractors

The sentence "retrieval locates the most useful text segments for a question" is a controlled paraphrase of retrieval finds relevant chunks for a query. Nearby material about serialization, file discovery, and model identity serves as distractor content. A sound embedding may connect the paraphrase to this retrieval source while avoiding passages whose only connection is a generic term such as model, file, or test.

## Repeated concept

Relevant chunks are discussed again to exercise repeated language across overlapping windows. Retrieval finds relevant chunks, rankings preserve stable identifiers, and judgements identify supporting sources. Repetition is intentional but each emitted chunk remains unique because its source coordinates and bounded text participate in identifier derivation. The evaluator rejects duplicate identifiers inside any ranking.

## Safe source identities

Evaluation sources are canonical portable relative file identities ending in .txt or .md. They use a single forward slash between nested segments and reject traversal, absolute paths, drive prefixes, alternate data streams, control characters, and Windows device names. Reports expose these controlled relative identities only where the public evaluation fixture requires them; operational evidence avoids private local paths.

## Deterministic output

Canonical JSON uses schema version 1, finite numeric values, stable field ordering, and no dataclass internals. A Markdown summary is derived from the same validated evidence. Requested and actual backend or provider labels remain explicit, so a fallback cannot masquerade as the requested acceleration route. These reporting rules are separate from retrieval quality and do not create relevance for unsupported questions.

## Candidate route distractor

A two-bit candidate and a four-bit candidate are evaluated beside one float32 baseline. Each candidate has its own ranking metrics, timing summary, storage evidence, and explicit bit identity. Only the four-bit candidate feeds the release gate, but the two-bit result remains visible for analysis. Keeping both routes in one matched suite prevents evidence from separate corpora or query orders from being presented as a coherent comparison.

## Ordering distractor

Rankings contain stable unique identifiers in descending relevance order. A reversed ordering can preserve Recall at 10 when it contains the same ten identifiers while changing reciprocal rank. A disjoint top ten can reduce Recall at 10 to zero when the controlled corpus has at least twenty chunks. Both situations are useful tests: one isolates ordering quality and the other proves the recall threshold can actually fail on the fixture.

## Quantile distractor

The timing median uses the conventional definition, averaging the two middle sorted samples when the count is even. The p95 uses the deterministic nearest-rank definition at sorted index ceiling of ninety-five percent times sample count, minus one. These timing rules are deliberately documented in retrieval.md but do not themselves make a chunk relevant to a question about the Granite model family.

## Provider honesty distractor

Requested provider and actual provider are separate bounded identity fields. They may differ when the runtime honestly reports a fallback, while route and backend identities cannot be relabeled. A float32 route remains float32, a two-bit route remains TurboVec two-bit, and a four-bit route remains TurboVec four-bit. This distinction prevents an accelerated label from being inferred merely because acceleration was requested.

## Corpus discovery distractor

Document discovery accepts only supported UTF-8 text and Markdown files under controlled byte and count limits. It sorts portable relative identities deterministically and rejects unsafe links or races. Discovery is performed before chunking, but file enumeration is not a retrieval metric. This paragraph adds file-system terminology as realistic distractor content while keeping the fixture limited to granite.txt and retrieval.md.

## Chunk boundary distractor

Default chunking uses a maximum of twelve hundred characters with two hundred characters of overlap. Preferred paragraph, newline, and whitespace boundaries keep text readable while source coordinates preserve identity. The expanded fixture intentionally emits at least twenty unique default chunks, allowing one top-ten ranking to be compared with another disjoint top-ten ranking without inventing identifiers outside the corpus.

## Serialization distractor

Stable JSON ordering makes evidence easy to hash and compare. Serialization rejects NaN, Infinity, invalid route combinations, contradictory gate values, and private path-like identities. Markdown is a view of the same validated object rather than an independent calculation. These rules protect evidence integrity, but a query about relevant chunks should still rank the direct retrieval definition above this reporting-focused passage.

## Gate arithmetic distractor

The four-bit gate recomputes ratios from represented values. Candidate MRR is divided by baseline MRR, candidate persisted bytes are divided by float32 vector bytes, and candidate warm median is divided by baseline warm median. Zero denominators fail closed. Hit delta subtracts baseline Hit at 5 from candidate Hit at 5 with a tiny absolute tolerance only at the declared minus five-percent boundary.

## Wrong-answer distractor

A distractor can be syntactically valid, contain familiar terms, and still lack judged relevance. The fixture includes passages about schemas, providers, storage, and chunking so a poorly aligned ranking has credible alternatives to the relevant definition. No-supported-answer queries keep an empty source list even when a passage repeats a word from the question. This prevents benchmark code from treating lexical coincidence as ground truth.

## Retrieval recap

Retrieval finds relevant chunks for a query, and the controlled paraphrase says that retrieval locates useful text segments for a question. Those two sentences support the evaluation queries assigned to retrieval.md. The surrounding sections create enough independent chunks and distractors for Recall at 10, MRR, and Hit at 5 to move separately. Stable source judgements, not raw text copied into output, determine which returned identifiers count as relevant.
