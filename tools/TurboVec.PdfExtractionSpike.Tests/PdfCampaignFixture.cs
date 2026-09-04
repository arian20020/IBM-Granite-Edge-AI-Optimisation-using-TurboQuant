using System.Security.Cryptography;
using System.Text;
using TurboVec.PdfExtractionSpike;

namespace TurboVec.PdfExtractionSpike.Tests;

internal sealed class PdfFixture : IDisposable
{
    private PdfFixture(string directory, string path) { Directory = directory; Path = path; }
    public string Directory { get; }
    public string Path { get; }
    public PdfExtractionRequest Request => new(Path, Hash(Path), 1000, 20_000_000, 100 * 1024 * 1024);

    public static PdfFixture Create(params string[] pages)
        => CreateNamed(new string('a', 120) + ".pdf", pages);

    public static PdfFixture CreateNamed(string filename, params string[] pages)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tvpdf-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, filename);
        File.WriteAllBytes(path, BuildPdf(pages));
        return new PdfFixture(directory, path);
    }

    public static PdfFixture CreateEncryptedPdf()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tvpdf-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, "encrypted.pdf");
        const string fixture = "JVBERi0xLjMKJeLjz9MKMSAwIG9iago8PAovUHJvZHVjZXIgPDA3NTY3NWY0MDY+Ci9UaXRsZSA8MjM1YTc3ZjIwZjRmZTE4MDI5OWMxYzhkZGMxNTQ5MjIzNDllM2UwNmQxZjI1NjMwOGEyND4KPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1R5cGUgL1BhZ2VzCi9Db3VudCAxCi9LaWRzIFsgNCAwIFIgXQo+PgplbmRvYmoKMyAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMiAwIFIKPj4KZW5kb2JqCjQgMCBvYmoKPDwKL1R5cGUgL1BhZ2UKL1Jlc291cmNlcyA8PAo+PgovTWVkaWFCb3ggWyAwLjAgMC4wIDYxMiA3OTIgXQovUGFyZW50IDIgMCBSCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9WIDIKL1IgMwovTGVuZ3RoIDEyOAovUCA0Mjk0OTY3MjkyCi9GaWx0ZXIgL1N0YW5kYXJkCi9PIDxmZDRiZTJmMDJhYjZjMzM5NTJhODY0MGVjMWVmYWRmNGVjZjcxMzg1ODJkMTQxMjMyZjQwN2NiY2JjOGZkMzAzPgovVSA8YTMxMGRhZmFhYjZhMDdjNWUwMTg4ZmVlNzE0ZTU1NzgyOGJmNGU1ZTRlNzU4YTQxNjQwMDRlNTZmZmZhMDEwOD4KPj4KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNSAwMDAwMCBuIAowMDAwMDAwMTIxIDAwMDAwIG4gCjAwMDAwMDAxODAgMDAwMDAgbiAKMDAwMDAwMDIyOSAwMDAwMCBuIAowMDAwMDAwMzIzIDAwMDAwIG4gCnRyYWlsZXIKPDwKL1NpemUgNgovUm9vdCAzIDAgUgovSW5mbyAxIDAgUgovSUQgWyA8NjQzMDMyMzk2MTM4MzkzMzMzMzkzMjYyMzczMjMyMzczNjMyMzgzNDM1NjIzOTMzMzk2MTM5MzUzMTM0MzIzOD4gPDY0MzAzMjM5NjEzODM5MzMzMzM5MzI2MjM3MzIzMjM3MzYzMjM4MzQzNTYyMzkzMzM5NjEzOTM1MzEzNDMyMzg+IF0KL0VuY3J5cHQgNSAwIFIKPj4Kc3RhcnR4cmVmCjUzOAolJUVPRgo=";
        File.WriteAllBytes(path, Convert.FromBase64String(fixture));
        return new PdfFixture(directory, path);
    }

    public static PdfFixture CreateImageOnly()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tvpdf-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, "image only.pdf");
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>",
            "<< /Length 31 >>\nstream\nq 100 0 0 100 72 600 cm /Im0 Do Q\nendstream",
            "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\nFF0000>\nendstream"
        };
        File.WriteAllBytes(path, BuildObjects(objects));
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
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        return BuildObjects(objects);
    }

    private static byte[] BuildObjects(List<string> objects)
    {
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(builder.ToString());
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, true);
}
