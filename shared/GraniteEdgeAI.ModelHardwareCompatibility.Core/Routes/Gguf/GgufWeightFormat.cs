namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Candidate weight formats for the llama.cpp route, declared from highest to
/// lowest expected quality. Imported means the file is used unchanged.
/// </summary>
public enum GgufWeightFormat
{
    Unspecified = 0,
    Imported,
    BF16,
    F16,
    Q8_0,
    Q6K,
    Q5KM,
    Q4KM,
    Q3KM,
    Q2K
}
