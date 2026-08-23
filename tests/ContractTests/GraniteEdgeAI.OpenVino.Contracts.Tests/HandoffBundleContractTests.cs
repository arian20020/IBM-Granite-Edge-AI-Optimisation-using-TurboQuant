namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class HandoffBundleContractTests
{
    [TestMethod]
    public void HandoffAssetsExistAndKeepTransferredInputsOutOfAcceptance()
    {
        string[] assets =
        [
            "docs/handoffs/openvino-ucl/READ_FIRST.md",
            "docs/handoffs/openvino-ucl/CONTINUATION_PROMPT.md",
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1",
            "scripts/openvino/handoff/Initialize-UclHandoff.ps1"
        ];
        foreach (string asset in assets)
        {
            Assert.IsTrue(File.Exists(RepoPath(asset)), asset);
        }

        string prompt = File.ReadAllText(RepoPath(assets[1]));
        foreach (string required in new[]
        {
            "c1e0fe2f",
            "openvino_release_blocked",
            "openvino_release_accepted",
            "not trusted UCL evidence",
            "pinned Granite",
            "security/license",
            "normal WinUI",
            "GPU-01",
            "398 P1 atoms",
            "central registration"
        })
        {
            StringAssert.Contains(prompt, required);
        }
    }

    [TestMethod]
    public void BuilderRequiresExplicitRootsAndVerifiedSeparatePayloads()
    {
        string script = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        foreach (string required in new[]
        {
            "[Parameter(Mandatory)]",
            "$RepositoryRoot",
            "$OfficialStageDirectory",
            "$TurboQuantStageDirectory",
            "$ConverterStageDirectory",
            "$OutputDirectory",
            "Test-OpenVinoOfficialWorkerManifest.ps1",
            "Test-OpenVinoTurboQuantWorkerManifest.ps1",
            "Test-OpenVinoConverterWorkerManifest.ps1",
            "bundle create",
            "archive --format=zip",
            "feature/openvino-route",
            "transfer_input_only",
            "openvino_ucl_handoff_created"
        })
        {
            StringAssert.Contains(script, required);
        }
        Assert.IsFalse(script.Contains("GetTempPath", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("$HOME", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("TestResults", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InitializerVerifiesEverythingBeforeCreatingDestination()
    {
        string script = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/Initialize-UclHandoff.ps1"));
        foreach (string required in new[]
        {
            "$BundleRoot",
            "$DestinationRoot",
            "OpenVinoClosedJson.psm1",
            "Test-PayloadIntegrity",
            "git clone",
            "rev-parse HEAD",
            "status --porcelain",
            "archive-entry-traversal",
            "openvino_ucl_handoff_initialized",
            "openvino_ucl_handoff_invalid"
        })
        {
            StringAssert.Contains(script, required);
        }
        int verification = script.IndexOf("Test-PayloadIntegrity",
            StringComparison.Ordinal);
        int destinationCreation = script.LastIndexOf(
            "New-Item -ItemType Directory -Path $destination",
            StringComparison.Ordinal);
        int clone = script.IndexOf("git clone", StringComparison.Ordinal);
        Assert.IsGreaterThan(verification, destinationCreation);
        Assert.IsGreaterThan(verification, clone);
        Assert.IsFalse(script.Contains("GetTempPath", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("$HOME", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BundleToolsRejectTraversalReparseAliasingAndDirtyCandidates()
    {
        string builder = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        string initializer = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/Initialize-UclHandoff.ps1"));
        string combined = builder + initializer;
        foreach (string required in new[]
        {
            "ReparsePoint",
            "status --porcelain",
            "roots-aliased",
            "IsPathRooted",
            "(^|[\\/])\\.\\.([\\/]|$)",
            "duplicate-entry",
            "expanded-size-invalid"
        })
        {
            StringAssert.Contains(combined, required);
        }
    }

    [TestMethod]
    public void BuilderPinsDeterminismInventoryAndOwnedCleanupBoundaries()
    {
        string builder = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        foreach (string required in new[]
        {
            "db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3",
            "Sort-Object FullName",
            "LastWriteTime = [DateTimeOffset]::new(",
            "role = $Role",
            "evidenceDisposition = 'transfer_input_only'",
            "'.build-' + [Guid]::NewGuid()",
            "Remove-Item -LiteralPath $resolvedStaging -Recurse -Force"
        })
        {
            StringAssert.Contains(builder, required);
        }

        foreach (string relative in new[]
        {
            "docs/handoffs/openvino-ucl/READ_FIRST.md",
            "docs/handoffs/openvino-ucl/CONTINUATION_PROMPT.md"
        })
        {
            string document = File.ReadAllText(RepoPath(relative));
            Assert.IsFalse(document.Contains("C:\\openvino-o1-task",
                StringComparison.OrdinalIgnoreCase), relative);
        }
    }

    [TestMethod]
    public void BuilderPassesGitArchiveOutputAsASeparateNativeArgument()
    {
        string builder = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        StringAssert.Contains(builder, "--output $sourceSnapshotPath");
        Assert.IsFalse(builder.Contains("--output=$sourceSnapshotPath",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuilderAvoidsPowerShellSevenOnlyRelativePathApis()
    {
        string builder = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        Assert.IsFalse(builder.Contains("[IO.Path]::GetRelativePath",
            StringComparison.Ordinal));
        StringAssert.Contains(builder, "Get-SafeRelativePath");
    }

    [TestMethod]
    public void BuilderWritesStrictInventoryWithoutAUtf8Bom()
    {
        string builder = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1"));
        StringAssert.Contains(builder,
            "[Text.UTF8Encoding]::new($false, $true)");
        StringAssert.Contains(builder, "Write-Utf8NoBom $inventoryPath");
    }

    [TestMethod]
    public void InitializerAvoidsPowerShellSevenOnlyDictionaryApis()
    {
        string initializer = File.ReadAllText(RepoPath(
            "scripts/openvino/handoff/Initialize-UclHandoff.ps1"));
        Assert.IsFalse(initializer.Contains(".TryAdd(",
            StringComparison.Ordinal));
        StringAssert.Contains(initializer, "$checksums.Add(");
    }

    private static string RepoPath(string relative) => Path.Combine(
        FindRepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
