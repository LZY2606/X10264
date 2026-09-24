using System.Text;

namespace JvmVerify.Core;

/// <summary>Constant pool entry. Original index and raw representation are preserved.</summary>
public abstract record CpInfo
{
    public sealed record Utf8(string Value) : CpInfo;
    public sealed record ClassRef(ushort NameIndex, string Name) : CpInfo;
    public sealed record NameAndType(ushort NameIndex, ushort DescIndex, string Name, string Descriptor) : CpInfo;
    public sealed record MethodRef(ushort ClassIndex, ushort NameAndTypeIndex, string ClassName, string Name, string Descriptor) : CpInfo;
    public sealed record FieldRef(ushort ClassIndex, ushort NameAndTypeIndex, string ClassName, string Name, string Descriptor) : CpInfo;
    public sealed record StringConst(ushort StringIndex, string Value) : CpInfo;
    public sealed record IntConst(int Value) : CpInfo;
    public sealed record FloatConst(float Value) : CpInfo;
    public sealed record LongConst(long Value) : CpInfo;
    public sealed record DoubleConst(double Value) : CpInfo;
    public sealed record MethodHandle(byte Kind, ushort ReferenceIndex) : CpInfo;
    public sealed record MethodType(ushort DescriptorIndex) : CpInfo;
    public sealed record InvokeDynamic(ushort BootstrapIndex, ushort NameAndTypeIndex) : CpInfo;
}

/// <summary>Verification type as stored in a StackMapTable frame.</summary>
public abstract record VType
{
    public sealed record Top : VType;
    public sealed record IntT : VType;
    public sealed record FloatT : VType;
    public sealed record LongT : VType;
    public sealed record DoubleT : VType;
    public sealed record NullT : VType;
    public sealed record UninitializedThisT : VType;
    public sealed record Obj(string ClassName) : VType;
    public sealed record Uninit(ushort Offset) : VType;

    public bool IsCategory2 => this is LongT or DoubleT;

    public override string ToString() => this switch
    {
        Top => "top",
        IntT => "int",
        FloatT => "float",
        LongT => "long",
        DoubleT => "double",
        NullT => "null",
        UninitializedThisT => "uninitializedThis",
        Obj o => $"ref:{o.ClassName}",
        Uninit u => $"uninit@{u.Offset}",
        _ => "?"
    };
}

public sealed record StackMapFrame(
    int Offset,
    string Kind,
    IReadOnlyList<VType> Locals,
    IReadOnlyList<VType> Stack);

public sealed record ExceptionHandler(
    ushort StartPc, ushort EndPc, ushort HandlerPc,
    ushort CatchTypeIndex, string CatchType);

public sealed record MethodInfo(
    string Name,
    string Descriptor,
    ushort MaxStack,
    ushort MaxLocals,
    byte[] Code,
    IReadOnlyList<ExceptionHandler> Handlers,
    IReadOnlyList<StackMapFrame> Frames);

public sealed record ClassFile(
    ushort Minor,
    ushort Major,
    IReadOnlyList<CpInfo?> ConstantPool,
    string ThisClass,
    IReadOnlyList<MethodInfo> Methods)
{
    public CpInfo Cp(int index) => ConstantPool[index]
        ?? throw new VerifyParseException($"constant pool slot #{index} is empty (wide entry tail)");

    public string CpUtf8(int index) => Cp(index) is CpInfo.Utf8 u
        ? u.Value
        : throw new VerifyParseException($"constant pool #{index} is not Utf8");

    public string CpClassName(int index) => Cp(index) is CpInfo.ClassRef c
        ? c.Name
        : throw new VerifyParseException($"constant pool #{index} is not a Class entry");
}

public sealed class VerifyParseException(string message) : Exception(message);

/// <summary>
/// Big-endian classfile parser. Preserves raw offsets, constant pool entries,
/// descriptors, branch targets, exception ranges, stack map frames and the
/// classfile version exactly as imported; normalization happens only in
/// derived layers.
/// </summary>
public static class ClassFileParser
{
    public static ClassFile Parse(byte[] bytes)
    {
        var r = new Reader(bytes);
        if (r.U4() != 0xCAFEBABE) throw new VerifyParseException("bad magic");
        ushort minor = r.U2();
        ushort major = r.U2();
        int cpCount = r.U2();
        var cp = new CpInfo?[cpCount];
        var raw = new (byte tag, int a, int b)?[cpCount];
        for (int i = 1; i < cpCount; i++)
        {
            byte tag = r.U1();
            switch (tag)
            {
                case 1: { int len = r.U2(); raw[i] = (tag, r.Pos, len); r.Skip(len); break; }
                case 7: case 8: case 16: raw[i] = (tag, r.U2(), -1); break;
                case 9: case 10: case 11: case 12: case 18: raw[i] = (tag, r.U2(), r.U2()); break;
                case 15: raw[i] = (tag, r.U1(), r.U2()); break;
                case 3: case 4: raw[i] = (tag, (int)r.U4(), -1); break;
                case 5: case 6:
                    {
                        uint hi = r.U4(); uint lo = r.U4();
                        raw[i] = (tag, (int)hi, (int)lo);
                        i++; // long/double occupy two pool slots
                        break;
                    }
                default: throw new VerifyParseException($"unknown constant pool tag {tag} at #{i}");
            }
        }

        string NeedUtf8(int idx)
        {
            if (idx <= 0 || idx >= cpCount || raw[idx] is not { tag: 1 } e)
                throw new VerifyParseException($"constant pool #{idx} is not Utf8");
            return Encoding.UTF8.GetString(bytes, e.a, e.b);
        }

        string ClassNameOf(int classIdx)
        {
            if (raw[classIdx] is not { tag: 7 } e)
                throw new VerifyParseException($"constant pool #{classIdx} is not a Class entry");
            return NeedUtf8(e.a);
        }
        string NtName(int ntIdx) => NeedUtf8(raw[ntIdx]!.Value.a);
        string NtDesc(int ntIdx) => NeedUtf8(raw[ntIdx]!.Value.b);

        for (int i = 1; i < cpCount; i++)
        {
            if (raw[i] is not { } e) continue;
            cp[i] = e.tag switch
            {
                1 => new CpInfo.Utf8(NeedUtf8(i)),
                7 => new CpInfo.ClassRef((ushort)e.a, NeedUtf8(e.a)),
                8 => new CpInfo.StringConst((ushort)e.a, NeedUtf8(e.a)),
                9 => new CpInfo.FieldRef((ushort)e.a, (ushort)e.b,
                    ClassNameOf(e.a), NtName(e.b), NtDesc(e.b)),
                10 or 11 => new CpInfo.MethodRef((ushort)e.a, (ushort)e.b,
                    ClassNameOf(e.a), NtName(e.b), NtDesc(e.b)),
                12 => new CpInfo.NameAndType((ushort)e.a, (ushort)e.b, NeedUtf8(e.a), NeedUtf8(e.b)),
                3 => new CpInfo.IntConst(e.a),
                4 => new CpInfo.FloatConst(BitConverter.Int32BitsToSingle(e.a)),
                5 => new CpInfo.LongConst(((long)(uint)e.a << 32) | (uint)e.b),
                6 => new CpInfo.DoubleConst(BitConverter.Int64BitsToDouble(((long)(uint)e.a << 32) | (uint)e.b)),
                15 => new CpInfo.MethodHandle((byte)e.a, (ushort)e.b),
                16 => new CpInfo.MethodType((ushort)e.a),
                18 => new CpInfo.InvokeDynamic((ushort)e.a, (ushort)e.b),
                _ => throw new VerifyParseException($"tag {e.tag}")
            };
        }

        r.U2(); // access flags
        string thisClass = ClassNameOf(r.U2());
        r.U2(); // super
        int ifaces = r.U2();
        for (int i = 0; i < ifaces; i++) r.U2();
        SkipMembers(r); // fields
        var methods = ReadMethods(r, NeedUtf8, cp);
        return new ClassFile(minor, major, cp, thisClass, methods);
    }

    private static void SkipMembers(Reader r)
    {
        int count = r.U2();
        for (int i = 0; i < count; i++)
        {
            r.U2(); r.U2(); r.U2();
            SkipAttributes(r);
        }
    }

    private static void SkipAttributes(Reader r)
    {
        int attrs = r.U2();
        for (int a = 0; a < attrs; a++)
        {
            r.U2();
            r.Skip((int)r.U4());
        }
    }

    private static List<MethodInfo> ReadMethods(Reader r, Func<int, string> utf8, CpInfo?[] cp)
    {
        var result = new List<MethodInfo>();
        int count = r.U2();
        for (int i = 0; i < count; i++)
        {
            r.U2(); // access flags
            string name = utf8(r.U2());
            string desc = utf8(r.U2());
            ushort maxStack = 0, maxLocals = 0;
            byte[] code = Array.Empty<byte>();
            var handlers = new List<ExceptionHandler>();
            var frames = new List<StackMapFrame>();
            int attrs = r.U2();
            for (int a = 0; a < attrs; a++)
            {
                string attrName = utf8(r.U2());
                int len = (int)r.U4();
                if (attrName == "Code")
                {
                    maxStack = r.U2();
                    maxLocals = r.U2();
                    code = r.Bytes((int)r.U4());
                    int exCount = r.U2();
                    for (int e = 0; e < exCount; e++)
                    {
                        ushort spc = r.U2(); ushort epc = r.U2(); ushort hpc = r.U2(); ushort ct = r.U2();
                        string catchName = ct == 0 ? "java/lang/Throwable" :
                            cp[ct] is CpInfo.ClassRef cr ? cr.Name : $"cp#{ct}";
                        handlers.Add(new ExceptionHandler(spc, epc, hpc, ct, catchName));
                    }
                    int codeAttrs = r.U2();
                    for (int ca = 0; ca < codeAttrs; ca++)
                    {
                        string caName = utf8(r.U2());
                        int caLen = (int)r.U4();
                        if (caName == "StackMapTable")
                            frames.AddRange(ReadStackMapTable(r, cp));
                        else
                            r.Skip(caLen);
                    }
                }
                else
                {
                    r.Skip(len);
                }
            }
            result.Add(new MethodInfo(name, desc, maxStack, maxLocals, code, handlers, frames));
        }
        return result;
    }

    public static List<StackMapFrame> ReadStackMapTable(Reader r, CpInfo?[] cp)
    {
        var frames = new List<StackMapFrame>();
        int count = r.U2();
        int offset = -1;
        for (int i = 0; i < count; i++)
        {
            int tag = r.U1();
            if (tag <= 63) // same_frame
            {
                offset += tag + 1;
                frames.Add(new StackMapFrame(offset, "same", Array.Empty<VType>(), Array.Empty<VType>()));
            }
            else if (tag <= 127) // same_locals_1_stack_item
            {
                offset += tag - 64 + 1;
                var st = ReadVType(r, cp);
                frames.Add(new StackMapFrame(offset, "same_locals_1_stack_item",
                    Array.Empty<VType>(), new[] { st }));
            }
            else if (tag == 247)
            {
                offset += r.U2() + 1;
                var st = ReadVType(r, cp);
                frames.Add(new StackMapFrame(offset, "same_locals_1_stack_item_extended",
                    Array.Empty<VType>(), new[] { st }));
            }
            else if (tag is >= 248 and <= 250) // chop
            {
                offset += r.U2() + 1;
                frames.Add(new StackMapFrame(offset, $"chop_{251 - tag}", Array.Empty<VType>(), Array.Empty<VType>()));
            }
            else if (tag == 251) // same_frame_extended
            {
                offset += r.U2() + 1;
                frames.Add(new StackMapFrame(offset, "same_extended", Array.Empty<VType>(), Array.Empty<VType>()));
            }
            else if (tag is >= 252 and <= 254) // append
            {
                offset += r.U2() + 1;
                var locals = new List<VType>();
                for (int k = 0; k < tag - 251; k++) locals.Add(ReadVType(r, cp));
                frames.Add(new StackMapFrame(offset, $"append_{tag - 251}", locals, Array.Empty<VType>()));
            }
            else if (tag == 255) // full_frame
            {
                offset += r.U2() + 1;
                int nLocals = r.U2();
                var locals = new List<VType>();
                for (int k = 0; k < nLocals; k++) locals.Add(ReadVType(r, cp));
                int nStack = r.U2();
                var stack = new List<VType>();
                for (int k = 0; k < nStack; k++) stack.Add(ReadVType(r, cp));
                frames.Add(new StackMapFrame(offset, "full", locals, stack));
            }
            else
            {
                throw new VerifyParseException($"reserved stack map frame tag {tag}");
            }
        }
        return frames;
    }

    private static VType ReadVType(Reader r, CpInfo?[] cp)
    {
        int tag = r.U1();
        return tag switch
        {
            0 => new VType.Top(),
            1 => new VType.IntT(),
            2 => new VType.FloatT(),
            3 => new VType.DoubleT(),
            4 => new VType.LongT(),
            5 => new VType.NullT(),
            6 => new VType.UninitializedThisT(),
            7 => new VType.Obj(cp[r.U2()] is CpInfo.ClassRef cr ? cr.Name : "?"),
            8 => new VType.Uninit(r.U2()),
            _ => throw new VerifyParseException($"unknown verification type tag {tag}")
        };
    }

    public sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) => _b = b;
        public byte U1() => _b[Pos++];
        public ushort U2() { ushort v = (ushort)((_b[Pos] << 8) | _b[Pos + 1]); Pos += 2; return v; }
        public uint U4()
        {
            uint v = (uint)((_b[Pos] << 24) | (_b[Pos + 1] << 16) | (_b[Pos + 2] << 8) | _b[Pos + 3]);
            Pos += 4; return v;
        }
        public byte[] Bytes(int n) { var a = new byte[n]; Array.Copy(_b, Pos, a, 0, n); Pos += n; return a; }
        public void Skip(int n) => Pos += n;
    }
}
