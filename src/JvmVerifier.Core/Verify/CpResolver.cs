using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

/// <summary>Resolves constant-pool references to class/name/descriptor triples.</summary>
public sealed class CpResolver
{
    private readonly Dictionary<int, CpEntry> _cp;
    public CpResolver(IEnumerable<CpEntry> entries) => _cp = entries.ToDictionary(e => e.Index);

    private static int[] Refs(string value) => value.Split('.', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => int.Parse(p.TrimStart('#'))).ToArray();

    public string ClassName(int cpIndex)
    {
        var e = _cp[cpIndex];
        int nameIdx = int.Parse(e.Value.TrimStart('#'));
        return _cp[nameIdx].Value;
    }

    public (string ClassName, string Name, string Descriptor) MethodRef(int cpIndex)
    {
        var e = _cp[cpIndex];
        var parts = Refs(e.Value);
        string cls = ClassName(parts[0]);
        var nt = Refs(_cp[parts[1]].Value);
        return (cls, _cp[nt[0]].Value, _cp[nt[1]].Value);
    }

    public (string ClassName, string Name, string Descriptor) FieldRef(int cpIndex) => MethodRef(cpIndex);

    public string? EntryValue(int cpIndex) => _cp.TryGetValue(cpIndex, out var e) ? e.Value : null;
    public string? EntryTag(int cpIndex) => _cp.TryGetValue(cpIndex, out var e) ? e.TagName : null;
}
