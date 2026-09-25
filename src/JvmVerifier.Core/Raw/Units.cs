namespace JvmVerifier.Core.Raw;

/// <summary>
/// Every numeric domain quantity carries a coordinate basis / unit so the UI can
/// show "pc=8 [byte offset in Code[]]" rather than a bare number. Raw import
/// values keep the original representation; normalization happens only in the
/// derived layer.
/// </summary>
public enum DomainUnit
{
    ByteOffsetInCode,
    ConstantPoolIndex,
    CpSlotPlusOne,
    AttributeByteOffset,
    StackSlotDepth,
    LocalVariableSlot,
    ClassfileMajorVersion,
    ClassfileMinorVersion,
    OperandWord,
    DimensionCount,
    DeltaBytes,
}

public readonly record struct Coords(int Value, DomainUnit Unit)
{
    public override string ToString() => $"{Value} [{Unit.ToLabel()}]";
}

public static class UnitLabels
{
    public static string ToLabel(this DomainUnit u) => u switch
    {
        DomainUnit.ByteOffsetInCode => "byte offset in Code[]",
        DomainUnit.ConstantPoolIndex => "constant pool index (1-based)",
        DomainUnit.CpSlotPlusOne => "constant pool slot (wide entry occupies two)",
        DomainUnit.AttributeByteOffset => "byte offset in attribute[]",
        DomainUnit.StackSlotDepth => "stack depth (slots)",
        DomainUnit.LocalVariableSlot => "local variable slot",
        DomainUnit.ClassfileMajorVersion => "class file major version",
        DomainUnit.ClassfileMinorVersion => "class file minor version",
        DomainUnit.OperandWord => "operand word",
        DomainUnit.DimensionCount => "array dimension count",
        DomainUnit.DeltaBytes => "signed delta (bytes)",
        _ => u.ToString(),
    };
}
