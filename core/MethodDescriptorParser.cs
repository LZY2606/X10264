namespace JvmVerify.Core;

/// <summary>Derived-layer normalization of raw descriptor strings. Unconvertible
/// descriptors are left raw by the caller and annotated with a diagnostic.</summary>
public static class MethodDescriptorParser
{
    public static bool TryParse(string raw, out MethodDescriptor descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrEmpty(raw) || raw[0] != '(') return false;
        var i = 1;
        var parameters = new List<string>();
        while (i < raw.Length && raw[i] != ')')
        {
            var t = ParseType(raw, ref i);
            if (t is null) return false;
            parameters.Add(t);
        }
        if (i >= raw.Length || raw[i] != ')') return false;
        i++;
        var ret = i < raw.Length && raw[i] == 'V' ? (i++, "void") : ParseType(raw, ref i);
        if (ret is null || i != raw.Length) return false;
        descriptor = new MethodDescriptor(parameters, ret);
        return true;
    }

    private static string? ParseType(string raw, ref int i)
    {
        if (i >= raw.Length) return null;
        var c = raw[i];
        switch (c)
        {
            case 'B': i++; return "byte";
            case 'C': i++; return "char";
            case 'D': i++; return "double";
            case 'F': i++; return "float";
            case 'I': i++; return "int";
            case 'J': i++; return "long";
            case 'S': i++; return "short";
            case 'Z': i++; return "boolean";
            case 'L':
                var semi = raw.IndexOf(';', i);
                if (semi < 0) return null;
                var name = raw[(i + 1)..semi];
                i = semi + 1;
                return name;
            case '[':
                var dims = 0;
                while (i < raw.Length && raw[i] == '[') { dims++; i++; }
                var elem = ParseType(raw, ref i);
                if (elem is null) return null;
                return new string('[', dims) + elem;
            default:
                return null;
        }
    }
}
