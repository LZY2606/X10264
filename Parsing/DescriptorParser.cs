using JvmVerifier.Domain;

namespace JvmVerifier.Parsing;

public static class DescriptorParser
{
    public static (IReadOnlyList<VerifierType> Arguments, string ReturnDescriptor) Method(string descriptor)
    {
        if (descriptor.Length < 3 || descriptor[0] != '(') throw new FormatException($"invalid method descriptor {descriptor}");
        var close = descriptor.IndexOf(')');
        if (close < 0) throw new FormatException($"invalid method descriptor {descriptor}");
        var args = new List<VerifierType>();
        var index = 1;
        while (index < close)
        {
            var start = index;
            while (index < close && descriptor[index] == '[') index++;
            if (index >= close) throw new FormatException($"invalid method descriptor {descriptor}");
            if (descriptor[index] == 'L')
            {
                var semicolon = descriptor.IndexOf(';', index);
                if (semicolon < 0 || semicolon > close) throw new FormatException($"invalid method descriptor {descriptor}");
                var name = descriptor.Substring(start, semicolon + 1 - start);
                args.Add(new ObjectType(name));
                index = semicolon + 1;
            }
            else
            {
                args.Add(Field(descriptor.Substring(start, index - start + 1)));
                index++;
            }
        }
        return (args, descriptor[(close + 1)..]);
    }

    public static VerifierType Field(string descriptor) => descriptor[0] switch
    {
        'I' or 'Z' or 'B' or 'C' or 'S' => new IntegerType(),
        'J' => new LongType(),
        'F' => new FloatType(),
        'D' => new DoubleType(),
        'L' or '[' => new ObjectType(descriptor),
        'V' => new TopType(),
        _ => throw new FormatException($"unsupported field descriptor {descriptor}")
    };
}
