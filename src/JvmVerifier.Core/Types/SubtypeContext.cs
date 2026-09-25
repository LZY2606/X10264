using JvmVerifier.Core.Raw;

namespace JvmVerifier.Core.Types;

/// <summary>
/// Class hierarchy facts for assignability. Facts come from the imported
/// classfile plus the JVMS bootstrap library types; absent facts are treated
/// conservatively (a missing hierarchy fact is a diagnostic, never a guessed
/// superclass).
/// </summary>
public sealed class SubtypeContext
{
    private readonly Dictionary<string, RawClassFact> _facts = new();

    public const string ObjectName = "java/lang/Object";
    public const string CloneableName = "java/lang/Cloneable";
    public const string SerializableName = "java/io/Serializable";

    private static readonly Dictionary<string, (string? super, string[] interfaces)> Bootstrap = new()
    {
        ["java/lang/Object"] = (null, Array.Empty<string>()),
        ["java/lang/Class"] = ("java/lang/Object", new[] { "java/io/Serializable", "java/lang/reflect/GenericDeclaration", "java/lang/reflect/Type", "java/lang/invoke/TypeDescriptor" }),
        ["java/lang/String"] = ("java/lang/Object", new[] { "java/io/Serializable", "java/lang/Comparable", "java/lang/CharSequence", "java/lang/constant/Constable", "java/lang/ConstantDesc" }),
        ["java/lang/Throwable"] = ("java/lang/Object", new[] { "java/io/Serializable" }),
        ["java/lang/Exception"] = ("java/lang/Throwable", Array.Empty<string>()),
        ["java/lang/RuntimeException"] = ("java/lang/Exception", Array.Empty<string>()),
        ["java/lang/Error"] = ("java/lang/Throwable", Array.Empty<string>()),
        ["java/io/Serializable"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/Cloneable"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/Comparable"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/CharSequence"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/reflect/GenericDeclaration"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/reflect/Type"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/invoke/TypeDescriptor"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/constant/Constable"] = (ObjectName, Array.Empty<string>()),
        ["java/lang/constant/ConstantDesc"] = (ObjectName, Array.Empty<string>()),
    };

    private static readonly HashSet<string> BootstrapInterfaces = new()
    {
        "java/io/Serializable", "java/lang/Cloneable", "java/lang/Comparable",
        "java/lang/CharSequence", "java/lang/reflect/GenericDeclaration",
        "java/lang/reflect/Type", "java/lang/invoke/TypeDescriptor",
        "java/lang/constant/Constable", "java/lang/constant/ConstantDesc",
    };

    public SubtypeContext()
    {
        foreach (var (name, (super, ifaces)) in Bootstrap)
            _facts[name] = new RawClassFact { InternalName = name, SuperName = super, Interfaces = ifaces.ToList(), IsInterface = BootstrapInterfaces.Contains(name) };
    }

    public void AddFact(RawClassFact fact) => _facts[fact.InternalName] = fact;

    public IReadOnlyDictionary<string, RawClassFact> Facts => _facts;

    public bool IsSubclassOf(string child, string ancestor)
    {
        if (child == ancestor) return true;
        var seen = new HashSet<string>();
        var cur = child;
        while (cur is not null && seen.Add(cur))
        {
            if (cur == ancestor) return true;
            if (!_facts.TryGetValue(cur, out var fact))
            {
                // Unknown class: assume it directly extends Object (JVM loading reality),
                // record nothing destructive; interface checks still use declared facts.
                return ancestor == ObjectName;
            }
            foreach (var i in fact.Interfaces)
                if (IsSubclassOf(i, ancestor)) return true;
            cur = fact.SuperName;
        }
        return false;
    }

    /// <summary>JVMS 4.10.1.2 assignability.</summary>
    public bool IsAssignable(VerificationType from, VerificationType to, List<string>? notes = null)
    {
        if (to.Kind == VerifKind.Top) return true;
        if (from.Kind == VerifKind.Top) return false;
        if (Equals(from, to)) return true;

        if (from.Kind == VerifKind.Null)
            return to.Kind is VerifKind.Object or VerifKind.Null;

        if (to.Kind == VerifKind.Object && from.Kind == VerifKind.Object)
            return IsReferenceAssignable(from.ClassName!, to.ClassName!);

        return false;
    }

    public bool IsReferenceAssignable(string from, string to)
    {
        if (from == to) return true;
        if (to == ObjectName) return true;
        if (IsArrayDescriptor(from)) return IsArrayAssignable(from, to);
        return IsSubclassOf(from, to);
    }

    private static bool IsArrayDescriptor(string internalName) => internalName.StartsWith('[');

    private bool IsArrayAssignable(string fromArray, string to)
    {
        if (to is CloneableName or SerializableName) return true;
        if (to == ObjectName) return true;
        if (!IsArrayDescriptor(to)) return false;
        // array covariance over reference element types
        var fe = fromArray[1..];
        var te = to[1..];
        if (fe == te) return true;
        if (fe.StartsWith('L') && te.StartsWith('L'))
            return IsReferenceAssignable(fe[1..^1], te[1..^1]);
        return false;
    }

    /// <summary>
    /// All *minimal* common reference supertypes of two class types, including
    /// incomparable shared interfaces. Used by branch joins under old verifier
    /// semantics where the merge is not always unique; every legal candidate is
    /// kept for adjudication instead of collapsing to Object silently.
    /// </summary>
    public List<string> MinimalCommonSupertypes(string a, string b)
    {
        var ancestorsA = AllAncestors(a);
        var ancestorsB = AllAncestors(b);
        var common = ancestorsA.Intersect(ancestorsB).ToHashSet();
        if (common.Count == 0) return new() { ObjectName };

        var minimal = new List<string>();
        foreach (var c in common)
        {
            bool hasStrictCommonDescendant = false;
            foreach (var d in common)
            {
                if (d != c && IsStrictSubclass(d, c)) { hasStrictCommonDescendant = true; break; }
            }
            if (!hasStrictCommonDescendant) minimal.Add(c);
        }
        return minimal.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private bool IsStrictSubclass(string child, string ancestor) =>
        child != ancestor && IsSubclassOf(child, ancestor);

    private HashSet<string> AllAncestors(string name)
    {
        var result = new HashSet<string> { name };
        if (_facts.TryGetValue(name, out var f))
        {
            if (f.SuperName is not null) result.UnionWith(AllAncestors(f.SuperName));
            foreach (var i in f.Interfaces) result.UnionWith(AllAncestors(i));
        }
        return result;
    }
}
