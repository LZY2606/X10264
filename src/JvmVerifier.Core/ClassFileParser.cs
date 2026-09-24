namespace JvmVerifier.Core;

/// <summary>Binary classfile parser. Keeps original offsets and raw bytes.</summary>
public static class ClassFileParser
{
    public sealed class ParseException(string message, int fileOffset) : Exception(message)
    {
        public int FileOffset { get; } = fileOffset;
    }

    private sealed class Reader(byte[] data)
    {
        public int Pos;
        public byte U1() => Pos < data.Length ? data[Pos++] : throw new ParseException("unexpected EOF", Pos);
        public ushort U2() => (ushort)(U1() << 8 | U1());
        public uint U4() => (uint)U2() << 16 | U2();
        public byte[] Bytes(int n)
        {
            if (Pos + n > data.Length) throw new ParseException("unexpected EOF", Pos);
            var b = data[Pos..(Pos + n)]; Pos += n; return b;
        }
    }

    public static ClassFileModel Parse(byte[] data)
    {
        var r = new Reader(data);
        if (r.U4() != 0xCAFEBABE) throw new ParseException("bad magic", 0);
        int minor = r.U2(), major = r.U2();
        int cpCount = r.U2();
        var cp = new CpEntry[cpCount];
        cp[0] = new CpEntry.Unknown(0, []);
        for (int i = 1; i < cpCount; i++)
        {
            int tag = r.U1();
            switch (tag)
            {
                case 1: cp[i] = new CpEntry.Utf8(System.Text.Encoding.UTF8.GetString(r.Bytes(r.U2()))); break;
                case 3: cp[i] = new CpEntry.IntInfo((int)r.U4()); break;
                case 4: cp[i] = new CpEntry.FloatInfo(BitConverter.Int32BitsToSingle((int)r.U4())); break;
                case 5: cp[i] = new CpEntry.LongInfo((long)r.U4() << 32 | r.U4()); cp[++i] = new CpEntry.WidePadding(); break;
                case 6: cp[i] = new CpEntry.DoubleInfo(BitConverter.Int64BitsToDouble((long)r.U4() << 32 | (long)r.U4())); cp[++i] = new CpEntry.WidePadding(); break;
                case 7: cp[i] = new CpEntry.Class(r.U2()); break;
                case 8: cp[i] = new CpEntry.StringInfo(r.U2()); break;
                case 9: cp[i] = new CpEntry.Fieldref(r.U2(), r.U2()); break;
                case 10: cp[i] = new CpEntry.Methodref(r.U2(), r.U2()); break;
                case 11: cp[i] = new CpEntry.InterfaceMethodref(r.U2(), r.U2()); break;
                case 12: cp[i] = new CpEntry.NameAndType(r.U2(), r.U2()); break;
                case 15: cp[i] = new CpEntry.MethodHandle(r.U1(), r.U2()); break;
                case 16: cp[i] = new CpEntry.MethodType(r.U2()); break;
                case 18: cp[i] = new CpEntry.InvokeDynamic(r.U2(), r.U2()); break;
                default:
                    throw new ParseException($"unknown constant pool tag {tag}", r.Pos - 1);
            }
        }
        r.U2(); // access flags
        string thisClass = ((CpEntry.Class)cp[r.U2()]).NameIndex is var ni ? ((CpEntry.Utf8)cp[ni]).Value : "?";
        int superIdx = r.U2();
        string superClass = superIdx == 0 ? "java/lang/Object" : ((CpEntry.Utf8)cp[((CpEntry.Class)cp[superIdx]).NameIndex]).Value;
        int ifaceCount = r.U2(); r.Bytes(ifaceCount * 2);
        int fieldCount = r.U2();
        for (int i = 0; i < fieldCount; i++) SkipMember(r);
        int methodCount = r.U2();
        var methods = new List<MethodModel>();
        for (int i = 0; i < methodCount; i++)
        {
            int access = r.U2();
            string name = ((CpEntry.Utf8)cp[r.U2()]).Value;
            string desc = ((CpEntry.Utf8)cp[r.U2()]).Value;
            CodeModel? code = null;
            int attrCount = r.U2();
            for (int a = 0; a < attrCount; a++)
            {
                string attrName = ((CpEntry.Utf8)cp[r.U2()]).Value;
                uint len = r.U4();
                if (attrName == "Code")
                {
                    int maxStack = r.U2(), maxLocals = r.U2();
                    uint codeLen = r.U4();
                    int codeFileOffset = r.Pos;
                    byte[] codeBytes = r.Bytes((int)codeLen);
                    int exCount = r.U2();
                    var handlers = new List<ExceptionHandler>();
                    for (int e = 0; e < exCount; e++)
                        handlers.Add(new ExceptionHandler(r.U2(), r.U2(), r.U2(), r.U2()));
                    var stackMap = new List<StackMapFrame>();
                    int subAttrCount = r.U2();
                    for (int s = 0; s < subAttrCount; s++)
                    {
                        string subName = ((CpEntry.Utf8)cp[r.U2()]).Value;
                        uint subLen = r.U4();
                        if (subName == "StackMapTable")
                        {
                            int frameCount = r.U2();
                            for (int f = 0; f < frameCount; f++) stackMap.Add(ReadFrame(r));
                        }
                        else r.Bytes((int)subLen);
                    }
                    code = new CodeModel(maxStack, maxLocals, codeBytes, handlers, stackMap, codeFileOffset);
                }
                else r.Bytes((int)len);
            }
            methods.Add(new MethodModel(name, desc, DecodeAccess(access), code));
        }
        return new ClassFileModel(minor, major, cp, thisClass, superClass, methods, data);
    }

    private static void SkipMember(Reader r)
    {
        r.Bytes(6);
        int attrCount = r.U2();
        for (int a = 0; a < attrCount; a++) { r.U2(); r.Bytes((int)r.U4()); }
    }

    private static List<string> DecodeAccess(int access)
    {
        var flags = new List<string>();
        if ((access & 0x0001) != 0) flags.Add("public");
        if ((access & 0x0002) != 0) flags.Add("private");
        if ((access & 0x0008) != 0) flags.Add("static");
        if ((access & 0x0010) != 0) flags.Add("final");
        if ((access & 0x0100) != 0) flags.Add("native");
        if ((access & 0x0400) != 0) flags.Add("abstract");
        return flags;
    }

    private static StackMapFrame ReadFrame(Reader r)
    {
        int ft = r.U1();
        if (ft <= 63) return new StackMapFrame(StackMapFrameKind.Same, ft, [], []);
        if (ft <= 127) return new StackMapFrame(StackMapFrameKind.SameLocals1StackItem, ft - 64, [], [ReadVTI(r)]);
        if (ft == 247) return new StackMapFrame(StackMapFrameKind.Same, r.U2(), [], []);
        if (ft is >= 248 and <= 250) return new StackMapFrame(StackMapFrameKind.Chop, r.U2(), [], []);
        if (ft == 251) return new StackMapFrame(StackMapFrameKind.Same, r.U2(), [], []);
        if (ft is >= 252 and <= 254)
        {
            int delta = r.U2();
            var locals = new List<VerificationTypeInfo>();
            for (int i = 0; i < ft - 251; i++) locals.Add(ReadVTI(r));
            return new StackMapFrame(StackMapFrameKind.Append, delta, locals, []);
        }
        if (ft == 255)
        {
            int delta = r.U2();
            int nl = r.U2();
            var locals = new List<VerificationTypeInfo>();
            for (int i = 0; i < nl; i++) locals.Add(ReadVTI(r));
            int ns = r.U2();
            var stack = new List<VerificationTypeInfo>();
            for (int i = 0; i < ns; i++) stack.Add(ReadVTI(r));
            return new StackMapFrame(StackMapFrameKind.Full, delta, locals, stack);
        }
        throw new ParseException($"reserved frame_type {ft}", r.Pos - 1);
    }

    private static VerificationTypeInfo ReadVTI(Reader r) => r.U1() switch
    {
        0 => new VerificationTypeInfo.Top(),
        1 => new VerificationTypeInfo.Int(),
        2 => new VerificationTypeInfo.Float(),
        3 => new VerificationTypeInfo.Double(),
        4 => new VerificationTypeInfo.Long(),
        5 => new VerificationTypeInfo.Null(),
        6 => new VerificationTypeInfo.UninitializedThis(),
        7 => new VerificationTypeInfo.Object(r.U2()),
        8 => new VerificationTypeInfo.Uninitialized(r.U2()),
        var t => throw new ParseException($"bad verification_type_info tag {t}", r.Pos - 1),
    };
}
