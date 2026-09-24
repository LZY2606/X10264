namespace JvmVerifier.Domain;

/// <summary>常量池项，保留原始 tag 与原始字节区间。</summary>
public sealed record CpEntry(int Index, int Tag, string Kind, string Value, int RawOffset, int RawLength)
{
    public override string ToString() => $"#{Index} {Kind} {Value}";
}

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string CatchType);

public sealed record VerificationTypeInfo(int Tag, string Name, int? CpIndex = null, int? Offset = null);

public sealed record StackMapFrame(
    int Offset,            // 解码后的绝对字节偏移
    int FrameType,         // 原始 frame_type 字节
    string FrameKind,      // same / same_locals_1 / chop / append / full ...
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    int RawOffset,         // 在 StackMapTable 属性内的原始字节位置
    int OffsetDelta);      // 原始 offset_delta（未规范化）

public sealed record CodeAttribute(
    int MaxStack, int MaxLocals,
    byte[] Bytes,                       // 原始指令字节，原样保留
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrame> StackMapFrames,
    bool HasStackMapTableAttribute,     // 属性是否存在（区分“缺失”与“空表”）
    int CodeAttributeOffset);           // Code 属性在 class 文件中的偏移

public sealed record MethodInfo(
    string Name, string Descriptor, int AccessFlags,
    CodeAttribute? Code, int MemberOffset);

public sealed record ClassFile(
    int Minor, int Major,
    IReadOnlyList<CpEntry> ConstantPool,
    string ThisClass, string SuperClass,
    IReadOnlyList<MethodInfo> Methods,
    byte[] RawBytes,
    string ContentHash)
{
    public CpEntry Cp(int index) => ConstantPool.First(e => e.Index == index);
    public string Utf8(int index) => Cp(index).Value;
    public string ClassName(int cpIndex) { var e = Cp(cpIndex); return e.Kind == "Class" ? Utf8(int.Parse(e.Value)) : e.Value; }
}

public static class ClassFileParser
{
    public sealed class ParseDiagnostic
    {
        public required string Id { get; init; }
        public required int Offset { get; init; }
        public required string Message { get; init; }
        public required string Severity { get; init; }
    }

    public static (ClassFile File, List<ParseDiagnostic> Diagnostics) Parse(byte[] raw)
    {
        var diags = new List<ParseDiagnostic>();
        var p = new Reader(raw);
        uint magic = p.U4();
        if (magic != 0xCAFEBABE) throw new InvalidDataException("not a class file");
        int minor = p.U2();
        int major = p.U4() == 0 ? 0 : 0; // placeholder, replaced below
        // rewind: read minor/major properly
        p = new Reader(raw);
        p.U4();
        minor = p.U2();
        major = p.U2();
        int cpCount = p.U2();
        var entries = new List<CpEntry> { new(0, 0, "Reserved", "", 0, 0) };
        int i = 1;
        while (i < cpCount)
        {
            int entryOffset = p.Pos;
            int tag = p.U1();
            switch (tag)
            {
                case 1: { int len = p.U2(); string s = p.Utf(len); entries.Add(new(i, tag, "Utf8", s, entryOffset, p.Pos - entryOffset)); break; }
                case 3: entries.Add(new(i, tag, "Integer", p.I4().ToString(), entryOffset, p.Pos - entryOffset)); break;
                case 4: entries.Add(new(i, tag, "Float", p.I4().ToString("R"), entryOffset, p.Pos - entryOffset)); break;
                case 5: entries.Add(new(i, tag, "Long", p.I8().ToString(), entryOffset, p.Pos - entryOffset)); i++; entries.Add(new(i, 0, "Reserved", "", p.Pos, 0)); break;
                case 6: entries.Add(new(i, tag, "Double", BitConverter.Int64BitsToDouble(p.I8()).ToString("R"), entryOffset, p.Pos - entryOffset)); i++; entries.Add(new(i, 0, "Reserved", "", p.Pos, 0)); break;
                case 7: entries.Add(new(i, tag, "Class", p.U2().ToString(), entryOffset, p.Pos - entryOffset)); break;
                case 8: entries.Add(new(i, tag, "String", p.U2().ToString(), entryOffset, p.Pos - entryOffset)); break;
                case 9: case 10: case 11: { int a = p.U2(); int b = p.U2(); entries.Add(new(i, tag, tag == 9 ? "Fieldref" : tag == 10 ? "Methodref" : "InterfaceMethodref", $"{a}.{b}", entryOffset, p.Pos - entryOffset)); break; }
                case 12: { int a = p.U2(); int b = p.U2(); entries.Add(new(i, tag, "NameAndType", $"{a}.{b}", entryOffset, p.Pos - entryOffset)); break; }
                case 15: { int k = p.U1(); int r = p.U2(); entries.Add(new(i, tag, "MethodHandle", $"{k}.{r}", entryOffset, p.Pos - entryOffset)); break; }
                case 16: entries.Add(new(i, tag, "MethodType", p.U2().ToString(), entryOffset, p.Pos - entryOffset)); break;
                case 17: case 18: { int a = p.U2(); int b = p.U2(); entries.Add(new(i, tag, tag == 17 ? "Dynamic" : "InvokeDynamic", $"{a}.{b}", entryOffset, p.Pos - entryOffset)); break; }
                case 19: case 20: entries.Add(new(i, tag, tag == 19 ? "Module" : "Package", p.U2().ToString(), entryOffset, p.Pos - entryOffset)); break;
                default: throw new InvalidDataException($"unknown cp tag {tag} at {entryOffset}");
            }
            i++;
        }
        var cp = entries;
        string Utf8Of(int idx) => cp.First(e => e.Index == idx).Value;
        string ClassOf(int idx) => Utf8Of(int.Parse(cp.First(e => e.Index == idx).Value));

        p.U2(); // access flags
        int thisClass = p.U2();
        int superClass = p.U2();
        int ifaces = p.U2();
        for (int k = 0; k < ifaces; k++) p.U2();
        int fields = p.U2();
        for (int k = 0; k < fields; k++) SkipMember(p);
        int methods = p.U2();
        var methodList = new List<MethodInfo>();
        for (int k = 0; k < methods; k++)
        {
            int memberOffset = p.Pos;
            int acc = p.U2();
            string name = Utf8Of(p.U2());
            string desc = Utf8Of(p.U2());
            int attrCount = p.U2();
            CodeAttribute? code = null;
            for (int a = 0; a < attrCount; a++)
            {
                string attrName = Utf8Of(p.U2());
                uint len = p.U4();
                int attrStart = p.Pos;
                if (attrName == "Code") code = ParseCode(p, cp, (int)len, attrStart, diags);
                p.Pos = attrStart + (int)len;
            }
            methodList.Add(new(name, desc, acc, code, memberOffset));
        }

        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw))[..16];
        return (new ClassFile(minor, major, cp, ClassOf(thisClass), superClass == 0 ? "java/lang/Object" : ClassOf(superClass), methodList, raw, hash), diags);
    }

    private static void SkipMember(Reader p)
    {
        p.U2(); p.U2(); p.U2();
        int attrs = p.U2();
        for (int a = 0; a < attrs; a++) { p.U2(); uint len = p.U4(); p.Skip((int)len); }
    }

    private static CodeAttribute ParseCode(Reader p, List<CpEntry> cp, int len, int attrStart, List<ParseDiagnostic> diags)
    {
        string Utf8Of(int idx) => cp.First(e => e.Index == idx).Value;
        int maxStack = p.U2();
        int maxLocals = p.U2();
        uint codeLen = p.U4();
        byte[] code = p.Bytes((int)codeLen);
        int exCount = p.U2();
        var handlers = new List<ExceptionHandler>();
        for (int k = 0; k < exCount; k++)
        {
            int s = p.U2(); int e = p.U2(); int h = p.U2(); int c = p.U2();
            handlers.Add(new(s, e, h, c, c == 0 ? "java/lang/Throwable" : Utf8Of(int.Parse(cp.First(x => x.Index == c).Value))));
        }
        var frames = new List<StackMapFrame>();
        bool hasSmt = false;
        int attrs = p.U2();
        for (int a = 0; a < attrs; a++)
        {
            string name = Utf8Of(p.U2());
            uint alen = p.U4();
            int aStart = p.Pos;
            if (name == "StackMapTable")
            {
                hasSmt = true;
                frames = ParseStackMapTable(p, code.Length, aStart, diags);
            }
            p.Pos = aStart + (int)alen;
        }
        return new(maxStack, maxLocals, code, handlers, frames, hasSmt, attrStart);
    }

    private static List<StackMapFrame> ParseStackMapTable(Reader p, int codeLength, int attrStart, List<ParseDiagnostic> diags)
    {
        var frames = new List<StackMapFrame>();
        int count = p.U2();
        int offset = -1;
        for (int k = 0; k < count; k++)
        {
            int rawPos = attrStart + (p.Pos - (attrStart - (attrStart - p.Pos))); // absolute pos
            rawPos = p.Pos;
            int ft = p.U1();
            int delta;
            var locals = new List<VerificationTypeInfo>();
            var stack = new List<VerificationTypeInfo>();
            string kind;
            if (ft <= 63) { delta = ft; kind = "same"; }
            else if (ft <= 127) { delta = ft - 64; kind = "same_locals_1_stack_item"; stack.Add(ReadVti(p)); }
            else if (ft == 247) { delta = p.U2(); kind = "same_locals_1_stack_item_extended"; stack.Add(ReadVti(p)); }
            else if (ft >= 248 && ft <= 250) { delta = p.U2(); kind = $"chop_{251 - ft}"; }
            else if (ft == 251) { delta = p.U2(); kind = "same_extended"; }
            else if (ft >= 252 && ft <= 254) { delta = p.U2(); kind = $"append_{ft - 251}"; for (int j = 0; j < ft - 251; j++) locals.Add(ReadVti(p)); }
            else { delta = p.U2(); kind = "full"; int nl = p.U2(); for (int j = 0; j < nl; j++) locals.Add(ReadVti(p)); int ns = p.U2(); for (int j = 0; j < ns; j++) stack.Add(ReadVti(p)); }
            offset += delta + 1;
            // 损坏帧偏移：目标超出代码范围时保留原位并附诊断，不做修正
            if (offset >= codeLength || offset < 0)
                diags.Add(new ParseDiagnostic { Id = $"diag:frame-offset:{k}", Offset = offset, Severity = "error", Message = $"frame #{k} offset_delta={delta} resolves to {offset}, outside code length {codeLength}; kept in place, not repaired" });
            frames.Add(new(offset, ft, kind, locals, stack, rawPos, delta));
        }
        return frames;
    }

    private static VerificationTypeInfo ReadVti(Reader p)
    {
        int tag = p.U1();
        return tag switch
        {
            0 => new(tag, "Top"),
            1 => new(tag, "Integer"),
            2 => new(tag, "Float"),
            3 => new(tag, "Double"),
            4 => new(tag, "Long"),
            5 => new(tag, "Null"),
            6 => new(tag, "UninitializedThis"),
            7 => new(tag, "Object", CpIndex: p.U2()),
            8 => new(tag, "Uninitialized", Offset: p.U2()),
            _ => new(tag, $"Unknown({tag})"),
        };
    }

    private sealed class Reader(byte[] buf)
    {
        public int Pos;
        public int U1() => buf[Pos++];
        public int U2() { int v = (buf[Pos] << 8) | buf[Pos + 1]; Pos += 2; return v; }
        public uint U4() { uint v = (uint)(buf[Pos] << 24 | buf[Pos + 1] << 16 | buf[Pos + 2] << 8 | buf[Pos + 3]); Pos += 4; return v; }
        public int I4() { int v = buf[Pos] << 24 | buf[Pos + 1] << 16 | buf[Pos + 2] << 8 | buf[Pos + 3]; Pos += 4; return v; }
        public long I8() { long hi = I4(); long lo = I4() & 0xffffffffL; return (hi << 32) | lo; }
        public string Utf(int len) { var s = System.Text.Encoding.UTF8.GetString(buf, Pos, len); Pos += len; return s; }
        public byte[] Bytes(int n) { var b = buf.AsSpan(Pos, n).ToArray(); Pos += n; return b; }
        public void Skip(int n) => Pos += n;
    }
}
