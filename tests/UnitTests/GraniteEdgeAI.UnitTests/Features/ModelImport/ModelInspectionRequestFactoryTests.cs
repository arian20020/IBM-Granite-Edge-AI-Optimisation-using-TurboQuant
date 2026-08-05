using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Defines the file-backed boundary that converts a successful quick scan into
/// one immutable Model Inspection request immediately before navigation.
/// </summary>
[TestClass]
public sealed class ModelInspectionRequestFactoryTests
{
    [TestMethod]
    public void TryCreate_MatchingExistingGguf_CreatesImmutableRequest()
    {
        string modelPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            ModelQuickScanResult scanResult = CreateSuccessfulScan(64);
            DateTimeOffset expectedLastWriteTime = new(
                File.GetLastWriteTimeUtc(modelPath));

            bool created = ModelInspectionRequestFactory.TryCreate(
                modelPath,
                scanResult,
                out ModelInspectionRequest? request);

            Assert.IsTrue(created);
            Assert.IsNotNull(request);
            Assert.AreEqual(Path.GetFullPath(modelPath), request.ModelPath);
            Assert.AreEqual(Path.GetFileName(modelPath), request.FileName);
            Assert.AreEqual(64L, request.ExpectedFileIdentity.LengthBytes);
            Assert.AreEqual(
                expectedLastWriteTime,
                request.ExpectedFileIdentity.LastWriteTimeUtc);
            Assert.AreSame(scanResult.ModelName, request.QuickScan.ModelName);
            Assert.AreEqual(3U, request.QuickScan.GgufVersion);
        }
        finally
        {
            DeleteTemporaryModelFile(modelPath);
        }
    }

    [TestMethod]
    public void TryCreate_MissingFile_ReturnsFalseWithoutRequest()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.gguf");

        bool created = ModelInspectionRequestFactory.TryCreate(
            missingPath,
            CreateSuccessfulScan(64),
            out ModelInspectionRequest? request);

        Assert.IsFalse(created);
        Assert.IsNull(request);
    }

    [TestMethod]
    public void TryCreate_Directory_ReturnsFalseWithoutRequest()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"granite-edge-ai-{Guid.NewGuid():N}.gguf");
        Directory.CreateDirectory(directoryPath);

        try
        {
            bool created = ModelInspectionRequestFactory.TryCreate(
                directoryPath,
                CreateSuccessfulScan(64),
                out ModelInspectionRequest? request);

            Assert.IsFalse(created);
            Assert.IsNull(request);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [TestMethod]
    public void TryCreate_ChangedFileSize_ReturnsFalseWithoutRequest()
    {
        string modelPath = CreateTemporaryModelFile(lengthBytes: 65);

        try
        {
            bool created = ModelInspectionRequestFactory.TryCreate(
                modelPath,
                CreateSuccessfulScan(64),
                out ModelInspectionRequest? request);

            Assert.IsFalse(created);
            Assert.IsNull(request);
        }
        finally
        {
            DeleteTemporaryModelFile(modelPath);
        }
    }

    [TestMethod]
    public void TryCreate_NonGgufExtension_ReturnsFalseWithoutRequest()
    {
        string modelPath = CreateTemporaryModelFile(
            lengthBytes: 64,
            extension: ".bin");

        try
        {
            bool created = ModelInspectionRequestFactory.TryCreate(
                modelPath,
                CreateSuccessfulScan(64),
                out ModelInspectionRequest? request);

            Assert.IsFalse(created);
            Assert.IsNull(request);
        }
        finally
        {
            DeleteTemporaryModelFile(modelPath);
        }
    }

    [TestMethod]
    public void TryCreate_FailedScan_ReturnsFalseWithoutRequest()
    {
        string modelPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            ModelQuickScanResult failedScan =
                ModelQuickScanResult.CreateFailure(
                    failureCode: "invalid-gguf",
                    userMessage: "The file is not a supported GGUF model.",
                    technicalMessage: "The GGUF header was invalid.");

            bool created = ModelInspectionRequestFactory.TryCreate(
                modelPath,
                failedScan,
                out ModelInspectionRequest? request);

            Assert.IsFalse(created);
            Assert.IsNull(request);
        }
        finally
        {
            DeleteTemporaryModelFile(modelPath);
        }
    }

    [TestMethod]
    public void TryCreate_FileHeldOpenForWriting_ReturnsFalseWithoutRequest()
    {
        string modelPath = CreateTemporaryModelFile(lengthBytes: 64);

        try
        {
            // Hold write access while sharing reads. The request factory must
            // still reject the file because its own share mode must exclude a
            // concurrent writer during identity capture.
            using FileStream writer = new(
                modelPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.Read);

            bool created = ModelInspectionRequestFactory.TryCreate(
                modelPath,
                CreateSuccessfulScan(64),
                out ModelInspectionRequest? request);

            Assert.IsFalse(created);
            Assert.IsNull(request);
        }
        finally
        {
            DeleteTemporaryModelFile(modelPath);
        }
    }

    [TestMethod]
    public void TryCreate_InvalidArguments_ReturnFalseInsteadOfThrowing()
    {
        bool blankPathCreated = ModelInspectionRequestFactory.TryCreate(
            "   ",
            CreateSuccessfulScan(64),
            out ModelInspectionRequest? blankPathRequest);

        bool nullScanCreated = ModelInspectionRequestFactory.TryCreate(
            @"C:\Models\granite.gguf",
            null!,
            out ModelInspectionRequest? nullScanRequest);

        Assert.IsFalse(blankPathCreated);
        Assert.IsNull(blankPathRequest);
        Assert.IsFalse(nullScanCreated);
        Assert.IsNull(nullScanRequest);
    }

    private static ModelQuickScanResult CreateSuccessfulScan(long fileSizeBytes)
    {
        return ModelQuickScanResult.CreateSuccess(
            modelName: "Granite 4.1 3B Instruct",
            architecture: "granite",
            parameterSizeLabel: "3B",
            quantization: "Q4_K_M",
            fileSizeBytes: fileSizeBytes,
            contextLength: 131_072UL,
            ggufVersion: 3);
    }

    private static string CreateTemporaryModelFile(
        int lengthBytes,
        string extension = ".gguf")
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"granite-edge-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        string modelPath = Path.Combine(
            directoryPath,
            $"granite-4.1-3b{extension}");
        File.WriteAllBytes(modelPath, new byte[lengthBytes]);
        return modelPath;
    }

    private static void DeleteTemporaryModelFile(string modelPath)
    {
        string? directoryPath = Path.GetDirectoryName(modelPath);
        if (directoryPath is not null && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
