namespace JvmLab.Verify;

/// <summary>Verification types for abstract interpretation.</summary>
public abstract record VType
{
    public sealed record TopT : VType;                       // unusable slot / 2nd half of category-2
    public sealed record IntT : VType;
    public sealed record FloatT : VType;
    public sealed record LongT : VType;
    public sealed record DoubleT : VType;
    public sealed record NullT : VType;
    /// <summary>Reference. Candidates keeps all legal supertypes when the
    /// common supertype is not uniquely decidable — we never collapse to a
    /// single answer just to have one.</summary>
    public sealed record RefT(string ClassName, IReadOnlyList<string>? Candidates = null) : VType;
    public sealed record UninitThisT : VType;
    public sealed record UninitT(int Offset) : VType;

    public static readonly VType Top = new TopT();
    public static readonly VType Int = new IntT();
    public static readonly VType Float = new FloatT();
    public static readonly VType Long = new LongT();
    public static readonly VType Double = new DoubleT();
    public static readonly VType Null = new NullT();
    public static readonly VType UninitThis = new UninitThisT();

    public bool IsCategory2 => this is LongT or DoubleT;
    public bool IsUninitialized => this is UninitThisT or UninitT;

    public string Display => this switch
    {
        TopT => "top",
        IntT => "int",
        FloatT => "float",
        LongT => "long",
        DoubleT => "double",
        NullT => "null",
        RefT r when r.Candidates is { Count: > 0 } => $"ref({r.ClassName}?{string.Join("|", r.Candidates)})",
        RefT r => $"ref({r.ClassName})",
        UninitThisT => "uninitThis",
        UninitT u => $"uninit@{u.Offset}",
        _ => "?"
    };
}

public static class DescriptorParser
{
    /// <summary>Parses a field/method descriptor into verification types.</summary>
    public static List<VType> ParseArgs(string descriptor)
    {
        var result = new List<VType>();
        var i = descriptor.IndexOf('(') + 1;
        while (descriptor[i] != ')')
        {
            var (t, next) = ParseOne(descriptor, i);
            result.Add(t);
            i = next;
        }
        return result;
    }

    public static VType ParseReturn(string descriptor)
    {
        var close = descriptor.IndexOf(')');
        if (descriptor[close + 1] == 'V') return VType.Top;
        return ParseOne(descriptor, close + 1).Item1;
    }

    public static VType ParseFieldType(string descriptor) => ParseOne(descriptor, 0).Item1;

    private static (VType, int) ParseOne(string d, int i)
    {
        switch (d[i])
        {
            case 'B' or 'C' or 'I' or 'S' or 'Z': return (VType.Int, i + 1);
            case 'F': return (VType.Float, i + 1);
            case 'J': return (VType.Long, i + 1);
            case 'D': return (VType.Double, i + 1);
            case 'L':
                var semi = d.IndexOf(';', i);
                return (new VType.RefT(d.Substring(i + 1, semi - i - 1)), semi + 1);
            case '[':
                var j = i;
                while (d[j] == '[') j++;
                var (_, next) = ParseOne(d, j);
                return (new VType.RefT(d.Substring(i, next - i)), next);
            default:
                throw new ArgumentException($"bad descriptor '{d}' at {i}");
        }
    }
}
