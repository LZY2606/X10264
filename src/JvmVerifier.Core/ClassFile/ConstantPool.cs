using System.Text;

namespace JvmVerifier.Core.ClassFile;

/// <summary>
/// A single constant pool entry. <see cref="Tag"/> is the raw u1 tag byte.
/// Every entry keeps the exact raw bytes it was decoded from so the original
/// representation can always be reconstructed (no normalization on import).
/// </summary>
public sealed class CpInfo
{
    public required int Tag { get; init; }
    public required byte[] Raw { get; init; }
    public string? Utf8Text { get; init; }

    /// <summary>Number of slots this entry consumes (long/double consume 2).</summary>
    public int Slots => Tag is 5 or 6 ? 2 : 1;

    public string Describe()
    {
        if (Utf8Text is not null) return $"Utf8 \"{Utf8Text}\"";
        return Tag switch
        {
            7 => "Class",
            9 => "Fieldref",
            10 => "Methodref",
            11 => "InterfaceMethodref",
            8 => "String",
            3 => "Integer",
            4 => "Float",
            5 => "Long",
            6 => "Double",
            12 => "NameAndType",
            15 => "MethodHandle",
            16 => "MethodType",
            18 => "InvokeDynamic",
            _ => $"tag{Tag}"
        };
    }
}

public sealed class ConstantPool
{
    private readonly List<CpInfo?> _entries = new();

    public ConstantPool() => _entries.Add(null); // index 0 placeholder

    public int Add(CpInfo info)
    {
        int index = _entries.Count;
        _entries.Add(info);
        if (info.Slots == 2) _entries.Add(null);
        return index;
    }

    internal void LoadSlot(CpInfo? info) => _entries.Add(info);

    public CpInfo? At(int index) =>
        index >= 0 && index < _entries.Count ? _entries[index] : null;

    public int Count => _entries.Count;

    public string? Utf8(int index) => At(index)?.Utf8Text;

    public string? ClassName(int classInfoIndex)
    {
        var c = At(classInfoIndex);
        if (c is null || c.Tag != 7) return null;
        int ni = (c.Raw[0] << 8) | c.Raw[1];
        return Utf8(ni);
    }

    public IEnumerable<(int Index, CpInfo Info)> Entries
    {
        get
        {
            for (int i = 1; i < _entries.Count; i++)
                if (_entries[i] is { } e) yield return (i, e);
        }
    }
}
