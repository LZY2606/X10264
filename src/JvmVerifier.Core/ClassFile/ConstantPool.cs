namespace JvmVerifier.Core.ClassFile;

/// <summary>JVM constant pool tags (JVMS §4.4).</summary>
public enum CpTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10,
    InterfaceMethodref = 11, NameAndType = 12,
    MethodHandle = 15, MethodType = 16, Dynamic = 17,
    InvokeDynamic = 18, Module = 19, Package = 20
}

/// <summary>
/// A single constant pool slot. Long/Double occupy two indices: the second
/// slot is represented by <see cref="IsGhost"/>. The original index, tag and
/// raw operands are retained alongside resolved text.
/// </summary>
public sealed class ConstantPoolEntry
{
    public int Index { get; set; }
    public CpTag Tag { get; set; }
    public ushort[] Refs { get; set; } = Array.Empty<ushort>();
    public byte ReferenceKind { get; set; }
    public byte[]? RawValue { get; set; }
    public string? Utf8Text { get; set; }
    public bool IsGhost { get; set; }

    public string Display(ConstantPool cp)
    {
        if (IsGhost) return $"#{Index} <ghost (second half of long/double)>";
        return Tag switch
        {
            CpTag.Utf8 => $"Utf8 \"{Utf8Text}\"",
            CpTag.Class => $"Class {cp.Utf8(Refs[0])}",
            CpTag.String => $"String \"{cp.Utf8(Refs[0])}\"",
            CpTag.NameAndType => $"NameAndType {cp.Utf8(Refs[0])}:{cp.Utf8(Refs[1])}",
            CpTag.Methodref => $"Methodref {cp.ClassName(Refs[0])}.{cp.NameAndType(Refs[1])}",
            CpTag.InterfaceMethodref => $"InterfaceMethodref {cp.ClassName(Refs[0])}.{cp.NameAndType(Refs[1])}",
            CpTag.Fieldref => $"Fieldref {cp.ClassName(Refs[0])}.{cp.NameAndType(Refs[1])}",
            CpTag.Integer => $"Integer {IntValue}",
            CpTag.Float => $"Float 0x{Convert.ToHexString(RawValue ?? Array.Empty<byte>())}",
            CpTag.Long => $"Long 0x{Convert.ToHexString(RawValue ?? Array.Empty<byte>())}",
            CpTag.Double => $"Double 0x{Convert.ToHexString(RawValue ?? Array.Empty<byte>())}",
            CpTag.MethodHandle => $"MethodHandle kind={ReferenceKind} #{Refs[0]}",
            CpTag.MethodType => $"MethodType {cp.Utf8(Refs[0])}",
            CpTag.InvokeDynamic => $"InvokeDynamic bsm={Refs[0]} {cp.NameAndType(Refs[1])}",
            CpTag.Dynamic => $"Dynamic bsm={Refs[0]} {cp.NameAndType(Refs[1])}",
            _ => $"{Tag} [{string.Join(',', Refs)}]"
        };
    }

    public int IntValue
    {
        get
        {
            var b = (byte[]?)RawValue?.Clone() ?? throw new InvalidOperationException();
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt32(b);
        }
    }

    public long LongValue
    {
        get
        {
        var b = (byte[]?)RawValue?.Clone() ?? throw new InvalidOperationException();
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return BitConverter.ToInt64(b);
        }
    }
}

public sealed class ConstantPool
{
    private readonly Dictionary<int, ConstantPoolEntry> _entries = new();
    public int Count { get; private set; }

    public ConstantPoolEntry this[int index]
    {
        get
        {
            if (index < 1 || index >= Count)
                throw new ClassFormatException($"Invalid constant pool index #{index}");
            return _entries[index];
        }
    }

    public IEnumerable<ConstantPoolEntry> Entries =>
        Enumerable.Range(1, Count - 1).Select(i => _entries[i]);

    public bool Has(int index) => index >= 1 && index < Count && _entries.ContainsKey(index);

    public string Utf8(int index) => _entries[index].Tag == CpTag.Utf8
        ? _entries[index].Utf8Text ?? ""
        : throw new ClassFormatException($"Constant pool entry #{index} is not Utf8");

    public string ClassName(int classRefIndex)
    {
        var e = _entries[classRefIndex];
        return e.Tag == CpTag.Class ? Utf8(e.Refs[0])
            : throw new ClassFormatException($"#{classRefIndex} is not a Class entry");
    }

    public (string ClassName, string Name, string Descriptor) Ref(int refIndex)
    {
        var e = _entries[refIndex];
        if (e.Tag is not (CpTag.Methodref or CpTag.Fieldref or CpTag.InterfaceMethodref))
            throw new ClassFormatException($"#{refIndex} is not a ref entry");
        var nt = _entries[e.Refs[1]];
        return (ClassName(e.Refs[0]), Utf8(nt.Refs[0]), Utf8(nt.Refs[1]));
    }

    public string NameAndType(int ntIndex)
    {
        var nt = _entries[ntIndex];
        return $"{Utf8(nt.Refs[0])}:{Utf8(nt.Refs[1])}";
    }

    public static ConstantPool Parse(BigEndianReader r)
    {
        var cp = new ConstantPool();
        ushort count = r.U2();
        cp.Count = count;
        int i = 1;
        while (i < count)
        {
            int startOffset = r.Position;
            var tag = (CpTag)r.U1();
            var entry = new ConstantPoolEntry { Index = i, Tag = tag };
            switch (tag)
            {
                case CpTag.Utf8:
                {
                    ushort len = r.U2();
                    entry.Utf8Text = r.ModifiedUtf8(len);
                    break;
                }
                case CpTag.Integer or CpTag.Float:
                    entry.RawValue = r.ReadBytes(4);
                    break;
                case CpTag.Long or CpTag.Double:
                    entry.RawValue = r.ReadBytes(8);
                    cp._entries[i] = entry;
                    cp._entries[i + 1] = new ConstantPoolEntry { Index = i + 1, Tag = tag, IsGhost = true };
                    i += 2;
                    continue;
                case CpTag.Class or CpTag.String or CpTag.MethodType or CpTag.Module or CpTag.Package:
                    entry.Refs = new[] { r.U2() };
                    break;
                case CpTag.Fieldref or CpTag.Methodref or CpTag.InterfaceMethodref
                    or CpTag.NameAndType or CpTag.Dynamic or CpTag.InvokeDynamic:
                    entry.Refs = new[] { r.U2(), r.U2() };
                    break;
                case CpTag.MethodHandle:
                    entry.ReferenceKind = r.U1();
                    entry.Refs = new[] { r.U2() };
                    break;
                default:
                    throw new ClassFormatException($"Unknown constant pool tag {(byte)tag}", startOffset);
            }
            cp._entries[i] = entry;
            i++;
        }
        return cp;
    }
}
