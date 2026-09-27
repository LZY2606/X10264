using System.Text.Json.Serialization;

namespace BytecodeVerifier.Core.Classfile;

public sealed class ClassFile
{
    [JsonPropertyName("minorVersion")] public int MinorVersion { get; init; }
    [JsonPropertyName("majorVersion")] public int MajorVersion { get; init; }
    [JsonPropertyName("thisClass")] public string ThisClass { get; init; } = "";
    [JsonPropertyName("superClass")] public string SuperClass { get; init; } = "";
    [JsonPropertyName("accessFlags")] public int AccessFlags { get; init; }
    [JsonPropertyName("constantPoolCount")] public int ConstantPoolCount { get; init; }
    [JsonPropertyName("constantPool")] public CpEntry[] ConstantPool { get; init; } = System.Array.Empty<CpEntry>();
    [JsonPropertyName("interfaces")] public string[] Interfaces { get; init; } = System.Array.Empty<string>();
    [JsonPropertyName("methods")] public MethodBody[] Methods { get; init; } = System.Array.Empty<MethodBody>();
    /// <summary>导入时诊断：无法转换的值留在原位置，不丢弃。</summary>
    [JsonPropertyName("importDiagnostics")] public List<string> ImportDiagnostics { get; init; } = new();
    [JsonPropertyName("rawLength")] public int RawLength { get; init; }

    [JsonIgnore] public string VersionText => MajorVersion == 0 ? "unknown" : $"{MajorVersion}.{MinorVersion}";
    /// <summary>major >= 50（Java 6）必须使用 StackMapTable 类型检查；&lt; 50 为旧版推断。</summary>
    [JsonIgnore] public bool RequiresStackMapTable => MajorVersion >= 50;
}
