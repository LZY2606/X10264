namespace JvmCore;

/// <summary>
/// Parses a classfile while preserving every raw artifact: original bytes,
/// constant-pool payloads, descriptors, exception ranges, StackMapTable bytes
/// and classfile version. Normalization happens only in the derived layer.
/// </summary>
public static class ClassFileParser
{
    private sealed class Reader
    {
        private readonly byte[] _b; private int _p;
        public Reader(byte[] b) { _b = b; }
        public int Pos => _p;
        public byte U1() => _b[_p++];
        public ushort U2() { var v = BigEndian.UInt16(_b, _p); _p += 2; return v; }
        public uint U4() { var v = BigEndian.UInt32(_b, _p); _p += 4; return v; }
        public byte[] Bytes(int n) { var r = new byte[n]; Array.Copy(_b, _p, r, 0, n); _p += n; return r; }
    }

    public static ClassFile Parse(byte[] bytes)
    {
        var r = new Reader(bytes);
        if (r.U4() != 0xCAFEBABE) throw new ArgumentException("bad magic");
        var minor = r.U2(); var major = r.U2();
        var cpCount = r.U2();
        var entries = new CpEntry?[cpCount];
        for (var i = 1; i < cpCount; i++)
        {
            var tag = r.U1();
            byte[] payload = tag switch
            {
                1 => r.Bytes(r.U2()),
                3 or 4 => r.Bytes(4),
                5 or 6 => r.Bytes(8),
                7 or 8 or 16 or 19 or 20 => r.Bytes(2),
                9 or 10 or 11 or 12 or 17 or 18 => r.Bytes(4),
                15 => r.Bytes(3),
                _ => throw new ArgumentException($"unknown cp tag {tag} at index {i}")
            };
            entries[i] = new CpEntry(i, tag, payload);
            if (tag is 5 or 6) i++; // long/double take two slots
        }
        var cp = new ConstantPool(entries);
        var access = r.U2();
        var thisClass = cp.ClassName(r.U2());
        var superIdx = r.U2();
        var super = superIdx == 0 ? "java/lang/Object" : cp.ClassName(superIdx);
        var ifaceCount = r.U2();
        for (var i = 0; i < ifaceCount; i++) r.U2();

        var cls = new ClassFile
        {
            RawBytes = bytes,
            MajorVersion = Quantity.Version(major),
            MinorVersion = Quantity.Version(minor),
            ConstantPool = cp,
            ThisClass = thisClass,
            SuperClass = super,
            Fields = Array.Empty<FieldModel>(),
            Methods = Array.Empty<MethodModel>()
        };

        var fields = new List<FieldModel>();
        var fieldCount = r.U2();
        for (var i = 0; i < fieldCount; i++)
        {
            r.U2();
            var name = cp.Utf8(r.U2());
            var desc = cp.Utf8(r.U2());
            fields.Add(new FieldModel(name, desc));
            if (!DescriptorNormalizer.IsValidFieldDescriptor(desc))
                cls.Diagnostics.Add($"field {name}: descriptor \"{desc}\" not normalizable; raw value kept in place");
            SkipAttributes(r);
        }

        var methods = new List<MethodModel>();
        var methodCount = r.U2();
        for (var i = 0; i < methodCount; i++)
        {
            var maccess = r.U2();
            var name = cp.Utf8(r.U2());
            var desc = cp.Utf8(r.U2());
            if (!DescriptorNormalizer.IsValidMethodDescriptor(desc))
                cls.Diagnostics.Add($"method {name}: descriptor \"{desc}\" not normalizable; raw value kept in place");
            methods.Add(ParseMethod(r, cp, name, desc, maccess, cls));
        }
        SkipAttributes(r); // class-level attributes
        cls.Fields = fields;
        cls.Methods = methods;
        return cls;
    }

    private static void SkipAttributes(Reader r)
    {
        var n = r.U2();
        for (var i = 0; i < n; i++) { r.U2(); var len = r.U4(); r.Bytes((int)len); }
    }

    private static MethodModel ParseMethod(Reader r, ConstantPool cp, string name, string desc, ushort access, ClassFile cls)
    {
        byte[] code = Array.Empty<byte>();
        ushort maxStack = 0, maxLocals = 0;
        var handlers = new List<ExceptionHandler>();
        var frames = new List<StackMapFrameRaw>();
        byte[]? smtRaw = null;
        var attrCount = r.U2();
        for (var i = 0; i < attrCount; i++)
        {
            var attrName = cp.Utf8(r.U2());
            var len = (int)r.U4();
            if (attrName == "Code")
            {
                maxStack = r.U2(); maxLocals = r.U2();
                code = r.Bytes((int)r.U4());
                var exCount = r.U2();
                for (var e = 0; e < exCount; e++)
                {
                    var spc = r.U2(); var epc = r.U2(); var hpc = r.U2(); var ct = r.U2();
                    handlers.Add(new ExceptionHandler(
                        Quantity.ByteOffset(spc), Quantity.ByteOffset(epc), Quantity.ByteOffset(hpc),
                        Quantity.CpIndex(ct), ct == 0 ? null : cp.ClassName(ct)));
                }
                var subCount = r.U2();
                for (var s = 0; s < subCount; s++)
                {
                    var subName = cp.Utf8(r.U2());
                    var subLen = (int)r.U4();
                    if (subName == "StackMapTable")
                    {
                        smtRaw = r.Bytes(subLen);
                        frames = StackMapTable.Parse(smtRaw, cp, cls.Diagnostics);
                    }
                    else r.Bytes(subLen);
                }
            }
            else r.Bytes(len);
        }
        return new MethodModel(
            name, desc,
            Quantity.Depth(maxStack), Quantity.Slot(maxLocals),
            code, handlers, frames, smtRaw,
            name == "<init>", (access & 0x0008) != 0);
    }
}

/// <summary>Derived-layer normalization of descriptors; raw strings are never mutated.</summary>
public static class DescriptorNormalizer
{
    public static bool IsValidFieldDescriptor(string d)
    {
        var (ok, consumed) = TryType(d, 0, false);
        return ok && consumed == d.Length && consumed > 0;
    }

    public static bool IsValidMethodDescriptor(string d)
    {
        if (!d.StartsWith('(')) return false;
        var i = 1;
        while (i < d.Length && d[i] != ')')
        {
            var (ok, consumed) = TryType(d, i, false);
            if (!ok) return false;
            i = consumed;
        }
        if (i >= d.Length || d[i] != ')') return false;
        if (i + 1 >= d.Length) return false;
        if (d[i + 1] == 'V') return i + 2 == d.Length;
        var (rok, rend) = TryType(d, i + 1, false);
        return rok && rend == d.Length;
    }

    public static List<VType> ParseParameterTypes(string d, bool isStatic, string thisClass, bool isCtor)
    {
        var types = new List<VType>();
        if (!isStatic)
            types.Add(isCtor ? VType.UninitializedThis : VType.RefOf(thisClass));
        var i = 1;
        while (i < d.Length && d[i] != ')')
        {
            var (ok, next) = TryType(d, i, false);
            if (!ok) break;
            types.Add(ToVType(d, i, next));
            i = next;
        }
        return types;
    }

    public static VType ParseReturnType(string d)
    {
        var close = d.IndexOf(')');
        if (close < 0 || close + 1 >= d.Length) return VType.Top;
        if (d[close + 1] == 'V') return VType.Top;
        var (ok, next) = TryType(d, close + 1, false);
        return ok ? ToVType(d, close + 1, next) : VType.Top;
    }

    public static int SlotCount(VType t) => t.IsCategory2 ? 2 : 1;

    private static VType ToVType(string d, int start, int end) => d[start] switch
    {
        'I' or 'B' or 'C' or 'S' or 'Z' => VType.Int,
        'F' => VType.Float,
        'J' => VType.Long,
        'D' => VType.Double,
        'L' => VType.RefOf(d.Substring(start + 1, end - start - 2)),
        '[' => VType.RefOf(d.Substring(start, end - start)),
        _ => VType.Top
    };

    private static (bool ok, int next) TryType(string d, int i, bool _)
    {
        if (i >= d.Length) return (false, i);
        switch (d[i])
        {
            case 'B' or 'C' or 'D' or 'F' or 'I' or 'J' or 'S' or 'Z': return (true, i + 1);
            case 'L':
                var semi = d.IndexOf(';', i);
                return semi < 0 ? (false, i) : (true, semi + 1);
            case '[':
                var j = i;
                while (j < d.Length && d[j] == '[') j++;
                return TryType(d, j, false);
            default: return (false, i);
        }
    }
}
