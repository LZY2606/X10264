// Verification types as defined by JVMS 4.10.1, plus the implicit top type.
// Category-2 values (long/double) occupy two locals/stack slots and are
// represented by the type followed by Top markers; merging preserves width.
using System;
using System.Collections.Generic;
using System.Linq;

namespace JvmVerify.Core.Verify;

public abstract record VerificationType(string Display)
{
    public abstract int Slots { get; }
    public bool IsCategory2 => Slots == 2;
    public virtual bool IsReference => false;
    public virtual bool IsUninitialized => false;
    public virtual string StableId => GetType().Name + ":" + Display;
}

public sealed record TopType() : VerificationType("top")
{
    public static readonly TopType Instance = new();
    public override int Slots => 1;
    public override string StableId => "top";
}
public sealed record IntegerType() : VerificationType("int")
{
    public static readonly IntegerType Instance = new();
    public override int Slots => 1;
    public override string StableId => "int";
}
public sealed record FloatVType() : VerificationType("float")
{
    public static readonly FloatVType Instance = new();
    public override int Slots => 1;
    public override string StableId => "float";
}
public sealed record LongType() : VerificationType("long")
{
    public static readonly LongType Instance = new();
    public override int Slots => 2;
    public override string StableId => "long";
}
public sealed record DoubleVType() : VerificationType("double")
{
    public static readonly DoubleVType Instance = new();
    public override int Slots => 2;
    public override string StableId => "double";
}
public sealed record NullType() : VerificationType("null")
{
    public static readonly NullType Instance = new();
    public override int Slots => 1;
    public override bool IsReference => true;
    public override string StableId => "null";
}
// UninitializedThis is the special <uninitialized this> state before <init>.
public sealed record UninitializedThisType() : VerificationType("uninitializedThis")
{
    public static readonly UninitializedThisType Instance = new();
    public override int Slots => 1;
    public override bool IsReference => true;
    public override bool IsUninitialized => true;
    public override string StableId => "uninitializedThis";
}
// new-object state tagged with the raw `new` instruction offset.
public sealed record UninitializedType(int NewOffset) : VerificationType($"uninitialized@{NewOffset}")
{
    public override int Slots => 1;
    public override bool IsReference => true;
    public override bool IsUninitialized => true;
    public override string StableId => $"uninitialized:new@{NewOffset}";
}
public sealed record ObjectType(string Name) : VerificationType(Name)
{
    public override int Slots => 1;
    public override bool IsReference => true;
    public override string StableId => $"object:{Name}";
}

public static class VerificationTypes
{
    public static readonly ObjectType Object = new("java/lang/Object");
    public static readonly ObjectType Throwable = new("java/lang/Throwable");

    public static VerificationType ParseFrameType(int tag, System.Func<int, ObjectType> readObject,
        System.Func<int, UninitializedType> readUninit) => tag switch
    {
        0 => TopType.Instance,
        1 => IntegerType.Instance,
        2 => FloatVType.Instance,
        3 => DoubleVType.Instance,
        4 => LongType.Instance,
        5 => NullType.Instance,
        6 => UninitializedThisType.Instance,
        7 => readObject(tag),
        8 => readUninit(tag),
        _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, "unknown verification type info")
    };
}
