using System;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Carries the validated model selected for full model inspection.
    /// </summary>
    internal sealed class ModelInspectionRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates one model-inspection navigation request.
        /// </summary>
        /// <param name="modelPath">
        /// The authoritative local path of the validated model package.
        /// </param>
        public ModelInspectionRequestedEventArgs(string modelPath)
        {
            // Reject null, empty, or whitespace-only model paths.
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

            // Preserve the original validated path without changing it.
            ModelPath = modelPath;
        }

        /// <summary>
        /// Gets the validated model path passed to the inspection stage.
        /// </summary>
        public string ModelPath { get; }
    }
}