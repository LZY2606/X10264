// Raw classfile model. Everything here is source evidence: original byte offsets,
// constant pool entries, descriptors, branch targets, exception ranges,
// StackMapTable frames and the classfile version are retained verbatim.
using System.Collections.Generic;

namespace JvmVerify.Core.ClassFile;

public enum CpTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10,
    InterfaceMethodref = 11, NameAndType = 12, MethodHandle = 15,
    MethodType = 16, Dynamic = 17, InvokeDynamic = 18, Module = 19, Package = 20
}

public abstract record CpEntry(CpTag Tag);
public sealed record Utf8Info(string Value) : CpEntry(CpTag.Utf8);
public sealed record IntegerInfo(int Value) : CpEntry(CpTag.Integer);
public sealed record FloatInfo(float Value) : CpEntry(CpTag.Float);
public sealed record LongInfo(long Value) : CpEntry(CpTag.Long);
public sealed record DoubleInfo(double Value) : CpEntry(CpTag.Double);
public sealed record ClassInfo(ushort NameIndex) : CpEntry(CpTag.Class);
public sealed record StringInfo(ushort Utf8Index) : CpEntry(CpTag.String);
public sealed record FieldrefInfo(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(CpTag.Fieldref);
public sealed record MethodrefInfo(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(CpTag.Methodref);
public sealed record InterfaceMethodrefInfo(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(CpTag.InterfaceMethodref);
public sealed record NameAndTypeInfo(ushort NameIndex, ushort DescriptorIndex) : CpEntry(CpTag.NameAndType);
public sealed record MethodTypeInfo(ushort DescriptorIndex) : CpEntry(CpTag.MethodType);
public sealed record RawCpInfo(CpTag Tag, IReadOnlyList<byte> Payload) : CpEntry(Tag);

// Constant pool index uses 1-based JVM numbering; slot 0 is unused.
public readonly record struct CpIndex(int Value)
{
    public static implicit operator int(CpIndex i) => i.Value;
    public override string ToString() => $"#{Value}";
}

public sealed record AttributeInfo(string Name, IReadOnlyList<byte> Data);

public sealed record RawInstruction(int Offset, int Opcode, string Mnemonic, IReadOnlyList<byte> Operand,
    IReadOnlyList<int> BranchTargets, int Length)
{
    public int EndOffset => Offset + Length;
}

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, CpIndex CatchType,
    int Order, int RawStartOffsetInCode, bool CatchAny => CatchType.Value == 0)
{
    public bool Covers(int pc) => pc >= StartPc && pc < EndPc;
}

public sealed record StackMapFrame(int FrameType, int OffsetDelta, int ByteOffset,
    IReadOnlyList<VerificationType> Locals, IReadOnlyList<VerificationType> Stack,
    int RawStart, int RawLength)
{
    public bool IsFullFrame => FrameType is >= 247 or < 64;
}

public sealed record CodeAttribute(int MaxStack, int MaxLocals,
    IReadOnlyList<byte> Bytecode,
    IReadOnlyList<RawInstruction> Instructions,
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrame> StackMap,
    IReadOnlyList<AttributeInfo> Attributes);

public enum MemberAccess : ushort
{
    Public = 0x0001, Private = 0x0002, Protected = 0x0004, Static = 0x0008,
    Final = 0x0010, Synchronized = 0x0020, Bridge = 0x0040, Varargs = 0x0080,
    Native = 0x0100, Abstract = 0x0400, Strict = 0x0800
}

public sealed record MethodInfo(MemberAccess Access, string Name, string Descriptor,
    CodeAttribute? Code, IReadOnlyList<AttributeInfo> Attributes)
{
    public bool IsStatic => (Access & MemberAccess.Static) != 0;
}

public sealed record FieldInfo(MemberAccess Access, string Name, string Descriptor,
    IReadOnlyList<AttributeInfo> Attributes);

public sealed record ClassFileNode(
    uint Magic,
    ushort MinorVersion,
    ushort MajorVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    MemberAccess AccessFlags,
    CpIndex ThisClass,
    CpIndex SuperClass,
    IReadOnlyList<CpIndex> Interfaces,
    IReadOnlyList<FieldInfo> Fields,
    IReadOnlyList<MethodInfo> Methods,
    IReadOnlyList<AttributeInfo> Attributes,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public string VersionText => $"{MajorVersion}.{MinorVersion}";
    public CpEntry? EntryOrNull(int index) => index >= 1 && index < ConstantPool.Count ? ConstantPool[index] : null;
    public string Utf8(int index) => EntryOrNull(index) is Utf8Info u ? u.Value : $"#idx{index}";
    public string ClassName(CpIndex i) => EntryOrNull(i.Value) is ClassInfo c ? Utf8(c.NameIndex) : $"#idx{i.Value}";
    public string ThisClassName => ClassName(ThisClass);
    public string SuperClassName => SuperClass.Value == 0 ? "" : ClassName(SuperClass);
    public string MemberName(NameAndTypeInfo nt) => Utf8(nt.NameIndex);
    public string MemberDescriptor(NameAndTypeInfo nt) => Utf8(nt.DescriptorIndex);
}

public sealed record ParseDiagnostic(string Code, string Message, int Offset, bool Fatal);
