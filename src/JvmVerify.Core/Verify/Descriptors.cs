namespace JvmVerify.Core.Verify;

public sealed record FieldType(string Descriptor, VType? Type, bool Unconvertible = false, string? Diagnostic = null)
{
    public override string ToString() => Descriptor;
}

/// <summary>JVMS §4.3 field and method descriptor parser. Unparseable descriptors are retained raw with diagnostics.</summary>
public static class Descriptors
{
    public static FieldType ParseField(string d)
    {
        try
        {
            int p = 0;
            var t = ParseOne(d, ref p);
            if (p != d.Length)
                return new FieldType(d, null, true, $"trailing chars at {p}: '{d[p..]}'");
            return new FieldType(d, t);
        }
        catch (Exception ex)
        {
            return new FieldType(d, null, true, ex.Message);
        }
    }

    private static VType ParseOne(string d, ref int p)
    {
        if (p >= d.Length) throw new FormatException("unexpected end of descriptor");
        char c = d[p++];
        switch (c)
        {
            case 'B': case 'C': case 'I': case 'S': case 'Z': return VType.Int;
            case 'F': return VType.Float;
            case 'J': return VType.Long;
            case 'D': return VType.Double;
            case 'L':
                int end = d.IndexOf(';', p);
                if (end < 0) throw new FormatException("unterminated object descriptor");
                string name = d[p..end];
                p = end + 1;
                return new VType.Reference(name);
            case '[':
                string arr = "[" + d[(p - 1)..];
                var elem = ParseOne(d, ref p);
                _ = elem;
                int consumed = p;
                return new VType.Reference(d[..consumed], IsArray: true);
            default:
                throw new FormatException($"unknown type char '{c}'");
        }
    }

    public sealed record MethodDescriptor(string Raw, IReadOnlyList<FieldType> Parameters, FieldType Return,
        bool Unconvertible = false, string? Diagnostic = null)
    {
        public int ParameterSlots => Parameters.Sum(p => p.Type?.Category ?? 1);
        public override string ToString() => Raw;
    }

    public static MethodDescriptor ParseMethod(string d)
    {
        try
        {
            if (d.Length < 2 || d[0] != '(')
                return new MethodDescriptor(d, Array.Empty<FieldType>(), new FieldType("V", null), true, "method descriptor must start with '('");
            int p = 1;
            var ps = new List<FieldType>();
            while (p < d.Length && d[p] != ')')
            {
                int start = p;
                var t = ParseOne(d, ref p);
                ps.Add(new FieldType(d[start..p], t));
            }
            if (p >= d.Length)
                return new MethodDescriptor(d, ps, new FieldType("V", null), true, "missing ')'");
            p++;
            int rs = p;
            var ret = d[p..] == "V"
                ? new FieldType("V", null)
                : new FieldType(d[rs..], ParseOne(d, ref p));
            if (p != d.Length)
                return new MethodDescriptor(d, ps, ret, true, $"trailing chars after return descriptor: '{d[p..]}'");
            return new MethodDescriptor(d, ps, ret);
        }
        catch (Exception ex)
        {
            return new MethodDescriptor(d, Array.Empty<FieldType>(), new FieldType("V", null), true, ex.Message);
        }
    }
}
