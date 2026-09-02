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
        using var fixture = PdfFixture.Create("");
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
}

internal sealed class PdfFixture : IDisposable
{
    private PdfFixture(string directory, string path) { Directory = directory; Path = path; }
    public string Directory { get; }
    public string Path { get; }
    public PdfExtractionRequest Request => new(Path, Hash(Path), 1000, 20_000_000, 100 * 1024 * 1024);

    public static PdfFixture Create(params string[] pages)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tvpdf-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, new string('a', 120) + ".pdf");
        File.WriteAllBytes(path, BuildPdf(pages));
        return new PdfFixture(directory, path);
    }

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static byte[] BuildPdf(string[] pages)
    {
        var objects = new List<string>();
        var kids = string.Join(" ", Enumerable.Range(0, pages.Length).Select(i => $"{3 + i * 2} 0 R"));
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>");
        foreach (var text in pages)
        {
            var content = string.IsNullOrEmpty(text) ? "" : $"BT /F1 12 Tf 72 720 Td ({text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")}) Tj ET";
            var contentId = objects.Count + 2;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        var builder = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString())); builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString()); builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, true);
}
