using System.Text;

namespace JvmVerifier.Core.Verify;

public enum JvmKind { Int, Long, Float, Double, Ref, ReturnAddress, Top }

public static class DescriptorUtil
{
    public static IReadOnlyList<string> ParseParameters(string descriptor)
    {
        var ps = new List<string>();
        int i = descriptor.IndexOf('(');
        if (i < 0) throw new FormatException($"Not a method descriptor: {descriptor}");
        i++;
        while (descriptor[i] != ')') ps.Add(ReadFieldType(descriptor, ref i));
        return ps;
    }

    public static string ReadFieldType(string d, ref int i)
    {
        int start = i;
        char c = d[i++];
        switch (c)
        {
            case 'B': case 'C': case 'D': case 'F': case 'I':
            case 'J': case 'S': case 'Z': case 'V':
                return c.ToString();
            case 'L':
                while (d[i] != ';') i++;
                i++;
                return d.Substring(start, i - start);
            case '[':
                ReadFieldType(d, ref i);
                return d.Substring(start, i - start);
            default:
                throw new FormatException($"Bad field descriptor char '{c}' in {d}");
        }
    }

    public static string ReturnType(string descriptor)
    {
        int i = descriptor.IndexOf(')') + 1;
        return ReadFieldType(descriptor, ref i);
    }

    public static JvmKind KindOf(string fieldType) => fieldType[0] switch
    {
        'B' or 'C' or 'I' or 'S' or 'Z' => JvmKind.Int,
        'J' => JvmKind.Long,
        'F' => JvmKind.Float,
        'D' => JvmKind.Double,
        'V' => JvmKind.Top,
        _ => JvmKind.Ref
    };

    public static int Slots(string fieldType) => KindOf(fieldType) is JvmKind.Long or JvmKind.Double ? 2 : 1;
}
