# C0 integration contract

This patch is based on reviewed F1 tip `414ad9f97e5cc27e6b710f82d70b985aa14f3507`.
It moves, rather than copies, the five reviewed pins into one shared authority.
The existing F1 app consumes that authority through a project reference, so the
working slider and download behavior remain intact while the app-local catalog
files are removed.

C0 must make `ResumableVerifiedModelDownloadService` create
`DownloadedArtifactEvidence` only from the completed response and bytes it
actually hashes. `ModelDownloadCoordinator` must begin with the closed
`OptimizationPreferenceBand`, re-resolve it through `PinnedGraniteModelCatalog`
at the completion boundary, and call `CanonicalDownloadVerifier.IsExactMatch`
before publication, inspection, handoff, or navigation. It must bind the exact
operation, generation, publication identity, inspection request, and lifecycle
generation, and reject cancelled, retired, stale, duplicate, or late completion.

No contract, event, navigation payload, receipt, diagnostic, or UI evidence may
carry the downloader's local path. The existing internal `VerifiedDownloadedModel`
may retain its path only inside the app-owned handoff implementation. C0 must add
owner tests around the real coordinator/service before changing T1's intentional
download RED to GREEN.

The application assembly is deliberately not a friend of the authority assembly.
C0 must move the downloader-produced evidence creation, trust-boundary
re-resolution, and verification implementation into this authority project and
expose only a narrow operation API or opaque successful result to the app. No app
component may construct evidence/context or invoke the internal verifier. The
only friend grants are the two explicit test assemblies; the legacy constructor
overload exists solely so the unchanged F1 owner fixtures continue to compile.

The retained `GraniteEdgeAI.CrossFeature.Contracts` project preserves the R4.1
exact-result Chat/export seam. C0 must implement `IExactOptimizationResultConsumer`
in Model Optimization storage, validate the exact result and receipt identities
(including plan, configuration, source, hardware, output, manifest digest, byte
length, and lifecycle generation), reject runtime-only export, and never fall back
to an ambient last-result field. `ModelOptimizationPage` must retain and submit
only the selected result and its lifecycle generation through this consumer.
