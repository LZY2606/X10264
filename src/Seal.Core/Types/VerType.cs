namespace Seal.Core.Types;

// Verification type lattice (JVMS 4.10.1 verification types).
// Each type carries its own category (1 or 2 JVM slots).
public abstract record VerType
{
    public abstract int Slots { get; }
    public abstract string Mnemonic { get; }
    public sealed override string ToString() => Mnemonic;

    public bool IsCategory2 => Slots == 2;
    public virtual bool IsReference => false;
    public virtual bool IsUninitialized => false;
}

public sealed record VerTop : VerType
{
    public static readonly VerTop Instance = new();
    public override int Slots => 1;
    public override string Mnemonic => "top";
}

public sealed record VerInteger : VerType
{
    public static readonly VerInteger Instance = new();
    public override int Slots => 1;
    public override string Mnemonic => "int";
}

public sealed record VerFloat : VerType
{
    public static readonly VerFloat Instance = new();
    public override int Slots => 1;
    public override string Mnemonic => "float";
}

public sealed record VerLong : VerType
{
    public static readonly VerLong Instance = new();
    public override int Slots => 2;
    public override string Mnemonic => "long";
}

public sealed record VerDouble : VerType
{
    public static readonly VerDouble Instance = new();
    public override int Slots => 2;
    public override string Mnemonic => "double";
}

// byte/char/short/boolean are all represented as int at verification time.
public sealed record VerNull : VerType
{
    public static readonly VerNull Instance = new();
    public override int Slots => 1;
    public override string Mnemonic => "null";
    public override bool IsReference => true;
}

public sealed record VerObject(string ClassName) : VerType
{
    public override int Slots => 1;
    public override string Mnemonic => ClassName;
    public override bool IsReference => true;
}

public sealed record VerArray(VerType Element) : VerType
{
    public override int Slots => 1;
    public override string Mnemonic => Element.Mnemonic + "[]";
    public override bool IsReference => true;
}

// Uninitialized object allocated by `new` at the given bytecode offset.
public sealed record VerUninitialized(int NewOffset) : VerType
{
    public override int Slots => 1;
    public override string Mnemonic => $"uninitialized@{NewOffset}";
    public override bool IsReference => true;
    public override bool IsUninitialized => true;
}

// Uninitialized `this` in a constructor before super/<init> completes.
public sealed record VerUninitializedThis : VerType
{
    public static readonly VerUninitializedThis Instance = new();
    public override int Slots => 1;
    public override string Mnemonic => "uninitializedThis";
    public override bool IsReference => true;
    public override bool IsUninitialized => true;
}
