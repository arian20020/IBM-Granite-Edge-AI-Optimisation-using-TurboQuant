using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Carries the immutable, validated request for full model inspection.
    /// </summary>
    internal sealed class ModelInspectionRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates one model-inspection navigation request event.
        /// </summary>
        /// <param name="request">
        /// The immutable request created from the current validated model file.
        /// </param>
        public ModelInspectionRequestedEventArgs(
            ModelInspectionRequest request)
        {
            // a navigation event cannot exist without its validated request
            ArgumentNullException.ThrowIfNull(request);

            // preserve the exact request object for the shell to forward
            Request = request;
        }

        /// <summary>
        /// Gets the immutable request passed to the inspection stage.
        /// </summary>
        public ModelInspectionRequest Request { get; }
    }
}
