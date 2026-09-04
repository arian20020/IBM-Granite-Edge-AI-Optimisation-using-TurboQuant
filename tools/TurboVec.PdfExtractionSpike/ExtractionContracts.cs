namespace TurboVec.PdfExtractionSpike;

public sealed record PdfExtractionRequest(string InputPath, string ExpectedSha256, int MaxPages, int MaxScalars, long MaxBytes);
public sealed record ExtractedPage(int Page, string Text, int ScalarCount, string ContentSha256);
public sealed record PdfExtractionResult(bool Succeeded, string? FailureCode, int PageCount, IReadOnlyList<ExtractedPage> Pages)
{
    public static PdfExtractionResult Failure(string code, int pageCount = 0, IReadOnlyList<ExtractedPage>? pages = null) =>
        new(false, code, pageCount, pages ?? Array.Empty<ExtractedPage>());
}
