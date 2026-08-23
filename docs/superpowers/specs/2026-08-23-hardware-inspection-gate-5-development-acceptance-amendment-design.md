# Hardware Inspection Gate 5 Development Acceptance Amendment Design

**Status:** Approved direction recorded on 2026-08-23; written amendment awaiting review

**Scope:** Gate 5 development acceptance only

**Amends:** `docs/superpowers/specs/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities-design.md`

## 1. Decision

Gate 5 gains a second, explicitly weaker evidence route for development continuity. A self-signed package is prepared on the development host, then installed and activated only inside a disposable Windows guest. Three consecutive registered-AUMID passes may close the Gate 5 development-acceptance dependency and permit Gate 6 implementation to begin.

This route does not satisfy Smart App Control, public-trust signing, release readiness, or supported-machine acceptance. Those requirements remain open and must later be satisfied with an institutionally or publicly trusted signing identity on an appropriate Windows host.

The original production code, package identity, closed activation command, test selection, result grammar, cleanup requirements, and nonclaims remain unchanged.

## 2. Host constraints and selected approach

The current development machine reports Windows Home/Core, has no Windows Sandbox executable, and has insufficient free disk space for a supported Windows 11 guest. Microsoft does not support Windows Sandbox or Client Hyper-V on Windows Home. The repository therefore must not enable hidden features, patch edition checks, disable Smart App Control, or silently install a third-party hypervisor.

The selected implementation is hypervisor-neutral:

1. a host-side builder creates a closed development-acceptance bundle from the exact generated x64 test MSIX;
2. signing occurs only on the host that owns the purpose-specific private key;
3. the bundle contains the signed MSIX, the public certificate, a guest runner, and a strict manifest containing exact file lengths and SHA-256 hashes;
4. a disposable Windows guest receives the bundle through a read-only mapping or copy and exposes one dedicated writable result directory;
5. the guest validates the manifest before importing the public certificate, normally installing the package, activating its registered AUMID three times, validating each bounded result, and cleaning up the package and certificate in `finally`;
6. the guest writes one privacy-minimized campaign summary to the dedicated result directory.

This design works with any supported disposable Windows guest. Hypervisor provisioning, Windows licensing, storage reclamation, and base-image acquisition are operational prerequisites outside the repository change.

## 3. Alternatives rejected

### Enable Windows Sandbox or Hyper-V on Windows Home

Rejected because Microsoft does not license or support these features on the installed edition. Unsupported feature-enablement scripts would weaken maintainability and could change boot or virtualization state.

### Disable Smart App Control on the main host

Rejected because it weakens the user's normal security posture and still would not prove public-trust behavior.

### Export the signing private key into the guest

Rejected because the guest needs only the already signed package and public certificate. Exporting a PFX would unnecessarily widen private-key custody.

## 4. Bundle contract

The host builder accepts only:

- a 40-hex certificate thumbprint;
- `Debug` or `Release` configuration;
- an empty, caller-selected output directory outside the repository and source/build roots.

It reuses the existing closed package identity and staging rules. It requires subject `CN=GraniteEdgeAI`, code-signing EKU, current validity, and a private key in `Cert:\CurrentUser\My`. It exports only the public DER certificate.

The bundle contains exactly:

- `GraniteEdgeAI.UnitTests.msix`;
- `GraniteEdgeAI.cer`;
- `Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`;
- `bundle-manifest.json`.

The manifest uses schema `granite.hardware-inspection.development-acceptance-bundle/v1`, contains no host path or account data, and records the package identity, publisher, version, architecture, certificate thumbprint, and an ordinal array of the three payload filenames with exact byte length and lowercase SHA-256. UTF-8 framing is no BOM and exactly one LF. Unknown or duplicate properties fail closed.

The builder refuses reparse points, pre-existing output content, an unexpected MSIX inventory, an invalid signature, a certificate/private-key file, or any unlisted file. It removes only its owned temporary staging directory in `finally` and does not install the package or alter a trust store.

## 5. Guest execution contract

The guest runner accepts only an absolute bundle directory and an absolute empty result directory. Both directories must be non-root, distinct, and free of reparse points. The bundle must match the exact manifest before any state change.

The runner then:

1. requires an elevated Windows PowerShell process in a disposable Windows guest;
2. imports `GraniteEdgeAI.cer` into `Cert:\LocalMachine\TrustedPeople` only after validating its thumbprint, subject, validity, absence of a private key, and code-signing EKU;
3. normally installs the signed MSIX with `Add-AppxPackage`;
4. requires exact name, publisher, x64 architecture, version, `SignatureKind=Developer`, and an install location outside the bundle and result directories;
5. resolves exactly `<PackageFamilyName>!App` and activates only `--hardware-inspection-process-acceptance --result-token <32-lowercase-hex>` through `IApplicationActivationManager`;
6. performs exactly three sequential repetitions, each with a fresh token and the existing 180-second timeout;
7. validates the existing 64-KiB single-LF JSON-v1 result and requires total equal to passed with an empty failure list;
8. records only repetition number, package-identity-present, total, passed, and signature kind;
9. removes the exact installed test package, the imported certificate when this invocation added it, and all owned temporary result files in `finally`.

If cleanup cannot be proven, the campaign fails. No fallback launches package files by path, registers a loose layout, disables policy, retries a failed repetition, or retains exception text, stdout, stderr, device labels, paths, machine identity, usernames, or timing.

The campaign summary uses schema `granite.hardware-inspection.development-acceptance/v1`, includes `classification: development-only`, `publicTrustVerified: false`, `smartAppControlVerified: false`, exactly three repetition summaries, and an empty failure list. It is written atomically as UTF-8 without BOM plus one LF and is bounded to 64 KiB.

## 6. Isolation runbook

The runbook requires a disposable supported Windows guest with networking, clipboard, audio input, video input, printer redirection, and host-drive sharing disabled unless a narrowly mapped folder is needed. The bundle mapping is read-only. The result mapping is a separate empty writable directory. The guest has no access to the repository, certificate private key, user profile, Downloads folder, or unrelated host paths.

For Windows Sandbox, a generated `.wsb` file may express these settings only on a supported Windows edition where Sandbox is already installed. For other hypervisors, the operator must create equivalent isolation before running the guest script. The repository does not provision or configure the hypervisor.

## 7. Evidence and gate semantics

A valid three-run summary changes Gate 5 status only to:

> Development acceptance passed in a disposable guest. Smart App Control and public-trust signing remain unverified.

That status permits Gate 6 source-authority, normalization, consistency, freshness, provenance, and canonical-resolution implementation to begin. It does not authorize production composition, release, distribution, or a claim that Smart App Control accepts the package.

The original signed-AUMID/SAC requirement remains a release-evidence item. When a UCL or public certificate becomes available, the original host launcher and three-run evidence must still be executed on an appropriate supported Windows machine.

## 8. Testing and verification

Executable behavior is test-first. Tests run the real scripts against bounded temporary fixtures and prove:

- the host builder rejects an invalid certificate, dirty output directory, reparse point, package drift, unexpected member, and stale build;
- the manifest parser rejects malformed UTF-8, BOM/CRLF, duplicate/unknown properties, wrong hashes or lengths, wrong identity, and extra files;
- the guest runner performs no trust or package mutation before full bundle validation;
- exact package/certificate cleanup occurs on success and every simulated failure boundary;
- activation arguments, three-repetition count, result grammar, and privacy-minimized summary are exact;
- certificate private-key material, repository paths, host paths, device labels, process output, and exception text never enter the bundle or summary;
- Stage 0 inventory and the existing Hardware Inspection suites continue to pass.

The real three-run disposable-guest campaign remains an environmental verification step. Until a supported guest and sufficient storage are available, its evidence status is `not run`, not passed or failed.

