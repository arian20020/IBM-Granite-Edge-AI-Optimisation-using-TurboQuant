using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    internal sealed record ImportedModelCardData(
    string FileName,
    string ModelName,
    string Parameters,
    string Architecture,
    string Quantization,
    string FileSize,
    string DeclaredContext);
}
