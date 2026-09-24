using System.Collections.Generic;

namespace Seal.Core.ClassFile;

// JVMS 4.4 constant pool tags.
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
    InvokeDynamic = 18,
}

// One constant pool entry. The original index, tag, raw bytes and every
// referenced index are preserved verbatim ("导入时保留原始表示").
public sealed record CpEntry(
    int Index,
    CpTag Tag,
    IReadOnlyList<byte> Raw,
    string Display,
    IReadOnlyList<int> RefIndices,
    long? Numeric = null,
    string? Text = null);

// Constant pool with 1-based indexing; long/double consume two slots (JVMS 4.4.5).
public sealed class ConstantPool
{
    private readonly CpEntry?[] _entries;

    public ConstantPool(CpEntry?[] entries)
    {
        _entries = entries;
        Count = entries.Length; // includes the synthetic slot 0 and double-width second slots
    }

    public int Count { get; }

    public CpEntry? At(int index) =>
        index >= 0 && index < _entries.Length ? _entries[index] : null;

    public CpEntry Require(int index) =>
        At(index) ?? throw new ClassFileFormatException($"constant pool index #{index} missing (wide/long/double second slot?)");

    public string Utf8(int index)
    {
        var e = Require(index);
        if (e.Tag != CpTag.Utf8)
            throw new ClassFileFormatException($"constant pool #{index} is {e.Tag}, expected Utf8");
        return e.Text!;
    }

    public string ClassName(int classInfoIndex)
    {
        var cls = Require(classInfoIndex);
        if (cls.Tag != CpTag.Class)
            throw new ClassFileFormatException($"#{classInfoIndex} is {cls.Tag}, expected Class");
        return Utf8(cls.RefIndices[0]);
    }

    // fieldref/methodref/interfaceMethodref -> (class name, name, descriptor)
    public (string ClassName, string MemberName, string Descriptor) RefNameAndType(int refIndex)
    {
        var r = Require(refIndex);
        if (r.Tag is not (CpTag.Fieldref or CpTag.Methodref or CpTag.InterfaceMethodref))
            throw new ClassFileFormatException($"#{refIndex} is {r.Tag}, expected *ref");
        var className = ClassName(r.RefIndices[0]);
        var nt = Require(r.RefIndices[1]);
        if (nt.Tag != CpTag.NameAndType)
            throw new ClassFileFormatException($"#{r.RefIndices[1]} is {nt.Tag}, expected NameAndType");
        return (className, Utf8(nt.RefIndices[0]), Utf8(nt.RefIndices[1]));
    }
}
