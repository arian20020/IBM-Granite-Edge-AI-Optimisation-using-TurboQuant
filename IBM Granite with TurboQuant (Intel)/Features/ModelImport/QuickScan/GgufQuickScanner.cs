using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.QuickScan
{
    /// <summary>
    /// Performs a lightweight validation and metadata scan of a GGUF file.
    /// </summary>
    internal sealed class GgufQuickScanner
    {
        /// <summary>
        /// Scans the selected GGUF file and returns the completed result.
        /// </summary>
        internal Task<ModelQuickScanResult> ScanAsync(
            string modelFilePath,
            CancellationToken cancellationToken)
        {
            // Reject a missing or blank path before opening the file.
            ArgumentException.ThrowIfNullOrWhiteSpace(modelFilePath);

            // Stop immediately if cancellation was already requested.
            cancellationToken.ThrowIfCancellationRequested();

            // GGUF binary reading will be implemented next.
            throw new NotImplementedException();
        }
    }
}