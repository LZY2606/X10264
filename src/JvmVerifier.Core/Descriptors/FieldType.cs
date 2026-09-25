namespace JvmVerifier.Core.Descriptors;

public enum FieldTypeKind { Base, Object, Array }
public enum BaseType { Byte, Char, Double, Float, Int, Long, Short, Boolean, Void }

public sealed record FieldType
{
    public required FieldTypeKind Kind { get; init; }
    public BaseType? Base { get; init; }
    public string? InternalClassName { get; init; }
    public int? Dimensions { get; init; }
    public FieldType? ElementType { get; init; }
    public required string Descriptor { get; init; }

    public bool IsCategory2 => Base is BaseType.Double or BaseType.Long;

    public override string ToString() => Descriptor;
}

public sealed class MethodTypeDescriptor
{
    public required string Descriptor { get; init; }
    public required List<FieldType> Parameters { get; init; }
    public required FieldType ReturnType { get; init; }
    public int ParameterSlotCount => Parameters.Sum(p => p.IsCategory2 ? 2 : 1);
}
