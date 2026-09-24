namespace JvmVerify.Core;

/// <summary>
/// Coordinate basis for every domain quantity. Raw classfile positions are measured in
/// bytes from the start of the method's Code attribute (byte), cp indexes count slots in
/// the constant pool (index), class versions are major.minor (version) and verification
/// local/stack positions are slot numbers (slot).
/// </summary>
public enum Basis
{
    RawByte,
    CpIndex,
    Slot,
    VersionPair,
    DescriptorText,
    HandlerIndex,
    MapFrameIndex,
    NodeOffset,
    EdgeOrdinal,
    MutationOrdinal,
    RuleVersion,
    JsonText
}

/// <summary>
/// A value together with the original textual/binary representation imported from the
/// classfile and the coordinate basis it is measured on. Normalization happens only in
/// derived layers; the raw representation is never discarded.
/// </summary>
public sealed record Quantity<T>(T Value, string Raw, Basis Basis)
{
    public override string ToString() => $"{Value} [{Basis}]";
}

public static class Quantity
{
    public static Quantity<T> Byte(T value) => new(value, value?.ToString() ?? "", Basis.RawByte);
    public static Quantity<T> Cp(T value, string raw) => new(value, raw, Basis.CpIndex);
    public static Quantity<T> Slot(T value) => new(value, value?.ToString() ?? "", Basis.Slot);
}

/// <summary>
/// A value that could not be normalized (unknown constant-pool tag, corrupted frame ...).
/// It stays at its original position; derived analysis attaches diagnostics instead of
/// dropping it for the sake of producing a clean picture.
/// </summary>
public sealed record Unconvertible(string RawHex, string Position, IReadOnlyList<string> Diagnostics)
{
    public override string ToString() => $"<unconvertible@{Position}: {RawHex}>";
}
