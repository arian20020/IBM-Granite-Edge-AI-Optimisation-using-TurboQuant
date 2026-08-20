using System;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal readonly struct ModelSelectionOperationId : IEquatable<ModelSelectionOperationId>
{
    private readonly Guid value;

    private ModelSelectionOperationId(Guid value)
    {
        this.value = value;
    }

    internal static ModelSelectionOperationId CreateNew()
    {
        return new ModelSelectionOperationId(Guid.NewGuid());
    }

    public bool Equals(ModelSelectionOperationId other)
    {
        return value.Equals(other.value);
    }

    public override bool Equals(object? obj)
    {
        return obj is ModelSelectionOperationId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return value.GetHashCode();
    }

    public static bool operator ==(
        ModelSelectionOperationId left,
        ModelSelectionOperationId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        ModelSelectionOperationId left,
        ModelSelectionOperationId right)
    {
        return !left.Equals(right);
    }
}
