using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class GgufSavedCacheHandoffTests
{
    [TestMethod]
    [DataRow("f16", GgufCacheType.F16)]
    [DataRow("q8_0", GgufCacheType.Q8Zero)]
    [DataRow("turbo3", GgufCacheType.Turbo3)]
    [DataRow("turbo4", GgufCacheType.Turbo4)]
    public void RestoredDeclaredCacheReachesExactChatPayloadAndRetainsMemoryGate(string cache, GgufCacheType expected)
    {
        using var fixture = new Fixture(cache);
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(fixture.Prepared, fixture.Custody,
            out var authority, out bool rejected));
        Assert.IsFalse(rejected);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var evaluation = authority!.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(12UL << 30), 0, 100UL << 30, now), new HashSet<string>(), now, CancellationToken.None);
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, evaluation.Screen.State);
        using var registry = new CurrentModelChatLaunchRegistry(fixture.Custody);
        var handoff = authority.ResolveCurrentModel(evaluation, registry);
        Assert.IsNotNull(handoff);
        Assert.IsTrue(registry.TryGetExecutionPayload(handoff, out var payload));
        Assert.AreEqual(expected, payload!.Gguf!.KeyCacheType);
        Assert.AreEqual(expected, payload.Gguf.ValueCacheType);
        Assert.AreEqual(4096, payload.Gguf.ContextSize);
        Assert.IsFalse(payload.Gguf.RequiresPersistentConversion);
        var lowMemory = authority.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(512UL << 20), 0, 100UL << 30, now), new HashSet<string>(), now, CancellationToken.None);
        Assert.IsFalse(lowMemory.Screen.UseCurrentModelAvailable);
        Assert.IsNull(authority.ResolveCurrentModel(lowMemory, registry));
    }

    [TestMethod]
    [DataRow("turbo2")]
    [DataRow("q4_0")]
    [DataRow("mixed")]
    [DataRow("context")]
    [DataRow("tampered")]
    public void SavedUnsupportedOrAlteredProfileCannotSilentlyBecomeDefault(string condition)
    {
        using var fixture = new Fixture(condition);
        Assert.IsFalse(GgufOptimizationProductionAuthority.TryCreate(fixture.Prepared, fixture.Custody,
            out var authority, out bool rejected));
        Assert.IsTrue(rejected);
        Assert.IsNull(authority);
    }

    private sealed class Fixture : IDisposable
    {
        private const string ModelHash = "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        private const long ModelLength = 2_099_501_664;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "granite-cache-handoff-" + Guid.NewGuid().ToString("N"));
        public PreparedGgufCompatibilityInput Prepared { get; }
        public ModelSourceCustodyRegistry Custody { get; } = new();
        public Fixture(string cache)
        {
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "GgufRuntime"));
            string model = Path.Combine(_root, "model.gguf");
            // The inspection owner is a boundary fixture. Sparse bytes avoid
            // copying a multi-GB model; these tests do not replace model inspection.
            using (FileStream file = new(model, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Assert.IsTrue(DeviceIoControl(file.SafeFileHandle, 0x900c4, IntPtr.Zero, 0,
                    IntPtr.Zero, 0, out _, IntPtr.Zero), "The fixture requires NTFS sparse-file support.");
                file.SetLength(ModelLength);
            }
            Guid handoff = Guid.NewGuid();
            Prepared = new PreparedGgufCompatibilityInput(Guid.NewGuid(), handoff, ModelHash, Guid.NewGuid(), new string('a', 64),
                GgufCompatibilityModelInput.Create((ulong)ModelLength, 40, 2560, 40, 8, 131072, 15, 2, 3_402_836_480),
                CompatibilityHardwareInput.Create(TotalPhysicalMemory.FromBytes(16UL << 30), 0, 100UL << 30,
                    [DeviceRouteId.Cpu], [CompatibilityBackend.Cpu]));
            Custody.Register(new ModelSourceCustodyRecord(new ModelSourceCustodyKey(handoff, ModelHash, ModelLength,
                OptimizationRoute.Gguf), model));
            byte[] profile = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "granite.gguf-issued-runtime-profile.v1", validation_claim = "declared-not-runtime-validated",
                configuration_sha256 = new string('b', 64), runtime_build_id = PublishedGgufOptimizationEvidence.RuntimeBuildId,
                runtime_source_commit = PublishedGgufOptimizationEvidence.RuntimeSourceCommit, backend = "cpu", device_id = "CPU",
                context_size = cache == "context" ? 8192 : 4096,
                key_cache_type = cache is "mixed" or "context" or "tampered" ? "turbo3" : cache,
                value_cache_type = cache == "mixed" ? "f16" : cache is "context" or "tampered" ? "turbo3" : cache,
                gpu_layer_count = 0, flash_attention = true, thread_count = 4, batch_size = 512,
                evidence_grade = "Measured", profile_id = "cpu", maximum_generated_tokens = 256,
                persistent_target_weight_format = "imported", requires_persistent_conversion = false,
            });
            string path = Path.Combine(_root, "runtime-profile.json");
            File.WriteAllBytes(path, profile);
            File.WriteAllBytes(Path.Combine(_root, "bundle-manifest.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "granite.gguf-runtime-profile-bundle.v1", profile_claim = "issued-declared-not-runtime-validated", route = "gguf",
                configuration_sha256 = new string('b', 64), source_sha256 = ModelHash, source_length_bytes = ModelLength,
                members = new[] {
                    new { name = "model.gguf", sha256 = ModelHash, length_bytes = ModelLength },
                    new { name = "runtime-profile.json", sha256 = Convert.ToHexString(SHA256.HashData(profile)).ToLowerInvariant(), length_bytes = (long)profile.Length },
                },
            }));
            if (cache == "tampered") File.AppendAllText(path, " ");
        }
        public void Dispose() { Custody.Dispose(); Directory.Delete(_root, recursive: true); }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, IntPtr input, uint inputSize,
            IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
    }
}
