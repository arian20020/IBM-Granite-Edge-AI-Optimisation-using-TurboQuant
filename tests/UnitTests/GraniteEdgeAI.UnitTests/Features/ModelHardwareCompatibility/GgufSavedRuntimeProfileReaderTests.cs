using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class GgufSavedRuntimeProfileReaderTests
{
    [TestMethod]
    [DataRow("f16")]
    [DataRow("q8_0")]
    [DataRow("turbo2")]
    [DataRow("turbo3")]
    [DataRow("turbo4")]
    public void IntactBoundBundleRetainsDeclaredCacheWithoutClaimingRuntimeValidation(string cache)
    {
        using var bundle = new Bundle(cache);
        Assert.AreEqual(GgufSavedProfileDisposition.Restored,
            GgufSavedRuntimeProfileReader.Read(bundle.Model, bundle.ModelHash, 4,
                "build", "commit", out var profile));
        Assert.AreEqual(cache, profile!.KeyCache);
        Assert.AreEqual(cache, profile.ValueCache);
        Assert.AreEqual(4096, profile.Context);
    }

    [TestMethod]
    public void BareModelHasNoInventedCacheProfile()
    {
        using var bundle = new Bundle("turbo3");
        File.Delete(bundle.Profile);
        File.Delete(bundle.Manifest);
        Assert.AreEqual(GgufSavedProfileDisposition.Absent,
            GgufSavedRuntimeProfileReader.Read(bundle.Model, bundle.ModelHash, 4,
                "build", "commit", out var profile));
        Assert.IsNull(profile);
    }

    [TestMethod]
    [DataRow("model")]
    [DataRow("runtime")]
    [DataRow("profile")]
    [DataRow("missing-manifest")]
    [DataRow("oversized")]
    public void UnboundOrStaleProfileIsRejectedInsteadOfDefaulted(string corruption)
    {
        using var bundle = new Bundle("turbo3");
        if (corruption == "profile") File.AppendAllText(bundle.Profile, " ");
        if (corruption == "missing-manifest") File.Delete(bundle.Manifest);
        if (corruption == "oversized") File.WriteAllText(bundle.Profile, new string(' ', 70000));
        Assert.AreEqual(GgufSavedProfileDisposition.Rejected,
            GgufSavedRuntimeProfileReader.Read(bundle.Model,
                corruption == "model" ? new string('a', 64) : bundle.ModelHash, 4,
                corruption == "runtime" ? "other-build" : "build", "commit", out var profile));
        Assert.IsNull(profile);
    }

    private sealed class Bundle : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "granite-profile-test-" + Guid.NewGuid().ToString("N"));
        public string Model => Path.Combine(_root, "model.gguf");
        public string Profile => Path.Combine(_root, "runtime-profile.json");
        public string Manifest => Path.Combine(_root, "bundle-manifest.json");
        public string ModelHash { get; }
        public Bundle(string cache)
        {
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(Model, [1, 2, 3, 4]);
            ModelHash = Hash(File.ReadAllBytes(Model));
            byte[] profile = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "granite.gguf-issued-runtime-profile.v1",
                validation_claim = "declared-not-runtime-validated",
                configuration_sha256 = new string('b', 64),
                runtime_build_id = "build", runtime_source_commit = "commit",
                backend = "cpu", device_id = "CPU", context_size = 4096,
                key_cache_type = cache, value_cache_type = cache,
                gpu_layer_count = 0, flash_attention = true, thread_count = 4,
                batch_size = 128, evidence_grade = "Measured", profile_id = "cpu",
                maximum_generated_tokens = 512, persistent_target_weight_format = "imported",
                requires_persistent_conversion = false,
            });
            File.WriteAllBytes(Profile, profile);
            File.WriteAllBytes(Manifest, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "granite.gguf-runtime-profile-bundle.v1",
                profile_claim = "issued-declared-not-runtime-validated", route = "gguf",
                configuration_sha256 = new string('b', 64), source_sha256 = ModelHash,
                source_length_bytes = 4,
                members = new[] {
                    new { name = "model.gguf", sha256 = ModelHash, length_bytes = 4 },
                    new { name = "runtime-profile.json", sha256 = Hash(profile), length_bytes = profile.Length },
                },
            }));
        }
        private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
