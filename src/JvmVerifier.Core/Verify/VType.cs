namespace JvmVerifier.Core.Verify;

/// <summary>
/// Abstract verification type (JVMS 4.10.1). Values are interned-ish immutable
/// records with a stable token used as the machine-readable identity.
/// Long/Double are category 2 and reserve the following physical slot as <see cref="Top"/>.
/// </summary>
public abstract record VType
{
    public abstract string Token { get; }
    public abstract int Slots { get; }
    public bool IsCategory2 => Slots == 2;
    public bool IsReference => this is RefType or NullType or UninitType or UninitThisType;
    public static readonly TopType Top = TopType.Instance;
    public static readonly IntType Int = IntType.Instance;
    public static readonly FloatType Float = FloatType.Instance;
    public static readonly LongType Long = LongType.Instance;
    public static readonly DoubleType Double = DoubleType.Instance;
    public static readonly NullType Null = NullType.Instance;
    public static readonly UninitThisType UninitThis = UninitThisType.Instance;
    public static VType Ref(string name) => new RefType(name);
    public static VType Uninit(int newBci) => new UninitType(newBci);
    public static VType Addr(int bci) => new ReturnAddressType(bci);
    public override string ToString() => Token;
}

public sealed record TopType : VType
{
    public static readonly TopType Instance = new();
    public override string Token => "top";
    public override int Slots => 1;
}
public sealed record IntType : VType
{
    public static readonly IntType Instance = new();
    public override string Token => "int";
    public override int Slots => 1;
}
public sealed record FloatType : VType
{
    public static readonly FloatType Instance = new();
    public override string Token => "float";
    public override int Slots => 1;
}
public sealed record LongType : VType
{
    public static readonly LongType Instance = new();
    public override string Token => "long";
    public override int Slots => 2;
}
public sealed record DoubleType : VType
{
    public static readonly DoubleType Instance = new();
    public override string Token => "double";
    public override int Slots => 2;
}
public sealed record NullType : VType
{
    public static readonly NullType Instance = new();
    public override string Token => "null";
    public override int Slots => 1;
}
public sealed record UninitThisType : VType
{
    public static readonly UninitThisType Instance = new();
    public override string Token => "uninitializedThis";
    public override int Slots => 1;
}
public sealed record RefType(string Name) : VType
{
    public override string Token => "ref:" + Name;
    public override int Slots => 1;
}
/// <summary>An object created by NEW at <see cref="NewBci"/> before its &lt;init&gt; ran.</summary>
public sealed record UninitType(int NewBci) : VType
{
    public override string Token => $"uninit@{NewBci}";
    public override int Slots => 1;
}
public sealed record ReturnAddressType(int Bci) : VType
{
    public override string Token => $"returnAddress@{Bci}";
    public override int Slots => 1;
}
