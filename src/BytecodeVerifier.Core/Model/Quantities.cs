namespace BytecodeVerifier.Core.Model;

/// <summary>
/// Every domain quantity carries its unit/coordinate basis. Raw representations are
/// preserved on import; normalization only happens in the derived layer. Values that
/// cannot be converted stay at their original location with a diagnostic attached.
/// </summary>
public enum QuantityUnit
{
    ByteOffset,
    ConstantPoolIndex,
    MajorVersion,
    DescriptorString,
    Opcode,
    StackSlot,
    LocalSlot,
    Category,
    Dimension
}

public sealed record DomainQuantity(long Raw, QuantityUnit Unit, string? NormalizedText = null, string? Diagnostic = null)
{
    public bool HasDiagnostic => Diagnostic is not null;

    public static DomainQuantity Bci(int offset) => new(offset, QuantityUnit.ByteOffset, $"bci:{offset}");
    public static DomainQuantity CpIndex(int index) => new(index, QuantityUnit.ConstantPoolIndex, $"#{index}");
    public static DomainQuantity Version(ushort major) => new(major, QuantityUnit.MajorVersion, JavaVersionName(major));
    public static DomainQuantity Slot(int index, bool local) =>
        new(index, local ? QuantityUnit.LocalSlot : QuantityUnit.StackSlot, (local ? "local" : "stack") + index);
    public static DomainQuantity Cat2Top(int index) => new(index, QuantityUnit.StackSlot, $"stack{index}(top-half)");

    public static string JavaVersionName(ushort major) => major switch
    {
        >= 45 => $"Java SE {major - 44}",
        _ => $"unknown(major={major})"
    };
}

/// <summary>A coordinate-anchored raw text fragment (e.g. descriptor substring).</summary>
public sealed record RawSpan(string SourceKind, long Offset, int Length, string Text, string? Diagnostic = null)
{
    public bool Unconvertible => Diagnostic is not null;
}
