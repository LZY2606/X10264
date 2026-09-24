using System.Text;

namespace JvmVerifier.Core.ClassFile;

/// <summary>Parses a .class file preserving original byte offsets, constant pool, descriptors,
/// branch targets, exception ranges, stack-map frames and the classfile version.</summary>
public static class ClassFileParser
{
    public static ClassFileModel Parse(byte[] bytes)
    {
        var r = new Reader(bytes);
        uint magic = r.U4();
        if (magic != 0xCAFEBABE) throw new InvalidDataException($"bad magic 0x{magic:X8}");
        int minor = r.U2();
        int major = r.U2();
        int cpCount = r.U2();
        var cpRaw = new CpEntry?[cpCount];
        // First pass: keep raw bytes of every entry.
        for (int i = 1; i < cpCount; i++)
        {
            int start = r.Pos;
            byte tag = r.U1();
            string tagName;
            string value;
            switch (tag)
            {
                case 1: tagName = "Utf8"; { int len = r.U2(); value = Encoding.UTF8.GetString(r.Bytes(len)); } break;
                case 3: tagName = "Integer"; value = ((int)r.U4()).ToString(); break;
                case 4: tagName = "Float"; value = BitConverter.Int32BitsToSingle((int)r.U4()).ToString("R"); break;
                case 5: tagName = "Long"; value = ((long)r.U8()).ToString(); i++; break;
                case 6: tagName = "Double"; value = BitConverter.Int64BitsToDouble((long)r.U8()).ToString("R"); i++; break;
                case 7: tagName = "Class"; value = $"#{r.U2()}"; break;
                case 8: tagName = "String"; value = $"#{r.U2()}"; break;
                case 9: tagName = "Fieldref"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 10: tagName = "Methodref"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 11: tagName = "InterfaceMethodref"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 12: tagName = "NameAndType"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 15: tagName = "MethodHandle"; value = $"{r.U1()}.#{r.U2()}"; break;
                case 16: tagName = "MethodType"; value = $"#{r.U2()}"; break;
                case 17: tagName = "Dynamic"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 18: tagName = "InvokeDynamic"; value = $"#{r.U2()}.#{r.U2()}"; break;
                case 19: tagName = "Module"; value = $"#{r.U2()}"; break;
                case 20: tagName = "Package"; value = $"#{r.U2()}"; break;
                default: throw new InvalidDataException($"unknown cp tag {tag} at index {i}");
            }
            cpRaw[i] = new CpEntry(i, tag, tagName, value, bytes[start..r.Pos]);
        }

        string CpUtf8(int idx) => cpRaw[idx] is { Tag: 1 } e ? e.Value : throw new InvalidDataException($"cp #{idx} not utf8");
        string CpClassName(int idx)
        {
            var e = cpRaw[idx] ?? throw new InvalidDataException($"cp #{idx} empty");
            if (e.Tag != 7) throw new InvalidDataException($"cp #{idx} not class");
            int nameIdx = int.Parse(e.Value.TrimStart('#'));
            return CpUtf8(nameIdx);
        }

        r.U2(); // access_flags
        string thisClass = CpClassName(r.U2());
        r.U2(); // super_class
        int ifaces = r.U2();
        for (int i = 0; i < ifaces; i++) r.U2();
        SkipMembers(r); // fields
        int methodCount = r.U2();
        var methods = new List<MethodModel>();
        for (int m = 0; m < methodCount; m++)
        {
            int access = r.U2();
            string name = CpUtf8(r.U2());
            string desc = CpUtf8(r.U2());
            int attrCount = r.U2();
            MethodModel? model = null;
            for (int a = 0; a < attrCount; a++)
            {
                string attrName = CpUtf8(r.U2());
                int len = (int)r.U4();
                int end = r.Pos + len;
                if (attrName == "Code") model = ParseCode(r, CpUtf8, cpRaw, name, desc, access);
                r.Pos = end;
            }
            if (model != null) methods.Add(model);
        }
        return new ClassFileModel(minor, major, cpRaw.Where(e => e != null).Cast<CpEntry>().ToList(), thisClass, methods, bytes);
    }

    private static void SkipMembers(Reader r)
    {
        int count = r.U2();
        for (int i = 0; i < count; i++)
        {
            r.U2(); r.U2(); r.U2();
            int attrs = r.U2();
            for (int a = 0; a < attrs; a++) { r.U2(); int len = (int)r.U4(); r.Skip(len); }
        }
    }

    private static MethodModel ParseCode(Reader r, Func<int, string> cpUtf8, CpEntry?[] cp, string name, string desc, int access)
    {
        int maxStack = r.U2();
        int maxLocals = r.U2();
        byte[] code = r.Bytes((int)r.U4());
        var exceptions = new List<ExceptionHandler>();
        int exCount = r.U2();
        for (int i = 0; i < exCount; i++)
        {
            int startPc = r.U2(), endPc = r.U2(), handlerPc = r.U2(), catchType = r.U2();
            string ct = catchType == 0 ? "java/lang/Throwable(finally)" : ResolveClass(cp, catchType);
            exceptions.Add(new ExceptionHandler(startPc, endPc, handlerPc, catchType == 0 ? null : catchType, ct));
        }
        var frames = new List<FrameInfo>();
        bool hasSmt = false;
        int attrCount = r.U2();
        for (int a = 0; a < attrCount; a++)
        {
            string attrName = cpUtf8(r.U2());
            int len = (int)r.U4();
            int end = r.Pos + len;
            if (attrName == "StackMapTable")
            {
                hasSmt = true;
                frames = ParseStackMapTable(r, code.Length);
            }
            r.Pos = end;
        }
        var instructions = BytecodeDecoder.Decode(code, cp);
        return new MethodModel(name, desc, access, maxStack, maxLocals, code, instructions, exceptions, frames, hasSmt);
    }

    private static string ResolveClass(CpEntry?[] cp, int idx)
    {
        var e = cp[idx];
        if (e is { Tag: 7 })
        {
            int nameIdx = int.Parse(e.Value.TrimStart('#'));
            return cp[nameIdx]?.Value ?? $"#{nameIdx}";
        }
        return $"#{idx}";
    }

    private static readonly string[] VerificationTags =
        { "Top", "Integer", "Float", "Double", "Long", "Null", "UninitializedThis", "Object", "Uninitialized" };

    private static List<FrameInfo> ParseStackMapTable(Reader r, int codeLength)
    {
        var frames = new List<FrameInfo>();
        int count = r.U2();
        int prevPc = -1;
        for (int i = 0; i < count; i++)
        {
            int ft = r.U1();
            int offsetDelta;
            string frameType;
            var locals = new List<string>();
            var stack = new List<string>();
            if (ft <= 63) { frameType = "same"; offsetDelta = ft; }
            else if (ft <= 127)
            {
                frameType = "same_locals_1_stack_item";
                offsetDelta = ft - 64;
                stack.Add(ReadVerificationType(r));
            }
            else if (ft == 247) { frameType = "same_locals_1_stack_item_extended"; offsetDelta = r.U2(); stack.Add(ReadVerificationType(r)); }
            else if (ft is >= 248 and <= 250) { frameType = $"chop({251 - ft})"; offsetDelta = r.U2(); }
            else if (ft == 251) { frameType = "same_extended"; offsetDelta = r.U2(); }
            else if (ft is >= 252 and <= 254)
            {
                frameType = $"append({ft - 251})";
                offsetDelta = r.U2();
                for (int k = 0; k < ft - 251; k++) locals.Add(ReadVerificationType(r));
            }
            else { frameType = "full"; offsetDelta = r.U2(); int nl = r.U2(); for (int k = 0; k < nl; k++) locals.Add(ReadVerificationType(r)); int ns = r.U2(); for (int k = 0; k < ns; k++) stack.Add(ReadVerificationType(r)); }
            int pc = prevPc + offsetDelta + 1;
            prevPc = pc;
            frames.Add(new FrameInfo(pc, offsetDelta, frameType, locals.ToArray(), stack.ToArray(), false, null));
        }
        return frames;
    }

    private static string ReadVerificationType(Reader r)
    {
        int tag = r.U1();
        return tag switch
        {
            <= 6 => VerificationTags[tag],
            7 => $"Object(cp#{r.U2()})",
            8 => $"Uninitialized(offset={r.U2()})",
            _ => $"Unknown({tag})"
        };
    }

    private sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) { _b = b; }
        public byte U1() => _b[Pos++];
        public int U2() { int v = (_b[Pos] << 8) | _b[Pos + 1]; Pos += 2; return v; }
        public uint U4() { uint v = ((uint)_b[Pos] << 24) | ((uint)_b[Pos + 1] << 16) | ((uint)_b[Pos + 2] << 8) | _b[Pos + 3]; Pos += 4; return v; }
        public ulong U8() { ulong hi = U4(); ulong lo = U4(); return (hi << 32) | lo; }
        public byte[] Bytes(int n) { var a = _b[Pos..(Pos + n)]; Pos += n; return a; }
        public void Skip(int n) => Pos += n;
    }
}
