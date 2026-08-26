Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ManifestPaths = @(
    'docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json'
    'docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md'
)
$script:StagingMethod = 'verified no-filter blob hashing and literal update-index cacheinfo'
$script:ExpectedAuthorities = @(
    [ordered]@{
        name = 'planning-base'
        ref = 'fix/hardware-inspection-loq-baseline'
        commit = '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
    }
    [ordered]@{
        name = 'canonical-optimisation-ui'
        ref = 'origin/feature/cross-route-optimisation-ui-v1'
        commit = '8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8'
    }
    [ordered]@{
        name = 'openvino-route'
        ref = 'origin/feature/openvino-optimisation-adapter-v1'
        commit = 'f0189ed187ba900f27bade5fde282ae4e99e8d7b'
    }
    [ordered]@{
        name = 'historical-cross-route-contracts'
        ref = 'origin/feature/cross-route-optimisation-contracts-v2-1'
        commit = 'e254385997392601102b16acf19244437803bdcc'
    }
    [ordered]@{
        name = 'production-gguf-runtime'
        ref = 'origin/feature/gguf-cli-chat-production'
        commit = 'bacb3f4106e0191b05b870358342f8158765396d'
    }
    [ordered]@{
        name = 'llama-cpp-quantiser'
        ref = 'https://github.com/ggml-org/llama.cpp.git'
        commit = '3f7c29d318e317b63f54c558bc69803963d7d88c'
    }
)
function Get-ApprovedComponentPolicy {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateSet('UO1', 'GgufRuntime', 'OpenVino')][string]$Component)

    if ($Component -ceq 'UO1') {
        return [ordered]@{
        productionPolicyId = 'UO1-v1'
        manifestName = 'UO1'
        sourceRef = 'origin/feature/cross-route-optimisation-ui-v1'
        sourceCommit = '8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8'
        permittedCommits = @('8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8', '092589c38981ad86bb73c7c97dff01ab8b5a6c8e')
        exactSourceCommits = @('8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8')
        dependencyClosure = @(
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/**'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**'
        )
        excludedSharedPaths = @(
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml.cs'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationSelectionLayoutTests.cs'
        )
        patchPathRules = @('docs/handoffs/import-patches/uo1-v3-adaptation.patch')
        allowedKinds = @('Exact', 'Adapted', 'Created')
        adaptedBaseTrees = @{
            '8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8' = '65a265e28a03fa5702773a2f2d5b877a5b5e7011'
        }
        allowAdaptedIntegrationAncestor = $false
        createdBaseCommit = '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
        createdBaseTree = 'cc87f57b1239299976793fd54e806a5734273f56'
        requireCreatedDestinationHead = $false
        integrationOnlyPaths = @()
        verificationCommands = @(
            "powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.ModelOptimization'"
        )
        integrationBaseRoles = @{}
        }
    }
    if ($Component -ceq 'GgufRuntime') {
        return [ordered]@{
        productionPolicyId = 'GgufRuntime-v1'
        manifestName = 'GGUF runtime'
        sourceRef = 'origin/feature/gguf-cli-chat-production'
        sourceCommit = 'bacb3f4106e0191b05b870358342f8158765396d'
        permittedCommits = @('bacb3f4106e0191b05b870358342f8158765396d', '092589c38981ad86bb73c7c97dff01ab8b5a6c8e')
        exactSourceCommits = @('bacb3f4106e0191b05b870358342f8158765396d')
        dependencyClosure = @(
            'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/**'
            'shared/GraniteEdgeAI.GgufRuntime.Contracts/**'
            'shared/GraniteEdgeAI.GgufRuntime.Transport/**'
            'runtime/GraniteEdgeAI.GgufRuntime.Capabilities/**'
            'runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/**'
            'infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/**'
            'workers/GraniteEdgeAI.GgufRuntime.Worker/**'
            'IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets'
            'scripts/gguf-runtime/**'
            'scripts/Run-ChatPreview.ps1'
            'third-party/licenses/LICENSE.LLamaSharp.txt'
            'third-party/licenses/LICENSE.llama.cpp.txt'
            'tests/TestFixtures/Generate-GgufFixtures.ps1'
            'tests/TestFixtures/Generate-GgufHeaderFixtures.ps1'
            'tests/TestFixtures/Generate-GgufMetadataFixtures.ps1'
            'tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/**'
            'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/**'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufFixtureIntegrityTests.cs'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs'
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
        )
        excludedSharedPaths = @(
            'IBM Granite with TurboQuant (Intel)/Features/ModelImport/**'
            'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/**'
            'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/**'
            'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**'
            'IBM Granite with TurboQuant (Intel)/Features/Onboarding/**'
            'IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs'
            'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufInspectedModelLaunchFactoryTests.cs'
        )
        patchPathRules = @('docs/handoffs/import-patches/gguf-runtime-integration.patch')
        allowedKinds = @('Exact', 'Adapted', 'Created')
        adaptedBaseTrees = @{
            'bacb3f4106e0191b05b870358342f8158765396d' = '1adcbf5de05fc924d24742a7f4cf7b87a9140e81'
            '092589c38981ad86bb73c7c97dff01ab8b5a6c8e' = 'cc87f57b1239299976793fd54e806a5734273f56'
        }
        allowAdaptedIntegrationAncestor = $false
        createdBaseCommit = '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
        createdBaseTree = 'cc87f57b1239299976793fd54e806a5734273f56'
        requireCreatedDestinationHead = $false
        integrationOnlyPaths = @(
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
        )
        verificationCommands = @(
            'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1'
            'dotnet test --project $core --configuration Release'
            "powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufRuntime'"
        )
        integrationBaseRoles = @{}
        }
    }
    return [ordered]@{
        productionPolicyId = 'OpenVino-v1'
        manifestName = 'OpenVINO route'
        sourceRef = 'origin/feature/openvino-optimisation-adapter-v1'
        sourceCommit = 'f0189ed187ba900f27bade5fde282ae4e99e8d7b'
        permittedCommits = @('f0189ed187ba900f27bade5fde282ae4e99e8d7b', 'e254385997392601102b16acf19244437803bdcc', '092589c38981ad86bb73c7c97dff01ab8b5a6c8e')
        exactSourceCommits = @('f0189ed187ba900f27bade5fde282ae4e99e8d7b', 'e254385997392601102b16acf19244437803bdcc')
        dependencyClosure = @(
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**'
            'shared/GraniteEdgeAI.OpenVino.Contracts/**'
            'infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**'
            'IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteContract.cs'
            'IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteRegistry.cs'
            'IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptSessionPresenter.cs'
            'workers/OpenVinoConverter.Worker/**'
            'workers/OpenVinoOfficial.Worker/**'
            'workers/OpenVinoTurboQuant.Worker/**'
            'third-party/openvino-converter/**'
            'third-party/openvino-official/**'
            'third-party/openvino-turboquant/**'
            'scripts/openvino/**'
            'tests/TestFixtures/OpenVINO/**'
            'IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ArchitectureBoundaryTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/DependencyLockContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/FixtureContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GpuDeviceContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolJsonTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolSequenceTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/SupportCodeTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/Task7ProtocolExtensionTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj'
            'tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/**'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/**'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**'
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
            'scripts/gguf-runtime/Invoke-GgufChatVerification.ps1'
        )
        excludedSharedPaths = @(
            'IBM Granite with TurboQuant (Intel)/Features/ModelImport/**'
            'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/**'
            'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/**'
            'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**'
            'IBM Granite with TurboQuant (Intel)/Features/Onboarding/**'
            'IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs'
            'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoV2TestPayload.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/WorkflowContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/TurboQuantSourceContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/PackagingContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/packages.lock.json'
        )
        patchPathRules = @(
            'docs/handoffs/import-patches/openvino-v3-adaptation.patch'
            'docs/handoffs/import-patches/openvino-shared-integration.patch'
            'docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch'
        )
        allowedKinds = @('Exact', 'Adapted', 'Created')
        adaptedBaseTrees = @{
            'f0189ed187ba900f27bade5fde282ae4e99e8d7b' = '16f438d7a987f1094bb39bc0d1e8089d35af81f3'
            'e254385997392601102b16acf19244437803bdcc' = 'c71621efa4beb18a0b2eb049bb1ef386d0678ba6'
        }
        allowAdaptedIntegrationAncestor = $true
        createdBaseCommit = $null
        createdBaseTree = $null
        requireCreatedDestinationHead = $true
        integrationOnlyPaths = @(
            'IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**'
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
            'scripts/gguf-runtime/Invoke-GgufChatVerification.ps1'
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs'
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj'
        )
        verificationCommands = @(
            'dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false'
            'dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false'
            "dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~OptimizationEndToEndTests'"
            "powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoProductionCompositionTests'"
        )
        integrationBaseRoles = @{
            'docs/handoffs/import-patches/openvino-shared-integration.patch' = [ordered]@{
                role = 'destination-head'
                memberRules = @(
                    'IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets'
                    'IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets'
                    'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**'
                    'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
                    'IBM Granite with TurboQuant (Intel).slnx'
                    'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
                    'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs'
                    'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs'
                    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs'
                    'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj'
                )
            }
            'docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch' = [ordered]@{
                role = 'task11-gguf-verifier'
                memberRules = @('scripts/gguf-runtime/Invoke-GgufChatVerification.ps1')
            }
        }
    }
}
$script:TrustedGitIdentity = $null

if (-not ('CrossRouteProcessJob' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class CrossRouteCappedMemoryStream : MemoryStream
{
    private readonly long maximumLength;

    public CrossRouteCappedMemoryStream(long maximumLength)
    {
        if (maximumLength <= 0) throw new ArgumentOutOfRangeException("maximumLength");
        this.maximumLength = maximumLength;
    }

    private void EnsureCapacityFor(int count)
    {
        if (count < 0 || Length > maximumLength - count)
            throw new IOException("Bounded child process output exceeded its byte limit.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacityFor(count);
        base.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureCapacityFor(count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }
}

public sealed class CrossRouteOwnedProcess : IDisposable
{
    public Process Process { get; private set; }
    public FileStream StandardOutput { get; private set; }
    public FileStream StandardError { get; private set; }
    private IntPtr nativeProcess;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public CrossRouteOwnedProcess(Process process, IntPtr nativeProcessHandle, FileStream standardOutput, FileStream standardError)
    {
        Process = process;
        nativeProcess = nativeProcessHandle;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int GetAuthoritativeExitCode()
    {
        uint value;
        if (nativeProcess == IntPtr.Zero || !GetExitCodeProcess(nativeProcess, out value))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read authoritative child exit code.");
        if (value == 259) throw new InvalidOperationException("Child process was still active after its bounded wait.");
        return unchecked((int)value);
    }

    public void Dispose()
    {
        if (StandardOutput != null) { StandardOutput.Dispose(); StandardOutput = null; }
        if (StandardError != null) { StandardError.Dispose(); StandardError = null; }
        if (Process != null) { Process.Dispose(); Process = null; }
        if (nativeProcess != IntPtr.Zero) { CloseHandle(nativeProcess); nativeProcess = IntPtr.Zero; }
    }
}

public static class CrossRouteSafeDelete
{
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadAttributes = 0x00000080;
    private const uint WriteAttributes = 0x00000100;
    private const uint ShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint ReparsePoint = 0x00000400;
    private const uint ReadOnly = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInfo { public long CreationTime, LastAccessTime, LastWriteTime, ChangeTime; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct DispositionInfo { [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(IntPtr handle, int informationClass, out BasicInfo information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(IntPtr handle, int informationClass, ref BasicInfo information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(IntPtr handle, int informationClass, ref DispositionInfo information, uint size);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static void DeleteTree(string path, int timeoutMilliseconds)
    {
        if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
        DeleteTree(path, DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds).Ticks);
    }

    public static void DeleteOwnedTree(string path, string protectedMarker, FileStream ownerStream, int timeoutMilliseconds)
    {
        if (ownerStream == null) throw new ArgumentNullException("ownerStream");
        if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
        string root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string marker = Path.GetFullPath(protectedMarker);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(marker), root))
            throw new IOException("Owned cleanup marker is not an immediate child of its root.");
        long deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds).Ticks;
        IntPtr rootHandle = CreateFile(root, DeleteAccess | ReadAttributes | WriteAttributes, ShareReadWrite, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (rootHandle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to lock exact owned cleanup root.");
        try
        {
            BasicInfo basic;
            if (!GetFileInformationByHandleEx(rootHandle, 0, out basic, (uint)Marshal.SizeOf(typeof(BasicInfo))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to inspect exact owned cleanup root.");
            if ((basic.Attributes & ReparsePoint) != 0 || (basic.Attributes & 0x10) == 0)
                throw new IOException("Owned reconstruction root was rebound or replaced.");
            foreach (string child in Directory.GetFileSystemEntries(root))
                if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(child), marker)) DeleteTree(child, deadline);
            string[] remaining = Directory.GetFileSystemEntries(root);
            if (remaining.Length != 1 || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(remaining[0]), marker))
                throw new IOException("Owned reconstruction cleanup boundary changed during traversal.");
            ownerStream.Dispose();
            DeleteTree(marker, deadline);
            if ((basic.Attributes & ReadOnly) != 0)
            {
                basic.Attributes &= ~ReadOnly;
                if (basic.Attributes == 0) basic.Attributes = 0x80;
                if (!SetFileInformationByHandle(rootHandle, 0, ref basic, (uint)Marshal.SizeOf(typeof(BasicInfo))))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to clear exact owned cleanup root attributes.");
            }
            DispositionInfo disposition = new DispositionInfo { DeleteFile = true };
            if (!SetFileInformationByHandle(rootHandle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(DispositionInfo))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to delete exact owned cleanup root.");
        }
        finally { CloseHandle(rootHandle); }
    }

    private static void DeleteTree(string path, long deadlineTicks)
    {
        if (DateTime.UtcNow.Ticks >= deadlineTicks) throw new TimeoutException("Exact owned cleanup exceeded its bounded duration.");
        IntPtr handle = CreateFile(path, DeleteAccess | ReadAttributes | WriteAttributes, ShareReadWrite, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle == new IntPtr(-1))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2 || error == 3) return;
            throw new Win32Exception(error, "Unable to open exact owned cleanup entry.");
        }
        try
        {
            BasicInfo basic;
            if (!GetFileInformationByHandleEx(handle, 0, out basic, (uint)Marshal.SizeOf(typeof(BasicInfo))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to inspect exact owned cleanup entry.");
            if ((basic.Attributes & ReparsePoint) == 0 && (basic.Attributes & 0x10) != 0)
            {
                foreach (string child in Directory.GetFileSystemEntries(path)) DeleteTree(child, deadlineTicks);
            }
            if ((basic.Attributes & ReadOnly) != 0)
            {
                basic.Attributes &= ~ReadOnly;
                if (basic.Attributes == 0) basic.Attributes = 0x80;
                if (!SetFileInformationByHandle(handle, 0, ref basic, (uint)Marshal.SizeOf(typeof(BasicInfo))))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to clear exact owned cleanup attributes.");
            }
            DispositionInfo disposition = new DispositionInfo { DeleteFile = true };
            if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(DispositionInfo))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to delete exact owned cleanup entry.");
        }
        finally { CloseHandle(handle); }
    }
}

public static class CrossRoutePathspecPublisher
{
    private const uint ReadAttributes = 0x00000080;
    private const uint ShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint ReparsePoint = 0x00000400;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low, High; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleInformation
    {
        public uint Attributes;
        public FileTime CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(IntPtr handle, out ByHandleInformation information);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static string Identity(IntPtr handle)
    {
        ByHandleInformation value;
        if (!GetFileInformationByHandle(handle, out value)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to bind pathspec parent identity.");
        if ((value.Attributes & ReparsePoint) != 0) throw new IOException("Pathspec parent cannot be a reparse point.");
        return value.VolumeSerialNumber.ToString("x8") + ":" + value.FileIndexHigh.ToString("x8") + value.FileIndexLow.ToString("x8");
    }

    private static IntPtr OpenLockedDirectory(string directory)
    {
        IntPtr handle = CreateFile(directory, ReadAttributes, ShareReadWrite, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to lock pathspec parent directory.");
        return handle;
    }

    public static string CaptureParentIdentity(string directory)
    {
        IntPtr handle = OpenLockedDirectory(directory);
        try { return Identity(handle); }
        finally { CloseHandle(handle); }
    }

    public static void WriteNew(string path, string expectedParentIdentity, byte[] bytes)
    {
        string full = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(full);
        IntPtr handle = OpenLockedDirectory(parent);
        try
        {
            if (!StringComparer.Ordinal.Equals(Identity(handle), expectedParentIdentity)) throw new IOException("Pathspec parent identity changed before publication.");
            if (File.Exists(full) || Directory.Exists(full)) throw new IOException("Pathspec publication target is no longer absent.");
            string temporary = Path.Combine(parent, ".geai-pathspec-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool owned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    owned = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, full);
                owned = false;
            }
            finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { CloseHandle(handle); }
    }
}

public static class CrossRouteAtomicIndexPublisher
{
    private const uint MOVEFILE_REPLACE_EXISTING = 0x1;
    private const uint MOVEFILE_WRITE_THROUGH = 0x8;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingName, string newName, uint flags);

    public static void Replace(string ownedLockPath, string indexPath)
    {
        if (!MoveFileEx(ownedLockPath, indexPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to atomically publish the verified index.");
    }
}

public sealed class CrossRouteProcessJob : IDisposable
{
    private IntPtr handle;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr value);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out IntPtr readPipe, out IntPtr writePipe, ref SecurityAttributes attributes, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, ref SecurityAttributes attributes, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfoEx startupInfo, out ProcessInformation processInformation);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr attributeList, int attributeCount, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr attributeList, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int InheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo { public int Size; public string Reserved; public string Desktop; public string Title; public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags; public short ShowWindow, Reserved2; public IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError; }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit { public BasicLimit BasicLimitInformation; public IoCounters IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }

    public CrossRouteProcessJob()
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create process job.");
        var value = new ExtendedLimit();
        value.BasicLimitInformation.LimitFlags = 0x00002000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        int size = Marshal.SizeOf(typeof(ExtendedLimit));
        IntPtr memory = Marshal.AllocHGlobal(size);
        try {
            Marshal.StructureToPtr(value, memory, false);
            if (!SetInformationJobObject(handle, 9, memory, (uint)size)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to configure process job.");
        }
        catch { Dispose(); throw; }
        finally { Marshal.FreeHGlobal(memory); }
    }

    public void Assign(System.Diagnostics.Process process)
    {
        if (!AssignProcessToJobObject(handle, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to assign process to owned job.");
    }

    public CrossRouteOwnedProcess StartSuspended(string application, string commandLine, string workingDirectory, IDictionary environment)
    {
        const uint HandleFlagInherit = 1;
        const uint GenericRead = 0x80000000;
        const uint ShareReadWrite = 3;
        const uint OpenExisting = 3;
        const uint FileAttributeNormal = 0x80;
        const uint StartfUseStdHandles = 0x100;
        const uint CreateSuspended = 0x4; // CREATE_SUSPENDED
        const uint CreateUnicodeEnvironment = 0x400;
        const uint CreateNoWindow = 0x08000000;
        const uint ExtendedStartupInfoPresent = 0x00080000;
        const int ProcThreadAttributeHandleList = 0x00020002; // PROC_THREAD_ATTRIBUTE_HANDLE_LIST
        IntPtr stdoutRead = IntPtr.Zero, stdoutWrite = IntPtr.Zero, stderrRead = IntPtr.Zero, stderrWrite = IntPtr.Zero, stdin = IntPtr.Zero, environmentBlock = IntPtr.Zero, attributeList = IntPtr.Zero, inheritedHandles = IntPtr.Zero;
        ProcessInformation information = new ProcessInformation();
        Process managedProcess = null;
        try
        {
            SecurityAttributes attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), InheritHandle = 1 };
            if (!CreatePipe(out stdoutRead, out stdoutWrite, ref attributes, 0) || !SetHandleInformation(stdoutRead, HandleFlagInherit, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create bounded stdout pipe.");
            if (!CreatePipe(out stderrRead, out stderrWrite, ref attributes, 0) || !SetHandleInformation(stderrRead, HandleFlagInherit, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create bounded stderr pipe.");
            stdin = CreateFile("NUL", GenericRead, ShareReadWrite, ref attributes, OpenExisting, FileAttributeNormal, IntPtr.Zero);
            if (stdin == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open bounded child stdin.");

            List<string> entries = new List<string>();
            foreach (DictionaryEntry entry in environment) entries.Add((string)entry.Key + "=" + (string)entry.Value);
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            byte[] environmentBytes = Encoding.Unicode.GetBytes(string.Join("\0", entries.ToArray()) + "\0\0");
            environmentBlock = Marshal.AllocHGlobal(environmentBytes.Length);
            Marshal.Copy(environmentBytes, 0, environmentBlock, environmentBytes.Length);

            IntPtr attributeBytes = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeBytes);
            attributeList = Marshal.AllocHGlobal(attributeBytes);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeBytes)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to initialize the inherited-handle allowlist.");
            inheritedHandles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(inheritedHandles, 0, stdin);
            Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, stdoutWrite);
            Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size * 2, stderrWrite);
            if (!UpdateProcThreadAttribute(attributeList, 0, new IntPtr(ProcThreadAttributeHandleList), inheritedHandles, new IntPtr(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to bind the inherited-handle allowlist.");

            StartupInfoEx startup = new StartupInfoEx();
            startup.StartupInfo.Size = Marshal.SizeOf(typeof(StartupInfoEx));
            startup.StartupInfo.Flags = StartfUseStdHandles;
            startup.StartupInfo.StandardInput = stdin;
            startup.StartupInfo.StandardOutput = stdoutWrite;
            startup.StartupInfo.StandardError = stderrWrite;
            startup.AttributeList = attributeList;
            if (!CreateProcess(application, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true, CreateSuspended | CreateUnicodeEnvironment | CreateNoWindow | ExtendedStartupInfoPresent, environmentBlock, workingDirectory, ref startup, out information))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create suspended bounded child process.");
            if (!AssignProcessToJobObject(handle, information.Process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to assign suspended process to owned job.");
            managedProcess = Process.GetProcessById((int)information.ProcessId);
            if (ResumeThread(information.Thread) == UInt32.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to resume owned child process.");
            CloseHandle(stdoutWrite); stdoutWrite = IntPtr.Zero;
            CloseHandle(stderrWrite); stderrWrite = IntPtr.Zero;
            FileStream stdoutStream = new FileStream(new SafeFileHandle(stdoutRead, true), FileAccess.Read, 4096, false); stdoutRead = IntPtr.Zero;
            FileStream stderrStream = new FileStream(new SafeFileHandle(stderrRead, true), FileAccess.Read, 4096, false); stderrRead = IntPtr.Zero;
            IntPtr retainedNativeProcess = information.Process; information.Process = IntPtr.Zero;
            return new CrossRouteOwnedProcess(managedProcess, retainedNativeProcess, stdoutStream, stderrStream);
        }
        catch
        {
            if (information.Process != IntPtr.Zero) TerminateProcess(information.Process, 1);
            if (managedProcess != null) managedProcess.Dispose();
            throw;
        }
        finally
        {
            if (information.Thread != IntPtr.Zero) CloseHandle(information.Thread);
            if (information.Process != IntPtr.Zero) CloseHandle(information.Process);
            if (stdoutRead != IntPtr.Zero) CloseHandle(stdoutRead);
            if (stdoutWrite != IntPtr.Zero) CloseHandle(stdoutWrite);
            if (stderrRead != IntPtr.Zero) CloseHandle(stderrRead);
            if (stderrWrite != IntPtr.Zero) CloseHandle(stderrWrite);
            if (stdin != IntPtr.Zero && stdin != new IntPtr(-1)) CloseHandle(stdin);
            if (environmentBlock != IntPtr.Zero) Marshal.FreeHGlobal(environmentBlock);
            if (attributeList != IntPtr.Zero) { DeleteProcThreadAttributeList(attributeList); Marshal.FreeHGlobal(attributeList); }
            if (inheritedHandles != IntPtr.Zero) Marshal.FreeHGlobal(inheritedHandles);
        }
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; }
    }
}
'@
}

function ConvertTo-WindowsProcessArgument {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + ([regex]::Replace($Value, '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

function Stop-OwnedProcessTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][datetime]$ExpectedStartTimeUtc
    )

    if ($Process.HasExited) {
        return
    }
    $live = [Diagnostics.Process]::GetProcessById($Process.Id)
    try {
        if ($live.StartTime.ToUniversalTime() -ne $ExpectedStartTimeUtc) {
            throw 'Timed-out process ownership validation failed.'
        }
    }
    finally {
        $live.Dispose()
    }
    $systemDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::System)
    $taskkillPath = Join-Path -Path $systemDirectory -ChildPath 'taskkill.exe'
    Assert-NoReparseAncestors -Path $taskkillPath -IncludeLeaf
    $taskkillBefore = Get-RegularFileIdentity -Path $taskkillPath -Label 'System32 taskkill'
    $start = New-Object -TypeName Diagnostics.ProcessStartInfo
    $start.FileName = $taskkillBefore.Path
    $start.Arguments = "/PID $($Process.Id) /T /F"
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $killer = [Diagnostics.Process]::Start($start)
    if ($null -eq $killer) {
        throw 'Unable to start the exact System32 taskkill executable.'
    }
    try {
        $stdoutTask = $killer.StandardOutput.ReadToEndAsync()
        $stderrTask = $killer.StandardError.ReadToEndAsync()
        if (-not $killer.WaitForExit(10000)) {
            $killer.Kill()
            if (-not $killer.WaitForExit(5000)) {
                throw 'Hung taskkill could not be terminated within five seconds.'
            }
            [void]$stdoutTask.GetAwaiter().GetResult()
            [void]$stderrTask.GetAwaiter().GetResult()
            throw 'System32 taskkill exceeded its ten-second bound.'
        }
        $taskkillOutput = $stdoutTask.GetAwaiter().GetResult()
        $taskkillError = $stderrTask.GetAwaiter().GetResult()
        if ($killer.ExitCode -ne 0) {
            [void]$taskkillOutput
            [void]$taskkillError
            throw "System32 taskkill failed with exit code $($killer.ExitCode)."
        }
    }
    finally {
        $killer.Dispose()
    }
    $taskkillAfter = Get-RegularFileIdentity -Path $taskkillBefore.Path -Label 'System32 taskkill'
    Assert-FileIdentityUnchanged -Before $taskkillBefore -After $taskkillAfter -Label 'System32 taskkill'
    if (-not $Process.HasExited -and -not $Process.WaitForExit(5000)) {
        throw 'Timed-out process root did not exit within five seconds after verified taskkill.'
    }
    if (-not $Process.HasExited) {
        throw 'Timed-out process root remained alive after verified taskkill.'
    }
}

function Invoke-CrossRouteProcessCore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [string[]]$Arguments = @(),
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 30,
        [hashtable]$Environment = @{}
    )

    $processEnvironment = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) {
        $processEnvironment[[string]$entry.Key] = [string]$entry.Value
    }
    foreach ($environmentName in @($processEnvironment.Keys)) {
        if ($environmentName.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)) {
            $processEnvironment.Remove($environmentName)
        }
    }
    foreach ($environmentName in $Environment.Keys) {
        $processEnvironment[[string]$environmentName] = [string]$Environment[$environmentName]
    }
    $commandLine = ConvertTo-WindowsProcessArgument -Value $FilePath
    if ($Arguments.Count -ne 0) {
        $commandLine += ' ' + (@($Arguments | ForEach-Object { ConvertTo-WindowsProcessArgument -Value ([string]$_) }) -join ' ')
    }
    $job = New-Object CrossRouteProcessJob
    $ownedProcess = $null
    $process = $null
    try {
        $ownedProcess = $job.StartSuspended($FilePath, $commandLine, $WorkingDirectory, $processEnvironment)
        $process = $ownedProcess.Process
    }
    catch {
        $job.Dispose()
        if ($null -ne $ownedProcess) { $ownedProcess.Dispose() }
        throw
    }

    $stdout = New-Object -TypeName CrossRouteCappedMemoryStream -ArgumentList ([long](16 * 1024 * 1024))
    $stderr = New-Object -TypeName CrossRouteCappedMemoryStream -ArgumentList ([long](2 * 1024 * 1024))
    try {
        $stdoutTask = $ownedProcess.StandardOutput.CopyToAsync($stdout)
        $stderrTask = $ownedProcess.StandardError.CopyToAsync($stderr)
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(100)) {
            if ($stdoutTask.IsFaulted -or $stderrTask.IsFaulted) {
                $job.Dispose()
                [void]$process.WaitForExit(5000)
                throw 'Bounded child process output exceeded its byte limit.'
            }
            if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $job.Dispose()
                [void]$process.WaitForExit(5000)
                [void][Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 5000)
                throw "Bounded child process timed out after $TimeoutSeconds seconds."
            }
        }
        $exitCode = [int]$ownedProcess.GetAuthoritativeExitCode()
        $streamsCompletedWithRoot = [Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 1000)
        $job.Dispose()
        if (-not $streamsCompletedWithRoot -and -not [Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 5000)) {
            throw 'Bounded child process descendants retained redirected streams after Job termination.'
        }
        if ($stdoutTask.IsFaulted -or $stderrTask.IsFaulted) {
            throw 'Bounded child process output exceeded its byte limit.'
        }
        [void]$stdoutTask.GetAwaiter().GetResult()
        [void]$stderrTask.GetAwaiter().GetResult()
        $bytes = $stdout.ToArray()
        $stderrBytes = $stderr.ToArray()
        $encoding = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $false)
        return [pscustomobject]@{
            ExitCode = $exitCode
            StdoutBytes = $bytes
            Stdout = $encoding.GetString($bytes)
            Stderr = $encoding.GetString($stderrBytes)
        }
    }
    finally {
        $job.Dispose()
        $stdout.Dispose()
        $stderr.Dispose()
        $ownedProcess.Dispose()
    }
}

function Invoke-CrossRouteProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [string[]]$Arguments = @(),
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 30
    )

    return Invoke-CrossRouteProcessCore -FilePath $FilePath -WorkingDirectory $WorkingDirectory -Arguments $Arguments -TimeoutSeconds $TimeoutSeconds
}

function Assert-NoReparseAncestors {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$IncludeLeaf
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $cursor = if ($IncludeLeaf) { $fullPath } else { Split-Path -Parent $fullPath }
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if ([IO.Directory]::Exists($cursor) -or [IO.File]::Exists($cursor)) {
            $item = Get-Item -Force -LiteralPath $cursor -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "ReparsePoint ancestry is forbidden: $cursor"
            }
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or [StringComparer]::OrdinalIgnoreCase.Equals($parent, $cursor)) {
            break
        }
        $cursor = $parent
    }
    return $fullPath
}

function Get-RegularFileIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    $fullPath = Assert-NoReparseAncestors -Path $Path -IncludeLeaf
    if (-not [IO.File]::Exists($fullPath)) {
        throw "$Label must exist as a regular file."
    }
    $item = Get-Item -Force -LiteralPath $fullPath -ErrorAction Stop
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Label must be an exact regular non-reparse file."
    }
    return [pscustomobject]@{
        Path = [string]$item.FullName
        Length = [long]$item.Length
        Sha256 = [string](Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        LastWriteTimeUtc = [datetime]$item.LastWriteTimeUtc
        CreationTimeUtc = [datetime]$item.CreationTimeUtc
    }
}

function Assert-FileIdentityUnchanged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject]$Before,
        [Parameter(Mandatory)][pscustomobject]$After,
        [Parameter(Mandatory)][string]$Label
    )

    if (
        -not [StringComparer]::OrdinalIgnoreCase.Equals([string]$Before.Path, [string]$After.Path) -or
        [long]$Before.Length -ne [long]$After.Length -or
        [string]$Before.Sha256 -cne [string]$After.Sha256 -or
        [datetime]$Before.LastWriteTimeUtc -ne [datetime]$After.LastWriteTimeUtc -or
        [datetime]$Before.CreationTimeUtc -ne [datetime]$After.CreationTimeUtc
    ) {
        throw "$Label identity changed during the bounded operation."
    }
}

function Get-TrustedGitIdentity {
    [CmdletBinding()]
    param()

    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $candidate = [IO.Path]::GetFullPath((Join-Path -Path $programFiles -ChildPath 'Git\cmd\git.exe'))
    $current = Get-RegularFileIdentity -Path $candidate -Label 'Git executable'
    if ($null -eq $script:TrustedGitIdentity) {
        $script:TrustedGitIdentity = $current
    }
    else {
        Assert-FileIdentityUnchanged -Before $script:TrustedGitIdentity -After $current -Label 'Git executable'
    }
    return $current
}

function Invoke-CrossRouteGit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure,
        [ValidateRange(1, 600)][int]$TimeoutSeconds = 30,
        [hashtable]$Environment = @{}
    )

    $gitEnvironment = @{
        GIT_NO_REPLACE_OBJECTS = '1'
        GIT_OPTIONAL_LOCKS = '0'
        GIT_TERMINAL_PROMPT = '0'
        GIT_CONFIG_NOSYSTEM = '1'
        GIT_CONFIG_GLOBAL = 'NUL'
        GIT_CONFIG_SYSTEM = 'NUL'
        LC_ALL = 'C'
        LANG = 'C'
    }
    foreach ($environmentName in $Environment.Keys) {
        if (-not [StringComparer]::OrdinalIgnoreCase.Equals([string]$environmentName, 'GIT_INDEX_FILE')) {
            throw 'Only an explicit temporary GIT_INDEX_FILE may extend the trusted Git environment.'
        }
        $gitEnvironment[[string]$environmentName] = [string]$Environment[$environmentName]
    }
    $gitBefore = Get-TrustedGitIdentity
    $globalArguments = @('--no-replace-objects', '--no-optional-locks', '--literal-pathspecs', '-c', 'core.hooksPath=NUL', '-c', 'core.fsmonitor=false', '-c', 'core.untrackedCache=false', '-c', 'core.preloadIndex=false', '-c', 'commit.gpgSign=false', '-C', $Repository)
    $result = Invoke-CrossRouteProcessCore -FilePath $gitBefore.Path -WorkingDirectory $Repository -Arguments ($globalArguments + $Arguments) -TimeoutSeconds $TimeoutSeconds -Environment $gitEnvironment
    $gitAfter = Get-TrustedGitIdentity
    Assert-FileIdentityUnchanged -Before $gitBefore -After $gitAfter -Label 'Git executable'
    if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
        throw "Required Git command failed with exit code $($result.ExitCode)."
    }
    return $result
}

function Assert-SafeImportRepositoryConfiguration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    foreach ($entry in @(
        [pscustomobject]@{
            Key = 'core.fsmonitor'
            Message = 'Repository-local core.fsmonitor is forbidden for import staging authority.'
        }
        [pscustomobject]@{
            Key = 'core.hooksPath'
            Message = 'Repository-local core.hooksPath is forbidden for import staging authority.'
        }
        [pscustomobject]@{
            Key = 'core.untrackedCache'
            Message = 'Repository-local core.untrackedCache is forbidden for import staging authority.'
        }
    )) {
        $configured = Invoke-CrossRouteGit -Repository $Repository -Arguments @('config', '--local', '--null', '--get-all', [string]$entry.Key) -AllowFailure
        if ($configured.ExitCode -notin @(0, 1) -or $configured.Stderr.Length -ne 0) {
            throw "Repository-local Git configuration inspection failed: $($entry.Key)"
        }
        if ($configured.StdoutBytes.Length -ne 0) { throw [string]$entry.Message }
    }
    $names = Invoke-CrossRouteGit -Repository $Repository -Arguments @('config', '--local', '--name-only', '--null', '--list')
    foreach ($key in @(ConvertFrom-NulBytes -Bytes $names.StdoutBytes)) {
        if ($key -imatch '^(?:filter\.|gpg\.|commit\.gpgsign$|user\.signingkey$)') {
            throw 'Repository-local executable Git configuration is forbidden for import staging authority.'
        }
    }
}

function Get-GitText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure,
        [hashtable]$Environment = @{}
    )

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments $Arguments -AllowFailure:$AllowFailure -Environment $Environment
    return $result.Stdout.Trim()
}

function Get-VerifiedIndexFileState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($fullPath)) {
        return [pscustomobject]@{
            Exists = $false
            Length = [long]0
            Sha256 = ''
        }
    }
    [void](Assert-NoReparseAncestors -Path $fullPath -IncludeLeaf)
    $bytes = [IO.File]::ReadAllBytes($fullPath)
    return [pscustomobject]@{
        Exists = $true
        Length = [long]$bytes.Length
        Sha256 = Get-Sha256 -Bytes $bytes
    }
}

function Assert-VerifiedIndexFileState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Expected, [Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Message)

    $actual = Get-VerifiedIndexFileState -Path $Path
    if ([bool]$actual.Exists -ne [bool]$Expected.Exists -or [long]$actual.Length -ne [long]$Expected.Length -or [string]$actual.Sha256 -cne [string]$Expected.Sha256) {
        throw $Message
    }
}

function New-VerifiedTemporaryIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $reported = Get-GitText -Repository $Repository -Arguments @('rev-parse', '--git-path', 'index')
    $indexPath = if ([IO.Path]::IsPathRooted($reported)) { [IO.Path]::GetFullPath($reported) } else { [IO.Path]::GetFullPath((Join-Path $Repository $reported)) }
    $parent = Split-Path -Parent $indexPath
    [void](Assert-NoReparseAncestors -Path $parent -IncludeLeaf)
    $original = Get-VerifiedIndexFileState -Path $indexPath
    $temporary = Join-Path $parent ('.geai-index-' + [guid]::NewGuid().ToString('N'))
    if ($original.Exists) {
        $source = New-Object -TypeName IO.FileStream -ArgumentList @($indexPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $target = New-Object -TypeName IO.FileStream -ArgumentList @($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
            try {
                $source.CopyTo($target)
                $target.Flush($true)
            }
            finally { $target.Dispose() }
        }
        finally { $source.Dispose() }
    }
    return [pscustomobject]@{
        IndexPath = $indexPath
        TemporaryPath = $temporary
        OriginalState = $original
    }
}

function Remove-VerifiedTemporaryIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Transaction)

    foreach ($candidate in @([string]$Transaction.TemporaryPath + '.lock', [string]$Transaction.TemporaryPath)) {
        if ([IO.File]::Exists($candidate)) {
            [void](Assert-NoReparseAncestors -Path $candidate -IncludeLeaf)
            [IO.File]::Delete($candidate)
        }
    }
}

function Publish-VerifiedTemporaryIndex {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][object]$Transaction,
        [Parameter(Mandatory)][object]$ExpectedRepositoryIdentity,
        [Parameter(Mandatory)][object]$ExpectedTemporaryState
    )

    Assert-VerifiedIndexFileState -Expected $ExpectedTemporaryState -Path $Transaction.TemporaryPath -Message 'Verified temporary index state changed before atomic publication.'
    Assert-VerifiedIndexFileState -Expected $Transaction.OriginalState -Path $Transaction.IndexPath -Message 'Original index state changed before atomic publication.'
    Assert-RepositoryIdentityEqual -Before $ExpectedRepositoryIdentity -After (Get-RepositoryIdentity -Repository $Repository -Prefix 'destination' -IncludeIndex)
    $lockPath = [string]$Transaction.IndexPath + '.lock'
    $ownedLock = $false
    try {
        $source = New-Object -TypeName IO.FileStream -ArgumentList @([string]$Transaction.TemporaryPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $lock = New-Object -TypeName IO.FileStream -ArgumentList @($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
            $ownedLock = $true
            try {
                $source.CopyTo($lock)
                $lock.Flush($true)
            }
            finally { $lock.Dispose() }
        }
        finally { $source.Dispose() }
        Assert-VerifiedIndexFileState -Expected $Transaction.OriginalState -Path $Transaction.IndexPath -Message 'Original index state changed while the publication lock was held.'
        Assert-RepositoryIdentityEqual -Before $ExpectedRepositoryIdentity -After (Get-RepositoryIdentity -Repository $Repository -Prefix 'destination' -IncludeIndex)
        [CrossRouteAtomicIndexPublisher]::Replace($lockPath, [string]$Transaction.IndexPath)
        $ownedLock = $false
    }
    finally {
        if ($ownedLock -and [IO.File]::Exists($lockPath)) { [IO.File]::Delete($lockPath) }
    }
}

function Get-Sha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function ConvertFrom-NulBytes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $decoder = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $true)
    $records = New-Object -TypeName 'Collections.Generic.List[string]'
    $begin = 0
    $index = 0
    while ($index -lt $Bytes.Length) {
        if ($Bytes[$index] -eq 0) {
            $records.Add($decoder.GetString($Bytes, $begin, $index - $begin))
            $begin = $index + 1
        }
        $index++
    }
    if ($begin -ne $Bytes.Length) {
        throw 'Git records were not NUL terminated.'
    }
    return $records.ToArray()
}

function Normalize-CrossRouteGitPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'A non-empty Git path is required.'
    }
    if ($Path.Contains('\')) {
        throw 'A canonical Git path cannot contain a backslash.'
    }
    if (-not $Path.IsNormalized([Text.NormalizationForm]::FormC)) {
        throw 'A Git path must use Unicode NormalizationForm.FormC.'
    }
    $value = $Path
    $segments = @($value.Split('/'))
    $invalidSegments = @($segments | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' }).Count
    if ($value.StartsWith('./', [StringComparison]::Ordinal) -or $value.StartsWith('/') -or $value -match '^[A-Za-z]:' -or $value.Contains(':') -or $value -match '[<>"|?*]' -or $value -match '[\x00-\x1f\x7f]' -or $invalidSegments -ne 0) {
        throw 'Absolute, non-canonical, control-bearing, or traversing Git path rejected.'
    }
    foreach ($segment in $segments) {
        if ($segment.EndsWith('.', [StringComparison]::Ordinal) -or $segment.EndsWith(' ', [StringComparison]::Ordinal)) {
            throw 'A Git path segment cannot have a trailing dot or space.'
        }
        $deviceName = $segment.Split('.')[0]
        if ($deviceName -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') {
            throw 'A Git path segment cannot use a reserved device name.'
        }
    }
    return $value
}

function Assert-NoPathCollisions {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $seen = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $Path) {
        $normalized = $candidate.Normalize([Text.NormalizationForm]::FormC)
        if (-not $seen.Add($normalized)) {
            throw "$Name contains an OrdinalIgnoreCase or Unicode normalization collision: $candidate"
        }
    }
}

function Assert-SourceIdentityConsistency {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Source)

    $spellingByPath = New-Object -TypeName 'Collections.Generic.Dictionary[string,string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    $identityByCommitPath = New-Object -TypeName 'Collections.Generic.Dictionary[string,string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    foreach ($record in $Source) {
        $path = Normalize-CrossRouteGitPath -Path ([string]$record.path)
        if ($spellingByPath.ContainsKey($path)) {
            if ([string]$spellingByPath[$path] -cne $path) {
                throw "Source paths contain a case or Unicode alias variant: $path"
            }
        }
        else {
            $spellingByPath.Add($path, $path)
        }

        $commitPathKey = ([string]$record.commit) + [char]0 + $path
        $identity = @(
            [string]$record.blob
            [string]$record.mode
            [string]$record.sha256
        ) -join [char]0
        if ($identityByCommitPath.ContainsKey($commitPathKey)) {
            if ([string]$identityByCommitPath[$commitPathKey] -cne $identity) {
                throw "Source records make differing identity claims for the same commit and path: $path"
            }
        }
        else {
            $identityByCommitPath.Add($commitPathKey, $identity)
        }
    }
}

function Test-PathRuleMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Rule
    )

    if ($Rule.EndsWith('/**', [StringComparison]::Ordinal)) {
        $prefix = $Rule.Substring(0, $Rule.Length - 3).TrimEnd('/')
        return $Path -ceq $prefix -or $Path.StartsWith($prefix + '/', [StringComparison]::Ordinal)
    }
    return $Path -ceq $Rule
}

function Test-AnyPathRule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Rule
    )

    foreach ($candidateRule in $Rule) {
        if (Test-PathRuleMatch -Path $Path -Rule $candidateRule) {
            return $true
        }
    }
    return $false
}

function Normalize-CrossRoutePathRule {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Rule)

    if ($Rule.EndsWith('/**', [StringComparison]::Ordinal)) {
        $prefix = Normalize-CrossRouteGitPath -Path $Rule.Substring(0, $Rule.Length - 3)
        return $prefix + '/**'
    }
    return Normalize-CrossRouteGitPath -Path $Rule
}

function Assert-PathInsideRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $fullRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name escapes its repository root."
    }
    return $fullPath
}

function Resolve-ContainedRegularFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Name
    )

    $normalized = Normalize-CrossRouteGitPath -Path $RelativePath
    $candidate = Join-Path -Path $Root -ChildPath ($normalized -replace '/', [IO.Path]::DirectorySeparatorChar)
    $fullPath = Assert-PathInsideRoot -Root $Root -Path $candidate -Name $Name
    [void](Assert-NoReparseAncestors -Path $fullPath -IncludeLeaf)
    if (-not [IO.File]::Exists($fullPath)) {
        throw "$Name must be an existing regular file: $normalized"
    }
    $attributes = [IO.File]::GetAttributes($fullPath)
    if (($attributes -band [IO.FileAttributes]::Directory) -ne 0 -or ($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Name must be a regular non-reparse file: $normalized"
    }
    return $fullPath
}

function Assert-PathspecTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$Repository
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ([IO.File]::Exists($fullPath) -or [IO.Directory]::Exists($fullPath)) {
        throw 'WriteAllowedPathspec target must be absent; a pre-existing sentinel is never overwritten.'
    }
    foreach ($repositoryRoot in $Repository) {
        $root = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\', '/')
        if ([StringComparer]::OrdinalIgnoreCase.Equals($fullPath, $root) -or $fullPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'WriteAllowedPathspec must be outside the repository worktree.'
        }
    }
    $parent = Split-Path -Parent $fullPath
    if ([string]::IsNullOrWhiteSpace($parent) -or -not [IO.Directory]::Exists($parent)) {
        throw 'WriteAllowedPathspec parent must already exist.'
    }
    [void](Assert-NoReparseAncestors -Path $parent -IncludeLeaf)
    return [pscustomobject]@{
        Path = $fullPath
        ParentIdentity = [CrossRoutePathspecPublisher]::CaptureParentIdentity($parent)
    }
}

function Write-AtomicPathspec {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject]$Target,
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes
    )
    if ($Target.PSObject.Properties.Name.Count -ne 2 -or $null -eq $Target.PSObject.Properties['Path'] -or $null -eq $Target.PSObject.Properties['ParentIdentity']) {
        throw 'Pathspec publication target identity is malformed.'
    }
    try {
        [CrossRoutePathspecPublisher]::WriteNew([string]$Target.Path, [string]$Target.ParentIdentity, $Bytes)
    }
    catch {
        if ($null -ne $_.Exception.InnerException) {
            throw [string]$_.Exception.InnerException.Message
        }
        throw
    }
}

function Remove-OwnedTemporaryEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][Diagnostics.Stopwatch]$Stopwatch,
        [Parameter(Mandatory)][TimeSpan]$Timeout,
        [string]$ProtectedPath
    )

    if ($Stopwatch.Elapsed -ge $Timeout) {
        throw 'Owned reconstruction cleanup exceeded its bounded duration.'
    }
    if (-not [string]::IsNullOrWhiteSpace($ProtectedPath) -and [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($Path), [IO.Path]::GetFullPath($ProtectedPath))) {
        return
    }
    $remainingMilliseconds = [Math]::Max(1, [int](($Timeout - $Stopwatch.Elapsed).TotalMilliseconds))
    [CrossRouteSafeDelete]::DeleteTree([IO.Path]::GetFullPath($Path), $remainingMilliseconds)
}

function Remove-OwnedReconstructionRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][IO.FileStream]$OwnerStream,
        [Parameter(Mandatory)][string]$OwnerMarkerPath
    )

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $parent = Split-Path -Parent $fullPath
    $name = Split-Path -Leaf $fullPath
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($parent, $tempRoot) -or $name -cnotmatch '^geai-import-reconstruct-[0-9a-f]{32}$') {
        throw 'Refusing cleanup outside the exact operation-owned reconstruction root.'
    }
    $markerFullPath = [IO.Path]::GetFullPath($OwnerMarkerPath)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals((Split-Path -Parent $markerFullPath), $fullPath) -or (Split-Path -Leaf $markerFullPath) -cne '.geai-operation-owner') {
        throw 'Owned reconstruction marker identity is invalid.'
    }
    if (-not [IO.Directory]::Exists($fullPath) -or ([IO.File]::GetAttributes($fullPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Owned reconstruction root was rebound or replaced.'
    }
    if (-not [IO.File]::Exists($markerFullPath) -or ([IO.File]::GetAttributes($markerFullPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Owned reconstruction marker was rebound or replaced.'
    }
    [CrossRouteSafeDelete]::DeleteOwnedTree($fullPath, $markerFullPath, $OwnerStream, 10000)
}

function Require-LowerHex {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][int]$Length,
        [Parameter(Mandatory)][string]$Name
    )

    if ($Value -cnotmatch ('^[0-9a-f]{' + $Length + '}$')) {
        throw "$Name must be exact lowercase hexadecimal."
    }
}

function Assert-NoDuplicateJsonProperties {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Json)

    $pattern = '(?s)\s*(?:(?<string>"(?:\\.|[^"\\])*")|(?<punct>[{}\[\],:])|(?<literal>true|false|null|-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?))'
    $regex = New-Object -TypeName Text.RegularExpressions.Regex -ArgumentList $pattern
    $offset = 0
    $stack = New-Object -TypeName Collections.Stack
    while ($offset -lt $Json.Length) {
        if ($Json.Substring($offset) -match '^\s*$') { break }
        $match = $regex.Match($Json, $offset)
        if (-not $match.Success -or $match.Index -ne $offset) {
            throw 'Manifest JSON contains an invalid token.'
        }
        $offset = $match.Index + $match.Length
        if ($match.Groups['punct'].Success) {
            $punctuation = $match.Groups['punct'].Value
            if ($punctuation -eq '{') {
                $stack.Push(@{
                    kind = 'object'
                    keys = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::Ordinal)
                    expectKey = $true
                })
            }
            elseif ($punctuation -eq '[') { $stack.Push(@{ kind = 'array' }) }
            elseif ($punctuation -eq '}' -or $punctuation -eq ']') {
                if ($stack.Count -eq 0) { throw 'Manifest JSON structure is unbalanced.' }
                [void]$stack.Pop()
            }
            elseif ($punctuation -eq ',' -and $stack.Count -gt 0 -and $stack.Peek().kind -eq 'object') {
                $stack.Peek().expectKey = $true
            }
        }
        elseif ($match.Groups['string'].Success -and $stack.Count -gt 0 -and $stack.Peek().kind -eq 'object' -and $stack.Peek().expectKey) {
            $key = ConvertFrom-Json -InputObject $match.Groups['string'].Value
            if (-not $stack.Peek().keys.Add([string]$key)) { throw "Duplicate JSON property rejected: $key" }
            $stack.Peek().expectKey = $false
        }
    }
    if ($stack.Count -ne 0) { throw 'Manifest JSON structure is unbalanced.' }
}

function Assert-ExactStringSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Actual,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory)][string]$Name
    )

    $set = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::Ordinal)
    foreach ($item in @($Actual)) {
        $value = [string]$item
        if (-not $set.Add($value)) { throw "$Name contains a duplicate: $value" }
    }
    if ($set.Count -ne $Expected.Count) { throw "$Name is not the exact required set." }
    foreach ($expectedValue in $Expected) {
        if (-not $set.Contains($expectedValue)) { throw "$Name is not the exact required set." }
    }
    return $set
}

function Resolve-RepositoryRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $resolved = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path).TrimEnd('\', '/')
    [void](Assert-NoReparseAncestors -Path $resolved -IncludeLeaf)
    if (-not [IO.Directory]::Exists($resolved)) {
        throw "$Name must be an existing regular directory."
    }
    $gitText = Get-GitText -Repository $resolved -Arguments @('rev-parse', '--show-toplevel')
    $gitRoot = [IO.Path]::GetFullPath(($gitText -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\', '/')
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolved, $gitRoot)) { throw "$Name must be the exact repository root." }
    return $resolved
}

function Get-RepositoryIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Prefix,
        [switch]$IncludeIndex
    )

    $head = Get-GitText -Repository $Repository -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD')
    $tree = Get-GitText -Repository $Repository -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD^{tree}')
    Assert-NoHiddenIndexState -Repository $Repository
    $statusBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('status', '--porcelain=v1', '-z', '--untracked-files=all')).StdoutBytes
    $identity = [ordered]@{
        Name = $Prefix
        Head = $head
        Tree = $tree
        Status = [Convert]::ToBase64String($statusBytes)
    }
    if ($IncludeIndex) {
        $stageBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '--stage', '-z')).StdoutBytes
        $verboseBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '-v', '-z')).StdoutBytes
        $fsmonitorBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '-f', '-z')).StdoutBytes
        $identity.IndexSnapshot = [Convert]::ToBase64String([byte[]]($stageBytes + $verboseBytes + $fsmonitorBytes))
    }
    return [pscustomobject]$identity
}

function Assert-RepositoryIdentityEqual {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After
    )

    if ($Before.Head -cne $After.Head -or $Before.Tree -cne $After.Tree -or $Before.Status -cne $After.Status) {
        throw "$($Before.Name) HEAD/tree/raw status changed during verification."
    }
    $beforeIndex = $Before.PSObject.Properties['IndexSnapshot']
    $afterIndex = $After.PSObject.Properties['IndexSnapshot']
    if (($null -ne $beforeIndex -or $null -ne $afterIndex) -and ($null -eq $beforeIndex -or $null -eq $afterIndex -or $beforeIndex.Value -cne $afterIndex.Value)) {
        throw "$($Before.Name) index state changed during verification."
    }
}

function Assert-NoHiddenIndexState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository, [hashtable]$Environment = @{})

    foreach ($record in (ConvertFrom-NulBytes -Bytes (Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '-v', '-z') -Environment $Environment).StdoutBytes)) {
        if ($record.Length -gt 1 -and ($record[0] -ceq 'S' -or [char]::IsLower($record[0]))) {
            throw 'Git index contains skip-worktree or assume-unchanged state.'
        }
    }
    foreach ($record in (ConvertFrom-NulBytes -Bytes (Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '-f', '-z') -Environment $Environment).StdoutBytes)) {
        if ($record.Length -gt 1 -and [char]::IsLower($record[0])) {
            throw 'Git index contains fsmonitor-valid state.'
        }
    }
}

function Get-EffectiveComponentPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Component,
        [Parameter(Mandatory)][object]$Policy
    )

    if ($Component -cne 'UO1' -or $Policy.PSObject.Properties['FixturePolicyId'] -eq $null -or [string]$Policy.FixturePolicyId -cne 'internal-controlled-uo1-v1') {
        throw 'Internal fixture policy is restricted to the synthetic UO1 verifier fixture.'
    }
    return [ordered]@{
        fixturePolicy = $true
        manifestName = [string]$Policy.ManifestName
        sourceRef = [string]$Policy.SourceRef
        sourceCommit = [string]$Policy.SourceCommit
        permittedCommits = [string[]]$Policy.PermittedCommits
        exactSourceCommits = @([string]$Policy.SourceCommit)
        dependencyClosure = @(
            'imports/**'
            'README.md'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/**'
        )
        excludedSharedPaths = @(
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml'
        )
        patchPathRules = @(
            'docs/handoffs/import-patches/adapted.patch'
            'docs/handoffs/import-patches/created.patch'
        )
        allowedKinds = @('Exact', 'Adapted', 'Created')
        adaptedBaseTrees = @{
            '8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8' = '65a265e28a03fa5702773a2f2d5b877a5b5e7011'
        }
        allowAdaptedIntegrationAncestor = $false
        createdBaseCommit = '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
        createdBaseTree = 'cc87f57b1239299976793fd54e806a5734273f56'
        requireCreatedDestinationHead = $false
        integrationOnlyPaths = @()
        verificationCommands = @('fixture')
        integrationBaseRoles = @{}
    }
}

function Assert-ComponentPolicyBoundary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Policy,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$DestinationPath,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$PatchPath,
        [Parameter(Mandatory)][object]$Record
    )

    foreach ($path in $DestinationPath) {
        if (-not (Test-AnyPathRule -Path $path -Rule ([string[]]$Policy.dependencyClosure))) {
            throw "Destination path is outside the hard-coded component ownership closure: $path"
        }
        if (Test-AnyPathRule -Path $path -Rule ([string[]]$Policy.excludedSharedPaths)) {
            throw "Destination path is excluded by the hard-coded component policy: $path"
        }
    }
    foreach ($path in $PatchPath) {
        if (-not (Test-AnyPathRule -Path $path -Rule ([string[]]$Policy.patchPathRules))) {
            throw "Patch path is outside the hard-coded component patch policy: $path"
        }
    }
    $isFixturePolicy = $Policy -is [Collections.IDictionary] -and $Policy.Contains('fixturePolicy')
    if (-not $isFixturePolicy) {
        $closure = @($Record.dependencyClosure | ForEach-Object { Normalize-CrossRoutePathRule -Rule ([string]$_) })
        $exclusions = @($Record.excludedSharedPaths | ForEach-Object { Normalize-CrossRoutePathRule -Rule ([string]$_) })
        [void](Assert-ExactStringSet -Actual $closure -Expected ([string[]]$Policy.dependencyClosure) -Name 'dependencyClosure')
        [void](Assert-ExactStringSet -Actual $exclusions -Expected ([string[]]$Policy.excludedSharedPaths) -Name 'excludedSharedPaths')
    }
}

function Assert-VerificationCommandPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Policy,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Command
    )

    try {
        [void](Assert-ExactStringSet -Actual $Command -Expected ([string[]]$Policy.verificationCommands) -Name 'verificationCommands')
    }
    catch {
        throw 'verificationCommands is not the exact hard-coded command set.'
    }
}

function Get-ManifestModel {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ManifestPath)

    $json = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path)
    Assert-NoDuplicateJsonProperties -Json $json
    try { return ConvertFrom-Json -InputObject $json -ErrorAction Stop }
    catch { throw "Manifest JSON is invalid: $($_.Exception.Message)" }
}

function Assert-AuthorityAndPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [object]$FixturePolicy
    )

    if ([int]$Manifest.schemaVersion -ne 1) { throw 'Manifest schemaVersion must be 1.' }
    if ([string]$Manifest.policy.importMethod -cne 'bounded commit:path:blob verification') { throw 'Import method policy is not the exact bounded policy.' }
    if ([string]$Manifest.policy.stagingMethod -cne $script:StagingMethod) { throw 'Staging method policy is not the exact verified no-filter policy.' }
    $policyPaths = @($Manifest.policy.manifestPaths | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_) })
    [void](Assert-ExactStringSet -Actual $policyPaths -Expected $script:ManifestPaths -Name 'policy.manifestPaths')
    $authorities = @($Manifest.authorities)
    if ($authorities.Count -ne $script:ExpectedAuthorities.Count) { throw 'Authority table must contain exactly six records.' }
    $seenNames = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    $seenRefs = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    $seenCommits = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::Ordinal)
    foreach ($expected in $script:ExpectedAuthorities) {
        $matches = @($authorities | Where-Object { [string]$_.name -ceq [string]$expected.name })
        if ($matches.Count -ne 1) { throw "Authority name missing, duplicated, or rebound: $($expected.name)" }
        $actual = $matches[0]
        Require-LowerHex -Value ([string]$actual.commit) -Length 40 -Name 'authority commit'
        if ([string]$actual.ref -cne [string]$expected.ref -or [string]$actual.commit -cne [string]$expected.commit) { throw "Authority tuple mismatch: $($expected.name)" }
        if (-not $seenNames.Add([string]$actual.name) -or -not $seenRefs.Add([string]$actual.ref) -or -not $seenCommits.Add([string]$actual.commit)) { throw 'Authority identities must be unique without normalized duplicates.' }
    }
    $componentExpectations = @(
        (Get-ApprovedComponentPolicy -Component UO1)
        (Get-ApprovedComponentPolicy -Component GgufRuntime)
        (Get-ApprovedComponentPolicy -Component OpenVino)
    )
    if ($null -ne $FixturePolicy -and $FixturePolicy.fixturePolicy) {
        $componentExpectations[0] = $FixturePolicy
    }
    if (@($Manifest.components).Count -ne 3) { throw 'Manifest must contain exactly the three ruled component records.' }
    foreach ($expectedComponent in $componentExpectations) {
        $records = @($Manifest.components | Where-Object { [string]$_.name -ceq [string]$expectedComponent.manifestName })
        if ($records.Count -ne 1 -or [string]$records[0].sourceRef -cne [string]$expectedComponent.sourceRef -or [string]$records[0].sourceCommit -cne [string]$expectedComponent.sourceCommit) { throw "Component source authority mismatch: $($expectedComponent.manifestName)" }
        if ([string]$records[0].status -cnotin @('Pending', 'Verified')) { throw 'Component status must be Pending or Verified.' }
        Assert-VerificationCommandPolicy -Policy $expectedComponent -Command ([string[]]@($records[0].verificationCommands))
    }
}

function Get-LsTreeEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Commit,
        [Parameter(Mandatory)][string]$Path,
        [switch]$AllowMissing
    )

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-tree', '-z', '--full-tree', '--end-of-options', $Commit, '--', $Path)
    $records = @(ConvertFrom-NulBytes -Bytes $result.StdoutBytes)
    if ($records.Count -eq 0) {
        if ($AllowMissing) { return $null }
        throw "Required commit:path is absent: $Commit`:$Path"
    }
    if ($records.Count -ne 1 -or $records[0] -cnotmatch '^(?<mode>[0-9]{6}) (?<type>blob|tree|commit) (?<oid>[0-9a-f]{40})\t(?<path>.+)$') { throw 'Git ls-tree returned an unexpected record.' }
    if ($Matches.path -cne $Path) { throw 'Git ls-tree path identity mismatch.' }
    return [pscustomobject]@{
        Mode = $Matches.mode
        Type = $Matches.type
        Oid = $Matches.oid
        Path = $Matches.path
    }
}

function Get-IndexEntry {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string]$Path, [hashtable]$Environment = @{})

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments @('ls-files', '-s', '-z', '--', $Path) -Environment $Environment
    $records = @(ConvertFrom-NulBytes -Bytes $result.StdoutBytes)
    if ($records.Count -ne 1 -or $records[0] -cnotmatch '^(?<mode>[0-9]{6}) (?<oid>[0-9a-f]{40}) (?<stage>[0-3])\t(?<path>.+)$') { throw "Expected exactly one index entry for $Path." }
    if ($Matches.stage -cne '0' -or $Matches.path -cne $Path) { throw "Index entry is unmerged or rebound for $Path." }
    return [pscustomobject]@{
        Mode = $Matches.mode
        Oid = $Matches.oid
        Path = $Matches.path
    }
}

function Get-ChangedIndexEndpoints {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository, [hashtable]$Environment = @{})

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments @('diff', '--cached', '--no-ext-diff', '--no-textconv', '--name-status', '-z', '--find-renames', '--find-copies') -Environment $Environment
    $records = @(ConvertFrom-NulBytes -Bytes $result.StdoutBytes)
    $paths = New-Object -TypeName 'Collections.Generic.List[string]'
    $index = 0
    while ($index -lt $records.Count) {
        $status = $records[$index]
        $index++
        if ($status -cnotmatch '^[ACDMRTUXB][0-9]*$') { throw "Unexpected staged status record: $status" }
        if ($index -ge $records.Count) { throw 'Incomplete staged path record.' }
        $paths.Add((Normalize-CrossRouteGitPath -Path $records[$index]))
        $index++
        if ($status[0] -eq 'R' -or $status[0] -eq 'C') {
            if ($index -ge $records.Count) { throw 'Incomplete staged rename/copy endpoint.' }
            $paths.Add((Normalize-CrossRouteGitPath -Path $records[$index]))
            $index++
        }
    }
    return $paths.ToArray()
}

function Get-WorktreeChangedEndpoints {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments @('status', '--porcelain=v1', '-z', '--untracked-files=all')
    $records = @(ConvertFrom-NulBytes -Bytes $result.StdoutBytes)
    $paths = New-Object -TypeName 'Collections.Generic.List[string]'
    $index = 0
    while ($index -lt $records.Count) {
        $record = $records[$index]
        if ($record.Length -lt 4 -or $record[2] -ne ' ') { throw 'Unexpected Git status record.' }
        $status = $record.Substring(0, 2)
        $paths.Add((Normalize-CrossRouteGitPath -Path $record.Substring(3)))
        if ($status.Contains('R') -or $status.Contains('C')) {
            $index++
            if ($index -ge $records.Count) { throw 'Incomplete rename/copy status record.' }
            $paths.Add((Normalize-CrossRouteGitPath -Path $records[$index]))
        }
        $index++
    }
    return $paths.ToArray()
}

function Get-ChangedIndexStatusByPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $result = Invoke-CrossRouteGit -Repository $Repository -Arguments @('diff', '--cached', '--no-ext-diff', '--no-textconv', '--name-status', '-z', '--no-renames')
    $records = @(ConvertFrom-NulBytes -Bytes $result.StdoutBytes)
    if (($records.Count % 2) -ne 0) {
        throw 'Incomplete staged status output.'
    }
    $statusByPath = @{}
    $index = 0
    while ($index -lt $records.Count) {
        $status = $records[$index]
        $path = Normalize-CrossRouteGitPath -Path $records[$index + 1]
        if ($status -cnotin @('A', 'M', 'D', 'T')) {
            throw "Unexpected staged status record: $status"
        }
        if ($statusByPath.ContainsKey($path)) {
            throw "Duplicate staged status path: $path"
        }
        $statusByPath[$path] = $status
        $index += 2
    }
    return $statusByPath
}

function Set-IndexMode {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Mode
    )

    if ($Mode -eq '100755') { [void](Invoke-CrossRouteGit -Repository $Repository -Arguments @('update-index', '--chmod=+x', '--', $Path)) }
    elseif ($Mode -eq '100644') { [void](Invoke-CrossRouteGit -Repository $Repository -Arguments @('update-index', '--chmod=-x', '--', $Path)) }
    else { throw "Forbidden regular-file mode: $Mode" }
}

function Resolve-PolicyCommitRepository {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Commit,
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$DestinationRoot,
        [Parameter(Mandatory)][object]$Policy,
        [string]$DestinationPath,
        [switch]$AllowIntegration
    )

    $planning = '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
    $isFixturePolicy = $Policy -is [Collections.IDictionary] -and $Policy.Contains('fixturePolicy') -and $Policy.fixturePolicy
    if ($Commit -ceq $planning) {
        if ($isFixturePolicy) { return $SourceRoot }
        return $DestinationRoot
    }
    if ($Commit -cin [string[]]$Policy.permittedCommits) { return $SourceRoot }
    if (-not $AllowIntegration -or -not $Policy.allowAdaptedIntegrationAncestor) {
        throw 'Commit is not permitted for this component and evidence kind.'
    }
    $authorizedIntegrationPath = $false
    foreach ($role in @($Policy.integrationBaseRoles.Values)) {
        if (-not [string]::IsNullOrWhiteSpace($DestinationPath) -and (Test-AnyPathRule -Path $DestinationPath -Rule ([string[]]$role.memberRules))) {
            $authorizedIntegrationPath = $true
        }
    }
    if (-not $authorizedIntegrationPath) {
        throw 'Integration commit is not authorized for this destination path.'
    }
    [void](Get-GitText -Repository $DestinationRoot -Arguments @('cat-file', '-e', '--end-of-options', ($Commit + '^{commit}')))
    $head = Get-GitText -Repository $DestinationRoot -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD')
    $chain = @(Get-GitText -Repository $DestinationRoot -Arguments @('rev-list', '--first-parent', ($planning + '..' + $head)) | ForEach-Object { $_ -split "`n" })
    if ($Commit -cne $planning -and $Commit -cnotin $chain) {
        throw 'Integration commit must be on the destination HEAD first-parent chain after the planning base.'
    }
    return $DestinationRoot
}

function Assert-PatchGroup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$DestinationRoot,
        [Parameter(Mandatory)][object]$Group,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Members,
        [Parameter(Mandatory)][hashtable]$SourceByDestination,
        [Parameter(Mandatory)][object]$Policy
    )

    if ([string]::IsNullOrWhiteSpace([string]$Group.id)) { throw 'Patch-group ID is required.' }
    Require-LowerHex -Value ([string]$Group.baseCommit) -Length 40 -Name 'patch base commit'
    Require-LowerHex -Value ([string]$Group.baseTree) -Length 40 -Name 'patch base tree'
    Require-LowerHex -Value ([string]$Group.patchSha256) -Length 64 -Name 'patch SHA-256'
    $patchPath = Normalize-CrossRouteGitPath -Path ([string]$Group.patchPath)
    $rolePolicy = $Policy.integrationBaseRoles[$patchPath]
    $isDynamicIntegrationBase = ([string]$Group.baseCommit -cnotin [string[]]$Policy.permittedCommits) -and [string]$Group.baseCommit -cne '092589c38981ad86bb73c7c97dff01ab8b5a6c8e'
    $representativePath = if ($Members.Count -eq 0) { $null } else { Normalize-CrossRouteGitPath -Path ([string]$Members[0].path) }
    $baseRepository = Resolve-PolicyCommitRepository -Commit ([string]$Group.baseCommit) -SourceRoot $SourceRoot -DestinationRoot $DestinationRoot -Policy $Policy -DestinationPath $representativePath -AllowIntegration
    if ($isDynamicIntegrationBase) {
        if ($null -eq $rolePolicy -or [string]$Group.baseRole -cne [string]$rolePolicy.role) {
            throw 'Integration patch base lacks its exact authorized predecessor role.'
        }
        foreach ($member in $Members) {
            $memberPath = Normalize-CrossRouteGitPath -Path ([string]$member.path)
            if (-not (Test-AnyPathRule -Path $memberPath -Rule ([string[]]$rolePolicy.memberRules))) {
                throw 'Integration-base patch member is outside its exact authorized predecessor role.'
            }
        }
    }
    elseif ($null -ne $Group.PSObject.Properties['baseRole']) {
        throw 'Pinned patch bases cannot declare an integration predecessor role.'
    }
    [void](Get-GitText -Repository $baseRepository -Arguments @('cat-file', '-e', '--end-of-options', ([string]$Group.baseCommit + '^{commit}')))
    $baseTree = Get-GitText -Repository $baseRepository -Arguments @('rev-parse', '--verify', '--end-of-options', ([string]$Group.baseCommit + '^{tree}'))
    if ($baseTree -cne [string]$Group.baseTree) { throw 'Patch-group base tree does not match its authority commit.' }
    if ($isDynamicIntegrationBase -and [string]$rolePolicy.role -ceq 'destination-head') {
        $destinationHead = Get-GitText -Repository $DestinationRoot -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD')
        $destinationTree = Get-GitText -Repository $DestinationRoot -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD^{tree}')
        if ([string]$Group.baseCommit -cne $destinationHead -or [string]$Group.baseTree -cne $destinationTree) {
            throw 'Destination-head patch role must use the exact current destination HEAD commit/tree.'
        }
    }
    $patchFull = Resolve-ContainedRegularFile -Root $DestinationRoot -RelativePath $patchPath -Name 'Integration patch'
    $patchBytes = [IO.File]::ReadAllBytes($patchFull)
    if ($patchBytes.Length -eq 0) { throw 'Integration patch must be non-empty.' }
    if ((Get-Sha256 -Bytes $patchBytes) -cne [string]$Group.patchSha256) { throw 'Tracked integration patch SHA-256 differs.' }
    if ($Members.Count -eq 0) { throw 'Patch group has no declared destinations.' }

    $adaptedMembers = @($Members | Where-Object { [string]$_.kind -eq 'Adapted' })
    $createdMembers = @($Members | Where-Object { [string]$_.kind -eq 'Created' })
    if ($adaptedMembers.Count -gt 0) {
        $approvedTree = $Policy.adaptedBaseTrees[[string]$Group.baseCommit]
        $isIntegrationBase = $isDynamicIntegrationBase -and [StringComparer]::OrdinalIgnoreCase.Equals($baseRepository, $DestinationRoot)
        if (-not $isIntegrationBase -and ([string]::IsNullOrWhiteSpace([string]$approvedTree) -or [string]$approvedTree -cne [string]$Group.baseTree)) {
            throw 'Adapted patch base is not the exact hard-coded authority commit/tree.'
        }
    }
    if ($createdMembers.Count -gt 0) {
        if ($Policy.requireCreatedDestinationHead) {
            $destinationHead = Get-GitText -Repository $DestinationRoot -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD')
            $destinationTree = Get-GitText -Repository $DestinationRoot -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD^{tree}')
            if ([string]$Group.baseCommit -cne $destinationHead -or [string]$Group.baseTree -cne $destinationTree) {
                throw 'Created patch base must be the exact destination HEAD commit/tree.'
            }
        }
        elseif ([string]$Group.baseCommit -cne [string]$Policy.createdBaseCommit -or [string]$Group.baseTree -cne [string]$Policy.createdBaseTree) {
            throw 'Created patch base must be the exact planning authority commit/tree.'
        }
    }

    $temporary = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath ('geai-import-reconstruct-' + [guid]::NewGuid().ToString('N'))
    $ownerStream = $null
    $ownerMarkerPath = Join-Path -Path $temporary -ChildPath '.geai-operation-owner'
    try {
        if ([IO.Directory]::Exists($temporary) -or [IO.File]::Exists($temporary)) { throw 'Fresh reconstruction root unexpectedly already exists.' }
        [IO.Directory]::CreateDirectory($temporary) | Out-Null
        $ownerStream = New-Object -TypeName IO.FileStream -ArgumentList @($ownerMarkerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('init', '--quiet'))
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('config', 'user.email', 'tests@example.invalid'))
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('config', 'user.name', 'Tests'))
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('config', 'core.autocrlf', 'false'))
        [IO.File]::WriteAllText((Join-Path -Path $temporary -ChildPath '.gitignore'), "seed`n", (New-Object -TypeName Text.UTF8Encoding -ArgumentList $false))
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('add', '.gitignore'))
        foreach ($member in $Members) {
            $memberPath = Normalize-CrossRouteGitPath -Path ([string]$member.path)
            if ([string]$member.kind -eq 'Adapted') {
                $sourceRecord = $SourceByDestination[$memberPath]
                if ([string]$sourceRecord.record.commit -cne [string]$Group.baseCommit) { throw 'Adapted files from different bases require separate patch groups.' }
                $full = Join-Path -Path $temporary -ChildPath ($memberPath -replace '/', [IO.Path]::DirectorySeparatorChar)
                [IO.Directory]::CreateDirectory((Split-Path -Parent $full)) | Out-Null
                [IO.File]::WriteAllBytes($full, [byte[]]$sourceRecord.bytes)
                [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('add', '--', $memberPath))
                Set-IndexMode -Repository $temporary -Path $memberPath -Mode ([string]$sourceRecord.record.mode)
            }
            elseif ([string]$member.kind -eq 'Created') {
                if ($member.integrationOwned -ne $true) { throw 'Created destination must be integration-owned.' }
                $baseEntry = Get-LsTreeEntry -Repository $baseRepository -Commit ([string]$Group.baseCommit) -Path $memberPath -AllowMissing
                if ($null -ne $baseEntry) { throw 'Created destination already exists at the checked integration base.' }
            }
            else { throw 'Patch groups may contain only Adapted or Created destinations.' }
        }
        [void](Invoke-CrossRouteGit -Repository $temporary -Arguments @('commit', '--quiet', '-m', 'reconstruction base'))
        $check = Invoke-CrossRouteGit -Repository $temporary -Arguments @('apply', '--check', '--index', '--whitespace=nowarn', '--', $patchFull) -AllowFailure
        if ($check.ExitCode -ne 0 -or $check.Stderr.Length -ne 0) { throw 'Integration patch failed structured git apply --check.' }
        $apply = Invoke-CrossRouteGit -Repository $temporary -Arguments @('apply', '--index', '--whitespace=nowarn', '--', $patchFull) -AllowFailure
        if ($apply.ExitCode -ne 0 -or $apply.Stderr.Length -ne 0) { throw 'Integration patch failed structured git apply.' }
        $changed = @(Get-ChangedIndexEndpoints -Repository $temporary)
        $expected = @($Members | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_.path) })
        [void](Assert-ExactStringSet -Actual $changed -Expected $expected -Name 'patch reconstructed paths')
        $statusByPath = Get-ChangedIndexStatusByPath -Repository $temporary
        foreach ($member in $Members) {
            $memberPath = Normalize-CrossRouteGitPath -Path ([string]$member.path)
            $expectedStatus = if ([string]$member.kind -eq 'Adapted') { 'M' } else { 'A' }
            if (-not $statusByPath.ContainsKey($memberPath) -or [string]$statusByPath[$memberPath] -cne $expectedStatus) {
                throw "Patch member does not have the required $expectedStatus status: $memberPath"
            }
            $entry = Get-IndexEntry -Repository $temporary -Path $memberPath
            if ($entry.Mode -cne [string]$member.resultMode -or $entry.Mode -cnotin @('100644', '100755')) { throw 'Patch result mode differs from the declared regular-file mode.' }
            $reconstructed = (Invoke-CrossRouteGit -Repository $temporary -Arguments @('cat-file', 'blob', $entry.Oid)).StdoutBytes
            $destinationFull = Resolve-ContainedRegularFile -Root $DestinationRoot -RelativePath $memberPath -Name 'Imported destination'
            $actual = [IO.File]::ReadAllBytes($destinationFull)
            if ((Get-Sha256 -Bytes $reconstructed) -cne [string]$member.resultSha256 -or (Get-Sha256 -Bytes $actual) -cne [string]$member.resultSha256) { throw 'Reconstructed or destination result SHA-256 differs.' }
        }
    }
    finally {
        if ($null -ne $ownerStream) {
            try {
                Remove-OwnedReconstructionRoot -Path $temporary -OwnerStream $ownerStream -OwnerMarkerPath $ownerMarkerPath
            }
            finally {
                $ownerStream.Dispose()
            }
        }
        elseif ([IO.Directory]::Exists($temporary)) {
            throw 'Reconstruction root was created without its exclusive owner marker.'
        }
    }
    return [pscustomobject]@{
        Path = $patchPath
        Bytes = $patchBytes
        Sha256 = [string]$Group.patchSha256
    }
}

function Assert-StagedState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][hashtable]$DestinationByPath,
        [Parameter(Mandatory)][hashtable]$PatchByPath,
        [Parameter(Mandatory)][string[]]$ManifestPaths,
        [hashtable]$Environment = @{}
    )

    $expected = @($DestinationByPath.Keys) + @($PatchByPath.Keys) + @($ManifestPaths)
    $endpointSet = Assert-ExactStringSet -Actual @(Get-ChangedIndexEndpoints -Repository $Repository -Environment $Environment) -Expected $expected -Name 'staged path endpoints'
    foreach ($path in $expected) {
        if (-not $endpointSet.Contains($path)) { throw "Required staged path missing: $path" }
        $entry = Get-IndexEntry -Repository $Repository -Path $path -Environment $Environment
        if ($entry.Mode -cnotin @('100644', '100755')) { throw "Staged path has a forbidden mode: $path" }
        $indexBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('cat-file', 'blob', $entry.Oid)).StdoutBytes
        $full = Join-Path -Path $Repository -ChildPath ($path -replace '/', [IO.Path]::DirectorySeparatorChar)
        $worktreeBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $full -ErrorAction Stop).Path)
        if ((Get-Sha256 -Bytes $indexBytes) -cne (Get-Sha256 -Bytes $worktreeBytes)) { throw "Staged blob differs from verified worktree bytes: $path" }
        if ($DestinationByPath.ContainsKey($path)) {
            $destination = $DestinationByPath[$path]
            if ($entry.Mode -cne [string]$destination.resultMode -or (Get-Sha256 -Bytes $indexBytes) -cne [string]$destination.resultSha256) { throw "Staged destination identity differs: $path" }
        }
        elseif ($PatchByPath.ContainsKey($path)) {
            if ((Get-Sha256 -Bytes $indexBytes) -cne [string]$PatchByPath[$path].Sha256) { throw "Staged patch identity differs: $path" }
        }
        elseif ($entry.Mode -cne '100644') { throw "Manifest index mode must be 100644: $path" }
    }
}

function Set-VerifiedIndexState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][hashtable]$DestinationByPath,
        [Parameter(Mandatory)][hashtable]$PatchByPath,
        [Parameter(Mandatory)][hashtable]$EvidenceHashes,
        [Parameter(Mandatory)][string[]]$ManifestPaths
    )

    Assert-SafeImportRepositoryConfiguration -Repository $Repository
    $repositoryIdentity = Get-RepositoryIdentity -Repository $Repository -Prefix 'destination' -IncludeIndex
    $entries = New-Object -TypeName 'Collections.Generic.List[object]'
    foreach ($path in $DestinationByPath.Keys) {
        $record = $DestinationByPath[$path]
        $entries.Add([pscustomobject]@{
            Path = [string]$path
            Mode = [string]$record.resultMode
            Sha256 = [string]$record.resultSha256
        })
    }
    foreach ($path in $PatchByPath.Keys) {
        $entries.Add([pscustomobject]@{
            Path = [string]$path
            Mode = '100644'
            Sha256 = [string]$PatchByPath[$path].Sha256
        })
    }
    foreach ($path in $ManifestPaths) {
        $entries.Add([pscustomobject]@{
            Path = [string]$path
            Mode = '100644'
            Sha256 = [string]$EvidenceHashes[$path]
        })
    }
    Assert-NoPathCollisions -Path ([string[]]@($entries | ForEach-Object { $_.Path })) -Name 'verified staging paths'
    $transaction = New-VerifiedTemporaryIndex -Repository $Repository
    $indexEnvironment = @{ GIT_INDEX_FILE = [string]$transaction.TemporaryPath }
    try {
        foreach ($entry in $entries) {
            $fullPath = Resolve-ContainedRegularFile -Root $Repository -RelativePath $entry.Path -Name 'Verified staging input'
            $oid = Get-GitText -Repository $Repository -Arguments @('hash-object', '-w', '--no-filters', '--', $fullPath)
            Require-LowerHex -Value $oid -Length 40 -Name 'verified staging blob OID'
            $blobBytes = (Invoke-CrossRouteGit -Repository $Repository -Arguments @('cat-file', 'blob', $oid)).StdoutBytes
            if ((Get-Sha256 -Bytes $blobBytes) -cne [string]$entry.Sha256) {
                throw "No-filter staged blob differs from verified bytes: $($entry.Path)"
            }
            [void](Invoke-CrossRouteGit -Repository $Repository -Arguments @('update-index', '--add', '--cacheinfo', ([string]$entry.Mode + ',' + $oid + ',' + [string]$entry.Path)) -Environment $indexEnvironment)
        }
        Assert-StagedState -Repository $Repository -DestinationByPath $DestinationByPath -PatchByPath $PatchByPath -ManifestPaths $ManifestPaths -Environment $indexEnvironment
        Assert-NoHiddenIndexState -Repository $Repository -Environment $indexEnvironment
        $temporaryState = Get-VerifiedIndexFileState -Path $transaction.TemporaryPath
        Publish-VerifiedTemporaryIndex -Repository $Repository -Transaction $transaction -ExpectedRepositoryIdentity $repositoryIdentity -ExpectedTemporaryState $temporaryState
        Assert-StagedState -Repository $Repository -DestinationByPath $DestinationByPath -PatchByPath $PatchByPath -ManifestPaths $ManifestPaths
    }
    finally {
        Remove-VerifiedTemporaryIndex -Transaction $transaction
    }
}

function Assert-VerificationPowerShellStructure {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$Path)

    $parsedFiles = New-Object -TypeName 'Collections.Generic.List[object]'
    $functionNames = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $Path) {
        $resolved = (Resolve-Path -LiteralPath $candidate -ErrorAction Stop).Path
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile($resolved, [ref]$tokens, [ref]$errors)
        if ($errors.Count -ne 0) { throw "PowerShell parser errors found in $resolved." }
        if (@($tokens | Where-Object { $_.Kind -eq 'Semi' }).Count -ne 0) {
            throw "Compressed semicolon statements found in $resolved."
        }
        foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
            [void]$functionNames.Add($definition.Name)
        }
        $parsedFiles.Add([pscustomobject]@{
            Path = $resolved
            Ast = $ast
            Tokens = $tokens
        })
    }
    foreach ($parsed in $parsedFiles) {
        $source = [IO.File]::ReadAllText($parsed.Path)
        foreach ($token in $parsed.Tokens) {
            $binaryFlag = $token.TokenFlags -band [Management.Automation.Language.TokenFlags]::BinaryOperator
            if ($binaryFlag -eq 0) { continue }
            $start = $token.Extent.StartOffset
            $end = $token.Extent.EndOffset
            if ($start -eq 0 -or $end -ge $source.Length -or -not [char]::IsWhiteSpace($source[$start - 1]) -or -not [char]::IsWhiteSpace($source[$end])) {
                throw "Compressed binary operator found in $($parsed.Path): $($token.Text)"
            }
        }
        foreach ($command in $parsed.Ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true)) {
            if (-not $functionNames.Contains($command.GetCommandName())) { continue }
            $elements = @($command.CommandElements)
            $index = 1
            while ($index -lt $elements.Count) {
                $element = $elements[$index]
                if ($element -is [Management.Automation.Language.CommandParameterAst]) {
                    $index++
                    continue
                }
                if ($elements[$index - 1] -isnot [Management.Automation.Language.CommandParameterAst]) { throw "Positional project-function call found in $($parsed.Path): $($command.Extent.Text)" }
                if ($element.Extent.Text -match '^(?:\d+|\$[A-Za-z_][A-Za-z0-9_.:]*)[''"]') { throw "Compressed command element found in $($parsed.Path): $($element.Extent.Text)" }
                $index++
            }
        }
    }
    return [pscustomobject]@{
        ParsedFiles = $parsedFiles.Count
        ProjectFunctions = $functionNames.Count
        PositionalCalls = 0
        CompressedElements = 0
        CompressedOperators = 0
        SemicolonStatements = 0
    }
}

function Invoke-CrossRouteImportVerificationLegacy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Component,
        [Parameter(Mandatory)][string]$SourceRepository,
        [Parameter(Mandatory)][string]$DestinationRepository,
        [Parameter(Mandatory)][object]$ManifestModel,
        [Parameter(Mandatory)][object]$Policy,
        [string]$WriteAllowedPathspec,
        [switch]$VerifyStaged
    )

    if ($WriteAllowedPathspec -and $VerifyStaged) { throw 'WriteAllowedPathspec and VerifyStaged are separate phases.' }
    $policy = $Policy
    $sourceRoot = Resolve-RepositoryRoot -Path $SourceRepository -Name 'SourceRepository'
    $destinationRoot = Resolve-RepositoryRoot -Path $DestinationRepository -Name 'DestinationRepository'
    Assert-SafeImportRepositoryConfiguration -Repository $destinationRoot
    $legacyPathspecTarget = $null
    if ($WriteAllowedPathspec) {
        $legacyPathspecTarget = Assert-PathspecTarget -Path $WriteAllowedPathspec -Repository @($sourceRoot, $destinationRoot)
    }
    $manifest = $ManifestModel
    $fixtureAuthorityPolicy = if ($policy -is [Collections.IDictionary] -and $policy.Contains('fixturePolicy')) { $policy } else { $null }
    Assert-AuthorityAndPolicy -Manifest $manifest -FixturePolicy $fixtureAuthorityPolicy
    $records = @($manifest.components | Where-Object { [string]$_.name -ceq [string]$policy.manifestName })
    if ($records.Count -ne 1) { throw 'The requested component must have exactly one manifest record.' }
    $record = $records[0]
    if ([string]$record.status -cne 'Verified') { throw "Component $Component is $($record.status); only Verified components authorize import." }
    foreach ($field in @('allowedDestinationPaths', 'dependencyClosure', 'integrationPatches', 'verificationCommands')) {
        if (@($record.$field).Count -eq 0) { throw "Verified component has an empty $field array." }
    }
    foreach ($field in @('dependencyClosure', 'verificationCommands')) {
        foreach ($value in @($record.$field)) {
            if ([string]::IsNullOrWhiteSpace([string]$value)) { throw "Verified component contains an empty $field entry." }
        }
    }
    Assert-VerificationCommandPolicy -Policy $policy -Command ([string[]]@($record.verificationCommands))
    $sourceByDestination = @{}
    foreach ($source in @($record.sources)) {
        $path = Normalize-CrossRouteGitPath -Path ([string]$source.path)
        $destination = Normalize-CrossRouteGitPath -Path ([string]$source.destination)
        Require-LowerHex -Value ([string]$source.commit) -Length 40 -Name 'source commit'
        Require-LowerHex -Value ([string]$source.blob) -Length 40 -Name 'source blob'
        Require-LowerHex -Value ([string]$source.sha256) -Length 64 -Name 'source SHA-256'
        $sourceCommitRepository = Resolve-PolicyCommitRepository -Commit ([string]$source.commit) -SourceRoot $sourceRoot -DestinationRoot $destinationRoot -Policy $policy -DestinationPath $destination -AllowIntegration
        if ([string]$source.mode -cnotin @('100644', '100755')) { throw 'Source declares a forbidden Git mode.' }
        if ($sourceByDestination.ContainsKey($destination)) { throw 'Multiple source records target one destination.' }
        [void](Get-GitText -Repository $sourceCommitRepository -Arguments @('cat-file', '-e', '--end-of-options', ([string]$source.commit + '^{commit}')))
        $entry = Get-LsTreeEntry -Repository $sourceCommitRepository -Commit ([string]$source.commit) -Path $path
        if ($entry.Type -cne 'blob' -or $entry.Mode -cne [string]$source.mode -or $entry.Mode -cnotin @('100644', '100755')) { throw 'Source commit:path has a forbidden or mismatched Git mode/type.' }
        if ($entry.Oid -cne [string]$source.blob) { throw 'Declared source blob does not match commit:path.' }
        if ((Get-GitText -Repository $sourceCommitRepository -Arguments @('rev-parse', '--verify', '--end-of-options', ([string]$source.commit + ':' + $path))) -cne [string]$source.blob) { throw 'Resolved commit:path blob differs from the declaration.' }
        $bytes = (Invoke-CrossRouteGit -Repository $sourceCommitRepository -Arguments @('cat-file', 'blob', [string]$source.blob)).StdoutBytes
        if ((Get-Sha256 -Bytes $bytes) -cne [string]$source.sha256) { throw 'Declared source SHA-256 does not match commit:path bytes.' }
        $sourceByDestination[$destination] = [pscustomobject]@{
            record = $source
            bytes = $bytes
            repository = $sourceCommitRepository
        }
    }
    $destinationByPath = @{}
    foreach ($destination in @($record.destinations)) {
        $path = Normalize-CrossRouteGitPath -Path ([string]$destination.path)
        if ($destinationByPath.ContainsKey($path)) { throw 'Duplicate destination path.' }
        if ([string]$destination.kind -cnotin [string[]]$policy.allowedKinds) { throw 'Destination kind is not permitted by the component policy.' }
        $resultModeProperty = $destination.PSObject.Properties['resultMode']
        if ($null -eq $resultModeProperty -or [string]$resultModeProperty.Value -cnotin @('100644', '100755')) { throw 'Destination result mode must be a regular-file mode.' }
        Require-LowerHex -Value ([string]$destination.resultSha256) -Length 64 -Name 'destination result SHA-256'
        if ([string]$destination.kind -eq 'Created' -and $sourceByDestination.ContainsKey($path)) { throw 'Created destinations cannot invent source blobs.' }
        if ([string]$destination.kind -in @('Exact', 'Adapted') -and -not $sourceByDestination.ContainsKey($path)) { throw 'Imported destination lacks source provenance.' }
        $destinationByPath[$path] = $destination
    }
    foreach ($sourcePath in $sourceByDestination.Keys) {
        if (-not $destinationByPath.ContainsKey($sourcePath)) { throw 'Source provenance names no declared destination.' }
    }
    $allowed = @($record.allowedDestinationPaths | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_) })
    [void](Assert-ExactStringSet -Actual $allowed -Expected @($destinationByPath.Keys) -Name 'allowedDestinationPaths')
    foreach ($path in $destinationByPath.Keys) {
        $destination = $destinationByPath[$path]
        if ([string]$destination.kind -eq 'Exact') {
            $patchGroupProperty = $destination.PSObject.Properties['patchGroupId']
            if (($null -ne $patchGroupProperty) -and ($null -ne $patchGroupProperty.Value)) { throw 'Exact destinations cannot use a patch group.' }
            $source = $sourceByDestination[$path]
            if ([string]$source.record.commit -cnotin [string[]]$policy.exactSourceCommits) { throw 'Exact destination must use an exact source-authority commit.' }
            if (Test-AnyPathRule -Path $path -Rule ([string[]]$policy.integrationOnlyPaths)) { throw 'Integration-only destination cannot be Exact.' }
            if ([string]$destination.resultMode -cne [string]$source.record.mode) { throw 'Exact destination mode differs from source mode.' }
            $full = Join-Path -Path $destinationRoot -ChildPath ($path -replace '/', [IO.Path]::DirectorySeparatorChar)
            $actual = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $full -ErrorAction Stop).Path)
            if ((Get-Sha256 -Bytes $actual) -cne [string]$destination.resultSha256 -or (Get-Sha256 -Bytes $actual) -cne [string]$source.record.sha256) { throw 'Exact destination bytes differ from commit:path:blob authority.' }
        }
    }
    $groups = @($record.integrationPatches)
    $groupIds = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::Ordinal)
    foreach ($group in $groups) {
        if (-not $groupIds.Add([string]$group.id)) { throw 'Patch-group IDs must be unique.' }
    }
    $patchDestinations = @($destinationByPath.Values | Where-Object { [string]$_.kind -in @('Adapted', 'Created') })
    foreach ($destination in $patchDestinations) {
        $patchGroupProperty = $destination.PSObject.Properties['patchGroupId']
        $patchGroupId = if ($null -eq $patchGroupProperty) { '' } else { [string]$patchGroupProperty.Value }
        $matches = @($groups | Where-Object { [string]$_.id -ceq $patchGroupId })
        if ([string]::IsNullOrWhiteSpace($patchGroupId) -or $matches.Count -ne 1) { throw 'Adapted/Created destination lacks exactly one patch group.' }
    }
    $patchByPath = @{}
    foreach ($group in $groups) {
        $members = @($patchDestinations | Where-Object { [string]$_.patchGroupId -ceq [string]$group.id })
        $patch = Assert-PatchGroup -SourceRoot $sourceRoot -DestinationRoot $destinationRoot -Group $group -Members $members -SourceByDestination $sourceByDestination -Policy $policy
        if ($patchByPath.ContainsKey($patch.Path)) { throw 'Patch paths must be unique.' }
        $patchByPath[$patch.Path] = $patch
    }
    if ($VerifyStaged) { Assert-StagedState -Repository $destinationRoot -DestinationByPath $destinationByPath -PatchByPath $patchByPath -ManifestPaths $script:ManifestPaths }
    if ($WriteAllowedPathspec) {
        $stagingAllowed = @($destinationByPath.Keys) + @($patchByPath.Keys) + @($script:ManifestPaths)
        $changed = @(Get-WorktreeChangedEndpoints -Repository $destinationRoot)
        foreach ($path in $changed) {
            if ($path -cnotin $stagingAllowed) { throw "An actual changed path is outside the verified component boundary: $path" }
        }
        $pathspecBytes = New-Object -TypeName 'Collections.Generic.List[byte]'
        foreach ($path in $changed) {
            $pathspecBytes.AddRange([Text.Encoding]::UTF8.GetBytes($path))
            $pathspecBytes.Add(0)
        }
        Write-AtomicPathspec -Target $legacyPathspecTarget -Bytes $pathspecBytes.ToArray()
    }
    return [pscustomobject]@{
        Component = $Component
        DestinationCount = $destinationByPath.Count
        PatchCount = $patchByPath.Count
        VerifyStaged = [bool]$VerifyStaged
    }
}

function Invoke-CrossRouteImportVerificationInternal {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Component,
        [Parameter(Mandatory)][string]$SourceRepository,
        [Parameter(Mandatory)][string]$DestinationRepository,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][object]$Policy,
        [string]$WriteAllowedPathspec,
        [switch]$VerifyStaged,
        [switch]$StageVerified
    )

    if (@($WriteAllowedPathspec, [bool]$VerifyStaged, [bool]$StageVerified | Where-Object { $_ }).Count -gt 1) {
        throw 'WriteAllowedPathspec, VerifyStaged, and StageVerified are separate phases.'
    }
    $policy = $Policy
    $sourceRoot = Resolve-RepositoryRoot -Path $SourceRepository -Name 'SourceRepository'
    $destinationRoot = Resolve-RepositoryRoot -Path $DestinationRepository -Name 'DestinationRepository'
    $pathspecTarget = $null
    if ($WriteAllowedPathspec) {
        $pathspecTarget = Assert-PathspecTarget -Path $WriteAllowedPathspec -Repository @($sourceRoot, $destinationRoot)
    }

    $canonicalManifest = Resolve-ContainedRegularFile -Root $destinationRoot -RelativePath $script:ManifestPaths[0] -Name 'ManifestPath'
    $requestedManifest = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($canonicalManifest, $requestedManifest)) {
        throw 'ManifestPath must be the canonical manifest inside DestinationRepository.'
    }
    $manifest = Get-ManifestModel -ManifestPath $canonicalManifest
    $fixtureAuthorityPolicy = if ($policy -is [Collections.IDictionary] -and $policy.Contains('fixturePolicy')) { $policy } else { $null }
    Assert-AuthorityAndPolicy -Manifest $manifest -FixturePolicy $fixtureAuthorityPolicy
    $records = @($manifest.components | Where-Object { [string]$_.name -ceq [string]$policy.manifestName })
    if ($records.Count -ne 1) {
        throw 'The requested component must have exactly one manifest record.'
    }
    $record = $records[0]
    if ([string]$record.status -cne 'Verified') {
        throw "Component $Component is $($record.status); only Verified components authorize import."
    }
    $evidenceHashes = @{}
    foreach ($manifestRelativePath in $script:ManifestPaths) {
        $manifestFullPath = Resolve-ContainedRegularFile -Root $destinationRoot -RelativePath $manifestRelativePath -Name 'Manifest evidence'
        $evidenceHashes[$manifestRelativePath] = Get-Sha256 -Bytes ([IO.File]::ReadAllBytes($manifestFullPath))
    }
    $destinationPaths = @($record.destinations | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_.path) })
    $allowedPaths = @($record.allowedDestinationPaths | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_) })
    $sourceDestinationPaths = @($record.sources | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_.destination) })
    $patchPaths = @($record.integrationPatches | ForEach-Object { Normalize-CrossRouteGitPath -Path ([string]$_.patchPath) })
    Assert-NoPathCollisions -Path ([string[]]$destinationPaths) -Name 'destination paths'
    Assert-NoPathCollisions -Path ([string[]]$allowedPaths) -Name 'allowedDestinationPaths'
    Assert-SourceIdentityConsistency -Source @($record.sources)
    Assert-NoPathCollisions -Path ([string[]]$sourceDestinationPaths) -Name 'source destination paths'
    Assert-NoPathCollisions -Path ([string[]]$patchPaths) -Name 'integration patch paths'
    [void](Assert-ExactStringSet -Actual $allowedPaths -Expected ([string[]]$destinationPaths) -Name 'allowedDestinationPaths')
    Assert-ComponentPolicyBoundary -Policy $policy -DestinationPath ([string[]]$destinationPaths) -PatchPath ([string[]]$patchPaths) -Record $record

    foreach ($path in $destinationPaths) {
        $fullPath = Resolve-ContainedRegularFile -Root $destinationRoot -RelativePath $path -Name 'Imported destination'
        $evidenceHashes[$path] = Get-Sha256 -Bytes ([IO.File]::ReadAllBytes($fullPath))
    }
    foreach ($path in $patchPaths) {
        $fullPath = Resolve-ContainedRegularFile -Root $destinationRoot -RelativePath $path -Name 'Integration patch'
        $evidenceHashes[$path] = Get-Sha256 -Bytes ([IO.File]::ReadAllBytes($fullPath))
    }

    $sourcePreIdentity = Get-RepositoryIdentity -Repository $sourceRoot -Prefix 'source'
    $destinationPreIdentity = Get-RepositoryIdentity -Repository $destinationRoot -Prefix 'destination' -IncludeIndex
    $sourcePreHead = $sourcePreIdentity.Head
    $sourcePreTree = $sourcePreIdentity.Tree
    $destinationPreHead = $destinationPreIdentity.Head
    $destinationPreTree = $destinationPreIdentity.Tree

    $verification = if ($VerifyStaged) {
        Invoke-CrossRouteImportVerificationLegacy -Component $Component -SourceRepository $sourceRoot -DestinationRepository $destinationRoot -ManifestModel $manifest -Policy $policy -VerifyStaged
    }
    else {
        Invoke-CrossRouteImportVerificationLegacy -Component $Component -SourceRepository $sourceRoot -DestinationRepository $destinationRoot -ManifestModel $manifest -Policy $policy
    }

    $changed = @()
    if ($WriteAllowedPathspec) {
        $stagingAllowed = @($destinationPaths) + @($patchPaths) + @($script:ManifestPaths)
        Assert-NoPathCollisions -Path ([string[]]$stagingAllowed) -Name 'staging boundary paths'
        $changed = @(Get-WorktreeChangedEndpoints -Repository $destinationRoot)
        Assert-NoPathCollisions -Path ([string[]]$changed) -Name 'actual changed paths'
        foreach ($path in $changed) {
            if ($path -cnotin $stagingAllowed) {
                throw "An actual changed path is outside the verified component boundary: $path"
            }
        }
    }

    $sourcePostIdentity = Get-RepositoryIdentity -Repository $sourceRoot -Prefix 'source'
    $destinationPostIdentity = Get-RepositoryIdentity -Repository $destinationRoot -Prefix 'destination' -IncludeIndex
    $sourcePostHead = $sourcePostIdentity.Head
    $sourcePostTree = $sourcePostIdentity.Tree
    $destinationPostHead = $destinationPostIdentity.Head
    $destinationPostTree = $destinationPostIdentity.Tree
    [void]$sourcePreHead
    [void]$sourcePreTree
    [void]$destinationPreHead
    [void]$destinationPreTree
    [void]$sourcePostHead
    [void]$sourcePostTree
    [void]$destinationPostHead
    [void]$destinationPostTree
    Assert-RepositoryIdentityEqual -Before $sourcePreIdentity -After $sourcePostIdentity
    Assert-RepositoryIdentityEqual -Before $destinationPreIdentity -After $destinationPostIdentity
    foreach ($path in $evidenceHashes.Keys) {
        $fullPath = Resolve-ContainedRegularFile -Root $destinationRoot -RelativePath $path -Name 'Final verified evidence'
        $finalHash = Get-Sha256 -Bytes ([IO.File]::ReadAllBytes($fullPath))
        if ($finalHash -cne [string]$evidenceHashes[$path]) {
            throw "Verified evidence changed after the final repository snapshot: $path"
        }
    }

    if ($StageVerified) {
        $destinationByPath = @{}
        foreach ($destination in @($record.destinations)) {
            $destinationByPath[(Normalize-CrossRouteGitPath -Path ([string]$destination.path))] = $destination
        }
        $patchByPath = @{}
        foreach ($patch in @($record.integrationPatches)) {
            $patchPath = Normalize-CrossRouteGitPath -Path ([string]$patch.patchPath)
            $patchByPath[$patchPath] = [pscustomobject]@{ Sha256 = [string]$patch.patchSha256 }
        }
        Set-VerifiedIndexState -Repository $destinationRoot -DestinationByPath $destinationByPath -PatchByPath $patchByPath -EvidenceHashes $evidenceHashes -ManifestPaths $script:ManifestPaths
        $verification = Invoke-CrossRouteImportVerificationLegacy -Component $Component -SourceRepository $sourceRoot -DestinationRepository $destinationRoot -ManifestModel $manifest -Policy $policy -VerifyStaged
    }

    if ($WriteAllowedPathspec) {
        Assert-SafeImportRepositoryConfiguration -Repository $destinationRoot
        $pathspecBytes = New-Object -TypeName 'Collections.Generic.List[byte]'
        foreach ($path in $changed) {
            $pathspecBytes.AddRange([Text.Encoding]::UTF8.GetBytes($path))
            $pathspecBytes.Add(0)
        }
        Write-AtomicPathspec -Target $pathspecTarget -Bytes $pathspecBytes.ToArray()
    }
    return [pscustomobject]@{
        Component = $verification.Component
        DestinationCount = $verification.DestinationCount
        PatchCount = $verification.PatchCount
        VerifyStaged = $verification.VerifyStaged
    }
}

function Invoke-CrossRouteImportVerification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Component,
        [Parameter(Mandatory)][string]$SourceRepository,
        [Parameter(Mandatory)][string]$DestinationRepository,
        [Parameter(Mandatory)][string]$ManifestPath,
        [string]$WriteAllowedPathspec,
        [switch]$VerifyStaged,
        [switch]$StageVerified
    )

    if ($Component -cnotin @('UO1', 'GgufRuntime', 'OpenVino')) {
        throw 'Unknown component selector.'
    }
    $policy = Get-ApprovedComponentPolicy -Component $Component
    return Invoke-CrossRouteImportVerificationInternal -Component $Component -SourceRepository $SourceRepository -DestinationRepository $DestinationRepository -ManifestPath $ManifestPath -Policy $policy -WriteAllowedPathspec $WriteAllowedPathspec -VerifyStaged:$VerifyStaged -StageVerified:$StageVerified
}

Export-ModuleMember -Function @(
    'Assert-NoHiddenIndexState'
    'Assert-VerificationPowerShellStructure'
    'Get-RepositoryIdentity'
    'Get-Sha256'
    'Get-VerifiedIndexFileState'
    'Invoke-CrossRouteGit'
    'Invoke-CrossRouteImportVerification'
    'Invoke-CrossRouteProcess'
    'New-VerifiedTemporaryIndex'
    'Publish-VerifiedTemporaryIndex'
    'Remove-VerifiedTemporaryIndex'
)
