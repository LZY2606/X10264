namespace JvmVerifier.Core.ClassFile;

public enum AttributeParseMode
{
    /// <summary>Unknown attributes keep their raw bytes; nothing is dropped.</summary>
    KeepRaw
}

public sealed class AttributeInfo
{
    public ushort NameIndex { get; set; }
    public string Name { get; set; } = "";
    public int DataOffset { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public sealed class FieldInfo
{
    public ushort AccessFlags { get; set; }
    public string Name { get; set; } = "";
    public string Descriptor { get; set; } = "";
    public List<AttributeInfo> Attributes { get; } = new();
}

public sealed class ExceptionHandler
{
    /// <summary>Original byte offsets inside the Code array (JVMS §4.7.3).</summary>
    public ushort StartPc { get; set; }
    public ushort EndPc { get; set; }
    public ushort HandlerPc { get; set; }
    /// <summary>0 = catch-all finally handler (JVMS).</summary>
    public ushort CatchTypeIndex { get; set; }
    public string CatchType { get; set; } = "<finally>";
}

public enum StackFrameTag
{
    Same, SameLocals1StackItem, SameLocals1StackItemExtended, Chop,
    SameFrameExtended, Append, Full
}

/// <summary>A verification_type_info from the StackMapTable (JVMS §4.7.4).</summary>
public sealed class VerificationTypeInfo
{
    public byte Tag { get; set; }
    /// <summary>Constant pool index for ObjectVariable(7); offset for UninitializedVariable(8).</summary>
    public ushort Payload { get; set; }
    public string Name { get; set; } = "";

    public const byte Top = 0, Integer = 1, Float = 2, Double = 3, Long = 4,
        Null = 5, UninitializedThis = 6, Object = 7, Uninitialized = 8;

    public override string ToString() => Name;
}

public sealed class StackMapFrame
{
    public byte FrameType { get; set; }
    public StackFrameTag Tag { get; set; }
    /// <summary>Byte offset of this frame's first byte in the classfile (coordinate basis).</summary>
    public int FrameOffset { get; set; }
    /// <summary>Resolved absolute bytecode offset (before any corruption).</summary>
    public int OffsetDelta { get; set; }
    public int BytecodeOffset { get; set; }
    public List<VerificationTypeInfo> Locals { get; } = new();
    public List<VerificationTypeInfo> Stack { get; } = new();
    /// <summary>Locals carried over from the previous frame by delta rules (raw evidence).</summary>
    public int InheritedLocalCount { get; set; }
}

public sealed class CodeAttribute
{
    public ushort MaxStack { get; set; }
    public ushort MaxLocals { get; set; }
    public int CodeOffset { get; set; }
    public byte[] Code { get; set; } = Array.Empty<byte>();
    public List<ExceptionHandler> ExceptionTable { get; } = new();
    public List<StackMapFrame> StackMap { get; } = new();
    public List<AttributeInfo> Attributes { get; } = new();
}

public sealed class MethodInfo
{
    public ushort AccessFlags { get; set; }
    public string Name { get; set; } = "";
    public string Descriptor { get; set; } = "";
    public CodeAttribute? Code { get; set; }
    public List<AttributeInfo> Attributes { get; } = new();
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
    public bool IsAbstract => (AccessFlags & 0x0400) != 0;
}

public sealed class ParsedClass
{
    public uint Magic { get; set; }
    public ushort MinorVersion { get; set; }
    public ushort MajorVersion { get; set; }
    public ConstantPool ConstantPool { get; set; } = null!;
    public ushort AccessFlags { get; set; }
    public string ThisClass { get; set; } = "";
    public string SuperClass { get; set; } = "";
    public List<string> Interfaces { get; } = new();
    public List<FieldInfo> Fields { get; } = new();
    public List<MethodInfo> Methods { get; } = new();
    public List<AttributeInfo> Attributes { get; } = new();
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();

    public string VersionText => MajorVersion switch
    {
        >= 61 => $"Java {MajorVersion - 44}",
        _ => $"major {MajorVersion}"
    };
}
