namespace GraniteEdgeAI.Features.ModelImport.QuickScan
{
    /// <summary>
    /// Contains the non-user-facing details needed to diagnose one failed
    /// model quick scan without retaining the selected file's full path.
    /// </summary>
    internal sealed record ModelQuickScanFailureDiagnostic(
        string SelectedFileName,
        string FailureCode,
        string TechnicalMessage);
}
