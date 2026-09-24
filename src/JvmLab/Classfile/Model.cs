namespace JvmLab.Classfile;

/// <summary>
/// A domain quantity that always carries its unit and coordinate basis.
/// Raw keeps the original representation exactly as imported; the derived
/// layer may normalize Value, but Raw is never rewritten.
/// </summary>
public sealed record Quantity(long Value, string Unit, string Basis, string Raw)
{
    public static Quantity Bytes(long value, string basis, string raw) =>
        new(value, "byte", basis, raw);
}

public sealed class CpEntry
{
    public required int Index { get; init; }
    public required Quantity FileOffset { get; init; }
    public required byte Tag { get; init; }
    public required string TagName { get; init; }
    public required string RawHex { get; init; }
    public List<object?> Values { get; } = new();

    public string Display => TagName switch
    {
        "Utf8" => $"Utf8 \"{Values[0]}\"",
        "Integer" => $"int {Values[0]}",
        "Long" => $"long {Values[0]}L",
        "Double" => $"double {Values[0]}",
        "Float" => $"float {Values[0]}",
        "Class" => $"Class #{Values[0]}",
        "String" => $"String #{Values[0]}",
        "Fieldref" => $"Fieldref #{Values[0]}.#{Values[1]}",
        "Methodref" => $"Methodref #{Values[0]}.#{Values[1]}",
        "InterfaceMethodref" => $"InterfaceMethodref #{Values[0]}.#{Values[1]}",
        "NameAndType" => $"NameAndType #{Values[0]}:#{Values[1]}",
        _ => $"{TagName}({string.Join(",", Values)})"
    };
}

public sealed class VerificationTypeInfo
{
    public required Quantity FileOffset { get; init; }
    public required byte Tag { get; init; }
    public int? CpoolIndex { get; init; }
    public int? Offset { get; init; }
    public required string RawHex { get; init; }

    public string TagName => Tag switch
    {
        0 => "Top", 1 => "Integer", 2 => "Float", 3 => "Long", 4 => "Double",
        5 => "Null", 6 => "UninitializedThis", 7 => "Object", 8 => "Uninitialized",
        _ => $"Bad({Tag})"
    };
}

public sealed class StackMapFrame
{
    public required Quantity FileOffset { get; init; }
    public required byte FrameType { get; init; }
    public required string Kind { get; init; }
    public int OffsetDelta { get; init; }
    /// <summary>Absolute code offset this frame applies to (derived).</summary>
    public int FrameOffset { get; set; }
    public List<VerificationTypeInfo> Locals { get; } = new();
    public List<VerificationTypeInfo> Stack { get; } = new();
    public required string RawHex { get; init; }

    public string Id => $"frame@{FrameOffset}";
}

public sealed class ExceptionRange
{
    public required Quantity FileOffset { get; init; }
    public required int StartPc { get; init; }
    public required int EndPc { get; init; }
    public required int HandlerPc { get; init; }
    public required int CatchType { get; init; }
    public required string RawHex { get; init; }
}

public sealed class Instruction
{
    public required int Offset { get; init; }            // code-array offset
    public required Quantity FileOffset { get; init; }   // absolute file offset with unit/basis
    public required byte Opcode { get; init; }
    public required string Mnemonic { get; init; }
    public required byte[] Operands { get; init; }
    public int? BranchTarget { get; set; }
    public List<int> SwitchTargets { get; } = new();
    public int? CpIndex { get; set; }
    public int Length => 1 + Operands.Length;
    public bool IsBranch => BranchTarget.HasValue || SwitchTargets.Count > 0;
    public bool IsReturn => Mnemonic is "return" or "ireturn" or "lreturn" or "freturn" or "dreturn" or "areturn" or "athrow";

    public string OperandText =>
        CpIndex is { } cp ? $"#{cp}" :
        BranchTarget is { } bt ? $"-> {bt}" :
        SwitchTargets.Count > 0 ? $"-> [{string.Join(",", SwitchTargets)}]" :
        Convert.ToHexString(Operands);
}

public sealed class CodeAttribute
{
    public required Quantity FileOffset { get; init; }
    public required int MaxStack { get; init; }
    public required int MaxLocals { get; init; }
    public required Quantity CodeFileOffset { get; init; }
    public required byte[] CodeBytes { get; init; }
    public List<Instruction> Instructions { get; } = new();
    public List<ExceptionRange> ExceptionTable { get; } = new();
    public List<StackMapFrame> Frames { get; } = new();
    public required string RawHex { get; init; }
}

public sealed class MethodInfo
{
    public required Quantity FileOffset { get; init; }
    public required string Name { get; init; }
    public required string Descriptor { get; init; }
    public required int NameIndex { get; init; }
    public required int DescriptorIndex { get; init; }
    public CodeAttribute? Code { get; set; }
    public required string RawHex { get; init; }

    public string Id => $"{Name}{Descriptor}";
}

public sealed class FieldInfo
{
    public required string Name { get; init; }
    public required string Descriptor { get; init; }
    public required int NameIndex { get; init; }
    public required int DescriptorIndex { get; init; }
}

public sealed class ClassFile
{
    public required byte[] RawBytes { get; init; }
    public required int MinorVersion { get; init; }
    public required int MajorVersion { get; init; }
    public List<CpEntry> ConstantPool { get; } = new();
    public required string ThisClass { get; init; }
    public required string SuperClass { get; init; }
    public List<FieldInfo> Fields { get; } = new();
    public List<MethodInfo> Methods { get; } = new();

    public string Version => $"{MajorVersion}.{MinorVersion}";

    public CpEntry? Cp(int index) => ConstantPool.FirstOrDefault(e => e.Index == index);

    public string? CpUtf8(int index) =>
        Cp(index) is { TagName: "Utf8" } e ? (string?)e.Values[0] : null;

    public string? CpClassName(int index) =>
        Cp(index) is { TagName: "Class" } e ? CpUtf8(Convert.ToInt32(e.Values[0])) : null;

    public (string? Owner, string? Name, string? Desc) CpMethodRef(int index)
    {
        if (Cp(index) is not { TagName: "Methodref" or "InterfaceMethodref" } m) return (null, null, null);
        var owner = CpClassName(Convert.ToInt32(m.Values[0]));
        if (Cp(Convert.ToInt32(m.Values[1])) is { TagName: "NameAndType" } nt)
            return (owner, CpUtf8(Convert.ToInt32(nt.Values[0])), CpUtf8(Convert.ToInt32(nt.Values[1])));
        return (owner, null, null);
    }

    public (string? Owner, string? Name, string? Desc) CpFieldRef(int index)
    {
        if (Cp(index) is not { TagName: "Fieldref" } f) return (null, null, null);
        var owner = CpClassName(Convert.ToInt32(f.Values[0]));
        if (Cp(index) is { TagName: "NameAndType" } nt)
            return (owner, CpUtf8(Convert.ToInt32(nt.Values[0])), CpUtf8(Convert.ToInt32(nt.Values[1])));
        return (owner, null, null);
    }
}
