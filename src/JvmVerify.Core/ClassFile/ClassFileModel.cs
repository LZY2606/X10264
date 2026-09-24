using JvmVerify.Core;

namespace JvmVerify.Core.ClassFile;

public sealed class AttributeInfo
{
    public required string Name { get; init; }
    public required ushort NameIndex { get; init; }
    public required byte[] Data { get; init; }
    public required int AbsoluteOffset { get; init; }
    public override string ToString() => $"@{AbsoluteOffset} {Name}[{Data.Length}]";
}

public sealed class FieldInfo
{
    public required Quantity<ushort> AccessFlags { get; init; }
    public required Quantity<string> Name { get; init; }
    public required Quantity<string> Descriptor { get; init; }
    public required IReadOnlyList<AttributeInfo> Attributes { get; init; }
}

public sealed record ExceptionEntry(
    Quantity<int> StartPc, Quantity<int> EndPc, Quantity<int> HandlerPc,
    Quantity<int> CatchType, Quantity<int> HandlerIndex)
{
    public override string ToString() => $"[{StartPc.Value},{EndPc.Value}) -> {HandlerPc.Value} catch #{CatchType.Value}";
}

public enum MapFrameKind
{
    Same, SameLocalsOneStackItem, SameFrameExtended, Chop, Append, Full,
    Unconvertible
}

/// <summary>
/// One StackMapTable frame as it appears in the classfile: offset delta and verification
/// type tags are retained raw. DeclaredBytecodeOffset is the derived absolute offset
/// (previous offset + offset_delta + 1 for compressed frames) and is explicitly labeled
/// derived; corrupt frames keep their raw bytes and carry diagnostics instead.
/// </summary>
public sealed record StackMapFrame(
    Quantity<int> MapFrameIndex,
    MapFrameKind Kind,
    Quantity<int> OffsetDelta,
    Quantity<int> DeclaredBytecodeOffset,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    string RawHex,
    IReadOnlyList<string> Diagnostics)
{
    public override string ToString() =>
        $"frame#{MapFrameIndex.Value} {Kind} @byte {DeclaredBytecodeOffset.Value} L{Locals.Count} S{Stack.Count}";
}

public sealed class MethodInfo
{
    public required Quantity<ushort> AccessFlags { get; init; }
    public required Quantity<string> Name { get; init; }
    public required Quantity<string> Descriptor { get; init; }
    public required IReadOnlyList<AttributeInfo> Attributes { get; init; }
    public bool HasCode => Code is not null;
    public CodeAttribute? Code { get; init; }
}

public sealed class CodeAttribute
{
    /// <summary>Byte offset of the first instruction relative to the method's Code data.</summary>
    public required int CodeDataOffset { get; init; }
    public required Quantity<int> MaxStack { get; init; }
    public required Quantity<int> MaxLocals { get; init; }
    public required byte[] Bytecode { get; init; }
    public required IReadOnlyList<ExceptionEntry> ExceptionTable { get; init; }
    public required IReadOnlyList<StackMapFrame> StackMapTable { get; init; }
    public required IReadOnlyList<AttributeInfo> CodeAttributes { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }
}

public sealed class ClassFileModel
{
    public required byte[] RawBytes { get; init; }
    /// <summary>major.minor exactly as imported (e.g. 52.0). Version semantics are applied downstream.</summary>
    public required Quantity<(ushort Major, ushort Minor)> Version { get; init; }
    public required ConstantPool ConstantPool { get; init; }
    public required Quantity<ushort> AccessFlags { get; init; }
    public required Quantity<string> ThisClass { get; init; }
    public required Quantity<string> SuperClass { get; init; }
    public required IReadOnlyList<Quantity<string>> Interfaces { get; init; }
    public required IReadOnlyList<FieldInfo> Fields { get; init; }
    public required IReadOnlyList<MethodInfo> Methods { get; init; }
    public required IReadOnlyList<AttributeInfo> Attributes { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }
}

public sealed class ClassFormatException : Exception
{
    public ClassFormatException(string message, Exception? inner = null) : base(message, inner) { }
}
