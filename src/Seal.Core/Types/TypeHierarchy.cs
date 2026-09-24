using System.Collections.Generic;

namespace Seal.Core.Types;

// Result of a join point. When several types are legally possible without a
// unique LUB (JVMS 4.10.1.4), Candidates holds every legal option; the engine
// must not silently collapse ambiguity into a single "answer".
public sealed record JoinResult(VerType Type, IReadOnlyList<VerType> Candidates, string Kind)
{
    public bool Ambiguous => Candidates.Count > 1;
    public bool Conflict => Kind == "conflict";
}

// Explicitly declared reference hierarchy for the imported class set.
// Fixtures ship the hierarchy they need (coordinate basis: class names).
public sealed class TypeHierarchy
{
    // class/interface name -> direct superclass (null for java/lang/Object)
    private readonly Dictionary<string, string?> _super = new();
    private readonly Dictionary<string, List<string>> _interfaces = new();
    private readonly HashSet<string> _interfaceNames = new();

    public void DeclareClass(string name, string? super, IEnumerable<string>? ifaces = null)
    {
        _super[name] = super;
        _interfaces[name] = ifaces == null ? new() : new(ifaces);
    }

    public void DeclareInterface(string name, IEnumerable<string>? supers = null)
    {
        _interfaceNames.Add(name);
        _super[name] = "java/lang/Object";
        _interfaces[name] = supers == null ? new() : new(supers);
    }

    public bool IsInterface(string name) => _interfaceNames.Contains(name);
    public bool Knows(string name) => _super.ContainsKey(name) || name == "java/lang/Object";

    public bool IsSubclassOf(string sub, string super)
    {
        if (sub == super) return true;
        var cur = sub;
        while (cur != null && _super.TryGetValue(cur, out var parent))
        {
            if (parent == super) return true;
            cur = parent;
        }
        return false;
    }

    public bool IsAssignable(VerType from, VerType to)
    {
        if (from == to) return true;
        if (to is VerTop) return true;
        if (from is VerNull && to.IsReference) return true;
        if (from is VerObject fo && to is VerObject to2) return IsSubclassOf(fo.ClassName, to2.ClassName);
        if (from is VerArray fa && to is VerArray ta)
        {
            if (fa.Element is VerInteger or VerFloat or VerLong or VerDouble)
                return fa.Element == ta.Element;
            return IsAssignable(fa.Element, ta.Element);
        }
        // Any reference array is assignable to java/lang/Object, Cloneable, Serializable.
        if (from.IsReference && to is VerObject { ClassName: "java/lang/Object" }) return true;
        if (from.IsReference && to is VerObject { ClassName: "java/lang/Cloneable" }) return true;
        if (from.IsReference && to is VerObject { ClassName: "java/io/Serializable" }) return true;
        return false;
    }

    // Least upper bound per JVMS 4.10.1.4, with explicit ambiguity/conflict.
    public JoinResult Join(VerType a, VerType b)
    {
        if (a == b) return new(a, new[] { a }, "same");
        if (a is VerTop || b is VerTop) return new(VerTop.Instance, new[] { VerTop.Instance }, "top");
        if (a.Slots != b.Slots)
            return new(VerTop.Instance, new[] { a, b }, "conflict");

        if (a is VerNull) return ReferenceJoinWithNull(b, b);
        if (b is VerNull) return ReferenceJoinWithNull(a, a);

        if (a is VerInteger && b is VerInteger) return Same(a);
        if (a is VerFloat && b is VerFloat) return Same(a);
        if (a is VerLong && b is VerLong) return Same(a);
        if (a is VerDouble && b is VerDouble) return Same(a);
        if (a is VerInteger or VerFloat or VerLong or VerDouble
            || b is VerInteger or VerFloat or VerLong or VerDouble)
        {
            return new(VerTop.Instance, new[] { a, b }, "conflict");
        }

        // Uninitialized types only join with themselves at the same new offset.
        if (a is VerUninitialized || b is VerUninitialized ||
            a is VerUninitializedThis || b is VerUninitializedThis)
        {
            return new(VerTop.Instance, new[] { a, b }, "conflict");
        }

        return ReferenceJoin(a, b);
    }

    private static JoinResult Same(VerType t) => new(t, new[] { t }, "same");

    private static JoinResult ReferenceJoinWithNull(VerType other, VerType result)
    {
        if (!other.IsReference) return new(VerTop.Instance, new[] { VerNull.Instance, other }, "conflict");
        return new(result, new[] { result }, "null");
    }

    private JoinResult ReferenceJoin(VerType a, VerType b)
    {
        if (a is VerObject ao && b is VerObject bo)
        {
            if (IsSubclassOf(ao.ClassName, bo.ClassName)) return new(b, new[] { b }, "lub");
            if (IsSubclassOf(bo.ClassName, ao.ClassName)) return new(a, new[] { a }, "lub");
            var common = CommonSuperclass(ao.ClassName, bo.ClassName);
            if (common == null)
                return new(VerTop.Instance, new[] { a, b }, "conflict");
            // Collect interface-based alternatives: if both branches implement a
            // common interface, that interface is also a legal join candidate.
            var candidates = new List<VerType> { new VerObject(common) };
            foreach (var iface in CommonInterfaces(ao.ClassName, bo.ClassName))
                candidates.Add(new VerObject(iface));
            return new(candidates[0], Dedup(candidates), candidates.Count > 1 ? "ambiguous" : "lub");
        }

        if (a is VerArray aa && b is VerArray ba)
        {
            if (VerPrimitiveName.Matches(aa.Element) && VerPrimitiveName.Matches(ba.Element))
            {
                if (aa.Element == ba.Element) return Same(a);
                return new(new VerObject("java/lang/Object"),
                    new VerType[] { new VerObject("java/lang/Object"), new VerObject("java/lang/Cloneable"), new VerObject("java/io/Serializable") },
                    "ambiguous");
            }
            var inner = Join(aa.Element, ba.Element);
            VerType arrayType = inner.Kind == "conflict" ? new VerObject("java/lang/Object") : new VerArray(inner.Type);
            var candidates = new List<VerType> { arrayType, new VerObject("java/lang/Cloneable"), new VerObject("java/io/Serializable") };
            return new(arrayType, candidates, "ambiguous");
        }

        // reference vs array, or array vs object
        if (a is VerArray && b is VerObject { ClassName: "java/lang/Object" })
            return new(b, new[] { b }, "lub");
        if (b is VerArray && a is VerObject { ClassName: "java/lang/Object" })
            return new(a, new[] { a }, "lub");
        return new(VerTop.Instance, new[] { a, b }, "conflict");
    }

    private string? CommonSuperclass(string a, string b)
    {
        var chain = new HashSet<string>();
        var cur = a;
        while (cur != null)
        {
            chain.Add(cur);
            _super.TryGetValue(cur, out cur);
        }
        cur = b;
        while (cur != null)
        {
            if (chain.Contains(cur)) return cur;
            _super.TryGetValue(cur, out cur);
        }
        return null;
    }

    private IEnumerable<string> CommonInterfaces(string a, string b)
    {
        var ia = AllInterfaces(a);
        var ib = AllInterfaces(b);
        foreach (var i in ia)
            if (ib.Contains(i) && i != "java/lang/Object")
                yield return i;
    }

    private HashSet<string> AllInterfaces(string name)
    {
        var result = new HashSet<string>();
        var cur = name;
        while (cur != null)
        {
            if (_interfaces.TryGetValue(cur, out var list))
                foreach (var i in list)
                    CollectInterface(i, result);
            _super.TryGetValue(cur, out cur);
        }
        return result;
    }

    private void CollectInterface(string name, HashSet<string> sink)
    {
        if (!sink.Add(name)) return;
        if (_interfaces.TryGetValue(name, out var supers))
            foreach (var s in supers) CollectInterface(s, sink);
    }

    private static List<VerType> Dedup(List<VerType> ts)
    {
        var seen = new HashSet<string>();
        var result = new List<VerType>();
        foreach (var t in ts)
            if (seen.Add(t.Mnemonic)) result.Add(t);
        return result;
    }
}

// Helper pattern: primitive component names of arrays.
internal static class VerPrimitiveName
{
    public static bool Matches(VerType t) =>
        t is VerInteger or VerFloat or VerLong or VerDouble;
}
