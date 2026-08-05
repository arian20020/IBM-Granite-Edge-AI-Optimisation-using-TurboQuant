using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.IO;
using System.Security;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Converts one successful quick scan into the immutable, file-backed
    /// request passed to Model Inspection.
    /// </summary>
    internal static class ModelInspectionRequestFactory
    {
        /// <summary>
        /// Tries to capture one stable model-file identity and create the
        /// corresponding Model Inspection request.
        /// </summary>
        /// <param name="modelPath">
        /// The local path selected and quick-scanned by Model Import.
        /// </param>
        /// <param name="scanResult">
        /// The successful GGUF quick-scan result associated with the path.
        /// </param>
        /// <param name="request">
        /// The immutable request when creation succeeds; otherwise, null.
        /// </param>
        /// <returns>
        /// True when the current file still agrees with the validated scan;
        /// otherwise, false.
        /// </returns>
        internal static bool TryCreate(
            string modelPath,
            ModelQuickScanResult scanResult,
            out ModelInspectionRequest? request)
        {
            // A failed attempt must never expose a partially built request.
            request = null;

            // Reject invalid callers and non-successful scans at the boundary.
            if (string.IsNullOrWhiteSpace(modelPath) ||
                scanResult is null ||
                scanResult.Outcome != ModelQuickScanOutcome.Success)
            {
                return false;
            }

            // Copy required scan values once so validation and construction use
            // the same immutable local values throughout this method.
            string? modelName = scanResult.ModelName;
            string? architecture = scanResult.Architecture;
            long? scannedFileSizeBytes = scanResult.FileSizeBytes;
            uint? ggufVersion = scanResult.GgufVersion;

            // A successful result must still contain every required GGUF fact.
            if (string.IsNullOrWhiteSpace(modelName) ||
                string.IsNullOrWhiteSpace(architecture) ||
                !scannedFileSizeBytes.HasValue ||
                scannedFileSizeBytes.Value <= 0 ||
                !ggufVersion.HasValue ||
                ggufVersion.Value == 0)
            {
                return false;
            }

            try
            {
                // Normalize the path before checking or storing it.
                string fullModelPath = Path.GetFullPath(modelPath);

                // Gate 1 supports only the verified GGUF inspection route.
                if (!string.Equals(
                        Path.GetExtension(fullModelPath),
                        ".gguf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Open the model for reading while refusing to share with a
                // writer. This prevents identity capture during a concurrent
                // modification without unnecessarily excluding other readers.
                using FileStream modelStream = new(
                    fullModelPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                // The current file length must still match the completed scan.
                long currentFileSizeBytes = modelStream.Length;
                if (currentFileSizeBytes != scannedFileSizeBytes.Value)
                {
                    return false;
                }

                // Capture the current UTC write timestamp while the no-writer
                // file handle remains open.
                DateTimeOffset lastWriteTimeUtc = new(
                    File.GetLastWriteTimeUtc(fullModelPath));

                // Copy only the bounded facts needed by the next use case.
                ValidatedQuickScanSnapshot quickScan =
                    ValidatedQuickScanSnapshot.CreateGguf(
                        modelName,
                        architecture,
                        scanResult.ParameterSizeLabel,
                        scanResult.Quantization,
                        currentFileSizeBytes,
                        scanResult.ContextLength,
                        ggufVersion.Value);

                // Preserve the lightweight identity the worker must re-check
                // before it trusts the model selection.
                ExpectedModelFileIdentity expectedFileIdentity = new(
                    currentFileSizeBytes,
                    lastWriteTimeUtc);

                // Publish the request only after every validation has succeeded.
                request = new ModelInspectionRequest(
                    fullModelPath,
                    Path.GetFileName(fullModelPath),
                    expectedFileIdentity,
                    quickScan);

                return true;
            }
            catch (ArgumentException)
            {
                // Invalid paths or contract values fail closed.
                request = null;
                return false;
            }
            catch (IOException)
            {
                // Missing, changed, locked, or unreadable files fail closed.
                request = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Directories and inaccessible files are not valid models.
                request = null;
                return false;
            }
            catch (SecurityException)
            {
                // A denied file-system permission is a controlled rejection.
                request = null;
                return false;
            }
            catch (NotSupportedException)
            {
                // Unsupported path forms are rejected without navigation.
                request = null;
                return false;
            }
        }
    }
}
