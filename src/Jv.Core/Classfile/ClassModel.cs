using System.Text;

namespace Jv.Core.Classfile;

// 常量池项。RawTag 保留原 tag 字节；引用类项保留原始 cp 索引（不预先解引用）。
public abstract record CpEntry(int Index, int RawTag)
{
    public sealed record Utf8(int Index, string Value, byte[] RawBytes) : CpEntry(Index, 1);
    public sealed record Integer(int Index, int Value) : CpEntry(Index, 3);
    public sealed record Float(int Index, float Value) : CpEntry(Index, 4);
    public sealed record Long(int Index, long Value) : CpEntry(Index, 5);
    public sealed record Double(int Index, double Value) : CpEntry(Index, 6);
    public sealed record ClassInfo(int Index, int NameIndex) : CpEntry(Index, 7);
    public sealed record String(int Index, int Utf8Index) : CpEntry(Index, 8);
    public sealed record Fieldref(int Index, int ClassIndex, int NameAndTypeIndex) : CpEntry(Index, 9);
    public sealed record Methodref(int Index, int ClassIndex, int NatIndex) : CpEntry(Index, 10);
    public sealed record InterfaceMethodref(int Index, int ClassIndex, int NatIndex) : CpEntry(Index, 11);
    public sealed record NameAndType(int Index, int NameIndex, int DescriptorIndex) : CpEntry(Index, 12);
    public sealed record MethodHandle(int Index, int ReferenceKind, int ReferenceIndex) : CpEntry(Index, 15);
    public sealed record MethodType(int Index, int DescriptorIndex) : CpEntry(Index, 16);
    public sealed record InvokeDynamic(int Index, int BootstrapMethodAttrIndex, int NatIndex) : CpEntry(Index, 18);
    // 损坏常量池项：保留原位置与原始字节，不丢弃。
    public sealed record Corrupt(int Index, byte[] RawBytes, string Diagnostics) : CpEntry(Index, -1);

    public string Describe(IReadOnlyList<CpEntry> pool) => CpPrinter.Describe(this, pool);
}

public static class CpPrinter
{
    public static string Utf8(IReadOnlyList<CpEntry> pool, int idx) =>
        idx > 0 && idx < pool.Count && pool[idx] is CpEntry.Utf8 u ? u.Value : $"<bad cp #{idx}>";

    public static string Describe(CpEntry e, IReadOnlyList<CpEntry> pool) => e switch
    {
        CpEntry.Utf8 u => $"Utf8 \"{u.Value}\"",
        CpEntry.ClassInfo c => $"Class #{c.NameIndex} // {Utf8(pool, c.NameIndex)}",
        CpEntry.String s => $"String #{s.Utf8Index} // {Utf8(pool, s.Utf8Index)}",
        CpEntry.NameAndType n => $"NameAndType #{n.NameIndex}:#{n.DescriptorIndex} // {Utf8(pool, n.NameIndex)}:{Utf8(pool, n.DescriptorIndex)}",
        CpEntry.Fieldref f => $"Fieldref #{f.ClassIndex}.#{f.NameAndTypeIndex} // {RefName(f.ClassIndex, f.NameAndTypeIndex, pool)}",
        CpEntry.Methodref m => $"Methodref #{m.ClassIndex}.#{m.NatIndex} // {RefName(m.ClassIndex, m.NatIndex, pool)}",
        CpEntry.InterfaceMethodref im => $"InterfaceMethodref #{im.ClassIndex}.#{im.NatIndex} // {RefName(im.ClassIndex, im.NatIndex, pool)}",
        CpEntry.Integer i => $"Integer {i.Value}",
        CpEntry.Float f => $"Float {f.Value}",
        CpEntry.Long l => $"Long {l.Value}",
        CpEntry.Double d => $"Double {d.Value}",
        CpEntry.MethodHandle h => $"MethodHandle kind={h.ReferenceKind} #{h.ReferenceIndex}",
        CpEntry.MethodType mt => $"MethodType #{mt.DescriptorIndex} // {Utf8(pool, mt.DescriptorIndex)}",
        CpEntry.InvokeDynamic id => $"InvokeDynamic bsm={id.BootstrapMethodAttrIndex} Nat=#{id.NatIndex}",
        CpEntry.Corrupt c => $"CORRUPT: {c.Diagnostics}",
        _ => $"tag={e.RawTag}"
    };

    private static string RefName(int classIdx, int natIdx, IReadOnlyList<CpEntry> pool)
    {
        string cls = classIdx > 0 && classIdx < pool.Count && pool[classIdx] is CpEntry.ClassInfo ci
            ? Utf8(pool, ci.NameIndex) : $"#{classIdx}";
        string member = "?";
        if (natIdx > 0 && natIdx < pool.Count && pool[natIdx] is CpEntry.NameAndType n)
            member = $"{Utf8(pool, n.NameIndex)}:{Utf8(pool, n.DescriptorIndex)}";
        return $"{cls}.{member}";
    }
}

public sealed record ExceptionEntry(int StartPc, int EndPc, int HandlerPc, int CatchType,
    string CatchTypeName, int Ordinal);

// StackMapTable：offset_delta 是“原始表示”，frameType 也是；派生层才换算绝对偏移。
public abstract record StackMapFrame(int FrameType, int OffsetDelta, int Ordinal)
{
    public sealed record Same(int FrameType, int OffsetDelta, int Ordinal) : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record SameLocals1StackItem(int FrameType, int OffsetDelta, int Ordinal, VTypeInfo Stack)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record SameLocals1StackItemExtended(int FrameType, int OffsetDelta, int Ordinal, VTypeInfo Stack)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record Chop(int FrameType, int OffsetDelta, int Ordinal, int ChoppedCount)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record SameExtended(int FrameType, int OffsetDelta, int Ordinal)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record Append(int FrameType, int OffsetDelta, int Ordinal, IReadOnlyList<VTypeInfo> AddedLocals)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
    public sealed record Full(int FrameType, int OffsetDelta, int Ordinal,
        IReadOnlyList<VTypeInfo> Locals, IReadOnlyList<VTypeInfo> Stack)
        : StackMapFrame(FrameType, OffsetDelta, Ordinal);
}

// verification_type_info，保留原 tag 与原 cp 索引/偏移；Unparseable 保留原字节+诊断。
public abstract record VTypeInfo(int Tag)
{
    public sealed record Top(int Dummy = 0) : VTypeInfo(0);
    public sealed record IntegerInfo(int Dummy = 0) : VTypeInfo(1);
    public sealed record FloatInfo(int Dummy = 0) : VTypeInfo(2);
    public sealed record DoubleInfo(int Dummy = 0) : VTypeInfo(3);
    public sealed record LongInfo(int Dummy = 0) : VTypeInfo(4);
    public sealed record NullInfo(int Dummy = 0) : VTypeInfo(5);
    public sealed record UninitializedThisInfo(int Dummy = 0) : VTypeInfo(6);
    public sealed record ObjectInfo(int CpIndex, string ClassName) : VTypeInfo(7);
    public sealed record UninitializedVarInfo(int NewOffset) : VTypeInfo(8);
    public sealed record Unparseable(int RawTag, byte[] RawBytes, string Diagnostics) : VTypeInfo(RawTag);

    public string Display(IReadOnlyList<CpEntry> pool) => this switch
    {
        ObjectInfo o => $"Object #{o.CpIndex} {CpPrinter.Utf8(pool, ClassNameIndex(pool, o.CpIndex))}",
        UninitializedVarInfo u => $"uninitialized offset {u.NewOffset}",
        Unparseable x => $"CORRUPT tag={x.RawTag}: {x.Diagnostics}",
        _ => KindName()
    };

    private static int ClassNameIndex(IReadOnlyList<CpEntry> pool, int idx) =>
        idx > 0 && idx < pool.Count && pool[idx] is CpEntry.ClassInfo c ? c.NameIndex : -1;

    public string KindName() => Tag switch
    {
        0 => "Top", 1 => "int", 2 => "float", 3 => "double",
        4 => "long", 5 => "null", 6 => "uninitializedThis",
        7 => "Object", 8 => "Uninitialized",
        _ => $"tag={Tag}"
    };
}

public sealed record CodeAttribute(int MaxStack, int MaxLocals, byte[] CodeBytes,
    IReadOnlyList<ExceptionEntry> ExceptionTable,
    IReadOnlyList<StackMapFrame> RawFrames,
    bool HasStackMapTable,
    IReadOnlyList<string> RawAttributeNames);

public sealed record MethodInfo(int AccessFlags, string Name, string Descriptor,
    CodeAttribute? Code, int Ordinal);

public sealed record ClassModel(
    int MinorVersion, int MajorVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    int AccessFlags, string ThisClass, string? SuperClass,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<MethodInfo> Methods,
    // 解析诊断（损坏项留在原位置，同时记录诊断）。
    IReadOnlyList<string> Diagnostics,
    byte[]? OriginalBytes)
{
    public string VersionText => $"{MajorVersion}.{MinorVersion}";
    public bool PreStackMap => MajorVersion < 50; // CLDC/旧版推断验证语义
}
