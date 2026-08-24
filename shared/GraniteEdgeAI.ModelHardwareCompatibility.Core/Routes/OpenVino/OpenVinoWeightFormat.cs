namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// How OpenVINO stores the weights it runs.
///
/// Declared from most bits to fewest, the same way
/// <see cref="Domain.WeightQuantisation"/> is, because tier distance is
/// measured by subtracting these values. An invariant test pins the order.
///
/// TurboQuant entries are last and are not ordinary members of the list: they
/// are admissible only through an experimental capability record naming the
/// exact backend, device, representation, cache and version.
/// </summary>
public enum OpenVinoWeightFormat
{
    Unspecified = 0,

    /// <summary>Whatever the source package already contains, unconverted.</summary>
    Original = 1,

    Fp16 = 2,
    Int8 = 3,
    Int4 = 4,

    /// <summary>Experimental. Requires exact evidence; never planned by default.</summary>
    TurboQuantTbq4 = 5,

    /// <summary>Experimental. Requires exact evidence; never planned by default.</summary>
    TurboQuantTbq3 = 6
}
