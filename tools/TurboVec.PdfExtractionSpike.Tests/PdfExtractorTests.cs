using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TurboVec.PdfExtractionSpike;

namespace TurboVec.PdfExtractionSpike.Tests;

[TestClass]
public sealed class PdfExtractorTests
{
    [TestMethod]
    public void ExtractPreservesEmptyPageAndFollowingPageNumber()
    {
        using var fixture = PdfFixture.Create("", "second page fact");
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, result.Pages.Count);
        Assert.AreEqual(string.Empty, result.Pages[0].Text);
        StringAssert.Contains(result.Pages[1].Text, "second page fact");
    }

    [TestMethod]
    public void ImageOnlyPdfReturnsOcrRequiredWithoutSuccess()
    {
        using var fixture = PdfFixture.CreateImageOnly();
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.AreEqual("ocr_required", result.FailureCode);
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public void HashMismatchAndMalformedPdfAreClassified()
    {
        using var fixture = PdfFixture.Create("safe sentinel");
        var mismatch = fixture.Request with { ExpectedSha256 = new string('0', 64) };
        Assert.AreEqual("hash_mismatch", new PdfExtractor().Extract(mismatch, CancellationToken.None).FailureCode);
        File.WriteAllBytes(fixture.Path, "%PDF-malformed"u8.ToArray());
        var malformed = fixture.Request with { ExpectedSha256 = PdfFixture.Hash(fixture.Path) };
        Assert.AreEqual("malformed_pdf", new PdfExtractor().Extract(malformed, CancellationToken.None).FailureCode);
    }

    [TestMethod]
    public void CancelledAndPageLimitAreClassified()
    {
        using var fixture = PdfFixture.Create("first", "second");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.AreEqual("cancelled", new PdfExtractor().Extract(fixture.Request, cancellation.Token).FailureCode);
        Assert.AreEqual("page_limit", new PdfExtractor().Extract(fixture.Request with { MaxPages = 1 }, CancellationToken.None).FailureCode);
    }

    [TestMethod]
    public void EmptyPdfIsRejectedExplicitly()
    {
        using var fixture = PdfFixture.Create();
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("empty_pdf", result.FailureCode);
    }

    [TestMethod]
    public void UnicodeAndSpaceFilenamePreservesTextPageAndContentHash()
    {
        using var fixture = PdfFixture.CreateNamed("résumé evidence file.pdf", "café evidence 42");
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, result.Pages[0].Page);
        StringAssert.Contains(result.Pages[0].Text, "café evidence 42");
        Assert.AreEqual(64, result.Pages[0].ContentSha256.Length);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Pages[0].Text))).ToLowerInvariant(),
            result.Pages[0].ContentSha256);
    }

    [TestMethod]
    public void MultipageHeadingsTablesAndRepeatedPassagesRemainOrdered()
    {
        using var fixture = PdfFixture.Create("HEADING ONE", "A | B | 42", "repeated passage", "repeated passage");
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(4, result.PageCount);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, result.Pages.Select(page => page.Page).ToArray());
        StringAssert.Contains(result.Pages[0].Text, "HEADING ONE");
        StringAssert.Contains(result.Pages[1].Text, "A | B | 42");
        Assert.AreEqual(result.Pages[2].Text, result.Pages[3].Text);
    }

    [TestMethod]
    public void ByteTextAndLongDocumentBoundariesAreExplicit()
    {
        using var fixture = PdfFixture.Create(Enumerable.Range(1, 120).Select(index => $"page {index} evidence").ToArray());
        var extractor = new PdfExtractor();
        Assert.AreEqual("byte_limit", extractor.Extract(fixture.Request with { MaxBytes = 1 }, CancellationToken.None).FailureCode);
        Assert.AreEqual("text_limit", extractor.Extract(fixture.Request with { MaxScalars = 10 }, CancellationToken.None).FailureCode);
        var accepted = extractor.Extract(fixture.Request with { MaxPages = 120 }, CancellationToken.None);
        Assert.IsTrue(accepted.Succeeded);
        Assert.AreEqual(120, accepted.PageCount);
    }

    [TestMethod]
    public void StructurallyEncryptedPdfIsClassifiedBeforeTextExtraction()
    {
        using var fixture = PdfFixture.CreateEncryptedPdf();
        Assert.AreEqual("1d0014c0d72cab2ef97cf50623eabf7fc0b6aa06a37275c27958c5cc354050a0", PdfFixture.Hash(fixture.Path));
        var result = new PdfExtractor().Extract(fixture.Request, CancellationToken.None);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("encrypted_pdf", result.FailureCode);
    }
}
