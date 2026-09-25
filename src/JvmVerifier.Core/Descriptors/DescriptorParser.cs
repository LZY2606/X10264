using JvmVerifier.Core.Raw;

namespace JvmVerifier.Core.Descriptors;

/// <summary>
/// Parses JVMS 4.3 field and method descriptors. A malformed descriptor is not
/// repaired: the token stays at its location and a RawDiagnostic is attached.
/// </summary>
public static class DescriptorParser
{
    public static FieldType ParseField(string descriptor, List<RawDiagnostic>? diagnostics = null, string? valueId = null)
    {
        int i = 0;
        int dims = 0;
        while (i < descriptor.Length && descriptor[i] == '[') { dims++; i++; }

        if (i >= descriptor.Length)
        {
            diagnostics?.Add(new RawDiagnostic("descriptor-truncated", valueId ?? descriptor,
                $"descriptor ends inside array prefix: {descriptor}", valueId));
            return new FieldType { Kind = FieldTypeKind.Base, Base = BaseType.Int, Descriptor = descriptor };
        }

        char c = descriptor[i];
        if (c == 'L')
        {
            int semi = descriptor.IndexOf(';', i);
            if (semi < 0)
            {
                diagnostics?.Add(new RawDiagnostic("descriptor-missing-semicolon", valueId ?? descriptor,
                    $"object descriptor missing ';': {descriptor}", valueId));
                return new FieldType { Kind = FieldTypeKind.Object, InternalClassName = descriptor.Substring(i + 1), Descriptor = descriptor };
            }
            string name = descriptor.Substring(i + 1, semi - i - 1);
            if (semi != descriptor.Length - 1)
            {
                diagnostics?.Add(new RawDiagnostic("descriptor-trailing-chars", valueId ?? descriptor,
                    $"trailing characters after object descriptor: {descriptor}", valueId));
            }
            FieldType obj = new() { Kind = FieldTypeKind.Object, InternalClassName = name, Descriptor = dims > 0 ? descriptor : descriptor };
            if (dims == 0) return obj with { Descriptor = descriptor };
            return new FieldType
            {
                Kind = FieldTypeKind.Array,
                Dimensions = dims,
                ElementType = obj,
                Descriptor = descriptor,
            };
        }

        var (baseType, ok) = c switch
        {
            'B' => (BaseType.Byte, true),
            'C' => (BaseType.Char, true),
            'D' => (BaseType.Double, true),
            'F' => (BaseType.Float, true),
            'I' => (BaseType.Int, true),
            'J' => (BaseType.Long, true),
            'S' => (BaseType.Short, true),
            'Z' => (BaseType.Boolean, true),
            'V' => (BaseType.Void, true),
            _ => ((BaseType?)null, false),
        };
        if (!ok)
        {
            diagnostics?.Add(new RawDiagnostic("descriptor-bad-base", valueId ?? descriptor,
                $"unknown field type character '{c}' in {descriptor}", valueId));
            return new FieldType { Kind = FieldTypeKind.Base, Base = BaseType.Int, Descriptor = descriptor };
        }
        if (i != descriptor.Length - 1)
        {
            diagnostics?.Add(new RawDiagnostic("descriptor-trailing-chars", valueId ?? descriptor,
                $"trailing characters after base type: {descriptor}", valueId));
        }
        FieldType plain = new() { Kind = FieldTypeKind.Base, Base = baseType, Descriptor = descriptor };
        if (dims == 0) return plain;
        return new FieldType { Kind = FieldTypeKind.Array, Dimensions = dims, ElementType = plain, Descriptor = descriptor };
    }

    public static MethodTypeDescriptor ParseMethod(string descriptor, List<RawDiagnostic>? diagnostics = null, string? valueId = null)
    {
        var loc = valueId ?? descriptor;
        if (string.IsNullOrEmpty(descriptor) || descriptor[0] != '(')
        {
            diagnostics?.Add(new RawDiagnostic("method-descriptor-no-open", loc,
                $"method descriptor must start with '(': {descriptor}", valueId));
        }
        var parameters = new List<FieldType>();
        int i = descriptor.IndexOf('(') + 1;
        int close = descriptor.IndexOf(')');
        if (close < 0)
        {
            diagnostics?.Add(new RawDiagnostic("method-descriptor-no-close", loc,
                $"method descriptor missing ')': {descriptor}", valueId));
            close = descriptor.Length;
        }
        while (i < close && i < descriptor.Length)
        {
            int start = i;
            while (i < close && descriptor[i] == '[') i++;
            if (i < close && descriptor[i] == 'L')
            {
                int semi = descriptor.IndexOf(';', i);
                if (semi < 0 || semi > close)
                {
                    diagnostics?.Add(new RawDiagnostic("method-descriptor-bad-object", loc,
                        $"parameter object descriptor malformed: {descriptor}", valueId));
                    i = close;
                }
                else i = semi + 1;
            }
            else if (i < close) i++;
            if (i > start) parameters.Add(ParseField(descriptor[start..i], diagnostics, loc));
        }
        string ret = close + 1 < descriptor.Length ? descriptor[(close + 1)..] : "V";
        return new MethodTypeDescriptor
        {
            Descriptor = descriptor,
            Parameters = parameters,
            ReturnType = ParseField(ret, diagnostics, loc),
        };
    }
}
