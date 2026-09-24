namespace JvmBc.Core;

/// <summary>Byte offset inside a method's Code attribute. Coordinate basis: pc = 0 at first byte of code[].</summary>
public readonly record struct BytecodeOffset(int Pc)
{
    public static implicit operator int(BytecodeOffset o) => o.Pc;
    public override string ToString() => $"pc:{Pc}";
}

/// <summary>1-based index into the constant pool (index 0 designates "no entry").</summary>
public readonly record struct ConstantPoolIndex(ushort Value)
{
    public static readonly ConstantPoolIndex None = new(0);
    public override string ToString() => $"cp:#{Value}";
}

/// <summary>0-based local variable array slot. category-2 values occupy slot and slot+1.</summary>
public readonly record struct LocalSlot(int Index)
{
    public override string ToString() => $"local:{Index}";
}

/// <summary>0-based position in the abstract operand stack.</summary>
public readonly record struct StackPos(int Index)
{
    public override string ToString() => $"stack:{Index}";
}

/// <summary>Major.minor classfile version; selects old (major&lt;50) vs stackmap (major&gt;=50) verification semantics.</summary>
public sealed record ClassVersion(ushort Minor, ushort Major)
{
    public bool UsesStackMapVerification => Major >= 50;
    public string Coordinate => $"classfile-version:{Major}.{Minor}";
    public override string ToString() => $"v{Major}.{Minor}";
}
