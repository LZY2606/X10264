using System.Text.Json.Serialization;

namespace JvmVerifier.Core.Raw;

public enum CpTag : byte
{
    Utf8 = 1,
    Integer = 3,
    Float = 4,
    Long = 5,
    Double = 6,
    Class = 7,
    String = 8,
    Fieldref = 9,
    Methodref = 10,
    InterfaceMethodref = 11,
    NameAndType = 12,
    MethodHandle = 15,
    MethodType = 16,
    Dynamic = 17,
    InvokeDynamic = 18,
    Module = 19,
    Package = 20,
}

/// <summary>
/// One constant pool slot, preserved exactly as imported. Index 0 is a slot too
/// (unused, or the second half of a wide Long/Double entry). <see cref="RawBytes"/>
/// keeps the original representation for long/double/integer/float; the derived
/// layer is the only place that may decode them.
/// </summary>
public sealed class CpEntry
{
    public int Index { get; set; }
    public CpTag? Tag { get; set; }
    public bool IsSecondHalfOfWide { get; set; }

    public string? Utf8 { get; set; }
    public int? NameIndex { get; set; }
    public int? ClassIndex { get; set; }
    public int? NameAndTypeIndex { get; set; }
    public int? DescriptorIndex { get; set; }
    public int? StringIndex { get; set; }
    public int? ReferenceKind { get; set; }
    public int? ReferenceIndex { get; set; }
    public int? BootstrapMethodAttrIndex { get; set; }
    public long? RawIntegerBits { get; set; }
    public double? RawDoubleValue { get; set; }
    public long? RawLongBits { get; set; }
    public float? RawFloatValue { get; set; }

    /// <summary>Original bytes exactly as they appeared in the class file (or JSON).</summary>
    public byte[]? RawBytes { get; set; }

    /// <summary>Stable identity, e.g. cp#12.</summary>
    [JsonIgnore]
    public string Id => $"cp#{Index}";
}

public sealed class ConstantPool
{
    public int Count { get; set; }
    public List<CpEntry> Entries { get; set; } = new();

    public CpEntry? At(int index) =>
        index >= 0 && index < Entries.Count ? Entries[index] : null;

    /// <summary>Resolves a Class entry to its Utf8 internal name, or null with a diagnostic.</summary>
    public string? ClassName(int classIndex, List<RawDiagnostic>? diagnostics = null)
    {
        var cls = At(classIndex);
        if (cls?.Tag != CpTag.Class)
        {
            diagnostics?.Add(new RawDiagnostic("cp-tag", $"cp#{classIndex}",
                $"cp#{classIndex} is not a CONSTANT_Class entry", $"cp#{classIndex}"));
            return null;
        }
        var name = At(cls.NameIndex!.Value);
        if (name?.Tag != CpTag.Utf8)
        {
            diagnostics?.Add(new RawDiagnostic("cp-tag", $"cp#{cls.NameIndex}",
                $"cp#{cls.NameIndex} (name of cp#{classIndex}) is not Utf8", $"cp#{cls.NameIndex}"));
            return null;
        }
        return name.Utf8;
    }

    public string? Utf8Text(int utf8Index, List<RawDiagnostic>? diagnostics = null)
    {
        var e = At(utf8Index);
        if (e?.Tag != CpTag.Utf8)
        {
            diagnostics?.Add(new RawDiagnostic("cp-tag", $"cp#{utf8Index}",
                $"cp#{utf8Index} is not a CONSTANT_Utf8 entry", $"cp#{utf8Index}"));
            return null;
        }
        return e.Utf8;
    }

    public (string? owner, string? name, string? descriptor) MemberRef(int refIndex, List<RawDiagnostic>? diagnostics = null)
    {
        var e = At(refIndex);
        if (e is null || e.Tag is not (CpTag.Fieldref or CpTag.Methodref or CpTag.InterfaceMethodref))
        {
            diagnostics?.Add(new RawDiagnostic("cp-tag", $"cp#{refIndex}",
                $"cp#{refIndex} is not a member reference", $"cp#{refIndex}"));
            return default;
        }
        var owner = ClassName(e.ClassIndex!.Value, diagnostics);
        var nt = At(e.NameAndTypeIndex!.Value);
        if (nt?.Tag != CpTag.NameAndType)
        {
            diagnostics?.Add(new RawDiagnostic("cp-tag", $"cp#{e.NameAndTypeIndex}",
                $"cp#{e.NameAndTypeIndex} is not NameAndType", $"cp#{e.NameAndTypeIndex}"));
            return (owner, null, null);
        }
        var name = Utf8Text(nt.NameIndex!.Value, diagnostics);
        var desc = Utf8Text(nt.DescriptorIndex!.Value, diagnostics);
        return (owner, name, desc);
    }
}
