using System.Collections.Generic;

namespace Gsb.Core.ClassFile;

/// <summary>
/// class 文件版本。major/minor 全部以原始 u2 形式保留，不在导入层归一化。
/// </summary>
public sealed record ClassFileVersion(int Minor, int Major)
{
    /// <summary>JDK 1.0.2。旧版验证语义从这里分岔。</summary>
    public bool PreStackMap => Major < 50;
    /// <summary>50.0：StackMapTable 强制；51+：不可达块允许缺帧。</summary>
    public bool DeadCodeFrameTolerant => Major >= 51;
    public override string ToString() => $"major={Major},minor={Minor}";
}

public enum ConstantTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10,
    InterfaceMethodref = 11, NameAndType = 12, MethodHandle = 15,
    MethodType = 16, Dynamic = 17, InvokeDynamic = 18, Module = 19, Package = 20
}

/// <summary>
/// 常量池项。<see cref="Index"/> 是 1 基的原始常量池下标；long/double 占据两格
/// （后续格位以 <see cref="DoubleSlotGhost"/> 标记），原表顺序永不重排。
/// </summary>
public abstract record ConstantPoolEntry(ConstantTag Tag, int Index)
{
    public bool IsDoubleSlot => Tag is ConstantTag.Long or ConstantTag.Double;
    public static readonly DoubleSlotPlaceholder DoubleSlotGhost = new(0);
}

public sealed record Utf8Info(int Index, string Value) : ConstantPoolEntry(ConstantTag.Utf8, Index);
public sealed record IntegerInfo(int Index, int Value) : ConstantPoolEntry(ConstantTag.Integer, Index);
public sealed record FloatInfo(int Index, float Value) : ConstantPoolEntry(ConstantTag.Float, Index);
public sealed record LongInfo(int Index, long Value) : ConstantPoolEntry(ConstantTag.Long, Index);
public sealed record DoubleInfo(int Index, double Value) : ConstantPoolEntry(ConstantTag.Double, Index);
public sealed record ClassInfo(int Index, int NameIndex) : ConstantPoolEntry(ConstantTag.Class, Index);
public sealed record StringInfo(int Index, int Utf8Index) : ConstantPoolEntry(ConstantTag.String, Index);
public sealed record FieldrefInfo(int Index, int ClassIndex, int NameAndTypeIndex) : ConstantPoolEntry(ConstantTag.Fieldref, Index);
public sealed record MethodrefInfo(int Index, int ClassIndex, int NameAndTypeIndex, bool IsInterface)
    : ConstantPoolEntry(IsInterface ? ConstantTag.InterfaceMethodref : ConstantTag.Methodref, Index);
public sealed record NameAndTypeInfo(int Index, int NameIndex, int DescriptorIndex) : ConstantPoolEntry(ConstantTag.NameAndType, Index);
public sealed record MethodHandleInfo(int Index, int ReferenceKind, int ReferenceIndex) : ConstantPoolEntry(ConstantTag.MethodHandle, Index);
public sealed record MethodTypeInfo(int Index, int DescriptorIndex) : ConstantPoolEntry(ConstantTag.MethodType, Index);
public sealed record InvokeDynamicInfo(int Index, int BootstrapMethodAttrIndex, int NameAndTypeIndex) : ConstantPoolEntry(ConstantTag.InvokeDynamic, Index);
public sealed record RawConstantInfo(int Index, ConstantTag Tag, byte[] RawBytes) : ConstantPoolEntry(Tag, Index);

/// <summary>long/double 占据的第二个常量池格位；不携带值，只保留槽位事实。</summary>
public sealed record DoubleSlotPlaceholder(int Index) : ConstantPoolEntry(0, Index);

public sealed record FieldOrMethodInfo(
    ushort AccessFlags,
    string Name,
    string Descriptor,
    IReadOnlyList<AttributeInfo> Attributes);

public sealed record AttributeInfo(string Name, byte[] Data);

/// <summary>解析后的 classfile。解析失败的属性保留为 <see cref="AttributeInfo"/> 原始字节并附诊断。</summary>
public sealed class ClassFileModel
{
    public required ClassFileVersion Version { get; init; }
    public required ushort AccessFlags { get; init; }
    public required string ThisClassName { get; init; }
    public string? SuperClassName { get; init; }
    public required IReadOnlyList<string> Interfaces { get; init; }
    /// <summary>常量池：保留 1 基下标与 long/double 空位，顺序即 classfile 原始顺序。</summary>
    public required IReadOnlyList<ConstantPoolEntry> ConstantPool { get; init; }
    public required IReadOnlyList<FieldOrMethodInfo> Fields { get; init; }
    public required IReadOnlyList<MethodInfo> Methods { get; init; }
    public required IReadOnlyList<AttributeInfo> ClassAttributes { get; init; }
    /// <summary>导入层无法解释、留在原位置的原始表示与诊断（不丢数据）。</summary>
    public List<RawDiagnostic> Diagnostics { get; } = new();
    public byte[] RawBytes { get; init; } = System.Array.Empty<byte>();
}

public sealed record MethodInfo(
    ushort AccessFlags,
    string Name,
    string Descriptor,
    CodeAttribute? Code,
    IReadOnlyList<AttributeInfo> Attributes);

/// <summary>导入阶段产生的诊断：原始坐标 + 无法转换的原值，不阻断其余解析。</summary>
public sealed record RawDiagnostic(string Where, string Message, long? Offset, string? RawHex);
