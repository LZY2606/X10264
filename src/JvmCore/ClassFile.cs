using System.Text;

namespace JvmCore;

/// <summary>Raw constant-pool entry: tag + original payload bytes, index preserved.</summary>
public sealed record CpEntry(int Index, byte Tag, byte[] RawPayload)
{
    public string Describe(ConstantPool cp) => Tag switch
    {
        1 => $"Utf8 \"{cp.Utf8(Index)}\"",
        7 => $"Class #{BitConverter.ToUInt16(RawPayload)} -> {cp.ClassName(Index)}",
        9 => $"Fieldref {cp.RefText(Index)}",
        10 => $"Methodref {cp.RefText(Index)}",
        11 => $"InterfaceMethodref {cp.RefText(Index)}",
        12 => $"NameAndType {cp.NameAndTypeText(Index)}",
        8 => $"String -> \"{cp.Utf8(BitConverter.ToUInt16(RawPayload))}\"",
        3 => $"Integer {BitConverter.ToInt32(RawPayload)}",
        4 => $"Float {BitConverter.ToSingle(RawPayload)}",
        5 => $"Long {BitConverter.ToInt64(RawPayload)}",
        6 => $"Double {BitConverter.ToDouble(RawPayload)}",
        _ => $"tag{Tag} [{Convert.ToHexString(RawPayload)}]"
    };
}

public sealed class ConstantPool
{
    private readonly CpEntry?[] _entries;
    public ConstantPool(CpEntry?[] entries) => _entries = entries;
    public int Count => _entries.Length;
    public CpEntry? this[int i] => i >= 1 && i < _entries.Length ? _entries[i] : null;
    public IEnumerable<CpEntry> Entries => _entries.Skip(1).Where(e => e != null)!;

    public string Utf8(int index)
    {
        var e = this[index] ?? throw new ArgumentException($"cp[{index}] empty");
        if (e.Tag != 1) throw new ArgumentException($"cp[{index}] not Utf8 (tag {e.Tag})");
        return Encoding.UTF8.GetString(e.RawPayload);
    }

    public string ClassName(int classIndex)
    {
        var e = this[classIndex] ?? throw new ArgumentException($"cp[{classIndex}] empty");
        if (e.Tag != 7) throw new ArgumentException($"cp[{classIndex}] not Class");
        return Utf8(BigEndian.UInt16(e.RawPayload, 0));
    }

    public (string name, string descriptor) NameAndType(int ntIndex)
    {
        var e = this[ntIndex] ?? throw new ArgumentException($"cp[{ntIndex}] empty");
        if (e.Tag != 12) throw new ArgumentException($"cp[{ntIndex}] not NameAndType");
        return (Utf8(BigEndian.UInt16(e.RawPayload, 0)), Utf8(BigEndian.UInt16(e.RawPayload, 2)));
    }

    public (string owner, string name, string descriptor) Ref(int refIndex)
    {
        var e = this[refIndex] ?? throw new ArgumentException($"cp[{refIndex}] empty");
        if (e.Tag is not (9 or 10 or 11)) throw new ArgumentException($"cp[{refIndex}] not a ref");
        var nt = NameAndType(BigEndian.UInt16(e.RawPayload, 2));
        return (ClassName(BigEndian.UInt16(e.RawPayload, 0)), nt.name, nt.descriptor);
    }

    public string RefText(int index) { var (o, n, d) = Ref(index); return $"{o}.{n}{d}"; }
    public string NameAndTypeText(int index) { var (n, d) = NameAndType(index); return $"{n}:{d}"; }
}

public sealed record ExceptionHandler(Quantity StartPc, Quantity EndPc, Quantity HandlerPc, Quantity CatchTypeIndex, string? CatchTypeName);

/// <summary>Raw stack-map frame as parsed; Offset carries byte-offset coordinates.</summary>
public sealed record StackMapFrameRaw(Quantity Offset, string FrameType, FrameState State, int RawTag);

public sealed record MethodModel(
    string Name,
    string DescriptorRaw,          // original descriptor string, never rewritten
    Quantity MaxStack,
    Quantity MaxLocals,
    byte[] Code,                   // raw instruction bytes, offsets are indexes into this array
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrameRaw> StackMapFrames,
    byte[]? StackMapTableRaw,      // original attribute bytes preserved
    bool IsConstructor,
    bool IsStatic);

public sealed record FieldModel(string Name, string DescriptorRaw);

public sealed class ClassFile
{
    public required byte[] RawBytes { get; init; }                 // original class bytes, byte-for-byte
    public required Quantity MajorVersion { get; init; }
    public required Quantity MinorVersion { get; init; }
    public required ConstantPool ConstantPool { get; init; }
    public required string ThisClass { get; init; }
    public required string SuperClass { get; init; }
    public required IReadOnlyList<FieldModel> Fields { get; set; }
    public required IReadOnlyList<MethodModel> Methods { get; set; }
    /// <summary>Normalization diagnostics: unconvertible values stay raw in place.</summary>
    public List<string> Diagnostics { get; } = new();

    public MethodModel Method(string name) => Methods.First(m => m.Name == name);
}

public static class BigEndian
{
    public static ushort UInt16(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
    public static uint UInt32(byte[] b, int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
}
