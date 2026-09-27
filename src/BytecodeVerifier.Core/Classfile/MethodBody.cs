using System.Text.Json.Serialization;

namespace BytecodeVerifier.Core.Classfile;

/// <summary>exception_table_entry：半开区间 [StartPc, EndPc)，HandlerPc 为入口，CatchType=0 表示 finally/any。</summary>
public sealed class ExceptionEntry
{
    [JsonPropertyName("startPc")] public int StartPc { get; init; }
    [JsonPropertyName("endPc")] public int EndPc { get; init; }
    [JsonPropertyName("handlerPc")] public int HandlerPc { get; init; }
    [JsonPropertyName("catchType")] public int CatchType { get; init; }
    [JsonPropertyName("catchClassName")] public string? CatchClassName { get; init; }
}

/// <summary>StackMapTable 中解码出的单个栈映帧（原始证据；偏移按 JVMS 为 delta，绝对偏移另算）。</summary>
public sealed class RawStackMapFrame
{
    [JsonPropertyName("order")] public int Order { get; init; }
    [JsonPropertyName("frameType")] public int FrameType { get; init; }
    [JsonPropertyName("offsetDelta")] public int OffsetDelta { get; init; }
    [JsonPropertyName("absoluteBci")] public int AbsoluteBci { get; init; }
    [JsonPropertyName("locals")] public FrameTypeInfo[] Locals { get; init; } = System.Array.Empty<FrameTypeInfo>();
    [JsonPropertyName("stack")] public FrameTypeInfo[] Stack { get; init; } = System.Array.Empty<FrameTypeInfo>();
    /// <summary>损坏帧诊断（如非法 tag、越界偏移），非空表示该帧不是可信证据。</summary>
    [JsonPropertyName("corruption")] public string? Corruption { get; init; }
}

/// <summary>verification_type_info 原始三元组，保留 tag 数值与常量池引用。</summary>
public sealed record FrameTypeInfo(
    [property: JsonPropertyName("tag")] int Tag,
    [property: JsonPropertyName("cpIndex")] int? CpIndex,
    [property: JsonPropertyName("offset")] int? Offset,
    [property: JsonPropertyName("mnemonic")] string Mnemonic);

public sealed class MethodBody
{
    public string Name { get; init; } = "";
    public string Descriptor { get; init; } = "";
    public int AccessFlags { get; init; }
    public int MaxStack { get; init; }
    public int MaxLocals { get; init; }
    public byte[] CodeBytes { get; init; } = System.Array.Empty<byte>();
    public Instruction[] Instructions { get; init; } = System.Array.Empty<Instruction>();
    public ExceptionEntry[] ExceptionTable { get; init; } = System.Array.Empty<ExceptionEntry>();
    public RawStackMapFrame[] StackMapFrames { get; init; } = System.Array.Empty<RawStackMapFrame>();
    public bool HasStackMapTable { get; init; }
}
