using System.Text;

namespace JvmVerify.Core.Verify;

/// <summary>
/// Verification types of JVMS §4.10.1. Category-2 values (long/double) are explicit types
/// rather than simulated slot pairs; Top marks an unusable slot; the Uninitialized*
/// types remember the new-instruction byte offset.
/// </summary>
public abstract record VType(string Name, int Category)
{
    public sealed record TopType() : VType("top", 1);
    public sealed record Integer() : VType("int", 1);
    public sealed record Float() : VType("float", 1);
    public sealed record Long() : VType("long", 2);
    public sealed record Double() : VType("double", 2);
    public sealed record Null() : VType("null", 1);
    public sealed record UninitializedThis() : VType("uninitializedThis", 1);
    public sealed record UninitializedOffset(int ByteOffset) : VType($"uninitialized@{ByteOffset}", 1)
    {
        public virtual bool Equals(UninitializedOffset? other) => other is not null && other.ByteOffset == ByteOffset;
        public override int GetHashCode() => ByteOffset;
    }
    /// <summary>Object or array reference. BinaryName holds the raw class name or array descriptor.</summary>
    public sealed record Reference(string BinaryName, bool IsArray = false) : VType(IsArray ? BinaryName.Replace('/', '.') : BinaryName.Replace('/', '.'), 1)
    {
        public virtual bool Equals(Reference? other) => other is not null && other.BinaryName == BinaryName;
        public override int GetHashCode() => BinaryName.GetHashCode();
    }
    /// <summary>Return address pushed by jsr (legacy verifier only).</summary>
    public sealed record ReturnAddress(int ReturnPc) : VType($"returnAddress@{ReturnPc}", 1)
    {
        public virtual bool Equals(ReturnAddress? other) => other is not null && other.ReturnPc == ReturnPc;
        public override int GetHashCode() => ReturnPc;
    }

    public static readonly TopType Top = new();
    public static readonly Integer Int = new();
    public static readonly Float Float = new();
    public static readonly Long Long = new();
    public static readonly Double Double = new();
    public static readonly Null Null = new();
    public static readonly UninitializedThis UninitThis = new();
}

/// <summary>
/// A verification_type_info item read from a StackMapTable. RawTag and RawHex preserve the
/// original bytes; unrecognized tag values remain Unconvertible with diagnostics.
/// </summary>
public abstract record VerificationTypeInfo(byte RawTag, string RawHex)
{
    public abstract VType ToVType(IReadOnlyList<string> diagnostics);

    public sealed record TopItem(byte Tag, string Hex) : VerificationTypeInfo(Tag, Hex)
    {
        public override VType ToVType(IReadOnlyList<string> d) => VType.Top;
    }
    public sealed record Item(byte Tag, string Hex, VType Type) : VerificationTypeInfo(Tag, Hex)
    {
        public override VType ToVType(IReadOnlyList<string> d) => Type;
    }
    public sealed record ObjectItem(byte Tag, string Hex, ushort CpIndex, string BinaryName) : VerificationTypeInfo(Tag, Hex)
    {
        public override VType ToVType(IReadOnlyList<string> d)
        {
            if (BinaryName.StartsWith('[')) return new VType.Reference(BinaryName, IsArray: true);
            return new VType.Reference(BinaryName);
        }
    }
    public sealed record UninitItem(byte Tag, string Hex, ushort ByteOffset) : VerificationTypeInfo(Tag, Hex)
    {
        public override VType ToVType(IReadOnlyList<string> d) => new VType.UninitializedOffset(ByteOffset);
    }
    public sealed record UnconvertibleItem(byte Tag, string Hex, IReadOnlyList<string> Diags) : VerificationTypeInfo(Tag, Hex)
    {
        public override VType ToVType(IReadOnlyList<string> d)
        {
            foreach (var x in Diags) d.Add(x);
            return VType.Top;
        }
    }
}
