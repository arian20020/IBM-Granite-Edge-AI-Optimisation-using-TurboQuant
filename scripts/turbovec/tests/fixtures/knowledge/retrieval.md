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
