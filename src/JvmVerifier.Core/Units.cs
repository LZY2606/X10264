using System.Text.Json;
using System.Text.Json.Serialization;

namespace JvmVerifier.Core;

/// <summary>字节码偏移（bytecode index）。坐标基准：某方法 Code[] 内的字节偏移。</summary>
[JsonConverter(typeof(BciJson))]
public readonly record struct Bci(int Value) : IComparable<Bci>
{
    public int CompareTo(Bci other) => Value.CompareTo(other.Value);
    public override string ToString() => $"bci:{Value}";
    public static Bci operator +(Bci a, int d) => new(a.Value + d);
}

/// <summary>常量池下标。坐标基准：本类 constant_pool[1..count-1]。</summary>
[JsonConverter(typeof(CpIndexJson))]
public readonly record struct CpIndex(int Value)
{
    public override string ToString() => $"cp#{Value}";
}

public sealed class BciJson : JsonConverter<Bci>
{
    public override Bci Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) => new(reader.GetInt32());
    public override void Write(Utf8JsonWriter w, Bci v, JsonSerializerOptions o) => w.WriteNumberValue(v.Value);
}

public sealed class CpIndexJson : JsonConverter<CpIndex>
{
    public override CpIndex Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) => new(reader.GetInt32());
    public override void Write(Utf8JsonWriter w, CpIndex v, JsonSerializerOptions o) => w.WriteNumberValue(v.Value);
}

/// <summary>领域量的坐标基准说明，供导出/复核界面标注单位来源。</summary>
public static class CoordinateBasis
{
    public const string BytecodeOffset = "bytecode-offset@method.Code[]";
    public const string ConstantPool = "constant-pool-index@class";
    public const string StackMapFrame = "frame-order@method.StackMapTable";
    public const string ExceptionRange = "half-open[startPc,endPc)@method.exception_table";
    public const string ClassVersion = "major.minor@classfile";
    public const string LocalSlot = "local-slot@method.frame";
    public const string StackSlot = "operand-stack-depth@method.frame";
}
