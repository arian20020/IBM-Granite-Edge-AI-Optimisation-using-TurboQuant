using System.Security.Cryptography;
using UglyToad.PdfPig;

namespace TurboVec.PdfExtractionSpike;

public sealed class PdfExtractor
{
    public PdfExtractionResult Extract(PdfExtractionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(request.InputPath);
            if (!info.Exists) return PdfExtractionResult.Failure("missing_file");
            if (info.Length > request.MaxBytes) return PdfExtractionResult.Failure("byte_limit");
            using (var hashStream = new FileStream(request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var actual = Convert.ToHexString(SHA256.HashData(hashStream)).ToLowerInvariant();
                if (!string.Equals(actual, request.ExpectedSha256, StringComparison.Ordinal))
                    return PdfExtractionResult.Failure("hash_mismatch");
            }

            using var stream = new FileStream(request.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = PdfDocument.Open(stream);
            if (document.NumberOfPages > request.MaxPages) return PdfExtractionResult.Failure("page_limit", document.NumberOfPages);
            var pages = new List<ExtractedPage>(document.NumberOfPages);
            var totalScalars = 0;
            for (var number = 1; number <= document.NumberOfPages; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = document.GetPage(number).Text ?? string.Empty;
                var scalars = text.EnumerateRunes().Count();
                totalScalars = checked(totalScalars + scalars);
                if (totalScalars > request.MaxScalars) return PdfExtractionResult.Failure("text_limit", document.NumberOfPages, pages);
                pages.Add(new ExtractedPage(number, text, scalars));
            }
            if (pages.Count > 0 && pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
                return PdfExtractionResult.Failure("ocr_required", pages.Count, pages);
            return new PdfExtractionResult(true, null, pages.Count, pages);
        }
        catch (OperationCanceledException) { return PdfExtractionResult.Failure("cancelled"); }
        catch (Exception error) when (error.Message.Contains("password", StringComparison.OrdinalIgnoreCase) || error.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase))
        { return PdfExtractionResult.Failure("encrypted_pdf"); }
        catch (Exception) { return PdfExtractionResult.Failure("malformed_pdf"); }
    }
}
