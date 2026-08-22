using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using LLama.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies stable diagnostic mapping without invoking native code.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ProbeFailureMapperTests
{
    [TestMethod]
    public void Map_WithFileNotFound_ReturnsModelFileNotFound()
    {
        AssertCode(
            new FileNotFoundException("missing"),
            "MI-OP-MODEL-FILE-NOT-FOUND");
    }

    [TestMethod]
    public void Map_WithUnauthorizedAccess_ReturnsModelFileAccessDenied()
    {
        AssertCode(
            new UnauthorizedAccessException("denied"),
            "MI-OP-MODEL-FILE-ACCESS-DENIED");
    }

    [TestMethod]
    public void Map_WithGenericIo_ReturnsModelFileIo()
    {
        AssertCode(
            new IOException("io"),
            "MI-OP-MODEL-FILE-IO");
    }

    [TestMethod]
    public void Map_WithMissingNativeLibrary_ReturnsRuntimeUnavailable()
    {
        AssertCode(
            new DllNotFoundException("missing"),
            "MI-OP-RUNTIME-UNAVAILABLE");
    }

    [TestMethod]
    public void Map_WithBadNativeImage_ReturnsArchitectureMismatch()
    {
        AssertCode(
            new BadImageFormatException("wrong architecture"),
            "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH");
    }

    [TestMethod]
    public void Map_WithLoadWeightsFailure_ReturnsModelLoadFailed()
    {
        ProbeFailure failure = ProbeFailureMapper.Map(
            new LoadWeightsFailedException("model.gguf"));

        Assert.AreEqual("MI-PROBE-MODEL-LOAD-FAILED", failure.Code);
        Assert.IsNotNull(failure.Type);
        StringAssert.Contains(failure.Message, "model.gguf");
    }

    [TestMethod]
    public void Map_WithNestedMissingNativeLibrary_ReturnsRuntimeUnavailable()
    {
        var exception = new TypeInitializationException(
            "LLama.Native.NativeApi",
            new DllNotFoundException("missing"));

        ProbeFailure failure = ProbeFailureMapper.Map(exception);

        Assert.AreEqual("MI-OP-RUNTIME-UNAVAILABLE", failure.Code);
        Assert.AreEqual(
            typeof(DllNotFoundException).FullName,
            failure.Type);
        Assert.AreEqual("missing", failure.Message);
    }

    [TestMethod]
    public void Map_WithNestedBadNativeImage_ReturnsArchitectureMismatch()
    {
        var exception = new TypeInitializationException(
            "LLama.Native.NativeApi",
            new BadImageFormatException("wrong architecture"));

        ProbeFailure failure = ProbeFailureMapper.Map(exception);

        Assert.AreEqual(
            "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH",
            failure.Code);
        Assert.AreEqual(
            typeof(BadImageFormatException).FullName,
            failure.Type);
    }

    [TestMethod]
    public void Map_WithUnexpectedException_ReturnsRuntimeInspectionFailed()
    {
        AssertCode(
            new InvalidOperationException("unexpected"),
            "MI-OP-RUNTIME-INSPECTION-FAILED");
    }

    [TestMethod]
    public void Map_WithNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ProbeFailureMapper.Map(null!));
    }

    private static void AssertCode(
        Exception exception,
        string expectedCode)
    {
        ProbeFailure failure = ProbeFailureMapper.Map(exception);

        Assert.AreEqual(expectedCode, failure.Code);
        Assert.AreEqual(exception.GetType().FullName, failure.Type);
        Assert.AreEqual(exception.Message, failure.Message);
    }
}
