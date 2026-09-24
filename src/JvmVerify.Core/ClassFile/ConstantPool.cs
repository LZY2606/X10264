using System.Globalization;
using System.Text;
using JvmVerify.Core;

namespace JvmVerify.Core.ClassFile;

public enum CpTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10,
    InterfaceMethodref = 11, NameAndType = 12, MethodHandle = 15,
    MethodType = 16, Dynamic = 17, InvokeDynamic = 18, Module = 19, Package = 20
}

/// <summary>
/// One constant-pool slot. Unrecognized tags are kept verbatim as Unknown so the original
/// representation, slot position and diagnostics survive import.
/// </summary>
public abstract record CpEntry(CpTag? Tag, byte TagByte, string RawHex)
{
    public abstract string Display { get; }
}

public sealed record UnknownCpEntry(byte TagByteValue, string RawHexValue, IReadOnlyList<string> Diagnostics)
    : CpEntry(null, TagByteValue, RawHexValue)
{
    public override string Display => $"unknown(tag={TagByteValue})";
}

public sealed record Utf8Entry(string Value, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Utf8, TagByteValue, RawHexValue)
{
    public override string Display => $"Utf8 \"{Value}\"";
}

public sealed record IntEntry(int Value, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Integer, TagByteValue, RawHexValue)
{
    public override string Display => $"int {Value}";
}

public sealed record FloatEntry(float Value, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Float, TagByteValue, RawHexValue)
{
    public override string Display => $"float {Value.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record LongEntry(long Value, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Long, TagByteValue, RawHexValue)
{
    public override string Display => $"long {Value}";
}

public sealed record DoubleEntry(double Value, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Double, TagByteValue, RawHexValue)
{
    public override string Display => $"double {Value.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record ClassEntry(ushort NameIndex, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.Class, TagByteValue, RawHexValue)
{
    public override string Display => $"Class #{NameIndex}";
}

public sealed record StringEntry(ushort Utf8Index, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.String, TagByteValue, RawHexValue)
{
    public override string Display => $"String #{Utf8Index}";
}

public sealed record RefEntry(CpTag TagKind, ushort ClassIndex, ushort NameAndTypeIndex,
    byte TagByteValue, string RawHexValue) : CpEntry(TagKind, TagByteValue, RawHexValue)
{
    public override string Display => $"{TagKind} #{ClassIndex}.#{NameAndTypeIndex}";
}

public sealed record NameAndTypeEntry(ushort NameIndex, ushort DescriptorIndex,
    byte TagByteValue, string RawHexValue) : CpEntry(CpTag.NameAndType, TagByteValue, RawHexValue)
{
    public override string Display => $"NameAndType #{NameIndex}:#{DescriptorIndex}";
}

public sealed record MethodHandleEntry(byte ReferenceKind, ushort ReferenceIndex,
    byte TagByteValue, string RawHexValue) : CpEntry(CpTag.MethodHandle, TagByteValue, RawHexValue)
{
    public override string Display => $"MethodHandle kind={ReferenceKind} #{ReferenceIndex}";
}

public sealed record MethodTypeEntry(ushort DescriptorIndex, byte TagByteValue, string RawHexValue)
    : CpEntry(CpTag.MethodType, TagByteValue, RawHexValue)
{
    public override string Display => $"MethodType #{DescriptorIndex}";
}

public sealed record DynamicEntry(CpTag TagKind, ushort BootstrapMethodAttrIndex, ushort NameAndTypeIndex,
    byte TagByteValue, string RawHexValue) : CpEntry(TagKind, TagByteValue, RawHexValue)
{
    public override string Display => $"{TagKind} bsm={BootstrapMethodAttrIndex}:#{NameAndTypeIndex}";
}

public sealed record SingleIndexEntry(CpTag TagKind, ushort Index, byte TagByteValue, string RawHexValue)
    : CpEntry(TagKind, TagByteValue, RawHexValue)
{
    public override string Display => $"{TagKind} #{Index}";
}

/// <summary>
/// Constant pool as imported. Entries is 1-based (slot 0 unused; long/double consume two
/// slots, the second is a CpPadding entry). All cp indexes exposed to the rest of the
/// system are Quantity values on the CpIndex basis and carry the original raw token.
/// </summary>
public sealed class ConstantPool
{
    public sealed record Padding(byte TagByteValue, string RawHexValue)
        : CpEntry(null, TagByteValue, RawHexValue)
    {
        public override string Display => "<padding after long/double>";
    }

    private readonly List<CpEntry?> _entries = new() { null };

    public IReadOnlyList<CpEntry?> Entries => _entries;
    public int Count => _entries.Count;

    internal void Add(CpEntry entry) => _entries.Add(entry);
    internal void AddPadding(byte tag, string raw) => _entries.Add(new Padding(tag, raw));

    public CpEntry? this[int index] =>
        index >= 0 && index < _entries.Count ? _entries[index] : null;

    public bool TryGet<T>(int index, out T entry) where T : CpEntry
    {
        if (_entries[index] is T t) { entry = t; return true; }
        entry = null!;
        return false;
    }

    public string Utf8(int index) => _entries[index] is Utf8Entry u ? u.Value : $"#{index}";

    public string ClassName(int index) =>
        _entries[index] is ClassEntry c ? Utf8(c.NameIndex) : $"#{index}";

    public string RefText(int index)
    {
        switch (_entries[index])
        {
            case RefEntry r:
                var cls = ClassName(r.ClassIndex);
                if (_entries[r.NameAndTypeIndex] is NameAndTypeEntry nt)
                    return $"{cls}.{Utf8(nt.NameIndex)}:{Utf8(nt.DescriptorIndex)}";
                return r.Display;
            case DynamicEntry d:
                return _entries[d.NameAndTypeIndex] is NameAndTypeEntry nt2
                    ? $"{d.TagKind} {Utf8(nt2.NameIndex)}:{Utf8(nt2.DescriptorIndex)}"
                    : d.Display;
            default:
                return _entries[index]?.Display ?? $"#{index}";
        }
    }

    public IReadOnlyList<string> Diagnostics { get; internal set; } = Array.Empty<string>();
}

internal ref struct CpReader
{
    private readonly byte[] _b;
    private int _p;
    public CpReader(byte[] bytes, int offset) { _b = bytes; _p = offset; }
    public int Position => _p;
    public byte U1() => _b[_p++];
    public ushort U2() { ushort v = (ushort)((_b[_p] << 8) | _b[_p + 1]); _p += 2; return v; }
    public int S4() { int v = (_b[_p] << 24) | (_b[_p + 1] << 16) | (_b[_p + 2] << 8) | _b[_p + 3]; _p += 4; return v; }
    public uint U4() { uint v = (uint)((_b[_p] << 24) | (_b[_p + 1] << 16) | (_b[_p + 2] << 8) | _b[_p + 3]); _p += 4; return v; }
    public void Skip(int n) => _p += n;
    public string Hex(int start, int len)
    {
        var sb = new StringBuilder(len * 2);
        for (int i = start; i < start + len && i < _b.Length; i++)
            sb.Append(_b[i].ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
