using System;
using System.Collections.Generic;
using System.Linq;

namespace JvmVerifier.Core.Domain;

/// <summary>
/// JVMS §4.10.1 verification types, plus the category-2 "top" half slot.
/// Identity is value-based and stable: a type renders deterministically via <see cref="Render"/>.
/// </summary>
public abstract record VType
{
    /// <summary>Number of local/stack operand words this type occupies (1 or 2).</summary>
    public abstract int Slots { get; }

    /// <summary>Stable machine-readable rendering, used as the display identity.</summary>
    public abstract string Render();

    public sealed override string ToString() => Render();
}

public sealed record TopV : VType
{
    public static readonly TopV Instance = new();
    public override int Slots => 1;
    public override string Render() => "top";
}

public sealed record IntegerV : VType
{
    public static readonly IntegerV Instance = new();
    public override int Slots => 1;
    public override string Render() => "int";
}

public sealed record FloatV : VType
{
    public static readonly FloatV Instance = new();
    public override int Slots => 1;
    public override string Render() => "float";
}

public sealed record LongV : VType
{
    public static readonly LongV Instance = new();
    public override int Slots => 2;
    public override string Render() => "long";
}

public sealed record DoubleV : VType
{
    public static readonly DoubleV Instance = new();
    public override int Slots => 2;
    public override string Render() => "double";
}

public sealed record NullV : VType
{
    public static readonly NullV Instance = new();
    public override int Slots => 1;
    public override string Render() => "null";
}

/// <summary>UninitializedThisV — the receiver of a constructor before super/this &lt;init&gt; completes.</summary>
public sealed record UninitializedThisV : VType
{
    public static readonly UninitializedThisV Instance = new();
    public override int Slots => 1;
    public override string Render() => "uninitializedThis";
}

/// <summary>
/// UninitializedV for an object created by new at bytecode offset <see cref="NewOffset"/>.
/// The offset is part of the identity: news at different offsets never merge to one type.
/// </summary>
public sealed record UninitializedV(string ClassName, int NewOffset) : VType
{
    public override int Slots => 1;
    public override string Render() => $"uninitialized({ClassName}@{NewOffset})";
}

public sealed record ClassV(string Name) : VType
{
    public static readonly ClassV Object = new("java/lang/Object");
    public override int Slots => 1;
    public override string Render() => Name;
}

public sealed record ArrayV(string Component, int Dimensions) : VType
{
    public ArrayV(string component, int dimensions = 1) : this(component, dimensions) { }
    public override int Slots => 1;
    public string Descriptor =>
        new string('[', Dimensions) + (Component[0] == '[' ? Component : "L" + Component + ";");
    public override string Render() => Descriptor;
}

/// <summary>
/// A verification type that could not be resolved to a finite unique answer because the
/// class hierarchy for <see cref="Reference"/> is unknown to the tool. Holds the
/// conservative candidate (<see cref="Fallback"/>) and every legitimately-possible
/// verification outcome so multi-solutions are never collapsed silently.
/// </summary>
public sealed record UnresolvedReferenceV(string Reference, string Fallback) : VType
{
    public override int Slots => 1;
    public override string Render() => $"unresolved({Reference})";
}
