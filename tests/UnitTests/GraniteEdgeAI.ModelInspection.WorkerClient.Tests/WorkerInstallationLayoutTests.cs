using System.Reflection;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Locks the production worker to one package-relative executable and makes the
/// verified executable directory, rather than caller input, the child CWD.
/// </summary>
[TestClass]
public sealed class WorkerInstallationLayoutTests
{
    private const string ProductionRelativePath =
        @"ModelInspection\Worker\GraniteEdgeAI.ModelInspection.Worker.exe";

    [TestMethod]
    public void ProductionClientExposesOnlyOptionsConstructor()
    {
        ConstructorInfo[] constructors = typeof(InspectionWorkerClient)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.HasCount(1, constructors);
        CollectionAssert.AreEqual(
            new[] { typeof(WorkerClientOptions) },
            constructors[0].GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray());
    }

    [TestMethod]
    public void ProductionLayoutNamesExactNestedWorkerExecutable()
    {
        Type? layout = typeof(InspectionWorkerClient).Assembly.GetType(
            "GraniteEdgeAI.ModelInspection.WorkerClient.WorkerInstallationLayout",
            throwOnError: false,
            ignoreCase: false);
        Assert.IsNotNull(layout);
        FieldInfo? relativePath = layout.GetField(
            "WorkerExecutableRelativePath",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(relativePath);

        Assert.AreEqual(
            ProductionRelativePath,
            relativePath.GetValue(obj: null));
    }

    [TestMethod]
    public void LaunchRequestDerivesNestedAndFlatWorkingDirectories()
    {
        using TemporaryLaunchRoot nested = TemporaryLaunchRoot.Create(
            ProductionRelativePath);
        using TemporaryLaunchRoot flat = TemporaryLaunchRoot.Create(
            "fixture.exe");

        WindowsProcessLaunchRequest nestedRequest = CreateLaunchRequest(
            nested.Executable);
        WindowsProcessLaunchRequest flatRequest = CreateLaunchRequest(
            flat.Executable);

        Assert.AreEqual(
            Path.Combine(nested.RootDirectory, "ModelInspection", "Worker"),
            nestedRequest.WorkingDirectory);
        Assert.AreEqual(flat.RootDirectory, flatRequest.WorkingDirectory);
    }

    [TestMethod]
    public void LaunchRequestRetainsCanonicalPathWithinLegacyLimit()
    {
        using TemporaryLaunchRoot deep = TemporaryLaunchRoot.CreateDeep("fixture.exe");
        WindowsProcessLaunchRequest request = CreateConverterLaunchRequest(deep.Executable);

        Assert.IsTrue(deep.RootDirectory.Length > 120);
        Assert.IsTrue(deep.Executable.ExecutableFinalPath.Length < 260);
        Assert.AreEqual(deep.Executable.ExecutableFinalPath, request.ApplicationName);
        Assert.AreEqual(deep.RootDirectory, request.WorkingDirectory);
    }

    [TestMethod]
    public void LaunchRequestShortensADeepVerifiedWorkerPathForLegacyRuntimeImports()
    {
        using TemporaryLaunchRoot deep = TemporaryLaunchRoot.CreateDeep(
            "fixture.exe", beyondLegacyLimit: true);

        WindowsProcessLaunchRequest request = CreateConverterLaunchRequest(
            deep.Executable);

        Assert.IsTrue(deep.Executable.ExecutableFinalPath.Length >= 260);
        Assert.IsTrue(request.ApplicationName.Length < 260);
        Assert.IsTrue(
            request.WorkingDirectory.Length < deep.RootDirectory.Length,
            request.WorkingDirectory);
        Assert.IsTrue(Directory.Exists(request.WorkingDirectory));
        Assert.IsTrue(File.Exists(Path.Combine(
            request.WorkingDirectory,
            "fixture.exe")));
    }

    [TestMethod]
    public void LaunchRequestHasNoCallerSuppliedWorkingDirectory()
    {
        ConstructorInfo[] constructors = typeof(WindowsProcessLaunchRequest)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsFalse(
            constructors.Any(
                static constructor => constructor.GetParameters().Any(
                    static parameter => parameter.ParameterType == typeof(string))),
            "The package root must not be accepted as a caller-supplied CWD.");
        Assert.IsTrue(
            constructors.Any(
                static constructor => constructor.GetParameters()
                    .Select(static parameter => parameter.ParameterType)
                    .SequenceEqual(
                    [
                        typeof(VerifiedWorkerExecutable),
                        typeof(IReadOnlyDictionary<string, string>),
                        typeof(IReadOnlyList<string>),
                        typeof(TimeSpan)
                    ])),
            "The launch request must expose the derived-CWD constructor.");
    }

    [TestMethod]
    public void LaunchRequestRejectsDerivedDirectoryOutsideApprovedRoot()
    {
        string ownedParent = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-WorkerLayout-Outside",
            Guid.NewGuid().ToString("N"));
        string approvedRoot = Path.Combine(ownedParent, "Package");
        string outsideDirectory = Path.Combine(ownedParent, "Outside");
        string outsideExecutable = Path.Combine(
            outsideDirectory,
            "worker.exe");
        Directory.CreateDirectory(approvedRoot);
        Directory.CreateDirectory(outsideDirectory);
        File.Copy(
            typeof(WorkerInstallationLayoutTests).Assembly.Location,
            outsideExecutable);

        try
        {
            using VerifiedWorkerExecutable executable = new(
                approvedRoot,
                outsideExecutable,
                File.OpenHandle(outsideExecutable));

            WorkerClientPolicyException error =
                Assert.ThrowsExactly<WorkerClientPolicyException>(
                    () => new WindowsProcessLaunchRequest(
                        executable,
                        new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase),
                        [],
                        TimeSpan.FromSeconds(5)));

            Assert.AreEqual(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                error.Failure.Code);
            Assert.IsFalse(
                error.Failure.Message.Contains(
                    ownedParent,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(ownedParent))
            {
                Directory.Delete(ownedParent, recursive: true);
            }
        }
    }

    private static WindowsProcessLaunchRequest CreateLaunchRequest(
        VerifiedWorkerExecutable executable) => new(
            executable,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        [],
        TimeSpan.FromSeconds(5));

    private static WindowsProcessLaunchRequest CreateConverterLaunchRequest(
        VerifiedWorkerExecutable executable) => new(
        executable,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        ["-I", "-s", "-E", "-S", "-B", "-m", "converter"],
        TimeSpan.FromSeconds(5));

    private sealed class TemporaryLaunchRoot : IDisposable
    {
        private TemporaryLaunchRoot(
            string rootDirectory,
            VerifiedWorkerExecutable executable)
        {
            RootDirectory = rootDirectory;
            Executable = executable;
        }

        internal string RootDirectory { get; }

        internal VerifiedWorkerExecutable Executable { get; }

        internal static TemporaryLaunchRoot Create(string relativePath)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI-WorkerLayout",
                Guid.NewGuid().ToString("N"));
            string executablePath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(executablePath) ?? root);
            File.Copy(
                typeof(WorkerInstallationLayoutTests).Assembly.Location,
                executablePath);

            VerifiedWorkerExecutable executable =
                new WorkerExecutableResolver(relativePath).Resolve(root);
            return new TemporaryLaunchRoot(root, executable);
        }

        internal static TemporaryLaunchRoot CreateDeep(string relativePath, bool beyondLegacyLimit = false)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "GraniteWorkerLongRoot",
                "RuntimePackageSegmentOne",
                "RuntimePackageSegmentTwo",
                "RuntimePackageSegmentThree",
                "RuntimePackageSegmentFour",
                Guid.NewGuid().ToString("N") + (beyondLegacyLimit ? new string('x', 100) : string.Empty));
            string executablePath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(root);
            File.Copy(
                typeof(WorkerInstallationLayoutTests).Assembly.Location,
                executablePath);

            VerifiedWorkerExecutable executable =
                beyondLegacyLimit
                    ? new VerifiedWorkerExecutable(root, executablePath, File.OpenHandle(executablePath))
                    : new WorkerExecutableResolver(relativePath).Resolve(root);
            return new TemporaryLaunchRoot(root, executable);
        }

        public void Dispose()
        {
            Executable.Dispose();
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }
}
