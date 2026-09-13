namespace GraniteEdgeAI.GgufQuantization.Contracts;

public enum GgufQuantizationSupportCode
{
    None = 0,
    InvalidRequest,
    UnsupportedFormat,
    BindingMismatch,
    PackageVerificationFailed,
    SourceChanged,
    OutputAlreadyExists,
    ProcessFailed,
    TimedOut,
    Cancelled,
    ProtocolViolation,
    OutputInvalid,
    InternalFailure,
}
