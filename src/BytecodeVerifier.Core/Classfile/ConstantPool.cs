using System.Text.Json.Serialization;

namespace BytecodeVerifier.Core.Classfile;

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
/// 常量池项。导入层保留原始 tag、原始索引与原始负载；RawText 保存该项在文件中的
/// 可核对表示（Utf8 原文/数字原文等）。Long/Double 占两个索引（following 为哨兵项）。
/// </summary>
public sealed class CpEntry
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("tag")] public CpTag Tag { get; init; }
    [JsonPropertyName("rawTag")] public int RawTag { get; init; }
    [JsonPropertyName("rawText")] public string RawText { get; init; } = "";
    [JsonPropertyName("ref1")] public int? Ref1 { get; init; }
    [JsonPropertyName("ref2")] public int? Ref2 { get; init; }
    [JsonPropertyName("secondSlot")] public bool SecondSlot { get; init; }
    [JsonPropertyName("bytesHex")] public string? BytesHex { get; init; }

    public override string ToString() => $"#{Index} {Tag} {RawText}";
}
