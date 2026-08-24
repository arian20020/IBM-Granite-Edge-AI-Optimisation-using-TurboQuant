namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

[TestClass]
[DoNotParallelize]
public sealed class HardwareInspectionAcceptanceResultStoreTests
{
    [TestMethod]
    public void IsResultToken_AcceptsExactlyLowercaseHex()
    {
        Assert.IsTrue(HardwareInspectionAcceptanceResultStore.IsResultToken(
            "0123456789abcdef0123456789abcdef"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0123456789abcdef0123456789abcde")]
    [DataRow("0123456789abcdef0123456789abcdef0")]
    [DataRow("0123456789abcdef0123456789abcdeg")]
    [DataRow("0123456789abcdef0123456789abcdeF")]
    [DataRow("../../../../0123456789abcdef012345")]
    public void IsResultToken_RejectsMalformedValues(string value)
    {
        Assert.IsFalse(HardwareInspectionAcceptanceResultStore.IsResultToken(value));
        Assert.ThrowsExactly<ArgumentException>(() =>
            HardwareInspectionAcceptanceResultStore.GetResultPath(value));
    }

    [TestMethod]
    public void GetResultPath_ContainsTokenUnderFixedTemporaryRoot()
    {
        const string Token = "0123456789abcdef0123456789abcdef";
        string expectedRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            "Acceptance"));

        string actual = HardwareInspectionAcceptanceResultStore.GetResultPath(Token);

        Assert.AreEqual(
            Path.Combine(expectedRoot, $"{Token}.json"),
            actual);
        Assert.AreEqual(expectedRoot, Path.GetDirectoryName(actual));
    }

    [TestMethod]
    public void WriteAtomically_PublishesExactUtf8WithOneLf()
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);

        try
        {
            HardwareInspectionAcceptanceResultStore.WriteAtomically(
                token,
                "{\"ok\":true}"u8,
                1024);

            CollectionAssert.AreEqual(
                "{\"ok\":true}\n"u8.ToArray(),
                File.ReadAllBytes(resultPath));
        }
        finally
        {
            DeleteOwnedFile(resultPath);
        }
    }

    [TestMethod]
    public void WriteAtomically_AllowsExactMaximumIncludingFinalLf()
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);

        try
        {
            HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{}"u8, 3);

            CollectionAssert.AreEqual("{}\n"u8.ToArray(), File.ReadAllBytes(resultPath));
        }
        finally
        {
            DeleteOwnedFile(resultPath);
        }
    }

    [TestMethod]
    public void WriteAtomically_RejectsPayloadBeyondMaximumIncludingFinalLf()
    {
        string token = Guid.NewGuid().ToString("N");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{}"u8, 2));
        Assert.IsFalse(File.Exists(
            HardwareInspectionAcceptanceResultStore.GetResultPath(token)));
    }

    [TestMethod]
    public void WriteAtomically_RejectsNonCanonicalFraming()
    {
        string token = Guid.NewGuid().ToString("N");

        Assert.ThrowsExactly<ArgumentException>(() =>
            HardwareInspectionAcceptanceResultStore.WriteAtomically(
                token,
                ReadOnlySpan<byte>.Empty,
                1024));
        Assert.ThrowsExactly<ArgumentException>(() =>
            HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{}\r"u8, 1024));
        Assert.ThrowsExactly<ArgumentException>(() =>
            HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{}\n"u8, 1024));
        Assert.ThrowsExactly<ArgumentException>(() =>
            HardwareInspectionAcceptanceResultStore.WriteAtomically(
                token,
                [0xef, 0xbb, 0xbf, 0x7b, 0x7d],
                1024));
        Assert.IsFalse(File.Exists(
            HardwareInspectionAcceptanceResultStore.GetResultPath(token)));
    }

    [TestMethod]
    public void WriteAtomically_DoesNotOverwriteExistingResult()
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);

        try
        {
            HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{\"run\":1}"u8, 1024);

            Assert.Throws<IOException>(() =>
                HardwareInspectionAcceptanceResultStore.WriteAtomically(
                    token,
                    "{\"run\":2}"u8,
                    1024));
            CollectionAssert.AreEqual(
                "{\"run\":1}\n"u8.ToArray(),
                File.ReadAllBytes(resultPath));
        }
        finally
        {
            DeleteOwnedFile(resultPath);
        }
    }

    [TestMethod]
    public void WriteAtomically_RemovesTemporaryFileWhenPublicationFails()
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);
        string resultRoot = Path.GetDirectoryName(resultPath)!;
        Directory.CreateDirectory(resultPath);

        try
        {
            Assert.Throws<IOException>(() =>
                HardwareInspectionAcceptanceResultStore.WriteAtomically(token, "{}"u8, 1024));

            Assert.IsEmpty(Directory.GetFiles(
                resultRoot,
                $".{token}.*.tmp",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(resultPath))
            {
                Directory.Delete(resultPath);
            }
        }
    }

    private static void DeleteOwnedFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
