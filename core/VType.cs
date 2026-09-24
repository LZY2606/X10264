namespace JvmVerify.Core;

/// <summary>Verification types (JVMS §4.10.1.2). long/double are category-2 and
/// occupy two local-variable / operand-stack slots.</summary>
public abstract record VType
{
    public sealed record Top : VType;
    public sealed record Int : VType;
    public sealed record Float : VType;
    public sealed record Long : VType;
    public sealed record Double : VType;
    public sealed record Null : VType;
    public sealed record Ref(string ClassName) : VType;
    public sealed record UninitializedThis : VType;
    public sealed record Uninitialized(ByteOffset NewOffset) : VType;

    public bool IsCategory2 => this is Long or Double;
    public int Slots => IsCategory2 ? 2 : 1;

    public override string ToString() => this switch
    {
        Top => "top",
        Int => "int",
        Float => "float",
        Long => "long",
        Double => "double",
        Null => "null",
        Ref r => r.ClassName,
        UninitializedThis => "uninitializedThis",
        Uninitialized u => $"uninitialized@{u.NewOffset}",
        _ => "?",
    };
}

/// <summary>A tiny class-hierarchy environment. Unknown classes fall back to
/// java/lang/Object as their supertype so verification stays total.</summary>
public interface IClassHierarchy
{
    string? SuperOf(string className);
    IReadOnlyList<string> InterfacesOf(string className);
    bool IsInterface(string className);
}

public sealed class MinimalClassHierarchy : IClassHierarchy
{
    private readonly Dictionary<string, (string? Super, string[] Ifaces, bool IsIface)> _map = new()
    {
        ["java/lang/Object"] = (null, Array.Empty<string>(), false),
        ["java/lang/String"] = ("java/lang/Object", new[] { "java/io/Serializable", "java/lang/Comparable", "java/lang/CharSequence" }, false),
        ["java/lang/Integer"] = ("java/lang/Number", new[] { "java/lang/Comparable" }, false),
        ["java/lang/Number"] = ("java/lang/Object", new[] { "java/io/Serializable" }, false),
        ["java/lang/Throwable"] = ("java/lang/Object", new[] { "java/io/Serializable" }, false),
        ["java/lang/Exception"] = ("java/lang/Throwable", Array.Empty<string>(), false),
        ["java/lang/RuntimeException"] = ("java/lang/Exception", Array.Empty<string>(), false),
        ["java/lang/Error"] = ("java/lang/Throwable", Array.Empty<string>(), false),
        ["java/io/Serializable"] = (null, Array.Empty<string>(), true),
        ["java/lang/Comparable"] = (null, Array.Empty<string>(), true),
        ["java/lang/CharSequence"] = (null, Array.Empty<string>(), true),
    };

    public string? SuperOf(string className) =>
        _map.TryGetValue(className, out var e) ? e.Super ?? "java/lang/Object" : "java/lang/Object";
    public IReadOnlyList<string> InterfacesOf(string className) =>
        _map.TryGetValue(className, out var e) ? e.Ifaces : Array.Empty<string>();
    public bool IsInterface(string className) =>
        _map.TryGetValue(className, out var e) && e.IsIface;
}
