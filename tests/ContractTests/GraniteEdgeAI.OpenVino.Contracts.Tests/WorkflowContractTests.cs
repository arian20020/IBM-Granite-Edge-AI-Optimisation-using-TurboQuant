using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Management.Automation.Language;
using YamlDotNet.RepresentationModel;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class WorkflowContractTests
{
    [TestMethod]
    public void TaskTenGpuEvidenceBoundaryIsExplicitAndOptional()
    {
        string verifier = RepoPath("scripts/openvino/Test-OpenVinoGpuEvidence.ps1");
        Assert.IsTrue(File.Exists(verifier));
        string workflow = UclWorkflow();
        foreach (string required in new[]
        {
            "gpu_device",
            "gpu_authorization",
            "GPU-01",
            "OPENVINO_UCL_GPU_DEVICE",
            "Test-OpenVinoGpuEvidence.ps1",
            "artifacts/openvino/gpu-evidence/gpu.json"
        })
        {
            StringAssert.Contains(workflow, required);
        }
    }

    [TestMethod]
    public void GpuEvidenceVerifierBindsExactDeviceDriverPluginRuntimeAndNegatives()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string stage = Path.Combine(temporary.Path, "stage");
        Directory.CreateDirectory(stage);
        string plugin = Path.Combine(stage, "openvino_intel_gpu_plugin.dll");
        File.WriteAllBytes(plugin, [1, 2, 3, 4]);
        string evidence = Path.Combine(temporary.Path, "gpu.json");
        string results = Path.Combine(temporary.Path, "gpu.trx");
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        string configSha = Convert.ToHexString(SHA256.HashData(
            "{\"ATTENTION_BACKEND\":\"SDPA\"}"u8)).ToLowerInvariant();
        object document = new
        {
            schemaVersion = 1,
            commitSha = commit,
            requestedDevice = "GPU.0",
            actualExecutionDevices = new[] { "GPU.0" },
            gpuIdentity = new
            {
                vendor = "Intel",
                name = "Intel Test GPU",
                driverVersion = "31.0.101.9999"
            },
            pluginIdentity = new
            {
                fileName = "openvino_intel_gpu_plugin.dll",
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(plugin)))
                    .ToLowerInvariant()
            },
            runtimeIdentity = new
            {
                runtime = "2026.3.0-22451-8a17657b995-releases/2026/3",
                genAi = "2026.3.0.0-3277-bd8d6542e3c",
                tokenizers = "2026.3.0.0-703-183c6f25cda"
            },
            configIdentity = new { attentionBackend = "SDPA", sha256 = configSha },
            forcedNegatives = new
            {
                nonexistentDevice = "runtime_device_unavailable",
                cpuResolutionMismatch = "runtime_device_mismatch"
            },
            testDispositions = new
            {
                oneTurn = "passed",
                twoTurn = "passed",
                cancellation = "passed",
                cleanup = "zero_residue"
            }
        };
        File.WriteAllText(evidence, JsonSerializer.Serialize(document));
        File.WriteAllText(
            results,
            "<TestRun><Results><UnitTestResult outcome=\"Passed\" " +
            "testName=\"AuthorizedPhysicalIntelGpuCompilesAndGeneratesOnExactDevice\">" +
            "<Output><StdOut>" +
            "OPENVINO_MEASURED_REQUESTED_DEVICE=GPU.0\n" +
            "OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES=GPU.0\n" +
            "OPENVINO_MEASURED_ATTENTION_BACKEND=SDPA\n" +
            "OPENVINO_MEASURED_GPU_ONE_TURN=passed\n" +
            "OPENVINO_MEASURED_GPU_TWO_TURN=passed\n" +
            "OPENVINO_MEASURED_GPU_CANCELLATION=passed\n" +
            "OPENVINO_MEASURED_GPU_CLEANUP=zero_residue" +
            "</StdOut></Output></UnitTestResult>" +
            "<UnitTestResult outcome=\"Passed\" " +
            "testName=\"NonexistentExplicitGpuFailsUnavailableWithoutCpuFallback\" /></Results>" +
            "<ResultSummary><Counters total=\"2\" executed=\"2\" passed=\"2\" " +
            "failed=\"0\" notExecuted=\"0\" /></ResultSummary></TestRun>");

        ScriptResult valid = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoGpuEvidence.ps1",
            "-EvidencePath", evidence,
            "-StageDirectory", stage,
            "-ResultsPath", results,
            "-ExpectedCommitSha", commit,
            "-ExpectedDevice", "GPU.0",
            "-ExpectedGpuName", "Intel Test GPU",
            "-ExpectedDriverVersion", "31.0.101.9999");
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual("gpu_evidence_valid", valid.StandardOutput.Trim());

        string hostile = File.ReadAllText(evidence).Replace(
            "\"actualExecutionDevices\":[\"GPU.0\"]",
            "\"actualExecutionDevices\":[\"CPU\"]",
            StringComparison.Ordinal);
        File.WriteAllText(evidence, hostile);
        ScriptResult mismatch = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoGpuEvidence.ps1",
            "-EvidencePath", evidence,
            "-StageDirectory", stage,
            "-ResultsPath", results,
            "-ExpectedCommitSha", commit,
            "-ExpectedDevice", "GPU.0",
            "-ExpectedGpuName", "Intel Test GPU",
            "-ExpectedDriverVersion", "31.0.101.9999");
        Assert.AreNotEqual(0, mismatch.ExitCode);
        Assert.AreEqual("gpu_evidence_invalid", mismatch.StandardOutput.Trim());
    }

    private const string CheckoutSha =
        "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0";
    private const string SetupDotNetSha =
        "d4c94342e560b34958eacfc5d055d21461ed1c5d";
    private const string SetupMsBuildSha =
        "30375c66a4eea26614e0d39710365f22f8b0af57";
    private const string UploadArtifactSha =
        "bbbca2ddaa5d8feaa63e36b76fdaad77386f024f";
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void TaskNineWorkflowAndEvidenceFilesExist()
    {
        foreach (string relativePath in new[]
        {
            ".github/workflows/openvino-official-ci.yml",
            ".github/workflows/openvino-ucl-intel.yml",
            "scripts/openvino/Invoke-OpenVinoOfficialEvidence.ps1",
            "scripts/openvino/Test-OpenVinoEvidencePrivacy.ps1",
            "scripts/openvino/Test-OpenVinoEvidenceArtifactSet.ps1",
            "scripts/openvino/OpenVinoClosedJson.psm1",
            "scripts/openvino/Test-OpenVinoClosedJsonDocument.ps1",
            "scripts/openvino/Test-OpenVinoUclEvidenceBinding.ps1",
            "scripts/openvino/OpenVinoTrustedInputLease.psm1",
            "scripts/openvino/Test-OpenVinoTrustedInputLease.ps1",
            "scripts/openvino/Invoke-OpenVinoUclCampaign.ps1",
            "scripts/openvino/Test-OpenVinoNativeJUnit.ps1",
            "scripts/openvino/Test-OpenVinoTrustedInputs.ps1",
            "scripts/openvino/Test-OpenVinoUclFixtureConsumption.ps1",
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1"
        })
        {
            Assert.IsTrue(File.Exists(RepoPath(relativePath)), relativePath);
        }
    }

    [TestMethod]
    public void NewWorkflowsUseOnlyRepositoryApprovedFullShaActionPins()
    {
        HashSet<string> approved = new(StringComparer.OrdinalIgnoreCase)
        {
            $"actions/checkout@{CheckoutSha}",
            $"actions/setup-dotnet@{SetupDotNetSha}",
            $"microsoft/setup-msbuild@{SetupMsBuildSha}",
            $"actions/upload-artifact@{UploadArtifactSha}"
        };
        foreach (string workflow in Workflows())
        {
            string source = File.ReadAllText(workflow);
            MatchCollection uses = Regex.Matches(
                source,
                @"(?m)^\s*uses:\s*([^\s#]+)\s*$",
                RegexOptions.CultureInvariant);
            Assert.IsGreaterThan(0, uses.Count, workflow);
            foreach (Match match in uses)
            {
                string reference = match.Groups[1].Value;
                Assert.IsTrue(
                    Regex.IsMatch(
                        reference,
                        @"^[a-z0-9_.-]+/[a-z0-9_.-]+@[0-9a-f]{40}$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                    $"Floating action reference in {workflow}: {reference}");
                Assert.IsTrue(
                    approved.Contains(reference),
                    $"Unreviewed action identity in {workflow}: {reference}");
            }
        }

        string combined = string.Join('\n', Workflows().Select(File.ReadAllText));
        StringAssert.Contains(combined, $"actions/checkout@{CheckoutSha}");
        StringAssert.Contains(combined, $"actions/setup-dotnet@{SetupDotNetSha}");
        StringAssert.Contains(combined, $"microsoft/setup-msbuild@{SetupMsBuildSha}");
        StringAssert.Contains(combined, $"actions/upload-artifact@{UploadArtifactSha}");
    }

    [TestMethod]
    public void RunnerContextIsScopedToJobsInOpenVinoWorkflows()
    {
        var workflows = new[]
        {
            new
            {
                Path = ".github/workflows/openvino-official-ci.yml",
                Job = "official-cpu",
                RunnerKeys = new[]
                {
                    "OPENVINO_ARCHIVES", "OPENVINO_BUILD_A", "OPENVINO_BUILD_B",
                    "OPENVINO_STAGE_A", "OPENVINO_STAGE_B", "OPENVINO_RESULTS",
                    "OPENVINO_CONVERTER_CLOSURE", "OPENVINO_CONVERTER_BUILD",
                    "OPENVINO_CONVERTER_STAGE", "OPENVINO_MEASUREMENTS"
                }
            },
            new
            {
                Path = ".github/workflows/openvino-ucl-intel.yml",
                Job = "trusted-intel-cpu",
                RunnerKeys = new[]
                {
                    "OPENVINO_BUILD_A", "OPENVINO_BUILD_B", "OPENVINO_STAGE_A",
                    "OPENVINO_STAGE_B", "OPENVINO_RESULTS", "OPENVINO_CONVERTER_BUILD",
                    "OPENVINO_CONVERTER_STAGE", "GRANITE_OPENVINO_CONVERTER_STAGE"
                }
            },
            new
            {
                Path = ".github/workflows/openvino-turboquant-ucl.yml",
                Job = "trusted-turboquant-cpu",
                RunnerKeys = new[]
                {
                    "TURBOQUANT_RUNTIME_BUILD", "TURBOQUANT_RUNTIME_STAGE",
                    "TURBOQUANT_WORKER_BUILD", "OPENVINO_TURBOQUANT_WORKER_STAGE",
                    "TURBOQUANT_RESULTS"
                }
            }
        };

        foreach (var workflow in workflows)
        {
            YamlMappingNode root = ParseWorkflow(File.ReadAllText(RepoPath(workflow.Path)));
            YamlMappingNode rootEnvironment = Mapping(root, "env");
            Assert.IsFalse(
                rootEnvironment.Children.Values
                    .Select(Scalar)
                    .Any(value => value.Contains("${{ runner.", StringComparison.Ordinal)),
                $"{workflow.Path} uses the runner context before a runner is assigned.");

            YamlMappingNode job = Mapping(Mapping(root, "jobs"), workflow.Job);
            if (job.Children.TryGetValue(new YamlScalarNode("env"), out YamlNode? environment))
            {
                Assert.IsFalse(
                    AsMapping(environment).Children.Values
                        .Select(Scalar)
                        .Any(value => value.Contains("${{ runner.", StringComparison.Ordinal)),
                    $"{workflow.Path} uses the runner context before its steps begin.");
            }

            YamlMappingNode initialize = Step(
                Sequence(job, "steps").Select(AsMapping),
                "Bind operation-owned runner paths");
            string script = Scalar(initialize, "run");
            StringAssert.Contains(script, "$env:RUNNER_TEMP");
            StringAssert.Contains(script, "$env:GITHUB_ENV");
            foreach (string key in workflow.RunnerKeys)
            {
                Assert.IsTrue(
                    Regex.IsMatch(
                        script,
                        $@"(?m)^\s*{Regex.Escape(key)}\s*=",
                        RegexOptions.CultureInvariant),
                    $"{workflow.Path} must bind {key} after its runner is assigned.");
            }
        }
    }

    [TestMethod]
    public void OpenVinoWorkflowsEnableLongPathsWithoutPersistentGitMutation()
    {
        foreach (string path in new[]
        {
            ".github/workflows/openvino-official-ci.yml",
            ".github/workflows/openvino-ucl-intel.yml",
            ".github/workflows/openvino-turboquant-ucl.yml"
        })
        {
            YamlMappingNode root = ParseWorkflow(File.ReadAllText(RepoPath(path)));
            YamlMappingNode environment = Mapping(root, "env");

            Assert.AreEqual("1", Scalar(environment, "GIT_CONFIG_COUNT"), path);
            Assert.AreEqual("core.longpaths", Scalar(environment, "GIT_CONFIG_KEY_0"), path);
            Assert.AreEqual("true", Scalar(environment, "GIT_CONFIG_VALUE_0"), path);
        }
    }

    [TestMethod]
    public void HostedWorkflowUsesStreamCapableOperationOwnedNativeTemp()
    {
        string source = OfficialWorkflow();
        YamlMappingNode root = ParseWorkflow(source);
        YamlMappingNode job = Mapping(Mapping(root, "jobs"), "official-cpu");
        YamlMappingNode[] steps = Sequence(job, "steps").Select(AsMapping).ToArray();
        string bind = Scalar(Step(steps, "Bind operation-owned runner paths"), "run");

        foreach (string required in new[]
        {
            "$env:SystemRoot", "$env:GITHUB_RUN_ID", "$env:GITHUB_RUN_ATTEMPT",
            "OPENVINO_NATIVE_TEMP",
            "OPENVINO_WINDOWS_POWERSHELL_MODULES",
            "System32/WindowsPowerShell/v1.0/Modules",
            ":openvino-stream-probe", "-Stream *",
            "$streams -notcontains 'openvino-stream-probe'"
        })
        {
            StringAssert.Contains(bind, required);
        }

        Assert.IsFalse(Regex.IsMatch(bind, @"(?m)^\s*TEMP\s*="));
        Assert.IsFalse(Regex.IsMatch(bind, @"(?m)^\s*TMP\s*="));
        foreach (string stepName in new[]
        {
            "Build and test independent Release x64 native closure A",
            "Build and test independent Release x64 native closure B",
            "Record and validate native unit count",
            "Run managed client and protected process gates",
            "Run the closed stable-route acceptance filter"
        })
        {
            string run = Scalar(Step(steps, stepName), "run");
            StringAssert.Contains(run, "$env:TEMP = $env:OPENVINO_NATIVE_TEMP", stepName);
            StringAssert.Contains(run, "$env:TMP = $env:OPENVINO_NATIVE_TEMP", stepName);
        }

        foreach (string stepName in new[]
        {
            "Run contracts and static route tests with nonzero floors",
            "Run app adapter and package tamper partitions",
            "Run managed client and protected process gates",
            "Run the closed stable-route acceptance filter",
            "Build the Release x64 app against the pinned worker manifest"
        })
        {
            string run = Scalar(Step(steps, stepName), "run");
            StringAssert.Contains(
                run,
                "$env:PSModulePath = $env:OPENVINO_WINDOWS_POWERSHELL_MODULES",
                stepName);
        }

        string contracts = Scalar(
            Step(steps, "Run contracts and static route tests with nonzero floors"),
            "run");
        StringAssert.Contains(contracts, "Get-Command Get-FileHash -ErrorAction Stop");

        string cleanup = Scalar(
            Step(steps, "Verify post-run integrity and clean operation-owned state"),
            "run");
        Assert.AreEqual(0, Regex.Matches(source, @"--artifacts-path").Count);
        string shortWorkspace = Scalar(
            Step(steps, "Bind operation-owned short workspace drive"),
            "run");
        foreach (string required in new[]
        {
            "$env:GITHUB_WORKSPACE", "Test-Path -LiteralPath 'O:\\'",
            "subst.exe", "OPENVINO_SHORT_WORKSPACE=O:\\"
        })
        {
            StringAssert.Contains(shortWorkspace, required);
        }
        foreach (string stepName in new[]
        {
            "Run managed client and protected process gates",
            "Run the closed stable-route acceptance filter"
        })
        {
            string run = Scalar(Step(steps, stepName), "run");
            StringAssert.Contains(
                run,
                "$env:OPENVINO_SHORT_WORKSPACE",
                stepName);
        }
        StringAssert.Contains(cleanup, "$env:OPENVINO_SHORT_WORKSPACE");
        StringAssert.Contains(cleanup, "& subst.exe 'O:' /D");
        StringAssert.Contains(cleanup, "$env:OPENVINO_NATIVE_TEMP");
        StringAssert.Contains(cleanup, "$env:GITHUB_RUN_ID");
        StringAssert.Contains(cleanup, "$env:GITHUB_RUN_ATTEMPT");
        StringAssert.Contains(cleanup, "Remove-Item -LiteralPath $nativeTemp -Recurse -Force");
    }

    [TestMethod]
    public void HostedWorkflowIsImmutableLeastPrivilegeReleaseX64AndCountGated()
    {
        string source = OfficialWorkflow();
        StringAssert.Contains(source, "permissions:\n  contents: read");
        StringAssert.Contains(source, "runs-on: windows-2022");
        StringAssert.Contains(source, "timeout-minutes:");
        StringAssert.Contains(source, "OPENVINO_EVIDENCE_COMMIT:");
        StringAssert.Contains(source, "ref: ${{ env.OPENVINO_EVIDENCE_COMMIT }}");
        StringAssert.Contains(source, "persist-credentials: false");
        StringAssert.Contains(source, "git rev-parse HEAD");
        StringAssert.Contains(source, "--configuration Release");
        StringAssert.Contains(source, "-p:Platform=x64");
        StringAssert.Contains(source, "--minimum-expected-tests 60");
        StringAssert.Contains(source, "--minimum-expected-tests 177");
        StringAssert.Contains(source, "--minimum-expected-tests 13");
        StringAssert.Contains(source, "--minimum-expected-tests 41");
        StringAssert.Contains(source, "Test-OpenVinoDependencyLocks.ps1");
        StringAssert.Contains(source, "Test-OpenVinoGenAiFixture.ps1");
        StringAssert.Contains(source, "Test-OpenVinoOfficialWorkerManifest.ps1");
        StringAssert.Contains(source, "Build-OpenVinoOfficialWorker.ps1");
        StringAssert.Contains(source, "Invoke-OpenVinoOfficialEvidence.ps1");
    }

    [TestMethod]
    public void HostedWorkflowRunsEveryOfficialCpuEvidenceLayer()
    {
        string source = OfficialWorkflow();
        foreach (string required in new[]
        {
            "GraniteEdgeAI.OpenVino.Contracts.Tests.csproj",
            "GraniteEdgeAI.OpenVino.Tests.csproj",
            "GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj",
            "GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj",
            "OpenVinoPromptAdapterTests",
            "OpenVinoWorkerPackagingTargetTests",
            "ctest.exe",
            "--output-junit"
        })
        {
            StringAssert.Contains(source, required);
        }
    }

    [TestMethod]
    public void TrustedWorkflowIsManualOnlyAndFailsClosedOnEveryUclPrecondition()
    {
        string source = UclWorkflow();
        Assert.IsTrue(Regex.IsMatch(
            source,
            @"(?ms)^on:\s*\r?\n\s*workflow_dispatch:\s*\r?\n"));
        Assert.IsFalse(Regex.IsMatch(source, @"(?m)^\s*(push|pull_request|schedule):"));
        StringAssert.Contains(source, "permissions:\n  contents: read");
        foreach (string label in new[]
        {
            "self-hosted", "Windows", "X64", "workbook05", "intel-target"
        })
        {
            StringAssert.Contains(source, $"- {label}");
        }

        StringAssert.Contains(source, "environment: openvino-ucl-01");
        StringAssert.Contains(source, "OPENVINO_UCL_01_AUTHORIZED");
        StringAssert.Contains(source, "UCL-01-approved");
        StringAssert.Contains(source, "target_commit");
        StringAssert.Contains(source, "^[0-9a-f]{40}$");
        StringAssert.Contains(source, "ref: ${{ env.OPENVINO_EVIDENCE_COMMIT }}");
        StringAssert.Contains(source, "persist-credentials: false");
        StringAssert.Contains(source, "Invoke-OpenVinoUclCampaign.ps1");
        StringAssert.Contains(source, "ExpectedModelLength");
        StringAssert.Contains(source, "ExpectedModelSha256");
        StringAssert.Contains(source, "ExpectedFixtureManifestSha256");
        StringAssert.Contains(source, "ExpectedFixtureManifestSha256");
    }

    [TestMethod]
    public void CleanupIntegrityPrivacyAndSanitizedArtifactRulesAlwaysRun()
    {
        foreach (string source in new[] { OfficialWorkflow(), UclWorkflow() })
        {
            Assert.IsGreaterThanOrEqualTo(
                2,
                Regex.Matches(source, @"if:\s*\$\{\{\s*always\(\)").Count);
            StringAssert.Contains(source, "Test-OpenVinoEvidenceArtifactSet.ps1");
            StringAssert.Contains(source, "retention-days: 30");
            StringAssert.Contains(source, "if-no-files-found: error");
            StringAssert.Contains(source, "artifacts/openvino/evidence/");
            Assert.IsFalse(source.Contains("artifacts/openvino/evidence/*.json", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("TestResults/**", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.trx", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.log", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.bin", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.xml", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void WorkflowNodesCloseInputArtifactFixtureAndNativeCountBoundaries()
    {
        ValidateWorkflowStructure(OfficialWorkflow(), trusted: false);
        ValidateWorkflowStructure(UclWorkflow(), trusted: true);
    }

    [TestMethod]
    public void HostedTriggersCoverEveryInfluentialOpenVinoInput()
    {
        YamlMappingNode root = ParseWorkflow(OfficialWorkflow());
        YamlMappingNode triggers = Mapping(root, "on");
        string[] required =
        [
            "third-party/openvino-official/**",
            "shared/GraniteEdgeAI.OpenVino.Contracts/**",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/**",
            "shared/GraniteEdgeAI.ModelInspection.Transport/**",
            "infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**",
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/**",
            "IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**",
            "IBM Granite with TurboQuant (Intel)/**",
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj",
            "IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets",
            "workers/OpenVinoOfficial.Worker/**",
            "scripts/openvino/**",
            "tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/**",
            "tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/**",
            "tests/ProcessFixtures/GraniteEdgeAI.OpenVino.ProtocolTestWorker/**",
            "tests/ProcessFixtures/GraniteEdgeAI.OpenVino.ParentExitFixture/**",
            "tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/**",
            "tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/**",
            "tests/TestFixtures/OpenVINO/**",
            "global.json",
            ".gitattributes",
            "Directory.Build.*",
            "**/*.slnx",
            "**/*.csproj",
            "**/*.props",
            "**/*.targets"
        ];

        foreach (string triggerName in new[] { "push", "pull_request" })
        {
            string[] paths = Sequence(Mapping(triggers, triggerName), "paths")
                .Select(Scalar)
                .ToArray();
            CollectionAssert.IsSubsetOf(required, paths, triggerName);
        }
    }

    [TestMethod]
    public void StructuralWorkflowValidationRejectsWrongNodeCommentsAndInputPayloads()
    {
        string official = OfficialWorkflow();
        string wrongUploadNode = official.Replace(
            "path: artifacts/openvino/evidence/cpu.json",
            "path: artifacts/openvino/evidence/ignored.json # path: artifacts/openvino/evidence/cpu.json",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<AssertFailedException>(() =>
            ValidateWorkflowStructure(wrongUploadNode, trusted: false));

        string ucl = UclWorkflow();
        const string hostilePayload =
            "'${{ inputs.authorization }}' -cne 'UCL-01' # UCL-01'; throw 'injected'\n" +
            "              Write-Output owned";
        string directInterpolation = ucl.Replace(
            "$env:OPENVINO_UCL_AUTHORIZATION -cne 'UCL-01'",
            hostilePayload,
            StringComparison.Ordinal);
        Assert.ThrowsExactly<AssertFailedException>(() =>
            ValidateWorkflowStructure(directInterpolation, trusted: true));

        string misplacedAlways = official.Replace(
            "if: ${{ always() }}",
            "if: ${{ success() }} # if: ${{ always() }}",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<AssertFailedException>(() =>
            ValidateWorkflowStructure(misplacedAlways, trusted: false));
    }

    [TestMethod]
    public void TrustedWorkflowHasOneAstVerifiedRetainedLeaseCampaignOwner()
    {
        YamlMappingNode root = ParseWorkflow(UclWorkflow());
        YamlMappingNode job = Mapping(Mapping(root, "jobs"), "trusted-intel-cpu");
        YamlMappingNode[] steps = Sequence(job, "steps").Select(AsMapping).ToArray();
        YamlMappingNode campaign = Step(steps, "Run retained trusted UCL CPU campaign");
        string run = Scalar(campaign, "run");
        CommandAst command = SingleCommand(run, "powershell.exe");
        string[] elements = command.CommandElements.Select(item => item.Extent.Text).ToArray();
        CollectionAssert.Contains(elements, "scripts/openvino/Invoke-OpenVinoUclCampaign.ps1");
        foreach (string required in new[]
        {
            "$env:GITHUB_WORKSPACE", "$env:OPENVINO_ARCHIVES",
            "$env:OPENVINO_CONTROLLED_FIXTURE", "$env:OPENVINO_BUILD_A",
            "$env:OPENVINO_BUILD_B", "$env:OPENVINO_STAGE_A",
            "$env:OPENVINO_STAGE_B", "$env:OPENVINO_RESULTS",
            "$env:OPENVINO_EVIDENCE_FILE"
        })
        {
            CollectionAssert.Contains(elements, required);
        }

        string[] forbiddenOwners =
        [
            "Build-OpenVinoOfficialWorker.ps1", "Test-OpenVinoTrustedInputs.ps1",
            "Test-OpenVinoDependencyLocks.ps1", "Test-OpenVinoGenAiFixture.ps1",
            "New-OpenVinoEvidenceMeasurements.ps1", "Invoke-OpenVinoOfficialEvidence.ps1",
            "dotnet", "msbuild", "ctest.exe"
        ];
        foreach (YamlMappingNode other in steps.Where(step => HasRun(step) && step != campaign))
        {
            string otherRun = Scalar(other, "run");
            Token[] tokens;
            ParseError[] errors;
            ScriptBlockAst ast = Parser.ParseInput(otherRun, out tokens, out errors);
            Assert.HasCount(0, errors, Scalar(other, "name"));
            CommandAst[] otherCommands = ast.FindAll(
                    node => node is CommandAst, searchNestedScriptBlocks: true)
                .Cast<CommandAst>().ToArray();
            foreach (string forbidden in forbiddenOwners)
            {
                Assert.IsFalse(otherCommands.Any(item =>
                        (item.GetCommandName() ?? string.Empty).Contains(
                            forbidden, StringComparison.OrdinalIgnoreCase) ||
                        item.CommandElements.Any(element => element.Extent.Text.Contains(
                            forbidden, StringComparison.OrdinalIgnoreCase))),
                    $"{Scalar(other, "name")} duplicates trusted-root consumer {forbidden}.");
            }
        }

        string quoted = run.Replace(
            "& powershell.exe -NoLogo -NoProfile -NonInteractive `",
            "Write-Output \"& powershell.exe -NoLogo -NoProfile -NonInteractive `\"",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<AssertFailedException>(() => SingleCommand(quoted, "powershell.exe"));
        string commented = run.Replace("& powershell.exe", "# & powershell.exe", StringComparison.Ordinal);
        Assert.ThrowsExactly<AssertFailedException>(() => SingleCommand(commented, "powershell.exe"));

        string cleanup = Scalar(Step(steps, "Clean operation-owned outputs after retained campaign"), "run");
        Assert.ThrowsExactly<AssertFailedException>(() =>
            SingleCommand(cleanup, "powershell.exe"));
    }

    [TestMethod]
    public void HostedWorkflowCommandsArePowerShellAstVerifiedInTheirOwningSteps()
    {
        YamlMappingNode root = ParseWorkflow(OfficialWorkflow());
        YamlMappingNode job = Mapping(Mapping(root, "jobs"), "official-cpu");
        YamlMappingNode[] steps = Sequence(job, "steps").Select(AsMapping).ToArray();
        AssertPowerShellCommandArgument(
            Step(steps, "Create closed measured evidence inputs"),
            "powershell.exe", "New-OpenVinoEvidenceMeasurements.ps1");
        AssertPowerShellCommandArgument(
            Step(steps, "Create typed sanitized official CPU evidence"),
            "powershell.exe", "Invoke-OpenVinoOfficialEvidence.ps1");
        AssertPowerShellCommandArgument(
            Step(steps, "Record and validate native unit count"),
            "powershell.exe", "Test-OpenVinoNativeJUnit.ps1");
    }

    [TestMethod]
    public void PrivacyVerifierAcceptsTheClosedDeterministicEvidenceSchema()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "evidence.json");
        File.WriteAllText(evidencePath, ValidEvidence());

        ScriptResult result = RunPrivacyVerifier(evidencePath);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("evidence_privacy_valid", result.StandardOutput.Trim());
    }

    [TestMethod]
    public void ArtifactSetVerifierAcceptsOneExactFileAndRejectsASecondJson()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "cpu.json");
        File.WriteAllText(evidencePath, ValidEvidence());

        ScriptResult valid = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoEvidenceArtifactSet.ps1",
            "-EvidencePath", evidencePath);
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual("evidence_artifact_set_valid", valid.StandardOutput.Trim());

        File.WriteAllText(
            Path.Combine(temporary.Path, "hostile.json"),
            "{\"prompt\":\"must never be selected\"}");
        ScriptResult hostile = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoEvidenceArtifactSet.ps1",
            "-EvidencePath", evidencePath);
        Assert.AreNotEqual(0, hostile.ExitCode);
        Assert.AreEqual("evidence_artifact_set_invalid", hostile.StandardOutput.Trim());
    }

    [TestMethod]
    public void NativeJUnitVerifierRejectsSixAndAcceptsTheReviewedFloorOfSeven()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string resultsPath = Path.Combine(temporary.Path, "native.xml");
        File.WriteAllText(resultsPath, NativeJUnit(6));

        ScriptResult belowFloor = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoNativeJUnit.ps1",
            "-ResultsPath", resultsPath,
            "-MinimumCount", "7");
        Assert.AreNotEqual(0, belowFloor.ExitCode);
        Assert.AreEqual("native_junit_invalid", belowFloor.StandardOutput.Trim());

        File.WriteAllText(resultsPath, NativeJUnit(7));
        ScriptResult exactFloor = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoNativeJUnit.ps1",
            "-ResultsPath", resultsPath,
            "-MinimumCount", "7");
        Assert.AreEqual(0, exactFloor.ExitCode, exactFloor.StandardError);
        Assert.AreEqual("native_junit_valid:7", exactFloor.StandardOutput.Trim());
    }

    [TestMethod]
    public void TrustedInputVerifierRejectsContainmentReparseAndFinalPathSubstitution()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string workspace = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "workspace")).FullName;
        string fixture = Path.Combine(temporary.Path, "controlled-fixture");
        CopyDirectory(
            RepoPath("tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1"),
            fixture);
        string archives = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "controlled-archives")).FullName;
        File.WriteAllText(Path.Combine(archives, "archive.zip"), "controlled");
        SetFilesReadOnly(fixture, readOnly: true);
        SetFilesReadOnly(archives, readOnly: true);
        string model = Path.Combine(fixture, "package", "openvino_model.bin");
        string manifest = Path.Combine(fixture, "manifest.json");

        string[] commonArguments =
        [
            "-WorkspaceRoot", workspace,
            "-ArchiveRoot", archives,
            "-FixtureRoot", fixture,
            "-ExpectedModelLength", new FileInfo(model).Length.ToString(),
            "-ExpectedModelSha256", LowerSha256(model),
            "-ExpectedFixtureManifestSha256", LowerSha256(manifest)
        ];

        ScriptResult valid = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoTrustedInputs.ps1",
            commonArguments);
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual("trusted_inputs_valid", valid.StandardOutput.Trim());

        AssertTrustedInputsInvalid(commonArguments, "-FixtureRoot", workspace);
        AssertTrustedInputsInvalid(commonArguments, "-WorkspaceRoot", temporary.Path);

        string targetInsideWorkspace = Path.Combine(workspace, "fixture-target");
        SetFilesReadOnly(fixture, readOnly: false);
        CopyDirectory(fixture, targetInsideWorkspace);
        SetFilesReadOnly(fixture, readOnly: true);
        SetFilesReadOnly(targetInsideWorkspace, readOnly: true);
        string substitutedRoot = Path.Combine(temporary.Path, "substituted-fixture");
        Directory.CreateSymbolicLink(substitutedRoot, targetInsideWorkspace);
        AssertTrustedInputsInvalid(commonArguments, "-FixtureRoot", substitutedRoot);
        Directory.Delete(substitutedRoot);

        string childTarget = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "child-target")).FullName;
        string childLink = Path.Combine(fixture, "package", "linked-child");
        Directory.CreateSymbolicLink(childLink, childTarget);
        AssertTrustedInputsInvalid(commonArguments);
        Directory.Delete(childLink);

        string archiveLink = Path.Combine(temporary.Path, "archive-root-link");
        Directory.CreateSymbolicLink(archiveLink, archives);
        AssertTrustedInputsInvalid(commonArguments, "-ArchiveRoot", archiveLink);
        Directory.Delete(archiveLink);

        string originalModel = Path.Combine(temporary.Path, "original-model.bin");
        SetFilesReadOnly(fixture, readOnly: false);
        File.Move(model, originalModel);
        File.CreateSymbolicLink(model, originalModel);
        AssertTrustedInputsInvalid(commonArguments);
        File.Delete(model);
        File.Move(originalModel, model);
        SetFilesReadOnly(fixture, readOnly: true);

        SetFilesReadOnly(fixture, readOnly: false);
        SetFilesReadOnly(archives, readOnly: false);
        SetFilesReadOnly(targetInsideWorkspace, readOnly: false);
    }

    [TestMethod]
    public void TrustedInputLeaseDeniesMutationUntilReleasedAndThenRestoresMutability()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string archives = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "archives")).FullName;
        string fixture = Path.Combine(temporary.Path, "fixture");
        CopyDirectory(
            RepoPath("tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1"),
            fixture);
        File.WriteAllText(Path.Combine(archives, "archive.zip"), "controlled");

        ScriptResult result = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoTrustedInputLease.ps1",
            "-ArchiveRoot", archives,
            "-FixtureRoot", fixture);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("trusted_input_lease_valid", result.StandardOutput.Trim());
        Assert.IsFalse(File.Exists(Path.Combine(archives, "lease-add-probe.tmp")));
        Assert.IsTrue(File.Exists(Path.Combine(archives, "archive.zip")));
    }

    [TestMethod]
    public void TrustedInputLeaseRejectsAlternateDataStreamsBeforeAcquisition()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string archives = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "archives")).FullName;
        string fixture = Path.Combine(temporary.Path, "fixture");
        CopyDirectory(
            RepoPath("tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1"),
            fixture);
        string archive = Path.Combine(archives, "archive.zip");
        File.WriteAllText(archive, "controlled");
        File.WriteAllText(archive + ":hidden", "must be rejected");

        ScriptResult result = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoTrustedInputLease.ps1",
            "-ArchiveRoot", archives,
            "-FixtureRoot", fixture);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual("trusted_input_lease_invalid", result.StandardOutput.Trim());
    }

    [TestMethod]
    public void UclFixtureConsumptionVerifierRequiresExactMeasuredIdentity()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string resultsPath = Path.Combine(temporary.Path, "process.trx");
        const string identity =
            "4444444444444444444444444444444444444444444444444444444444444444";
        File.WriteAllText(resultsPath, ProcessTrx(measuredFixtureIdentity: null));

        ScriptResult missing = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoUclFixtureConsumption.ps1",
            "-ResultsPath", resultsPath,
            "-ExpectedFixtureManifestSha256", identity);
        Assert.AreNotEqual(0, missing.ExitCode);
        Assert.AreEqual("ucl_fixture_consumption_invalid", missing.StandardOutput.Trim());

        File.WriteAllText(resultsPath, ProcessTrx(identity));
        ScriptResult exact = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoUclFixtureConsumption.ps1",
            "-ResultsPath", resultsPath,
            "-ExpectedFixtureManifestSha256", identity);
        Assert.AreEqual(0, exact.ExitCode, exact.StandardError);
        Assert.AreEqual("ucl_fixture_consumption_valid", exact.StandardOutput.Trim());
    }

    [TestMethod]
    public void MeasurementBuilderDerivesClosedFieldsAndRejectsPartialResults()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string results = Directory.CreateDirectory(
            Path.Combine(temporary.Path, "results")).FullName;
        Dictionary<string, int> counts = new(StringComparer.Ordinal)
        {
            ["contracts.trx"] = 60,
            ["static-inspection.trx"] = 177,
            ["worker-client.trx"] = 13,
            ["process-containment.trx"] = 41,
            ["app-adapter.trx"] = 35,
            ["package-tamper.trx"] = 5
        };
        foreach ((string name, int count) in counts)
        {
            File.WriteAllText(
                Path.Combine(results, name),
                EvidenceTrx(
                    count,
                    name == "process-containment.trx" ? MeasurementMarkers() : null));
        }
        string native = Path.Combine(results, "native.xml");
        File.WriteAllText(native, NativeJUnit(7));
        string measurements = Path.Combine(results, "measurements.json");

        string unexpected = Path.Combine(results, "raw-worker.log");
        File.WriteAllText(unexpected, "raw output must never enter the measured input set");
        ScriptResult unexpectedInput = RunPowerShellScript(
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1",
            "-TestResultsDirectory", results,
            "-NativeResultsPath", native,
            "-PerformanceDurationsMilliseconds", "3000,1000,2000",
            "-MeasurementsPath", measurements);
        Assert.AreNotEqual(0, unexpectedInput.ExitCode);
        Assert.AreEqual("openvino_measurements_invalid", unexpectedInput.StandardOutput.Trim());
        Assert.IsFalse(File.Exists(measurements));
        File.Delete(unexpected);

        ScriptResult valid = RunPowerShellScript(
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1",
            "-TestResultsDirectory", results,
            "-NativeResultsPath", native,
            "-PerformanceDurationsMilliseconds", "3000,1000,2000",
            "-MeasurementsPath", measurements);
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual("openvino_measurements_created", valid.StandardOutput.Trim());
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(measurements)))
        {
            Assert.AreEqual("CPU", document.RootElement.GetProperty("requestedDevice").GetString());
            Assert.AreEqual(7, document.RootElement.GetProperty("testCounts")
                .GetProperty("nativeUnit").GetInt32());
            Assert.AreEqual("2026.3.0.0-703-183c6f25cda", document.RootElement
                .GetProperty("runtimeBuildIdentity").GetProperty("tokenizers").GetString());
        }

        File.Delete(measurements);
        foreach (int run in Enumerable.Range(1, 3))
        {
            File.Copy(
                Path.Combine(results, "process-containment.trx"),
                Path.Combine(results, $"process-containment-{run}.trx"));
        }
        ScriptResult trustedCampaign = RunPowerShellScript(
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1",
            "-TestResultsDirectory", results,
            "-NativeResultsPath", native,
            "-PerformanceDurationsMilliseconds", "3000,1000,2000",
            "-MeasurementsPath", measurements);
        Assert.AreEqual(0, trustedCampaign.ExitCode, trustedCampaign.StandardError);
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(measurements)))
        {
            Assert.AreEqual(3, document.RootElement.GetProperty("performanceAggregates")
                .GetProperty("sampleCount").GetInt32());
        }
        File.Delete(measurements);
        File.AppendAllText(
            Path.Combine(results, "process-containment-3.trx"),
            "substituted");
        ScriptResult substitutedCampaign = RunPowerShellScript(
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1",
            "-TestResultsDirectory", results,
            "-NativeResultsPath", native,
            "-PerformanceDurationsMilliseconds", "3000,1000,2000",
            "-MeasurementsPath", measurements);
        Assert.AreNotEqual(0, substitutedCampaign.ExitCode);
        Assert.IsFalse(File.Exists(measurements));
        foreach (int run in Enumerable.Range(1, 3))
        {
            File.Delete(Path.Combine(results, $"process-containment-{run}.trx"));
        }

        File.WriteAllText(
            Path.Combine(results, "process-containment.trx"),
            EvidenceTrx(41, MeasurementMarkers().Replace(
                "OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES=CPU\n",
                string.Empty,
                StringComparison.Ordinal)));
        ScriptResult partial = RunPowerShellScript(
            "scripts/openvino/New-OpenVinoEvidenceMeasurements.ps1",
            "-TestResultsDirectory", results,
            "-NativeResultsPath", native,
            "-PerformanceDurationsMilliseconds", "1000,2000,3000",
            "-MeasurementsPath", measurements);
        Assert.AreNotEqual(0, partial.ExitCode);
        Assert.AreEqual("openvino_measurements_invalid", partial.StandardOutput.Trim());
        Assert.IsFalse(File.Exists(measurements));
    }

    [TestMethod]
    public void RouteNeutralGeneratorCannotSelfClaimUclWithFullySpoofedEnvironment()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidence = Path.Combine(temporary.Path, "evidence", "cpu.json");
        string measurements = Path.Combine(temporary.Path, "measurements.json");
        File.WriteAllText(measurements, "{}");
        string[] arguments =
        [
            "-EvidenceKind", "ucl",
            "-ExpectedCommitSha", GitHead(),
            "-OfficialArchiveDirectory", temporary.Path,
            "-WorkerStageDirectory", temporary.Path,
            "-FixtureRoot", temporary.Path,
            "-MeasurementsPath", measurements,
            "-CpuVendor", "Intel",
            "-EvidencePath", evidence,
            "-ExpectedModelLength", "1",
            "-ExpectedModelSha256", new string('1', 64)
        ];

        ScriptResult spoofed = RunPowerShellScriptWithEnvironment(
            "scripts/openvino/Invoke-OpenVinoOfficialEvidence.ps1",
            new Dictionary<string, string?>
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_EVENT_NAME"] = "workflow_dispatch",
                ["GITHUB_SHA"] = GitHead(),
                ["GITHUB_WORKFLOW_SHA"] = GitHead(),
                ["GITHUB_WORKFLOW_REF"] =
                    "attacker/repo/.github/workflows/openvino-ucl-intel.yml@refs/heads/main",
                ["GITHUB_REPOSITORY"] = "attacker/repo",
                ["GITHUB_REF"] = "refs/heads/main",
                ["GITHUB_RUN_ID"] = "1",
                ["GITHUB_RUN_ATTEMPT"] = "1",
                ["GITHUB_JOB"] = "trusted-intel-cpu",
                ["OPENVINO_UCL_ENVIRONMENT"] = "openvino-ucl-01",
                ["OPENVINO_UCL_01_AUTHORIZED"] = "UCL-01-approved",
                ["OPENVINO_UCL_AUTHORIZATION"] = "UCL-01",
                ["RUNNER_ARCH"] = "X64"
            },
            arguments);
        Assert.AreNotEqual(0, spoofed.ExitCode);
        Assert.IsFalse(File.Exists(evidence));

        string source = File.ReadAllText(RepoPath(
            "scripts/openvino/Invoke-OpenVinoOfficialEvidence.ps1"));
        Assert.IsFalse(source.Contains("EvidenceKind", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("evidenceKind", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("GITHUB_", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("OPENVINO_UCL_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LocalCpuDocumentCannotSatisfyUclBindingWithoutExternalRunRecord()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidence = Path.Combine(temporary.Path, "cpu.json");
        File.WriteAllText(evidence, ValidEvidence());

        ScriptResult result = RunPowerShellScriptWithEnvironment(
            "scripts/openvino/Test-OpenVinoUclEvidenceBinding.ps1",
            new Dictionary<string, string?>
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_EVENT_NAME"] = "workflow_dispatch",
                ["GITHUB_SHA"] = new string('a', 40),
                ["GITHUB_RUN_ID"] = "123"
            },
            "-EvidencePath", evidence);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual("ucl_evidence_binding_invalid", result.StandardOutput.Trim());
        Assert.IsFalse(result.StandardOutput.Contains(
            "ucl_evidence_binding_valid_not_authenticated",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ExternallySuppliedRunRecordBindsDigestWithoutClaimingAuthentication()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidence = Path.Combine(temporary.Path, "cpu.json");
        string metadata = Path.Combine(temporary.Path, "trusted-run.json");
        File.WriteAllText(evidence, ValidEvidence());
        File.WriteAllText(metadata,
            "{\"schemaVersion\":1," +
            "\"provider\":\"github-actions\"," +
            "\"workflowPath\":\".github/workflows/openvino-ucl-intel.yml\"," +
            "\"eventName\":\"workflow_dispatch\"," +
            "\"commitSha\":\"0123456789abcdef0123456789abcdef01234567\"," +
            "\"runId\":123,\"runAttempt\":1," +
            "\"jobName\":\"trusted-intel-cpu\"," +
            "\"artifactName\":\"openvino-ucl-intel-cpu-evidence\"," +
            $"\"evidenceSha256\":\"{LowerSha256(evidence)}\"}}");

        ScriptResult result = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoUclEvidenceBinding.ps1",
            "-EvidencePath", evidence,
            "-TrustedRunMetadataPath", metadata);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "ucl_evidence_binding_valid_not_authenticated",
            result.StandardOutput.Trim());

        File.WriteAllText(metadata, File.ReadAllText(metadata).Replace(
            "0123456789abcdef0123456789abcdef01234567",
            "fedcba9876543210fedcba9876543210fedcba98",
            StringComparison.Ordinal));
        ScriptResult wrongCommit = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoUclEvidenceBinding.ps1",
            "-EvidencePath", evidence,
            "-TrustedRunMetadataPath", metadata);
        Assert.AreNotEqual(0, wrongCommit.ExitCode);
        Assert.AreEqual("ucl_evidence_binding_invalid", wrongCommit.StandardOutput.Trim());
    }

    [TestMethod]
    public void ClosedJsonParserRejectsDecodedDuplicatesSurrogatesAndDepth()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string json = Path.Combine(temporary.Path, "input.json");
        File.WriteAllText(json, "{\"a\":1,\"nested\":{\"b\":[true,null,\"ok\"]}}");
        ScriptResult valid = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoClosedJsonDocument.ps1",
            "-JsonPath", json,
            "-MaximumBytes", "1024",
            "-MaximumDepth", "8");
        Assert.AreEqual(0, valid.ExitCode, valid.StandardError);
        Assert.AreEqual("closed_json_valid", valid.StandardOutput.Trim());

        foreach (string hostile in new[]
        {
            "{\"schemaVersion\":true,\"\\u0073chemaVersion\":1}",
            "{\"outer\":{\"runtime\":true,\"\\u0072untime\":1}}",
            "{\"\\uD800\":null}",
            "{\"value\":\"\\uDC00\"}",
            "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{}}}}}}}}}"
        })
        {
            File.WriteAllText(json, hostile);
            ScriptResult rejected = RunPowerShellScript(
                "scripts/openvino/Test-OpenVinoClosedJsonDocument.ps1",
                "-JsonPath", json,
                "-MaximumBytes", "1024",
                "-MaximumDepth", "8");
            Assert.AreNotEqual(0, rejected.ExitCode, hostile);
            Assert.AreEqual("closed_json_invalid", rejected.StandardOutput.Trim());
        }
    }

    [TestMethod]
    [DataRow("absolute-path")]
    [DataRow("posix-absolute-path")]
    [DataRow("unc-path")]
    [DataRow("username")]
    [DataRow("hostname")]
    [DataRow("prompt")]
    [DataRow("generated-text")]
    [DataRow("environment")]
    [DataRow("secret")]
    [DataRow("stdout")]
    [DataRow("stderr")]
    [DataRow("model-bytes")]
    [DataRow("unexpected-nested")]
    [DataRow("duplicate")]
    [DataRow("escaped-root-duplicate")]
    [DataRow("escaped-nested-duplicate")]
    [DataRow("lone-high-surrogate-key")]
    [DataRow("lone-low-surrogate-value")]
    [DataRow("excessive-depth")]
    [DataRow("noncanonical-order")]
    [DataRow("zero-test-count")]
    [DataRow("wrong-type")]
    [DataRow("schema-boolean")]
    [DataRow("actual-devices-scalar")]
    [DataRow("performance-overflow")]
    public void PrivacyVerifierRejectsHostileEvidence(string hostileCase)
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "hostile.json");
        File.WriteAllText(evidencePath, HostileEvidence(hostileCase));

        ScriptResult result = RunPrivacyVerifier(evidencePath);

        Assert.AreNotEqual(0, result.ExitCode, hostileCase);
        Assert.AreEqual("evidence_privacy_invalid", result.StandardOutput.Trim());
    }

    [TestMethod]
    [DataRow("root")]
    [DataRow("commitSha")]
    [DataRow("dependencyLockIdentities")]
    [DataRow("dependencyLockIdentities.runtimeLockSha256")]
    [DataRow("dependencyLockIdentities.genAiLockSha256")]
    [DataRow("dependencyLockIdentities.tokenizersLockSha256")]
    [DataRow("fixtureManifestSha256")]
    [DataRow("workerManifestSha256")]
    [DataRow("requestedDevice")]
    [DataRow("actualExecutionDevices.element")]
    [DataRow("cpuIdentity")]
    [DataRow("cpuIdentity.architecture")]
    [DataRow("cpuIdentity.vendor")]
    [DataRow("runtimeBuildIdentity")]
    [DataRow("runtimeBuildIdentity.runtime")]
    [DataRow("runtimeBuildIdentity.genAi")]
    [DataRow("runtimeBuildIdentity.tokenizers")]
    [DataRow("testCounts")]
    [DataRow("testCounts.contracts")]
    [DataRow("testCounts.staticInspection")]
    [DataRow("testCounts.nativeUnit")]
    [DataRow("testCounts.workerClient")]
    [DataRow("testCounts.processContainment")]
    [DataRow("testCounts.appAdapter")]
    [DataRow("testCounts.packageTamper")]
    [DataRow("performanceAggregates")]
    [DataRow("performanceAggregates.sampleCount")]
    [DataRow("performanceAggregates.durationMillisecondsMinimum")]
    [DataRow("performanceAggregates.durationMillisecondsMedian")]
    [DataRow("performanceAggregates.durationMillisecondsMaximum")]
    [DataRow("cancellationDisposition")]
    [DataRow("cleanupDisposition")]
    [DataRow("numeric-nonfinite")]
    [DataRow("numeric-int64-overflow")]
    public void PrivacyVerifierRejectsWrongExactTypeAtEverySchemaNode(string path)
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "wrong-type.json");
        File.WriteAllText(evidencePath, WrongTypeEvidence(path));

        ScriptResult result = RunPrivacyVerifier(evidencePath);

        Assert.AreNotEqual(0, result.ExitCode, path);
        Assert.AreEqual("evidence_privacy_invalid", result.StandardOutput.Trim());
    }

    private static string HostileEvidence(string hostileCase)
    {
        string valid = ValidEvidence();
        return hostileCase switch
        {
            "absolute-path" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"C:\\\\Users\\\\alice\\\\model\"",
                StringComparison.Ordinal),
            "posix-absolute-path" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"/home/alice/model\"",
                StringComparison.Ordinal),
            "unc-path" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"\\\\\\\\server\\\\share\"",
                StringComparison.Ordinal),
            "username" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"username=alice\"",
                StringComparison.Ordinal),
            "hostname" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"hostname=WORKBOOK05\"",
                StringComparison.Ordinal),
            "prompt" => AddRootField(valid, "\"prompt\":\"private question\""),
            "generated-text" => AddRootField(valid, "\"generatedText\":\"private answer\""),
            "environment" => AddRootField(valid, "\"environment\":{\"PATH\":\"private\"}"),
            "secret" => AddRootField(valid, "\"secret\":\"token-value\""),
            "stdout" => AddRootField(valid, "\"stdout\":\"raw native output\""),
            "stderr" => AddRootField(valid, "\"stderr\":\"raw native error\""),
            "model-bytes" => AddRootField(valid, "\"modelBytes\":\"AAECAwQ=\""),
            "unexpected-nested" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"Intel\",\"account\":\"alice\"",
                StringComparison.Ordinal),
            "duplicate" => valid.Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal),
            "escaped-root-duplicate" => valid.Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":true,\"\\u0073chemaVersion\":1",
                StringComparison.Ordinal),
            "escaped-nested-duplicate" => valid.Replace(
                "\"runtime\":\"2026.3.0-22451-8a17657b995-releases/2026/3\"",
                "\"runtime\":true,\"\\u0072untime\":\"2026.3.0-22451-8a17657b995-releases/2026/3\"",
                StringComparison.Ordinal),
            "lone-high-surrogate-key" => AddRootField(
                valid,
                "\"\\uD800\":null"),
            "lone-low-surrogate-value" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"\\uDC00\"",
                StringComparison.Ordinal),
            "excessive-depth" => AddRootField(
                valid,
                "\"extra\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{}}}}}}}}}"),
            "noncanonical-order" => valid.Replace(
                "{\"schemaVersion\":1,\"commitSha\"",
                "{\"commitSha\"",
                StringComparison.Ordinal),
            "zero-test-count" => valid.Replace(
                "\"contracts\":60",
                "\"contracts\":0",
                StringComparison.Ordinal),
            "wrong-type" => valid.Replace(
                "\"contracts\":60",
                "\"contracts\":true",
                StringComparison.Ordinal),
            "schema-boolean" => valid.Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":true",
                StringComparison.Ordinal),
            "actual-devices-scalar" => valid.Replace(
                "\"actualExecutionDevices\":[\"CPU\"]",
                "\"actualExecutionDevices\":\"CPU\"",
                StringComparison.Ordinal),
            "performance-overflow" => valid.Replace(
                "\"durationMillisecondsMaximum\":3000",
                "\"durationMillisecondsMaximum\":600001",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(hostileCase))
        };
    }

    private static string WrongTypeEvidence(string path)
    {
        if (path == "root")
        {
            return "[" + ValidEvidence() + "]";
        }
        if (path == "numeric-nonfinite")
        {
            return ValidEvidence().Replace(
                "\"sampleCount\":3",
                "\"sampleCount\":NaN",
                StringComparison.Ordinal);
        }
        if (path == "numeric-int64-overflow")
        {
            return ValidEvidence().Replace(
                "\"sampleCount\":3",
                "\"sampleCount\":9223372036854775808",
                StringComparison.Ordinal);
        }

        JsonObject root = JsonNode.Parse(ValidEvidence())!.AsObject();
        if (path == "actualExecutionDevices.element")
        {
            root["actualExecutionDevices"] = new JsonArray(true);
            return root.ToJsonString();
        }

        string[] segments = path.Split('.');
        JsonObject owner = root;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            owner = owner[segments[index]]!.AsObject();
        }

        string leaf = segments[^1];
        bool objectNode = path is
            "dependencyLockIdentities" or "cpuIdentity" or
            "runtimeBuildIdentity" or "testCounts" or
            "performanceAggregates";
        bool integerNode = path.StartsWith("testCounts.", StringComparison.Ordinal) ||
            path.StartsWith("performanceAggregates.", StringComparison.Ordinal);
        owner[leaf] = objectNode
            ? new JsonArray()
            : integerNode
                ? JsonValue.Create("7")
                : JsonValue.Create(true);
        return root.ToJsonString();
    }

    private static string AddRootField(string json, string field) =>
        json[..^1] + "," + field + "}";

    private static string NativeJUnit(int count) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<testsuite name=\"native\" tests=\"{count}\" " +
        "failures=\"0\" disabled=\"0\" skipped=\"0\">" +
        string.Concat(Enumerable.Range(1, count).Select(index =>
            $"<testcase name=\"test-{index}\"/>")) +
        "</testsuite>";

    private static string ProcessTrx(string? measuredFixtureIdentity) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">" +
        "<ResultSummary outcome=\"Completed\"><Counters total=\"1\" executed=\"1\" " +
        "passed=\"1\" failed=\"0\" notExecuted=\"0\" /></ResultSummary>" +
        "<Results><UnitTestResult testName=\"UclOverrideRejectsRepositoryFixtureAndRecordsExactExternalIdentity\" " +
        "outcome=\"Passed\"><Output><StdOut>" +
        (measuredFixtureIdentity is null
            ? string.Empty
            : "OPENVINO_UCL_FIXTURE_CONSUMED_SHA256=" + measuredFixtureIdentity) +
        "</StdOut></Output></UnitTestResult></Results></TestRun>";

    private static string EvidenceTrx(int count, string? standardOutput) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">" +
        $"<ResultSummary outcome=\"Completed\"><Counters total=\"{count}\" " +
        $"executed=\"{count}\" passed=\"{count}\" failed=\"0\" " +
        "notExecuted=\"0\" /></ResultSummary>" +
        (standardOutput is null
            ? string.Empty
            : "<Results><UnitTestResult testName=\"CanonicalFixtureInspectsAndGeneratesFromTwoCleanClosures\" " +
              "outcome=\"Passed\"><Output><StdOut>" + standardOutput +
              "</StdOut></Output></UnitTestResult></Results>") +
        "</TestRun>";

    private static string MeasurementMarkers() =>
        "OPENVINO_MEASURED_REQUESTED_DEVICE=CPU\n" +
        "OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES=CPU\n" +
        "OPENVINO_MEASURED_RUNTIME_BUILD=2026.3.0-22451-8a17657b995-releases/2026/3\n" +
        "OPENVINO_MEASURED_GENAI_BUILD=2026.3.0.0-3277-bd8d6542e3c\n" +
        "OPENVINO_MEASURED_TOKENIZERS_BUILD=2026.3.0.0-703-183c6f25cda\n" +
        "OPENVINO_MEASURED_CANCELLATION_DISPOSITION=passed\n" +
        "OPENVINO_MEASURED_CLEANUP_DISPOSITION=zero_residue\n";

    private static string ValidEvidence() =>
        "{\"schemaVersion\":1," +
        "\"commitSha\":\"0123456789abcdef0123456789abcdef01234567\"," +
        "\"dependencyLockIdentities\":{" +
        "\"runtimeLockSha256\":\"1111111111111111111111111111111111111111111111111111111111111111\"," +
        "\"genAiLockSha256\":\"2222222222222222222222222222222222222222222222222222222222222222\"," +
        "\"tokenizersLockSha256\":\"3333333333333333333333333333333333333333333333333333333333333333\"}," +
        "\"fixtureManifestSha256\":\"4444444444444444444444444444444444444444444444444444444444444444\"," +
        "\"workerManifestSha256\":\"5555555555555555555555555555555555555555555555555555555555555555\"," +
        "\"requestedDevice\":\"CPU\"," +
        "\"actualExecutionDevices\":[\"CPU\"]," +
        "\"cpuIdentity\":{\"architecture\":\"X64\",\"vendor\":\"Intel\"}," +
        "\"runtimeBuildIdentity\":{" +
        "\"runtime\":\"2026.3.0-22451-8a17657b995-releases/2026/3\"," +
        "\"genAi\":\"2026.3.0.0-3277-bd8d6542e3c\"," +
        "\"tokenizers\":\"2026.3.0.0-703-183c6f25cda\"}," +
        "\"testCounts\":{\"contracts\":60,\"staticInspection\":177," +
        "\"nativeUnit\":7,\"workerClient\":13,\"processContainment\":41," +
        "\"appAdapter\":35,\"packageTamper\":5}," +
        "\"performanceAggregates\":{\"sampleCount\":3," +
        "\"durationMillisecondsMinimum\":1000,\"durationMillisecondsMedian\":2000," +
        "\"durationMillisecondsMaximum\":3000}," +
        "\"cancellationDisposition\":\"passed\"," +
        "\"cleanupDisposition\":\"zero_residue\"}";

    private static ScriptResult RunPrivacyVerifier(string evidencePath)
        => RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoEvidencePrivacy.ps1",
            "-EvidencePath",
            evidencePath);

    private static ScriptResult RunPowerShellScript(
        string relativeScript,
        params string[] arguments) => RunPowerShellScriptWithEnvironment(
            relativeScript,
            environment: null,
            arguments);

    private static ScriptResult RunPowerShellScriptWithEnvironment(
        string relativeScript,
        IReadOnlyDictionary<string, string?>? environment,
        params string[] arguments)
    {
        ProcessStartInfo start = new("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (environment is not null)
        {
            foreach ((string name, string? value) in environment)
            {
                start.Environment[name] = value;
            }
        }
        foreach (string argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", RepoPath(relativeScript)
        })
        {
            start.ArgumentList.Add(argument);
        }
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ScriptResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GitHead()
    {
        ProcessStartInfo start = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Root
        };
        start.ArgumentList.Add("rev-parse");
        start.ArgumentList.Add("HEAD");
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, error);
        return output;
    }

    private static void AssertTrustedInputsInvalid(
        string[] commonArguments,
        string? replacementName = null,
        string? replacementValue = null)
    {
        string[] arguments = commonArguments.ToArray();
        if (replacementName is not null)
        {
            int index = Array.IndexOf(arguments, replacementName);
            Assert.IsGreaterThanOrEqualTo(0, index);
            arguments[index + 1] = replacementValue!;
        }

        ScriptResult result = RunPowerShellScript(
            "scripts/openvino/Test-OpenVinoTrustedInputs.ps1",
            arguments);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual("trusted_inputs_invalid", result.StandardOutput.Trim());
        Assert.IsFalse(result.StandardOutput.Contains("C:\\", StringComparison.Ordinal));
    }

    private static string LowerSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            File.Copy(
                file,
                Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static void SetFilesReadOnly(string root, bool readOnly)
    {
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            FileInfo info = new(file) { IsReadOnly = readOnly };
        }
    }

    private static void ValidateWorkflowStructure(string source, bool trusted)
    {
        YamlMappingNode root = ParseWorkflow(source);
        YamlMappingNode permissions = Mapping(root, "permissions");
        Assert.AreEqual(1, permissions.Children.Count);
        Assert.AreEqual("read", Scalar(permissions, "contents"));

        YamlMappingNode triggers = Mapping(root, "on");
        string jobName = trusted ? "trusted-intel-cpu" : "official-cpu";
        if (trusted)
        {
            Assert.AreEqual(1, triggers.Children.Count);
            _ = Mapping(triggers, "workflow_dispatch");
        }
        else
        {
            foreach (string trigger in new[] { "push", "pull_request", "workflow_dispatch" })
            {
                Assert.IsTrue(triggers.Children.ContainsKey(new YamlScalarNode(trigger)), trigger);
            }
        }

        YamlMappingNode job = Mapping(Mapping(root, "jobs"), jobName);
        Assert.IsTrue(int.Parse(Scalar(job, "timeout-minutes")) > 0);
        if (trusted)
        {
            Assert.AreEqual("openvino-ucl-01", Scalar(job, "environment"));
            CollectionAssert.AreEqual(
                new[] { "self-hosted", "Windows", "X64", "workbook05", "intel-target" },
                Sequence(job, "runs-on").Select(Scalar).ToArray());
        }
        else
        {
            Assert.AreEqual("windows-2022", Scalar(job, "runs-on"));
        }

        YamlMappingNode rootEnvironment = Mapping(root, "env");
        const string expectedFile = "artifacts/openvino/evidence/cpu.json";
        Assert.AreEqual(expectedFile, Scalar(rootEnvironment, "OPENVINO_EVIDENCE_FILE"));
        if (trusted)
        {
            Assert.AreEqual(
                "${{ inputs.authorization }}",
                Scalar(rootEnvironment, "OPENVINO_UCL_AUTHORIZATION"));
            Assert.AreEqual(
                "${{ inputs.target_commit }}",
                Scalar(rootEnvironment, "OPENVINO_EVIDENCE_COMMIT"));
        }

        YamlSequenceNode stepNodes = Sequence(job, "steps");
        YamlMappingNode[] steps = stepNodes.Children.Select(AsMapping).ToArray();
        foreach (YamlMappingNode step in steps.Where(HasRun))
        {
            Assert.IsTrue(step.Children.ContainsKey(new YamlScalarNode("timeout-minutes")),
                "Every run step requires an explicit timeout.");
            Assert.IsFalse(
                Scalar(step, "run").Contains("${{ inputs.", StringComparison.Ordinal),
                "Dispatch inputs must never be interpolated into a run block.");
        }

        YamlMappingNode checkout = StepUsing(steps, $"actions/checkout@{CheckoutSha}");
        YamlMappingNode checkoutWith = Mapping(checkout, "with");
        Assert.AreEqual("${{ env.OPENVINO_EVIDENCE_COMMIT }}", Scalar(checkoutWith, "ref"));
        Assert.AreEqual("false", Scalar(checkoutWith, "persist-credentials"));

        YamlMappingNode evidence = Step(steps, trusted
            ? "Run retained trusted UCL CPU campaign"
            : "Create typed sanitized official CPU evidence");
        string evidenceRun = Scalar(evidence, "run");
        StringAssert.Contains(evidenceRun, "$env:OPENVINO_EVIDENCE_FILE");
        if (trusted)
        {
            StringAssert.Contains(evidenceRun, "Invoke-OpenVinoUclCampaign.ps1");
            StringAssert.Contains(evidenceRun, "-ArchiveRoot $env:OPENVINO_ARCHIVES");
            StringAssert.Contains(evidenceRun, "-FixtureRoot $env:OPENVINO_CONTROLLED_FIXTURE");
        }
        else
        {
            StringAssert.Contains(evidenceRun, "-MeasurementsPath $env:OPENVINO_MEASUREMENTS");
            Assert.IsFalse(evidenceRun.Contains("-TestResultsDirectory", StringComparison.Ordinal));
            YamlMappingNode measurements = Step(steps, "Create closed measured evidence inputs");
            string measurementRun = Scalar(measurements, "run");
            StringAssert.Contains(measurementRun, "New-OpenVinoEvidenceMeasurements.ps1");
            StringAssert.Contains(measurementRun, "-MeasurementsPath $env:OPENVINO_MEASUREMENTS");
            StringAssert.Contains(measurementRun, "openvino_measurements_created");
        }

        YamlMappingNode integrity = Step(steps, trusted
            ? "Clean operation-owned outputs after retained campaign"
            : "Verify post-run integrity and clean operation-owned state");
        Assert.AreEqual("${{ always() }}", Scalar(integrity, "if"));

        YamlMappingNode privacy = Step(steps, trusted
            ? "Privacy-check exact sanitized UCL evidence"
            : "Privacy-check exact sanitized official evidence");
        Assert.AreEqual("${{ always() }}", Scalar(privacy, "if"));
        string privacyRun = Scalar(privacy, "run");
        StringAssert.Contains(privacyRun, "$env:OPENVINO_EVIDENCE_FILE");
        StringAssert.Contains(privacyRun, "Get-ChildItem");
        StringAssert.Contains(privacyRun, "Count -ne 1");
        StringAssert.Contains(privacyRun, "Test-OpenVinoEvidenceArtifactSet.ps1");

        YamlMappingNode[] cpuUploads = steps
            .Where(step => step.Children.TryGetValue(
                    new YamlScalarNode("uses"),
                    out YamlNode? uses) &&
                Scalar(uses) == $"actions/upload-artifact@{UploadArtifactSha}")
            .Where(step => Scalar(Mapping(step, "with"), "path") == expectedFile)
            .ToArray();
        Assert.HasCount(1, cpuUploads);
        YamlMappingNode upload = cpuUploads[0];
        YamlMappingNode uploadWith = Mapping(upload, "with");
        Assert.AreEqual(expectedFile, Scalar(uploadWith, "path"));
        Assert.IsFalse(Scalar(uploadWith, "path").Contains('*'));
        Assert.AreEqual("30", Scalar(uploadWith, "retention-days"));
        StringAssert.Contains(Scalar(upload, "if"), "steps.privacy.outcome == 'success'");

        YamlMappingNode localCleanup = Step(
            steps,
            "Remove operation-owned local evidence residue");
        Assert.AreEqual("${{ always() }}", Scalar(localCleanup, "if"));
        string localCleanupRun = Scalar(localCleanup, "run");
        StringAssert.Contains(localCleanupRun, "$env:OPENVINO_EVIDENCE_FILE");
        StringAssert.Contains(localCleanupRun, "Remove-Item -LiteralPath");
        Assert.IsFalse(localCleanupRun.Contains("-Recurse", StringComparison.Ordinal));
        Assert.IsTrue(
            Array.IndexOf(steps, upload) < Array.IndexOf(steps, localCleanup),
            "The exact artifact must be uploaded before local evidence cleanup.");

        if (!trusted)
        {
            YamlMappingNode native = Step(steps, "Record and validate native unit count");
            string nativeRun = Scalar(native, "run");
            StringAssert.Contains(nativeRun, "Test-OpenVinoNativeJUnit.ps1");
            StringAssert.Contains(nativeRun, "-ResultsPath");
            StringAssert.Contains(nativeRun, "-MinimumCount 7");
        }

        if (trusted)
        {
            string preflight = Scalar(
                Step(steps, "Fail closed before repository code on UCL-01 and immutable inputs"),
                "run");
            StringAssert.Contains(preflight, "$env:OPENVINO_UCL_AUTHORIZATION");
            Assert.IsFalse(preflight.Contains("${{", StringComparison.Ordinal));

            Assert.AreEqual("evidence", Scalar(evidence, "id"));
        }
    }

    private static YamlMappingNode ParseWorkflow(string source)
    {
        YamlStream stream = new();
        stream.Load(new StringReader(source));
        Assert.HasCount(1, stream.Documents);
        return AsMapping(stream.Documents[0].RootNode);
    }

    private static CommandAst SingleCommand(string source, string commandName)
    {
        Token[] tokens;
        ParseError[] errors;
        ScriptBlockAst ast = Parser.ParseInput(source, out tokens, out errors);
        Assert.HasCount(0, errors);
        CommandAst[] commands = ast.FindAll(node => node is CommandAst, searchNestedScriptBlocks: true)
            .Cast<CommandAst>()
            .Where(command => string.Equals(
                command.GetCommandName(), commandName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.HasCount(1, commands);
        return commands[0];
    }

    private static void AssertPowerShellCommandArgument(
        YamlMappingNode step,
        string commandName,
        string requiredArgument)
    {
        CommandAst command = SingleCommand(Scalar(step, "run"), commandName);
        Assert.IsTrue(command.CommandElements.Any(element => element.Extent.Text.Contains(
            requiredArgument, StringComparison.Ordinal)),
            $"{Scalar(step, "name")} lacks executable argument {requiredArgument}.");
    }

    private static YamlMappingNode Mapping(YamlMappingNode parent, string key)
    {
        Assert.IsTrue(parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value), key);
        return AsMapping(value!);
    }

    private static YamlSequenceNode Sequence(YamlMappingNode parent, string key)
    {
        Assert.IsTrue(parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value), key);
        return Assert.IsInstanceOfType<YamlSequenceNode>(value);
    }

    private static string Scalar(YamlMappingNode parent, string key)
    {
        Assert.IsTrue(parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value), key);
        return Scalar(value!);
    }

    private static string Scalar(YamlNode node) =>
        Assert.IsInstanceOfType<YamlScalarNode>(node).Value ?? string.Empty;

    private static YamlMappingNode AsMapping(YamlNode node) =>
        Assert.IsInstanceOfType<YamlMappingNode>(node);

    private static bool HasRun(YamlMappingNode step) =>
        step.Children.ContainsKey(new YamlScalarNode("run"));

    private static YamlMappingNode Step(
        IEnumerable<YamlMappingNode> steps,
        string name) => steps.Single(step => Scalar(step, "name") == name);

    private static YamlMappingNode StepUsing(
        IEnumerable<YamlMappingNode> steps,
        string action) => steps.Single(step =>
            step.Children.TryGetValue(new YamlScalarNode("uses"), out YamlNode? value) &&
            Scalar(value) == action);

    private static IEnumerable<string> Workflows()
    {
        yield return RepoPath(".github/workflows/openvino-official-ci.yml");
        yield return RepoPath(".github/workflows/openvino-ucl-intel.yml");
    }

    private static string OfficialWorkflow() =>
        Normalize(File.ReadAllText(RepoPath(".github/workflows/openvino-official-ci.yml")));

    private static string UclWorkflow() =>
        Normalize(File.ReadAllText(RepoPath(".github/workflows/openvino-ucl-intel.yml")));

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string RepoPath(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI.OpenVino.Evidence",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                EnumerationOptions options = new()
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = true
                };
                foreach (string entry in Directory.EnumerateFileSystemEntries(
                    Path,
                    "*",
                    options))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
