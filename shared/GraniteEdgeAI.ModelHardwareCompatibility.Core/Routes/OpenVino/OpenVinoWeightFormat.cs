namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// How OpenVINO stores the weights it runs.
///
/// Declared from most bits to fewest, the same way
/// <see cref="Domain.WeightQuantisation"/> is, because tier distance is
/// measured by subtracting these values. An invariant test pins the order.
/// </summary>
public enum OpenVinoWeightFormat
{
    Unspecified = 0,

    /// <summary>Whatever the source package already contains, unconverted.</summary>
    Original = 1,

    Fp16 = 2,
    Int8 = 3,
    Int4 = 4,

    /// <summary>NNCF block-scaled microscaling FP4 weights.</summary>
    MxFp4 = 5
}
