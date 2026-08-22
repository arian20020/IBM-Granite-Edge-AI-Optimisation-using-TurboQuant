# Hardware Inspection Gate 1 LLM Fit Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Prove or reject one integrity-pinned LLM Fit Windows x64 candidate as the primary Hardware Inspection collection route, with machine-readable hardware JSON, target Intel comparison evidence, explicit GPU/NPU gaps, bounded execution, offline operation, and no dashboard or listening port.

**Architecture:** Build an isolated net8.0-windows feasibility CLI and deterministic MTP suite under tools/. The CLI owns only Gate 1 package verification, fixed command construction, bounded child-process execution, schema assessment, and privacy-safe evidence; it is never referenced by WinUI or production Hardware Inspection. Gate 2 will build the reusable production ExternalTools and ProcessExecution boundaries.

**Tech Stack:** C# 12, .NET 8, repository SDK 10.0.301, MSTest 4.3.2, Microsoft Testing Platform 2.3.2, System.Text.Json, Windows x64 PE inspection, PowerShell 7/Windows PowerShell 5.1-compatible acquisition and evidence scripts, GitHub Actions on windows-latest.

---

## 1. Locked research inputs

Use only this candidate in the first Gate 1 pass:

| Field | Locked value |
|---|---|
| Project | AlexsJones/llmfit |
| Candidate tag | v1.1.9 |
| Release commit | a02e13f1013ed69889ff44426a651bf7c68c292e |
| Published | 2026-08-09T17:07:55Z |
| Windows artifact | llmfit-v1.1.9-x86_64-pc-windows-msvc.zip |
| Artifact bytes | 5,255,910 |
| Artifact SHA-256 | a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738 |
| Extracted executable SHA-256 | db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19 |
| PE machine | AMD64 / 0x8664 |
| System command | --no-dashboard --json system |
| Version command | --version |
| License | MIT; retain LICENSE when redistributed |

Primary sources:

- https://github.com/AlexsJones/llmfit/releases/tag/v1.1.9
- https://github.com/AlexsJones/llmfit/commit/a02e13f1013ed69889ff44426a651bf7c68c292e
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/docs/cli.md
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/docs/platform-support.md
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/llmfit-tui/src/main.rs
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/llmfit-tui/src/serve_shared.rs
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/llmfit-core/src/hardware.rs
- https://raw.githubusercontent.com/AlexsJones/llmfit/v1.1.9/LICENSE

The tagged source establishes the actual v1.1.9 JSON envelope:

~~~text
system
├── total_ram_gb
├── available_ram_gb
├── cpu_cores
├── cpu_name
├── has_gpu
├── gpu_vram_gb
├── gpu_available_gb
├── gpu_name
├── gpu_count
├── unified_memory
├── backend
└── gpus[]
    ├── name
    ├── vram_gb
    ├── backend
    ├── count
    ├── unified_memory
    └── memory_bandwidth_gbps
~~~

The CLI help text still describes older names and an os field that the serializer does not emit. Gate 1 records this as schema-documentation drift; it must not build a production DTO around the prose-only schema.

A pre-planning download matched the official release digest and contained `LICENSE`, `README.md`, and `llmfit.exe`. On that temporary copy, both `Get-AuthenticodeSignature` and SignTool reported no Authenticode signature, contrary to the upstream README signing claim. This is a preliminary security finding, not Gate 1 acceptance evidence: Task 3 must freshly acquire the pinned archive, reproduce the observation, and record the signature-check result with the verified executable hash. Functional spike work may continue because the approved architecture requires a trusted manifest, SHA-256, PE architecture, and reported version, but production redistribution remains unapproved until the discrepancy is explicitly dispositioned. The upstream project is MIT-licensed, but Gate 1 does not perform a transitive Rust dependency-notice inventory; that remains a second explicit redistribution concern.

## 2. Scope boundaries

Gate 1 creates no WinUI, Model Inspection, HardwareSnapshot, DXGI, NPU, llama.cpp, compatibility, navigation, or production process-runner code. Do not modify:

- IBM Granite with TurboQuant (Intel)/**
- shared/GraniteEdgeAI.ModelInspection.**
- infrastructure/GraniteEdgeAI.ModelInspection.**
- workers/GraniteEdgeAI.ModelInspection.**
- runtime/GraniteEdgeAI.ModelInspection.**
- tests/UnitTests/GraniteEdgeAI.UnitTests/**
- IBM Granite with TurboQuant (Intel).slnx

The candidate executable stays under ignored third-party/bin/ or another operator-controlled path. Never commit the archive or executable.

## 3. File map

Create:

~~~text
tools/HardwareInspection.LlmFitSpike/
├── HardwareInspection.LlmFitSpike.csproj
├── README.md
├── Program.cs
├── SpikeOptions.cs
├── Candidate/
│   ├── LlmFitCandidateManifest.cs
│   ├── LlmFitCandidateManifestLoader.cs
│   ├── LlmFitCandidateVerifier.cs
│   ├── LlmFitCandidateVerification.cs
│   └── PeImageInspector.cs
├── Candidates/
│   └── llmfit-v1.1.9-win-x64.json
├── Command/
│   ├── LlmFitCommand.cs
│   └── LlmFitCommandBuilder.cs
├── Execution/
│   ├── BoundedTextReader.cs
│   ├── LlmFitProcessResult.cs
│   ├── LlmFitProcessRunner.cs
│   └── TcpListenerObserver.cs
├── Inspection/
│   ├── LlmFitSystemAssessment.cs
│   └── LlmFitSystemJsonAssessor.cs
└── Evidence/
    ├── LlmFitGate1Evidence.cs
    ├── LlmFitGate1EvidenceWriter.cs
    └── LlmFitGate1Runner.cs

tools/HardwareInspection.LlmFitSpike.Tests/
├── HardwareInspection.LlmFitSpike.Tests.csproj
├── README.md
├── Candidate/
├── Command/
├── Execution/
├── Inspection/
├── Evidence/
└── Support/

tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/
├── GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.csproj
└── Program.cs

tools/HardwareInspection.LlmFitSpike.IntegrationTests/
├── HardwareInspection.LlmFitSpike.IntegrationTests.csproj
├── README.md
└── LlmFitCandidateIntegrationTests.cs

scripts/hardware-inspection/
├── Acquire-HardwareInspectionLlmFitCandidate.ps1
├── Capture-HardwareInspectionWindowsReference.ps1
└── Write-HardwareInspectionLlmFitGate1Report.ps1

docs/testing/runbooks/
└── Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md

docs/testing/evidence/
└── 2026-08-15-hardware-inspection-gate1-llmfit-verification.md

.github/workflows/
└── hardware-inspection-llmfit-spike.yml
~~~

Modify:

- tools/README.md
- scripts/README.md
- docs/testing/evidence/README.md

## 4. Verification commands

The repository-level global.json selects Microsoft Testing Platform. With SDK 10.0.301, use --project rather than a positional project argument:

~~~powershell
$DeterministicProject =
    "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj"

dotnet restore $DeterministicProject --runtime win-x64

dotnet test `
    --project $DeterministicProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 43 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-deterministic.trx" `
    --no-ansi
~~~

The trusted target project is invoked separately:

~~~powershell
$TrustedProject =
    "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj"

dotnet restore $TrustedProject --runtime win-x64
dotnet build `
    $TrustedProject `
    --configuration Release `
    --runtime win-x64 `
    --no-restore

dotnet test `
    --project $TrustedProject `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedWindowsIntel" `
    --minimum-expected-tests 3 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-trusted.trx" `
    --no-ansi
~~~

The offline test is deliberately separate because it must start with no operational non-loopback network interface:

~~~powershell
dotnet test `
    --project $TrustedProject `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedOffline" `
    --minimum-expected-tests 1 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-offline.trx" `
    --no-ansi
~~~

### Task 1: Establish the candidate manifest and test projects

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/HardwareInspection.LlmFitSpike.csproj
- Create: tools/HardwareInspection.LlmFitSpike/Candidates/llmfit-v1.1.9-win-x64.json
- Create: tools/HardwareInspection.LlmFitSpike/Candidate/LlmFitCandidateManifest.cs
- Create: tools/HardwareInspection.LlmFitSpike/Candidate/LlmFitCandidateManifestLoader.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Candidate/LlmFitCandidateManifestTests.cs

- [ ] **Step 1: Create the two project files and the failing manifest tests**

Use `net8.0-windows10.0.19041.0`, `PlatformTarget=x64`, and `RuntimeIdentifier=win-x64` for both projects so the test project can reference the Windows-only spike. Set `ImplicitUsings`, `Nullable`, `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`, and `Deterministic`. The test project must also set `OutputType=Exe`, `IsTestProject=true`, `EnableMSTestRunner=true`, `TestingPlatformDotnetTestSupport=true`, and `TestingPlatformShowTestsFailure=true`; reference the spike project; pin `Microsoft.NET.Test.Sdk` 18.8.1, `Microsoft.Testing.Extensions.TrxReport` 2.3.2, `MSTest.TestAdapter` 4.3.2, and `MSTest.TestFramework` 4.3.2. In the spike project, copy `Candidates/llmfit-v1.1.9-win-x64.json` to the assembly output with `CopyToOutputDirectory=PreserveNewest`. In the test project, link that same manifest plus Task 4 fixtures into matching `Candidates/` and `Fixtures/` output folders with `CopyToOutputDirectory=PreserveNewest`; do not rely on transitive content copying.

The first tests are:

~~~csharp
[TestClass]
[TestCategory("Deterministic")]
public sealed class LlmFitCandidateManifestTests
{
    [TestMethod]
    public void Load_ApprovedCandidate_PreservesExactIdentity()
    {
        LlmFitCandidateManifest manifest = LoadApproved();

        Assert.AreEqual("1.0", manifest.SchemaVersion);
        Assert.AreEqual("llmfit-v1.1.9-win-x64", manifest.CandidateId);
        Assert.AreEqual("1.1.9", manifest.Version);
        Assert.AreEqual(
            "a02e13f1013ed69889ff44426a651bf7c68c292e",
            manifest.ReleaseCommit);
        Assert.AreEqual(
            "a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738",
            manifest.Archive.Sha256);
        Assert.AreEqual(
            "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19",
            manifest.Executable.Sha256);
    }

    [TestMethod]
    public void Load_ApprovedCandidate_UsesOnlyReadOnlyCommands()
    {
        LlmFitCandidateManifest manifest = LoadApproved();

        CollectionAssert.AreEqual(
            new[] { "--no-dashboard", "--json", "system" },
            manifest.Commands.System);
        CollectionAssert.AreEqual(
            new[] { "--version" },
            manifest.Commands.Version);
        Assert.IsFalse(
            manifest.Commands.System.Any(
                value => value.Equals("serve", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Load_ApprovedCandidate_RetainsMitLicense()
    {
        LlmFitCandidateManifest manifest = LoadApproved();

        Assert.AreEqual("MIT", manifest.License.Spdx);
        Assert.AreEqual("LICENSE", manifest.License.RelativePath);
        CollectionAssert.Contains(manifest.RequiredFiles, "LICENSE");
        CollectionAssert.Contains(manifest.RequiredFiles, "README.md");
    }

    [TestMethod]
    public void Load_TraversalPath_Throws()
    {
        string json = ApprovedJson().Replace(
            "\"relativePath\": \"llmfit.exe\"",
            "\"relativePath\": \"..\\\\llmfit.exe\"",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<InvalidDataException>(
            () => LlmFitCandidateManifestLoader.Parse(json));
    }

    private static LlmFitCandidateManifest LoadApproved() =>
        LlmFitCandidateManifestLoader.Load(
            Path.Combine(
                AppContext.BaseDirectory,
                "Candidates",
                "llmfit-v1.1.9-win-x64.json"));

    private static string ApprovedJson() =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Candidates",
                "llmfit-v1.1.9-win-x64.json"));
}
~~~

- [ ] **Step 2: Run the tests and verify the RED state**

Run:

~~~powershell
dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "FullyQualifiedName~LlmFitCandidateManifestTests" `
    --no-ansi
~~~

Expected: compilation fails because LlmFitCandidateManifest and LlmFitCandidateManifestLoader do not exist.

- [ ] **Step 3: Add the exact candidate manifest**

Use this complete JSON:

~~~json
{
  "schemaVersion": "1.0",
  "candidateId": "llmfit-v1.1.9-win-x64",
  "version": "1.1.9",
  "releaseTag": "v1.1.9",
  "releaseCommit": "a02e13f1013ed69889ff44426a651bf7c68c292e",
  "publishedAtUtc": "2026-08-09T17:07:55Z",
  "archive": {
    "fileName": "llmfit-v1.1.9-x86_64-pc-windows-msvc.zip",
    "downloadUri": "https://github.com/AlexsJones/llmfit/releases/download/v1.1.9/llmfit-v1.1.9-x86_64-pc-windows-msvc.zip",
    "lengthBytes": 5255910,
    "sha256": "a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738"
  },
  "executable": {
    "relativePath": "llmfit.exe",
    "sha256": "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19",
    "peMachine": "AMD64",
    "authenticodePolicy": "ObserveAndRecord"
  },
  "requiredFiles": [
    "llmfit.exe",
    "LICENSE",
    "README.md"
  ],
  "commands": {
    "version": [
      "--version"
    ],
    "system": [
      "--no-dashboard",
      "--json",
      "system"
    ]
  },
  "license": {
    "spdx": "MIT",
    "relativePath": "LICENSE"
  }
}
~~~

- [ ] **Step 4: Implement strict manifest parsing**

Define immutable records for the root, archive, executable, commands, and license. Parse with camel-case naming, disallow unknown manifest members, require 64-character lowercase hexadecimal hashes, require HTTPS with host github.com, reject rooted or parent-traversing relative paths, require the exact fixed commands above, and reject duplicate required files case-insensitively.

The loader's public contract is:

~~~csharp
public static class LlmFitCandidateManifestLoader
{
    public static LlmFitCandidateManifest Load(string path);

    public static LlmFitCandidateManifest Parse(string json);
}
~~~

Do not add a generic command or argument property. The manifest exposes only Version and System command arrays.

- [ ] **Step 5: Run the focused and complete deterministic suites**

Expected focused result: 4 tests passed, 0 failed, 0 skipped.

Expected complete result at this task: at least 4 tests passed.

- [ ] **Step 6: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike" `
    "tools/HardwareInspection.LlmFitSpike.Tests"
git commit -m "test(hardware-inspection): lock LLM Fit candidate"
~~~

### Task 2: Build the fixed, no-dashboard command contract

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/Command/LlmFitCommand.cs
- Create: tools/HardwareInspection.LlmFitSpike/Command/LlmFitCommandBuilder.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Command/LlmFitCommandBuilderTests.cs

- [ ] **Step 1: Write three failing command tests**

Test exact version/system arguments, a candidate root containing spaces, and rejection when the executable relative path escapes the root. Assert UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true, and no shell executable.

~~~csharp
[TestMethod]
public void BuildSystem_UsesOnlyPinnedReadOnlyArguments()
{
    LlmFitCommand command = LlmFitCommandBuilder.BuildSystem(
        @"C:\candidate root",
        ApprovedManifest());

    CollectionAssert.AreEqual(
        new[] { "--no-dashboard", "--json", "system" },
        command.Arguments);
    Assert.AreEqual(
        Path.GetFullPath(@"C:\candidate root\llmfit.exe"),
        command.ExecutablePath);
    Assert.AreEqual(
        Path.GetFullPath(@"C:\candidate root"),
        command.WorkingDirectory);
}

[TestMethod]
public void BuildVersion_UsesOnlyVersionArgument()
{
    LlmFitCommand command = LlmFitCommandBuilder.BuildVersion(
        @"C:\candidate",
        ApprovedManifest());

    CollectionAssert.AreEqual(new[] { "--version" }, command.Arguments);
}

[TestMethod]
public void BuildSystem_ExecutableOutsideRoot_Throws()
{
    LlmFitCandidateManifest manifest =
        ApprovedManifest() with
        {
            Executable = ApprovedManifest().Executable with
            {
                RelativePath = @"..\llmfit.exe"
            }
        };

    Assert.ThrowsExactly<InvalidDataException>(
        () => LlmFitCommandBuilder.BuildSystem(
            @"C:\candidate",
            manifest));
}
~~~

- [ ] **Step 2: Run RED**

Expected: compilation fails because the command types do not exist.

- [ ] **Step 3: Implement the command contract**

Use:

~~~csharp
public sealed record LlmFitCommand(
    string ExecutablePath,
    string WorkingDirectory,
    string[] Arguments)
{
    public ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
~~~

The builder canonicalizes the root and executable, verifies containment with Path.GetRelativePath, and copies the manifest arrays before returning them.

- [ ] **Step 4: Run GREEN**

Expected focused result: 3 tests passed. Expected cumulative deterministic floor: 7.

- [ ] **Step 5: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike/Command" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Command"
git commit -m "feat(hardware-inspection): fix LLM Fit spike commands"
~~~

### Task 3: Verify the candidate package before execution

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/Candidate/PeImageInspector.cs
- Create: tools/HardwareInspection.LlmFitSpike/Candidate/LlmFitCandidateVerification.cs
- Create: tools/HardwareInspection.LlmFitSpike/Candidate/LlmFitCandidateVerifier.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Candidate/PeImageInspectorTests.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Candidate/LlmFitCandidateVerifierTests.cs
- Create: scripts/hardware-inspection/Acquire-HardwareInspectionLlmFitCandidate.ps1

- [ ] **Step 1: Write nine failing verification tests**

Cover:

1. valid archive, AMD64 PE, matching hashes, and required files pass;
2. archive length or SHA-256 mismatch fails before executable inspection;
3. executable SHA-256 mismatch fails;
4. I386 PE fails;
5. missing LICENSE fails;
6. canonical path escape fails;
7. a reparse-point package member is rejected before hashing;
8. an unexpected colocated DLL or subdirectory is rejected before execution;
9. unsigned PE records `HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH` without changing a valid hash/architecture result into a false hardware failure.

Build deterministic PE bytes in test support:

~~~csharp
private static byte[] MinimalPe(ushort machine)
{
    var bytes = new byte[512];
    bytes[0] = (byte)'M';
    bytes[1] = (byte)'Z';
    BitConverter.GetBytes(0x80).CopyTo(bytes, 0x3c);
    bytes[0x80] = (byte)'P';
    bytes[0x81] = (byte)'E';
    BitConverter.GetBytes(machine).CopyTo(bytes, 0x84);
    return bytes;
}
~~~

- [ ] **Step 2: Run RED**

Expected: compilation fails because verifier types do not exist.

- [ ] **Step 3: Implement PE and package verification**

PeImageInspector must verify:

- MZ header;
- bounded positive PE offset;
- PE\0\0 signature;
- machine 0x8664;
- no read beyond file length.

LlmFitCandidateVerifier must:

- canonicalize the supplied package root;
- reject a package root or member with `FileAttributes.ReparsePoint`, walking every existing segment below the root before opening a file;
- resolve every required member beneath that root;
- reject missing files, directories, and containment escapes;
- reject every unexpected file or directory; the exact allowlist is the manifest archive, `requiredFiles`, and the locally generated `authenticode-observation.json`;
- require the archive named by the manifest, compare its exact byte length, and compare its SHA-256 bytes in fixed time;
- compare the executable SHA-256 bytes in fixed time;
- run PeImageInspector;
- attempt X509Certificate.CreateFromSignedFile only to record signature presence/subject;
- strictly parse `authenticode-observation.json`, reject unknown or path-like fields, and require its executable hash and presence/status claim to agree with the freshly observed executable;
- never accept an unsigned candidate without emitting HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH;
- never execute the file.

Keep reparse testing privilege-independent: place an internal `IFileMetadata` abstraction beside the verifier, provide a physical implementation using `File.GetAttributes`, and expose its constructor only to the test assembly through `InternalsVisibleTo`. The reparse test supplies attributes for a package segment and proves rejection before any stream is opened; it must not require administrator rights or Developer Mode.

Use this result:

~~~csharp
public sealed record LlmFitCandidateVerification(
    bool IntegrityPassed,
    string? ArchiveSha256,
    string? ExecutableSha256,
    string? PeMachine,
    bool AuthenticodePresent,
    string AuthenticodeStatus,
    string? AuthenticodeSubject,
    IReadOnlyList<string> DiagnosticCodes)
{
    public bool MayExecuteForGate1 =>
        IntegrityPassed &&
        string.Equals(PeMachine, "AMD64", StringComparison.Ordinal);
}
~~~

The C# status is limited to `NotSigned` or `PresentUnverified`; certificate presence alone is never described as trusted. The acquisition script separately writes a privacy-safe `authenticode-observation.json` under the ignored candidate root with the executable hash, `Get-AuthenticodeSignature.Status`, signer subject/thumbprint when present, and check timestamp, but no absolute path. Gate evidence must reproduce the executable hash before using that observation.

- [ ] **Step 4: Implement the pinned acquisition script**

The script takes one required `RepositoryRoot` parameter. It canonicalizes that directory, requires its `global.json` and committed candidate manifest, rejects a reparse-point root, and derives the destination itself as shown below; callers cannot redirect the package elsewhere. It reads the committed manifest, downloads to a new operating-system temporary directory, and verifies archive length/hash before extraction. Using `System.IO.Compression.ZipArchive`, it first rejects rooted names, parent traversal, link-like entries, unexpected directories, and members outside the single expected release folder; it requires exactly `llmfit.exe`, `LICENSE`, and `README.md`. It then expands to a second temporary package directory, verifies the extracted executable hash, required files, and AMD64 machine, records `Get-AuthenticodeSignature` output, and places both the verified archive and flattened package members under:

~~~text
third-party/bin/llmfit/v1.1.9/win-x64
~~~

The script must stop if that final directory already exists; it must never overwrite a candidate silently. It never writes a binary into a tracked repository path. It always removes only the two explicit operating-system temporary directories it created, while the verified candidate remains under the ignored `third-party/bin` tree.

- [ ] **Step 5: Run GREEN and script static checks**

Expected focused result: 9 tests passed. Expected cumulative floor: 16.

Run:

~~~powershell
$parseErrors = $null
$tokens = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path "scripts\hardware-inspection\Acquire-HardwareInspectionLlmFitCandidate.ps1"),
    [ref]$tokens,
    [ref]$parseErrors
) | Out-Null
if ($parseErrors.Count -ne 0) {
    throw ($parseErrors | Out-String)
}
~~~

- [ ] **Step 6: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike/Candidate" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Candidate" `
    "scripts/hardware-inspection/Acquire-HardwareInspectionLlmFitCandidate.ps1"
git commit -m "feat(hardware-inspection): verify LLM Fit candidate package"
~~~

### Task 4: Assess the real v1.1.9 system JSON without producing hardware conclusions

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/Inspection/LlmFitSystemAssessment.cs
- Create: tools/HardwareInspection.LlmFitSpike/Inspection/LlmFitSystemJsonAssessor.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Inspection/LlmFitSystemJsonAssessorTests.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Fixtures/valid-windows-intel.json
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Fixtures/valid-cpu-only.json

- [ ] **Step 1: Write eight failing assessment tests**

Cover valid Intel-GPU output, valid CPU-only output, malformed JSON, missing system, missing/blank CPU, non-positive cores/RAM, inconsistent GPU flags/counts, and additive unknown fields.

The valid Intel fixture must use the tagged serializer's exact field names:

~~~json
{
  "system": {
    "total_ram_gb": 31.72,
    "available_ram_gb": 18.40,
    "cpu_cores": 16,
    "cpu_name": "Fixture Intel CPU",
    "has_gpu": true,
    "gpu_vram_gb": 1.00,
    "gpu_available_gb": null,
    "gpu_name": "Fixture Intel Arc Graphics",
    "gpu_count": 1,
    "unified_memory": false,
    "backend": "SYCL",
    "gpus": [
      {
        "name": "Fixture Intel Arc Graphics",
        "vram_gb": 1.00,
        "backend": "SYCL",
        "count": 1,
        "unified_memory": false,
        "memory_bandwidth_gbps": null
      }
    ]
  }
}
~~~

- [ ] **Step 2: Run RED**

Expected: compilation fails because assessor types do not exist.

- [ ] **Step 3: Implement structural assessment**

Return:

~~~csharp
public sealed record LlmFitSystemAssessment(
    bool JsonValid,
    bool RequiredCpuRamPresent,
    bool CpuNamePresent,
    string? CpuName,
    int? CpuLogicalProcessorCount,
    double? TotalRamGiB,
    double? AvailableRamGiB,
    bool GpuReported,
    int ReportedGpuCount,
    bool IntelGpuReported,
    bool DedicatedSharedMemorySemanticsEstablished,
    string IntelNpuDetectionState,
    string RawJsonSha256,
    IReadOnlyList<string> DiagnosticCodes)
{
    public bool Gate1SchemaPassed =>
        JsonValid && RequiredCpuRamPresent;
}
~~~

Rules:

- parse with JsonDocument and permit additive fields;
- require finite positive total_ram_gb and cpu_cores;
- require 0 <= available_ram_gb <= total_ram_gb;
- require a non-blank cpu_name;
- require `has_gpu` to equal `gpu_count > 0`, require `gpus` to be empty when false and non-empty when true, require every `gpus[].count` to be positive, and require their sum to equal `gpu_count`;
- classify Intel by case-insensitive Intel token in any GPU name;
- never infer dedicated/shared memory from vram_gb or unified_memory on Windows Intel;
- set `IntelNpuDetectionState` to the sole Gate 1 value `DetectionUnavailable` because the system schema has no Intel NPU field; never map that state to absent hardware;
- emit HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP for Intel GPUs;
- emit HI-LLMFIT-WINDOWS-INTEL-NPU-GAP;
- emit HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT because the tagged serializer differs from its command help.

- [ ] **Step 4: Run GREEN**

Expected focused result: 8 tests passed. Expected cumulative floor: 24.

- [ ] **Step 5: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike/Inspection" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Inspection" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Fixtures"
git commit -m "feat(hardware-inspection): assess LLM Fit system JSON"
~~~

### Task 5: Run the child process with hard bounds and whole-tree termination

**Files:**

- Create: tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.csproj
- Create: tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/Program.cs
- Create: tools/HardwareInspection.LlmFitSpike/Execution/BoundedTextReader.cs
- Create: tools/HardwareInspection.LlmFitSpike/Execution/LlmFitProcessResult.cs
- Create: tools/HardwareInspection.LlmFitSpike/Execution/LlmFitProcessRunner.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Execution/LlmFitProcessRunnerTests.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Support/FakeLlmFitTool.cs

- [ ] **Step 1: Create the fake executable and eight failing process tests**

The fake tool accepts the real command shapes only. Its behavior is selected by a test-only `fake-mode.txt` file in its working directory; the production runner receives no environment-variable or argument override:

| Mode | Behavior |
|---|---|
| `success` | Writes a valid system envelope to stdout and exits 0. |
| `nonzero` | Writes a fixed diagnostic to stderr and exits 23. |
| `large-output` | Writes 2 MiB to each redirected stream and exits 0. |
| `sleep` | Waits for five minutes without output. |
| `spawn-child` | Starts the same executable in `sleep-child` mode, writes the child PID, then waits. |
| `sleep-child` | Waits for five minutes. |
| `dashboard` | Listens on loopback TCP port 8787 until killed. |

The fake tool returns 64 unless its arguments are exactly `--version` or `--no-dashboard --json system`. `--version` always emits `llmfit 1.1.9`; the mode applies to the system command. It never makes an outbound connection.

Create these exact deterministic tests:

~~~text
ExecuteAsync_Success_CapturesExitCodeAndBothStreams
ExecuteAsync_NonZeroExit_PreservesExitCode
ExecuteAsync_LargeOutput_CapsEachStreamAtOneMiB
ExecuteAsync_Timeout_KillsRootProcess
ExecuteAsync_Timeout_KillsDescendantProcess
ExecuteAsync_CallerCancellation_IsDistinctFromTimeout
ExecuteAsync_StartFailure_ReturnsSanitizedFailure
ExecuteAsync_ObserverFailure_KillsEntireProcessTree
~~~

Timeout tests use 500 milliseconds rather than the 15-second production default. The descendant test parses the fake child PID from the bounded stdout, waits for timeout, then verifies that `Process.GetProcessById(childPid)` throws or has exited. The cancellation test cancels a token only after the fake tool has signalled startup and verifies `Cancelled=true`, `TimedOut=false`, and whole-tree termination.

Add the fixture project to the deterministic test project as a `ProjectReference` with `ReferenceOutputAssembly=false`. `FakeLlmFitTool` follows the repository's existing `PublishedFixture` pattern: it uses `GRANITE_LLMFIT_FAKE_TOOL_ROOT` when CI supplies a controlled publish, otherwise it runs `dotnet publish` once into a GUID-named operating-system temporary directory with `UseShellExecute=false`, redirected streams, a two-minute timeout, `--no-restore`, `--runtime win-x64`, `--self-contained false`, and `-p:UseAppHost=true`. Each test copies that immutable publish into its own temporary directory and writes only its mode to `fake-mode.txt`; test cleanup removes only that owned directory.

Mark the process and socket-observer test classes `[DoNotParallelize]`. The dashboard test verifies port 8787 is free before starting its fake listener and fails with a clear fixture-precondition message if it is already occupied; it never kills or reconfigures an unrelated listener.

- [ ] **Step 2: Run RED**

Run:

~~~powershell
dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "FullyQualifiedName~LlmFitProcessRunnerTests" `
    --no-ansi
~~~

Expected: compilation fails because the execution types do not exist.

- [ ] **Step 3: Implement bounded capture and the immutable result**

Use these public contracts:

~~~csharp
public sealed record LlmFitProcessResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int? ProcessId,
    int? ExitCode,
    bool ProcessStartFailed,
    bool ObserverFailed,
    bool TimedOut,
    bool Cancelled,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated)
{
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    public bool Succeeded =>
        !ProcessStartFailed &&
        !ObserverFailed &&
        !TimedOut &&
        !Cancelled &&
        ExitCode == 0;
}

public sealed class LlmFitProcessRunner
{
    public const int MaximumCapturedBytesPerStream = 1_048_576;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public Task<LlmFitProcessResult> ExecuteAsync(
        LlmFitCommand command,
        TimeSpan? timeout = null,
        Func<int, CancellationToken, Task>? whileRunningObserver = null,
        CancellationToken cancellationToken = default);
}
~~~

`BoundedTextReader` reads bytes asynchronously from each base stream, retains at most 1 MiB, drains excess bytes to prevent pipe deadlock, decodes UTF-8 with replacement fallback, and reports truncation independently for stdout and stderr. Do not use `ReadToEndAsync` without a bound.

`LlmFitProcessRunner` must:

- reject a timeout outside 1 to 120 seconds;
- call only `command.CreateStartInfo()`;
- start no shell and add no arguments;
- begin both stream pumps immediately after `Process.Start`;
- start the optional while-running observer immediately after obtaining the child PID, and cancel and await it on every exit path;
- distinguish the caller token from the internal timeout token;
- call `Kill(entireProcessTree: true)` on timeout, cancellation, or capture failure;
- await process exit and both stream pumps after a kill;
- tolerate `InvalidOperationException` only when the process has already exited;
- map known start failures and observer failures to stable booleans and diagnostic codes without returning operating-system exception text;
- never include executable or working-directory paths in exception messages returned as evidence.

- [ ] **Step 4: Run GREEN**

Expected focused result: 8 tests passed. Expected cumulative deterministic floor: 32.

- [ ] **Step 5: Commit**

~~~powershell
git add -- `
    "tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool" `
    "tools/HardwareInspection.LlmFitSpike/Execution" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Execution" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Support"
git commit -m "feat(hardware-inspection): bound LLM Fit execution"
~~~

### Task 6: Observe dashboard/process leakage and write privacy-safe evidence

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/Execution/TcpListenerObserver.cs
- Create: tools/HardwareInspection.LlmFitSpike/Evidence/LlmFitGate1Evidence.cs
- Create: tools/HardwareInspection.LlmFitSpike/Evidence/LlmFitGate1EvidenceWriter.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Execution/TcpListenerObserverTests.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Evidence/LlmFitGate1EvidenceWriterTests.cs

- [ ] **Step 1: Write five failing observation and evidence tests**

Create these exact deterministic tests:

~~~text
ObserveAsync_SuccessMode_RecordsNoDashboardPort
ObserveAsync_DashboardMode_DetectsPort8787
ObserveAsync_AfterTimeout_RecordsNoNewCandidateProcess
WriteAsync_SanitizedEvidence_UsesAtomicReplacement
WriteAsync_SensitiveOrNonAllowlistedStrings_AreRejected
~~~

The privacy test reflects over the evidence model and rejects properties named for user, host, path, serial, stdout, stderr, or raw JSON content. It then attempts path separators in the raw-capture file name and free-form text in diagnostic codes and requires the writer to throw before creating a file. A valid write must contain the candidate ID, actual hashes, expected/reported version, both fixed argument lists, elapsed milliseconds, schema booleans, numeric CPU/RAM summaries, stable diagnostic codes, raw-capture base file name, and raw-capture SHA-256.

- [ ] **Step 2: Run RED**

Expected: compilation fails because the observer and evidence types do not exist.

- [ ] **Step 3: Implement bounded listener and process observation**

Use port 8787 as the dashboard sentinel because it is LLM Fit's tagged default. Follow the repository's proven process-owned socket-observation pattern: resolve `netstat.exe` only as `Path.Combine(Environment.SystemDirectory, "netstat.exe")`, verify that full path remains directly beneath `Environment.SystemDirectory`, and invoke it with `-a -n -o -p tcp`, no shell, and the system directory as its working directory every 50 milliseconds. Never search `PATH` or the candidate directory for this helper. Bound and drain its output, parse only `LISTENING` and `ESTABLISHED` rows, and retain only rows owned by the root PID or by a newly created process with the exact candidate image name. Snapshot same-name PIDs before launch so unrelated pre-existing LLM Fit processes cannot be attributed to this run. After process-tree termination, wait up to two seconds and record any newly created same-name process still alive.

Use:

~~~csharp
public sealed record LlmFitProcessObservation(
    bool CandidateSocketObserved,
    bool DashboardPortObserved,
    IReadOnlyList<int> CandidateListeningPorts,
    bool CandidateProcessRemainedAfterExit);

public sealed class TcpListenerObserver
{
    public const int LlmFitDashboardPort = 8787;

    public TcpListenerObserver(
        string candidateImageName,
        TimeSpan pollInterval);

    public Task ObserveWhileRunningAsync(
        int rootProcessId,
        CancellationToken cancellationToken);

    public Task<LlmFitProcessObservation> CompleteAsync(
        CancellationToken cancellationToken = default);
}
~~~

Task 5's process runner accepts an optional `Func<int, CancellationToken, Task>` while-running observer. It starts that observer immediately after obtaining the child PID, cancels and awaits it on every normal, timeout, cancellation, and exception path, then calls `CompleteAsync`. Observer failure fails the run and triggers whole-tree termination.

The observer is diagnostic evidence, not a firewall. It must not claim that absence from a sampled TCP table proves absence of all network syscalls; the separate offline target test supplies that operational control. A port opened by an unrelated process is not attributed to the candidate.

- [ ] **Step 4: Implement the evidence allowlist and atomic writer**

The committed evidence model is an allowlist:

~~~csharp
public sealed record LlmFitGate1Evidence(
    string SchemaVersion,
    string Disposition,
    string CandidateId,
    string ExpectedVersion,
    string? ReportedVersion,
    string ReleaseCommit,
    string ExpectedArchiveSha256,
    string? ObservedArchiveSha256,
    string ExpectedExecutableSha256,
    string? ObservedExecutableSha256,
    string ExpectedPeMachine,
    string? ObservedPeMachine,
    bool AuthenticodePresent,
    string AuthenticodeStatus,
    string[] VersionInvocationArguments,
    string[] SystemInvocationArguments,
    DateTimeOffset GateStartedAtUtc,
    DateTimeOffset GateCompletedAtUtc,
    long DurationMilliseconds,
    int? VersionExitCode,
    int? SystemExitCode,
    bool ProcessStartFailed,
    bool SocketObservationFailed,
    bool TimedOut,
    bool Cancelled,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    bool JsonValid,
    bool RequiredCpuRamPresent,
    int? CpuLogicalProcessorCount,
    double? TotalRamGiB,
    double? AvailableRamGiB,
    bool GpuReported,
    int ReportedGpuCount,
    bool IntelGpuReported,
    bool DedicatedSharedMemorySemanticsEstablished,
    string IntelNpuDetectionState,
    bool VersionCandidateSocketObserved,
    bool VersionDashboardPortObserved,
    bool SystemCandidateSocketObserved,
    bool SystemDashboardPortObserved,
    bool VersionCandidateProcessRemainedAfterExit,
    bool SystemCandidateProcessRemainedAfterExit,
    string? RawSystemJsonFileName,
    string? RawSystemJsonSha256,
    string[] DiagnosticCodes);
~~~

The model must not gain properties for executable paths, working directories, host/user names, device serials, raw stdout, raw stderr, raw JSON, or GPU device IDs. Define `LlmFitGate1DiagnosticCodes.All` as a finite ordinal set containing only the codes listed below; producers use those constants, and the writer rejects every other string even when it resembles a code:

~~~text
HI-LLMFIT-MANIFEST-INVALID
HI-LLMFIT-PACKAGE-MISSING
HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER
HI-LLMFIT-PACKAGE-CHANGED-DURING-RUN
HI-LLMFIT-PATH-ESCAPE
HI-LLMFIT-REPARSE-POINT
HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH
HI-LLMFIT-ARCHIVE-HASH-MISMATCH
HI-LLMFIT-EXECUTABLE-HASH-MISMATCH
HI-LLMFIT-PE-INVALID
HI-LLMFIT-PE-ARCHITECTURE-MISMATCH
HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH
HI-LLMFIT-SIGNATURE-STATUS-CHANGED
HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING
HI-LLMFIT-PROCESS-START-FAILED
HI-LLMFIT-VERSION-MISMATCH
HI-LLMFIT-PROCESS-TIMED-OUT
HI-LLMFIT-PROCESS-CANCELLED
HI-LLMFIT-PROCESS-EXIT-NONZERO
HI-LLMFIT-STDOUT-TRUNCATED
HI-LLMFIT-STDERR-TRUNCATED
HI-LLMFIT-SOCKET-OBSERVATION-FAILED
HI-LLMFIT-CANDIDATE-SOCKET-OBSERVED
HI-LLMFIT-DASHBOARD-PORT-OBSERVED
HI-LLMFIT-RESIDUAL-PROCESS
HI-LLMFIT-JSON-INVALID
HI-LLMFIT-CPU-RAM-MISSING
HI-LLMFIT-GPU-INCONSISTENT
HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP
HI-LLMFIT-WINDOWS-INTEL-NPU-GAP
HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT
HI-GATE1-WRONG-TARGET
HI-GATE1-WINDOWS-COMPARISON-FAILED
HI-GATE1-OFFLINE-PRECONDITION-FAILED
HI-GATE1-PRIVACY-VALIDATION-FAILED
HI-GATE1-REQUIRED-TEST-FAILURE
~~~

`LlmFitGate1EvidenceWriter` also validates that `Disposition` is `Blocked`, `Rejected`, `FunctionalPassWithPackagingConcern`, or `AcceptedForFunctionalEvaluation`; hashes are lowercase hexadecimal; invocation arrays equal the two manifest commands; `AuthenticodeStatus` is `NotSigned` or `PresentUnverified`; `IntelNpuDetectionState` is `DetectionUnavailable`; and a non-null raw-capture name equals its own base file name. It serializes with stable camel-case indentation to a same-directory temporary file, flushes it, then uses `File.Move(temp, destination, overwrite: true)`. It deletes only its own temporary file after failure.

Raw system JSON is permitted only under the ignored operator output directory. Evidence stores its base file name and SHA-256, never its content or absolute path. Those two fields are null when failure occurs before valid system JSON is captured.

- [ ] **Step 5: Run GREEN**

Expected focused result: 5 tests passed. Expected cumulative deterministic floor: 37.

- [ ] **Step 6: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike/Execution/TcpListenerObserver.cs" `
    "tools/HardwareInspection.LlmFitSpike/Evidence" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Execution/TcpListenerObserverTests.cs" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Evidence"
git commit -m "feat(hardware-inspection): capture safe LLM Fit evidence"
~~~

### Task 7: Orchestrate the exact Gate 1 run behind a narrow CLI

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike/SpikeOptions.cs
- Create: tools/HardwareInspection.LlmFitSpike/Evidence/LlmFitGate1Runner.cs
- Create: tools/HardwareInspection.LlmFitSpike/Program.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/Evidence/LlmFitGate1RunnerTests.cs
- Create: tools/HardwareInspection.LlmFitSpike.Tests/SpikeOptionsTests.cs

- [ ] **Step 1: Write six failing orchestration tests**

Create these exact deterministic tests:

~~~text
Parse_ApprovedArguments_ReturnsCanonicalOptions
Parse_ArbitraryExecutableOrArguments_IsRejected
RunAsync_ValidUnsignedCandidate_ReturnsFunctionalPassWithPackagingConcern
RunAsync_VersionMismatch_DoesNotRunSystemCommand
RunAsync_PackageChangesBetweenCommands_RejectsBeforeSystemCommand
RunAsync_SchemaOrLeakFailure_ReturnsRejectedAndSanitizedEvidence
~~~

Use constructor-injected `ICandidateManifestSource`, `ILlmFitCandidateVerifier`, `ILlmFitProcessRunner`, `ILlmFitSocketObserver`, `IClock`, `IGateOutputStore`, and `ILlmFitGate1EvidenceWriter` only at the orchestration boundary. `Program` binds these to concrete implementations and an assembly-output manifest source; the CLI exposes no injection or manifest-path option. Fakes capture call order. The version-mismatch test asserts calls end after `--version`. The valid-unsigned test uses a temporary package built from the published fake tool, a test manifest containing that package's real hashes, and the real verifier, process runner, observer, assessor, output store, and evidence writer. It asserts this exact order:

~~~text
load manifest
verify package before version
observe and run version
compare exact version
verify package before system
observe and run system
assess JSON
verify package after system
write ignored raw capture
write sanitized evidence
~~~

- [ ] **Step 2: Run RED**

Expected: compilation fails because `SpikeOptions` and `LlmFitGate1Runner` do not exist.

- [ ] **Step 3: Implement the CLI contract**

The only accepted syntax is:

~~~text
HardwareInspection.LlmFitSpike.exe
  --candidate-root <directory>
  --output <directory>
  [--timeout-seconds <1..120>]
~~~

Reject unknown, duplicate, or missing options; reject `--executable`, `--command`, and passthrough arguments. Canonicalize both directories and reject either directory being equal to or contained by the other. Candidate root must already exist and must not be a reparse point. Create the output directory only after options validate, and reject an existing reparse-point output. Default timeout is 15 seconds.

Use stable exit codes:

| Exit code | Meaning |
|---:|---|
| 0 | Functional pass, including a pass with an explicit packaging concern. |
| 1 | Integrity, version, execution, JSON, listener, or residual-process failure. |
| 2 | Invalid command line. |
| 3 | Caller cancellation. |

- [ ] **Step 4: Implement fail-closed orchestration**

Use:

~~~csharp
public enum LlmFitGate1Disposition
{
    Blocked,
    Rejected,
    FunctionalPassWithPackagingConcern,
    AcceptedForFunctionalEvaluation
}

public sealed record LlmFitGate1RunResult(
    LlmFitGate1Disposition Disposition,
    string EvidencePath,
    IReadOnlyList<string> DiagnosticCodes);
~~~

`LlmFitGate1Runner.RunAsync` must:

1. load the one committed manifest from the assembly output;
2. verify files, hashes, PE architecture, and observed Authenticode state without execution;
3. stop if `MayExecuteForGate1` is false;
4. run only `--version` under the same candidate-owned socket/process observer, trim one trailing line ending, and require the exact case-sensitive output `llmfit 1.1.9`;
5. stop before system collection on a version mismatch, timeout, truncation, nonzero exit, cancellation, candidate-owned socket, observer failure, or residual process;
6. repeat the complete package verification and require every observed value to equal the pre-version result;
7. run only `--no-dashboard --json system` under `TcpListenerObserver`;
8. reject timeout, cancellation, nonzero exit, truncated stdout, any candidate-owned TCP listener or established connection, port 8787 owned by the candidate tree, or a remaining candidate process;
9. assess stdout as the tagged JSON schema;
10. repeat package verification after system exit and reject any identity or package-member change;
11. write stdout verbatim only to `llmfit-system.raw.json` under the ignored output directory and compute its SHA-256;
12. write `llmfit-gate1.evidence.json` from the evidence allowlist;
13. return `FunctionalPassWithPackagingConcern` with `HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING` whenever all functional checks pass; also include `HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH` when Authenticode is absent, or `HI-LLMFIT-SIGNATURE-STATUS-CHANGED` when the fresh result differs from the preliminary finding. The spike never upgrades itself to redistribution approval.

Never fall back to another version, a PATH-resolved binary, WMI, a dashboard, REST, `serve`, `recommend`, `fit`, `plan`, or network retrieval.

`Program` handles Ctrl+C through a linked cancellation token and prints only the stable disposition, diagnostic codes, and evidence base file name. It must not print exception messages, absolute paths, stdout, stderr, or raw JSON.

- [ ] **Step 5: Run GREEN**

Expected focused result: 6 tests passed. Expected cumulative deterministic floor: 43.

- [ ] **Step 6: Re-run the real-boundary fake-tool path**

Run `RunAsync_ValidUnsignedCandidate_ReturnsFunctionalPassWithPackagingConcern` by itself. Verify it exercises the compiled fake apphost through the real package verifier and process boundary, returns exit-equivalent disposition 0, writes two output files, records no candidate-owned socket or remaining fake process, and places no absolute path or raw JSON in the sanitized evidence. The production CLI is not given a test-manifest switch.

- [ ] **Step 7: Commit**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike/SpikeOptions.cs" `
    "tools/HardwareInspection.LlmFitSpike/Program.cs" `
    "tools/HardwareInspection.LlmFitSpike/Evidence/LlmFitGate1Runner.cs" `
    "tools/HardwareInspection.LlmFitSpike.Tests/SpikeOptionsTests.cs" `
    "tools/HardwareInspection.LlmFitSpike.Tests/Evidence/LlmFitGate1RunnerTests.cs"
git commit -m "feat(hardware-inspection): orchestrate LLM Fit gate 1"
~~~

### Task 8: Prove behavior on a trusted Windows Intel target and while offline

**Files:**

- Create: tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj
- Create: tools/HardwareInspection.LlmFitSpike.IntegrationTests/README.md
- Create: tools/HardwareInspection.LlmFitSpike.IntegrationTests/LlmFitCandidateIntegrationTests.cs
- Create: scripts/hardware-inspection/Capture-HardwareInspectionWindowsReference.ps1
- Create: docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md

- [ ] **Step 1: Create the target test project and four explicitly gated tests**

Use the same Windows TFM, x64/runtime settings, analyzer settings, MTP properties, and four pinned test packages as the deterministic project. Add a `ProjectReference` to `HardwareInspection.LlmFitSpike.csproj` and explicitly link the committed candidate manifest into `Candidates/` in the integration-test output. Tests skip with a precise prerequisite message unless their category's environment variables are present; a configured test must fail, never skip, when evidence is wrong.

Create:

~~~text
TrustedCandidate_IdentityVersionAndCpuRamSchemaPass
TrustedCandidate_CpuAndRamAgreeWithNearSimultaneousWindowsReference
TrustedCandidate_LeavesNoDashboardListenerOrProcess
OfflineCandidate_ProducesCpuRamWithoutAnyListenerOrResidualProcess
~~~

The first three use `[TestCategory("TrustedWindowsIntel")]`; the fourth uses `[TestCategory("TrustedOffline")]`. Trusted Windows tests read all three variables below; the offline test reads candidate root and a separate offline output directory:

~~~text
GRANITE_LLMFIT_CANDIDATE_ROOT
GRANITE_LLMFIT_WINDOWS_REFERENCE
GRANITE_LLMFIT_GATE1_OUTPUT
~~~

The candidate-root environment value is operator input only and must never appear in committed or uploaded evidence.

- [ ] **Step 2: Implement the Windows reference capture script**

The script accepts `CandidateRoot` and `OutputRoot`. It resolves the repository root from its own `scripts/hardware-inspection` location and invokes only the fixed project `tools/HardwareInspection.LlmFitSpike/HardwareInspection.LlmFitSpike.csproj`; it accepts no CLI/project override. `OutputRoot` must not already exist, and the script creates it only after validating both inputs. It:

1. creates a new explicit output directory beneath `artifacts/hardware-inspection/llmfit`;
2. records `capturedBeforeUtc`;
3. reads only `Win32_Processor.Name`, `Win32_Processor.NumberOfLogicalProcessors`, `Win32_ComputerSystem.TotalPhysicalMemory`, `Win32_OperatingSystem.FreePhysicalMemory`, and `Win32_VideoController.Name`;
4. invokes the already-built Gate 1 CLI through `dotnet run --project tools/HardwareInspection.LlmFitSpike/HardwareInspection.LlmFitSpike.csproj --configuration Release --runtime win-x64 --no-restore --no-build --` with the candidate root and output directory;
5. immediately repeats the RAM readings and records `capturedAfterUtc`;
6. reads the ignored valid LLM Fit JSON and derives CPU-identity match, logical-processor equality, total-RAM delta, available-RAM delta, and Intel-GPU identity match without copying raw LLM Fit output into the reference summary;
7. writes `windows-reference.json` beside the ignored raw capture;
8. records the SHA-256 of `authenticode-observation.json` when present, without copying its path or free-form content;
9. returns the Gate 1 CLI exit code.

Do not read or serialize computer name, user name, serial number, UUID, BIOS identifier, PNP device ID, MAC address, IP address, or absolute candidate path.

- [ ] **Step 3: Implement the trusted comparison rules**

The tagged `hardware.rs` implementation populates `cpu_cores` from Rust's logical CPU count, so Gate 1 treats the field as logical processors and validates that interpretation rather than relying on the ambiguous JSON name. A mismatch rejects the candidate's required CPU topology evidence.

The target tests require:

- Windows x64 plus at least one Windows processor name containing the normalized `Intel` vendor token and at least one `Win32_VideoController.Name` containing `Intel`; otherwise fail with `HI-GATE1-WRONG-TARGET` rather than accepting evidence from the authoring machine;
- manifest archive/executable hashes, AMD64 PE, and exact reported version to match;
- normalized LLM Fit and Windows CPU identities to agree after removing trademark markers, punctuation, repeated whitespace, and case while retaining vendor and model-number tokens;
- LLM Fit `cpu_cores` to equal the sum of Windows logical processors;
- total RAM to be within 1 GiB of `TotalPhysicalMemory / 1073741824d`;
- available RAM to be within `max(2 GiB, 10 percent of total RAM)` of the midpoint of the before/after Windows free-memory readings;
- the reference capture and LLM Fit run interval to be no more than 30 seconds apart;
- valid JSON and required CPU/RAM fields;
- no dashboard port, no new listener, and no remaining candidate process.

For GPUs, record whether an Intel adapter name appears in both sources using case-insensitive normalized vendor/name tokens. A missing or conflicting match is a field-level gap, not a CPU/RAM failure. Never accept `gpu_vram_gb` as dedicated or shared Windows Intel memory. Always record Intel NPU detection as `DetectionUnavailable` for this candidate; never report an NPU as absent.

- [ ] **Step 4: Implement the offline operational gate**

Before launching the candidate, the offline test must require:

~~~csharp
Assert.IsFalse(
    NetworkInterface.GetIsNetworkAvailable(),
    "TrustedOffline must run only after non-loopback network access is disabled.");

bool operationalNonLoopbackExists =
    NetworkInterface.GetAllNetworkInterfaces().Any(
        adapter =>
            adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            adapter.OperationalStatus == OperationalStatus.Up);

Assert.IsFalse(
    operationalNonLoopbackExists,
    "TrustedOffline found an operational non-loopback interface.");
~~~

Evaluate both preconditions before launching the candidate. If either fails, atomically write a privacy-safe `Blocked` evidence file with `HI-GATE1-OFFLINE-PRECONDITION-FAILED`, then fail the test; do not start LLM Fit. Otherwise run the same integrity-pinned candidate and require valid CPU/RAM JSON, zero candidate-owned listeners or established TCP connections, no candidate-owned 8787 observation, and no residual candidate process. The runbook tells the operator to use a controlled offline target or disable all non-loopback interfaces before starting this test and restore them manually afterward. The test and script do not change network configuration.

- [ ] **Step 5: Run the trusted target checks**

Acquire the candidate using the pinned script, then:

~~~powershell
& "scripts\hardware-inspection\Acquire-HardwareInspectionLlmFitCandidate.ps1" `
    -RepositoryRoot (Resolve-Path ".").Path
if ($LASTEXITCODE -ne 0) {
    throw "Pinned LLM Fit acquisition failed with exit code $LASTEXITCODE."
}

$env:GRANITE_LLMFIT_CANDIDATE_ROOT =
    (Resolve-Path "third-party\bin\llmfit\v1.1.9\win-x64").Path
$env:GRANITE_LLMFIT_TRUSTED_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-windows-intel-" + (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT =
    $env:GRANITE_LLMFIT_TRUSTED_OUTPUT

dotnet restore `
    "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --runtime win-x64
dotnet build `
    "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --no-restore

& "scripts\hardware-inspection\Capture-HardwareInspectionWindowsReference.ps1" `
    -CandidateRoot $env:GRANITE_LLMFIT_CANDIDATE_ROOT `
    -OutputRoot $env:GRANITE_LLMFIT_GATE1_OUTPUT
if ($LASTEXITCODE -ne 0) {
    throw "Trusted Windows Intel capture failed with exit code $LASTEXITCODE."
}

$env:GRANITE_LLMFIT_WINDOWS_REFERENCE =
    Join-Path $env:GRANITE_LLMFIT_GATE1_OUTPUT "windows-reference.json"

dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedWindowsIntel" `
    --minimum-expected-tests 3 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-trusted.trx" `
    --no-ansi
~~~

Expected: 3 passed, 0 failed, 0 skipped on the named trusted Windows Intel target.

- [ ] **Step 6: Run the separately controlled offline check**

With non-loopback network access disabled by the operator, run:

~~~powershell
$env:GRANITE_LLMFIT_OFFLINE_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-offline-" + (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT =
    $env:GRANITE_LLMFIT_OFFLINE_OUTPUT

dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedOffline" `
    --minimum-expected-tests 1 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-offline.trx" `
    --no-ansi
~~~

Expected: 1 passed, 0 failed, 0 skipped. If the environment is online, the expected result is a deliberate failure and Gate 1 remains open.

- [ ] **Step 7: Commit code and runbook, but not ignored captures**

~~~powershell
git add -- `
    "tools/HardwareInspection.LlmFitSpike.IntegrationTests" `
    "scripts/hardware-inspection/Capture-HardwareInspectionWindowsReference.ps1" `
    "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md"
git commit -m "test(hardware-inspection): verify LLM Fit on Windows Intel"
~~~

### Task 9: Add deterministic CI, generate the evidence record, and decide Gate 1

**Files:**

- Create: .github/workflows/hardware-inspection-llmfit-spike.yml
- Create: scripts/hardware-inspection/Write-HardwareInspectionLlmFitGate1Report.ps1
- Create: docs/testing/evidence/2026-08-15-hardware-inspection-gate1-llmfit-verification.md
- Modify: tools/HardwareInspection.LlmFitSpike/README.md
- Modify: tools/HardwareInspection.LlmFitSpike.Tests/README.md
- Modify: tools/README.md
- Modify: scripts/README.md
- Modify: docs/testing/evidence/README.md

- [ ] **Step 1: Add the deterministic hosted workflow**

The workflow runs only the deterministic project; it never downloads or executes the candidate binary. Use this job structure:

~~~yaml
name: Hardware Inspection LLM Fit Spike

on:
  pull_request:
    paths:
      - ".github/workflows/hardware-inspection-llmfit-spike.yml"
      - "global.json"
      - "tools/HardwareInspection.LlmFitSpike/**"
      - "tools/HardwareInspection.LlmFitSpike.Tests/**"
      - "tools/HardwareInspection.LlmFitSpike.IntegrationTests/**"
      - "tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/**"
      - "scripts/hardware-inspection/**"
      - "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md"
  push:
    branches: [main]
    paths:
      - ".github/workflows/hardware-inspection-llmfit-spike.yml"
      - "global.json"
      - "tools/HardwareInspection.LlmFitSpike/**"
      - "tools/HardwareInspection.LlmFitSpike.Tests/**"
      - "tools/HardwareInspection.LlmFitSpike.IntegrationTests/**"
      - "tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/**"
      - "scripts/hardware-inspection/**"
  workflow_dispatch:

concurrency:
  group: hardware-inspection-llmfit-${{ github.ref }}
  cancel-in-progress: true

jobs:
  deterministic:
    runs-on: windows-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0
      - uses: actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d
        with:
          global-json-file: global.json
      - name: Restore
        shell: pwsh
        run: |
          dotnet restore tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj --runtime win-x64
          dotnet restore tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj --runtime win-x64
      - name: Build trusted-target test harness
        run: dotnet build tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj --configuration Release --runtime win-x64 --no-restore
      - name: Build deterministic test harness
        run: dotnet build tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj --configuration Release --runtime win-x64 --no-restore
      - name: Publish harmless process fixture
        shell: pwsh
        run: |
          $fixtureRoot = Join-Path $env:RUNNER_TEMP "hardware-inspection-llmfit-fake"
          dotnet publish tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained false `
            --no-restore `
            --output $fixtureRoot `
            -p:UseAppHost=true
          "GRANITE_LLMFIT_FAKE_TOOL_ROOT=$fixtureRoot" |
            Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
      - name: Test deterministic contracts
        run: >
          dotnet test
          --project tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj
          --configuration Release
          --no-restore
          --runtime win-x64
          --filter "TestCategory=Deterministic"
          --minimum-expected-tests 43
          --results-directory TestResults/HardwareInspectionLlmFit
          --report-trx
          --report-trx-filename hardware-inspection-llmfit-deterministic.trx
          --no-ansi
      - name: Upload test report
        if: always()
        uses: actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f
        with:
          name: hardware-inspection-llmfit-test-results
          path: TestResults/HardwareInspectionLlmFit/*.trx
          if-no-files-found: error
~~~

- [ ] **Step 2: Implement the evidence report generator**

`Write-HardwareInspectionLlmFitGate1Report.ps1` accepts:

~~~text
-EvidenceJson
-WindowsReferenceJson
-OfflineEvidenceJson
-DeterministicTrx
-TrustedWindowsTrx
-OfflineTrx
-OutputMarkdown
-RepositoryCommit
~~~

It validates all files, hashes the six input artifacts, and accepts all four defined evidence dispositions. Missing or malformed inputs stop generation. A wrong target or unavailable offline prerequisite produces `Blocked` without judging the candidate. Any other failed or skipped required test, or an evidence claim inconsistent with its inputs, forces `Rejected` with `HI-GATE1-REQUIRED-TEST-FAILURE`; it does not prevent creation of the rejection record. It emits a complete Markdown record with:

- branch and exact commit;
- candidate tag, release commit, archive/executable hashes, PE architecture, reported version, and license;
- deterministic count of at least 43;
- trusted Windows Intel count of exactly 3;
- offline count of exactly 1;
- fixed command arguments;
- CPU/RAM Windows comparison and tolerance result;
- JSON/schema result;
- dashboard/listener/residual-process result;
- privacy allowlist result;
- Intel GPU identity result and unaccepted memory semantics;
- Intel NPU `DetectionUnavailable` result;
- Authenticode claim mismatch;
- pending transitive dependency-license inventory;
- hashes of each source artifact;
- final Gate 1 disposition and the exact non-claims.

The generator must use only derived comparison booleans/counts/deltas from the Windows reference; it must not copy processor/GPU name strings, raw JSON, paths, stdout, stderr, host/user names, serials, IP/MAC data, or device IDs into Markdown. It writes atomically and fails if the output already contains unresolved template markers.

- [ ] **Step 3: Update the tool documentation**

Document:

- why v1.1.9 is pinned;
- the exact acquisition, deterministic, trusted, and offline commands;
- why `--no-dashboard --json system` is the only system command;
- that `serve`, dashboard, REST, model recommendation, and arbitrary arguments are prohibited;
- output/privacy boundaries;
- MIT license retention;
- schema-documentation drift;
- Windows Intel GPU memory and NPU gaps;
- the unsigned-binary discrepancy and its production-redistribution consequence;
- the pending transitive dependency-notice inventory and its production-redistribution consequence;
- Gate 2 ownership of reusable process and external-tool foundations.

Add the generated record to `docs/testing/evidence/README.md` under a new Hardware Inspection section and document the dedicated script folder in `scripts/README.md`. Do not mark generated requirement `F-M07` or non-existent `HE-01`/`HE-02` work-package indexes verified: Gate 1 proves only the candidate route and CPU/RAM behavior, not the full processor, memory, GPU, storage, runtime, and application-service acceptance requirement. Gate 9 will link the complete Block 2 evidence through the controlled traceability workflow.

- [ ] **Step 4: Validate Task 8 artifacts and run fresh deterministic regressions**

Keep the exact candidate, trusted-output, offline-output, and Windows-reference variables from Task 8, or restore them to those exact ignored artifact locations in a fresh shell. Task 8 already performed the target and offline executions; do not spend another candidate run or require a second network-disable cycle here.

Run:

~~~powershell
$DeterministicProject =
    "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj"
$ContractProject =
    "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj"

$requiredVariables = @(
    "GRANITE_LLMFIT_CANDIDATE_ROOT",
    "GRANITE_LLMFIT_WINDOWS_REFERENCE",
    "GRANITE_LLMFIT_TRUSTED_OUTPUT",
    "GRANITE_LLMFIT_OFFLINE_OUTPUT"
)
foreach ($name in $requiredVariables) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required Task 8 variable is missing: $name"
    }
}

$requiredArtifacts = @(
    $env:GRANITE_LLMFIT_WINDOWS_REFERENCE,
    (Join-Path $env:GRANITE_LLMFIT_TRUSTED_OUTPUT "llmfit-gate1.evidence.json"),
    (Join-Path $env:GRANITE_LLMFIT_OFFLINE_OUTPUT "llmfit-gate1.evidence.json"),
    "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-trusted.trx",
    "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-offline.trx"
)
foreach ($path in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Task 8 artifact is missing: $path"
    }
}

dotnet restore $DeterministicProject --runtime win-x64
dotnet restore $ContractProject
dotnet build `
    $DeterministicProject `
    --configuration Release `
    --runtime win-x64 `
    --no-restore

dotnet test `
    --project $DeterministicProject `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 43 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-deterministic.trx" `
    --no-ansi

dotnet test `
    --project $ContractProject `
    --configuration Release `
    --no-restore `
    --minimum-expected-tests 357 `
    --no-ansi

git diff --check
~~~

Expected:

- deterministic: at least 43 passed, 0 failed, 0 skipped;
- Task 8 trusted artifact: exactly 3 passed, 0 failed, 0 skipped;
- Task 8 offline artifact: exactly 1 passed, 0 failed, 0 skipped;
- existing Model Inspection contract suite: 357 passed, 0 failed;
- no whitespace errors.

- [ ] **Step 5: Generate and inspect the committed evidence record**

Run:

~~~powershell
& "scripts\hardware-inspection\Write-HardwareInspectionLlmFitGate1Report.ps1" `
    -EvidenceJson `
        (Join-Path $env:GRANITE_LLMFIT_TRUSTED_OUTPUT "llmfit-gate1.evidence.json") `
    -WindowsReferenceJson `
        (Join-Path $env:GRANITE_LLMFIT_TRUSTED_OUTPUT "windows-reference.json") `
    -OfflineEvidenceJson `
        (Join-Path $env:GRANITE_LLMFIT_OFFLINE_OUTPUT "llmfit-gate1.evidence.json") `
    -DeterministicTrx `
        "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-deterministic.trx" `
    -TrustedWindowsTrx `
        "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-trusted.trx" `
    -OfflineTrx `
        "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-offline.trx" `
    -OutputMarkdown `
        "docs\testing\evidence\2026-08-15-hardware-inspection-gate1-llmfit-verification.md" `
    -RepositoryCommit (git rev-parse HEAD)
~~~

Read the generated Markdown in full. Search it for absolute drive paths, the current user/computer names, raw JSON fields not in the allowlist, and unresolved template markers. Confirm that the six recorded artifact hashes match fresh `Get-FileHash -Algorithm SHA256` results.

- [ ] **Step 6: Apply the Gate 1 decision rule**

Choose exactly one evidence disposition:

| Disposition | Required result |
|---|---|
| `Blocked` | The named Windows Intel target or controlled offline prerequisite was unavailable, so the candidate was not judged. Record the missing prerequisite and stop; do not substitute another host or start Gate 2. |
| `Rejected` | Any candidate identity, hash, PE, version, required CPU/RAM schema or Windows comparison, bounded-execution, candidate-socket/process, privacy, or offline check failed. An expected recorded Intel GPU-memory or NPU detection gap alone does not reject otherwise valid CPU/RAM evidence. Stop; do not start Gate 2 and do not silently try another version. |
| `FunctionalPassWithPackagingConcern` | Every functional, target, privacy, and offline check passed, but v1.1.9 remains unsigned contrary to the upstream signing claim and its transitive dependency notices remain unaudited. Gate 2 may use the behavior contract for architectural work; production redistribution remains blocked pending explicit security/legal disposition and dependency-license inventory. |
| `AcceptedForFunctionalEvaluation` | Every required check passed and both the signature discrepancy and dependency-license inventory received explicit recorded dispositions. This still does not make LLM Fit authoritative for Windows Intel GPU memory or Intel NPU fields. |

The evidence record must state these non-claims:

- no production binary has been approved or committed;
- no WinUI or HardwareSnapshot implementation exists;
- no Windows Intel dedicated/shared GPU memory conclusion was established;
- no Intel NPU presence or absence was established;
- no model compatibility conclusion was made;
- no network-syscall proof is claimed beyond the controlled offline run and listener/process observations.

- [ ] **Step 7: Commit workflow, documentation, generator, and generated evidence**

~~~powershell
git add -- `
    ".github/workflows/hardware-inspection-llmfit-spike.yml" `
    "scripts/hardware-inspection/Write-HardwareInspectionLlmFitGate1Report.ps1" `
    "tools/HardwareInspection.LlmFitSpike/README.md" `
    "tools/HardwareInspection.LlmFitSpike.Tests/README.md" `
    "tools/README.md" `
    "scripts/README.md" `
    "docs/testing/evidence/README.md" `
    "docs/testing/evidence/2026-08-15-hardware-inspection-gate1-llmfit-verification.md"
git diff --cached --check
git commit -m "docs(hardware-inspection): record LLM Fit gate 1"
~~~

## 5. Gate 1 exit and next gate

Gate 1 evaluation concludes only when Task 9 records one of the four explicit dispositions from fresh evidence. `Blocked` or `Rejected` leaves Gate 1 unsatisfied and blocks Gate 2. A functional pass with the unresolved Authenticode discrepancy and pending dependency-license inventory satisfies the behavioral gate and is sufficient to continue architecture work, but it is not approval to bundle or redistribute the executable.

Gate 2 begins only after that disposition is committed. Gate 2 extracts the reusable `ExternalTools` manifest/hash/version boundary and the shared bounded `ProcessExecution` service from the behavior proven here. It must not copy the spike CLI wholesale into production, and it must preserve the fixed-command, no-shell, timeout, cancellation, whole-tree termination, bounded-stream, privacy, and no-dashboard requirements.

## 6. Final implementation handoff

Before calling the implementation complete:

- confirm the candidate ZIP and executable remain untracked;
- confirm the ignored raw JSON and Windows reference remain outside Git;
- confirm only the generated privacy-safe Markdown evidence is committed;
- confirm `IBM Granite with TurboQuant (Intel).slnx` and all Model Inspection paths are unchanged;
- show the exact deterministic, trusted, offline, and Model Inspection contract-suite results;
- show `git status --short`, `git diff --check`, and the Gate 1 disposition.
