using System.Text;

namespace BytecodeSeal.Core;

/// <summary>
/// Parses a classfile, preserving raw bytes, constant pool entries, descriptors,
/// exception ranges, StackMapTable frames and the classfile version exactly as stored.
/// </summary>
public static class ClassFileParser
{
    public static ClassFile Parse(byte[] data)
    {
        var r = new Reader(data);
        uint magic = r.U4();
        if (magic != 0xCAFEBABE) throw new ClassFormatException($"bad magic 0x{magic:X8}");
        ushort minor = r.U2(), major = r.U2();
        int cpCount = r.U2();
        var cp = new CpEntry?[cpCount];
        // first pass: read raw structures
        var raw = new (int tag, object a, object? b)?[cpCount];
        for (int i = 1; i < cpCount; i++)
        {
            int tag = r.U1();
            switch (tag)
            {
                case 1: { int len = r.U2(); raw[i] = (tag, Encoding.UTF8.GetString(r.Bytes(len)), null); break; }
                case 3: raw[i] = (tag, (int)r.U4(), null); break;
                case 4: raw[i] = (tag, r.U4(), null); break;
                case 5: raw[i] = (tag, r.U8(), null); i++; break;
                case 6: raw[i] = (tag, r.U8(), null); i++; break;
                case 7: case 8: case 16: case 19: case 20: raw[i] = (tag, (int)r.U2(), null); break;
                case 9: case 10: case 11: case 12: case 17: case 18: raw[i] = (tag, (int)r.U2(), (int)r.U2()); break;
                case 15: raw[i] = (tag, (int)r.U1(), (int)r.U2()); break;
                default: throw new ClassFormatException($"unknown cp tag {tag} at #{i}");
            }
        }
        string Utf8Of(int idx) => raw[idx] is { tag: 1 } t ? (string)t.a : throw new ClassFormatException($"cp #{idx} not utf8");
        for (int i = 1; i < cpCount; i++)
        {
            if (raw[i] is not { } t) continue;
            cp[i] = t.tag switch
            {
                1 => new CpEntry.Utf8(i, (string)t.a),
                3 => new CpEntry.IntConst(i, (int)t.a),
                5 => new CpEntry.LongConst(i, (long)(ulong)t.a),
                7 => new CpEntry.ClassRef(i, (int)t.a, Utf8Of((int)t.a)),
                8 => new CpEntry.StringConst(i, (int)t.a, Utf8Of((int)t.a)),
                12 => new CpEntry.NameAndTypeRef(i, (int)t.a, (int)t.b!, Utf8Of((int)t.a), Utf8Of((int)t.b!)),
                10 or 11 => ResolveMethod(i, (int)t.a, (int)t.b!),
                9 => ResolveField(i, (int)t.a, (int)t.b!),
                _ => new CpEntry.Raw(i, t.tag, Array.Empty<byte>())
            };
        }
        CpEntry ResolveMethod(int i, int cls, int nt)
        {
            var c = (CpEntry.ClassRef)cp[cls]!;
            var n = (CpEntry.NameAndTypeRef)cp[nt]!;
            return new CpEntry.MethodRef(i, cls, nt, c.Name, n.Name, n.Descriptor);
        }
        CpEntry ResolveField(int i, int cls, int nt)
        {
            var c = (CpEntry.ClassRef)cp[cls]!;
            var n = (CpEntry.NameAndTypeRef)cp[nt]!;
            return new CpEntry.FieldRef(i, cls, nt, c.Name, n.Name, n.Descriptor);
        }

        r.U2(); // access flags
        string thisClass = ((CpEntry.ClassRef)cp[r.U2()]!).Name;
        int superIdx = r.U2();
        string? superClass = superIdx == 0 ? null : ((CpEntry.ClassRef)cp[superIdx]!).Name;
        int ifaces = r.U2(); r.Bytes(2 * ifaces);
        var fields = new List<FieldModel>();
        int fcount = r.U2();
        for (int i = 0; i < fcount; i++)
        {
            ushort acc = r.U2(); string name = Utf8Of(r.U2()); string desc = Utf8Of(r.U2());
            SkipAttributes(r);
            fields.Add(new FieldModel(name, desc, acc));
        }
        var methods = new List<MethodModel>();
        int mcount = r.U2();
        for (int i = 0; i < mcount; i++)
        {
            ushort acc = r.U2(); string name = Utf8Of(r.U2()); string desc = Utf8Of(r.U2());
            CodeAttribute? code = null;
            int acount = r.U2();
            for (int a = 0; a < acount; a++)
            {
                string aname = Utf8Of(r.U2()); int alen = (int)r.U4();
                if (aname == "Code") code = ParseCode(r, cp);
                else r.Bytes(alen);
            }
            methods.Add(new MethodModel(name, desc, acc, code));
        }
        return new ClassFile(data, minor, major, cp!, thisClass, superClass, fields, methods);
    }

    private static void SkipAttributes(Reader r)
    {
        int n = r.U2();
        for (int i = 0; i < n; i++) { r.U2(); int len = (int)r.U4(); r.Bytes(len); }
    }

    private static CodeAttribute ParseCode(Reader r, CpEntry?[] cp)
    {
        ushort maxStack = r.U2(), maxLocals = r.U2();
        byte[] code = r.Bytes((int)r.U4());
        int exn = r.U2();
        var handlers = new List<ExceptionHandler>();
        for (int i = 0; i < exn; i++)
        {
            int spc = r.U2(), epc = r.U2(), hpc = r.U2(), ct = r.U2();
            handlers.Add(new ExceptionHandler(spc, epc, hpc,
                ct == 0 ? null : ((CpEntry.ClassRef)cp[ct]!).Name));
        }
        List<StackMapFrame>? frames = null;
        bool smtPresent = false;
        int acount = r.U2();
        for (int a = 0; a < acount; a++)
        {
            string aname = cp[r.U2()] is CpEntry.Utf8 u ? u.Value : "";
            int alen = (int)r.U4();
            if (aname == "StackMapTable")
            {
                smtPresent = true;
                frames = ParseStackMap(r);
            }
            else r.Bytes(alen);
        }
        return new CodeAttribute(maxStack, maxLocals, code, handlers, frames, smtPresent);
    }

    private static List<StackMapFrame> ParseStackMap(Reader r)
    {
        var frames = new List<StackMapFrame>();
        int n = r.U2();
        int offset = -1;
        for (int i = 0; i < n; i++)
        {
            int tag = r.U1();
            int delta; StackMapFrame f;
            if (tag <= 63) { delta = tag; f = new StackMapFrame.Same(delta, offset + delta + 1); }
            else if (tag <= 127)
            {
                delta = tag - 64;
                var item = ReadVti(r);
                f = new StackMapFrame.Same1Stack(delta, offset + delta + 1, item);
            }
            else if (tag == 247) { delta = r.U2(); var item = ReadVti(r); f = new StackMapFrame.Same1Stack(delta, offset + delta + 1, item); }
            else if (tag >= 248 && tag <= 250) { delta = r.U2(); f = new StackMapFrame.Chop(delta, offset + delta + 1, 251 - tag); }
            else if (tag == 251) { delta = r.U2(); f = new StackMapFrame.Same(delta, offset + delta + 1); }
            else if (tag >= 252 && tag <= 254)
            {
                delta = r.U2();
                var added = new List<VerificationTypeInfo>();
                for (int k = 0; k < tag - 251; k++) added.Add(ReadVti(r));
                f = new StackMapFrame.Append(delta, offset + delta + 1, added);
            }
            else
            {
                delta = r.U2();
                int nl = r.U2(); var locals = new List<VerificationTypeInfo>();
                for (int k = 0; k < nl; k++) locals.Add(ReadVti(r));
                int ns = r.U2(); var stack = new List<VerificationTypeInfo>();
                for (int k = 0; k < ns; k++) stack.Add(ReadVti(r));
                f = new StackMapFrame.Full(delta, offset + delta + 1, locals, stack);
            }
            offset = f.Offset;
            frames.Add(f);
        }
        return frames;
    }

    private static VerificationTypeInfo ReadVti(Reader r)
    {
        int tag = r.U1();
        return tag switch
        {
            0 => new(0, null, "top"),
            1 => new(1, null, "int"),
            2 => new(2, null, "float"),
            3 => new(3, null, "double"),
            4 => new(4, null, "long"),
            5 => new(5, null, "null"),
            6 => new(6, null, "uninitializedThis"),
            7 => new(7, r.U2(), "object"),
            8 => new(8, r.U2(), "uninitialized"),
            _ => throw new ClassFormatException($"bad verification_type_info tag {tag}")
        };
    }

    private sealed class Reader(byte[] d)
    {
        private int p;
        public int U1() => d[p++];
        public ushort U2() { ushort v = (ushort)((d[p] << 8) | d[p + 1]); p += 2; return v; }
        public uint U4() { uint v = ((uint)d[p] << 24) | ((uint)d[p+1] << 16) | ((uint)d[p+2] << 8) | d[p+3]; p += 4; return v; }
        public ulong U8() => ((ulong)U4() << 32) | U4();
        public byte[] Bytes(int n) { var b = d.AsSpan(p, n).ToArray(); p += n; return b; }
    }
}

public sealed class ClassFormatException(string message) : Exception(message);
