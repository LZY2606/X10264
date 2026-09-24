using System.Text;

namespace JvmVerify.Core;

/// <summary>One constant-pool entry. The original index and raw tag are preserved
/// exactly as imported; display text is derived, never the other way round.</summary>
public sealed record CpEntry(int Index, byte Tag, string TagName, string Display);

public sealed record ExceptionHandler(ushort StartPc, ushort EndPc, ushort HandlerPc, ushort CatchType);

public sealed record StackMapFrame(
    int FrameIndex, int OffsetDelta, int Offset, int FrameType,
    List<VType> Locals, List<VType> Stack, bool OffsetValid);

public sealed class CodeAttribute
{
    public required ushort MaxStack { get; init; }
    public required ushort MaxLocals { get; init; }
    public required byte[] Bytes { get; init; }              // original instruction bytes, offsets intact
    public required List<ExceptionHandler> ExceptionTable { get; init; }
    public required List<StackMapFrame> Frames { get; init; }
    public bool HasStackMapTable { get; init; }
}

public sealed class MethodInfo
{
    public required string Name { get; init; }
    public required string Descriptor { get; init; }
    public required ushort AccessFlags { get; init; }
    public CodeAttribute? Code { get; set; }
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
}

public sealed class ClassFile
{
    public required ushort Minor { get; init; }
    public required ushort Major { get; init; }
    public required List<CpEntry?> ConstantPool { get; init; } // 1-based; index 0 and long/double padding slots are null
    public required string ThisClass { get; init; }
    public required List<MethodInfo> Methods { get; init; }
    public required byte[] OriginalBytes { get; init; }        // raw import, byte-for-byte

    public string CpClassName(int index)
    {
        var e = ConstantPool[index] ?? throw new FormatException($"cp#{index} empty");
        if (e.Tag != 7) throw new FormatException($"cp#{index} is {e.TagName}, not Class");
        return e.Display;
    }

    public (string Owner, string Name, string Desc) CpMethodRef(int index)
    {
        var e = ConstantPool[index] ?? throw new FormatException($"cp#{index} empty");
        if (e.Tag is not (10 or 11)) throw new FormatException($"cp#{index} is {e.TagName}, not Methodref");
        var parts = e.Display.Split('|');
        return (parts[0], parts[1], parts[2]);
    }

    public (string Owner, string Name, string Desc) CpFieldRef(int index)
    {
        var e = ConstantPool[index] ?? throw new FormatException($"cp#{index} empty");
        if (e.Tag != 9) throw new FormatException($"cp#{index} is {e.TagName}, not Fieldref");
        var parts = e.Display.Split('|');
        return (parts[0], parts[1], parts[2]);
    }

    public string CpUtf8(int index) => (ConstantPool[index] ?? throw new FormatException($"cp#{index} empty")).Display;

    public static ClassFile Parse(byte[] bytes)
    {
        var r = new Reader(bytes);
        if (r.U4() != 0xCAFEBABE) throw new FormatException("bad magic");
        var minor = r.U2();
        var major = r.U2();
        var cpCount = r.U2();
        var cp = new List<CpEntry?> { null };
        // raw entries kept as (tag, a, b) so references can be resolved in a second pass
        var raw = new List<(byte tag, int a, int b, string text)?> { null };
        for (var i = 1; i < cpCount; i++)
        {
            var tag = r.U1();
            switch (tag)
            {
                case 1: { var len = r.U2(); var s = Encoding.UTF8.GetString(r.Bytes(len)); raw.Add((tag, 0, 0, s)); break; }
                case 7: case 8: case 16: case 19: case 20: { var a = r.U2(); raw.Add((tag, a, 0, "")); break; }
                case 9: case 10: case 11: case 12: case 17: case 18: { var a = r.U2(); var b = r.U2(); raw.Add((tag, a, b, "")); break; }
                case 15: { var a = r.U1(); var b = r.U2(); raw.Add((tag, a, b, "")); break; }
                case 3: case 4: { r.U4(); raw.Add((tag, 0, 0, $"<{tag}>")); break; }
                case 5: case 6: { r.U4(); r.U4(); raw.Add((tag, 0, 0, $"<{(tag == 5 ? "long" : "double")}>")); cp.Add(null); raw.Add(null); i++; break; }
                default: throw new FormatException($"unknown cp tag {tag}");
            }
            cp.Add(null); // placeholder, resolved below
        }
        string Utf8(int idx) => raw[idx]!.Value.text;
        string ClassName(int idx) => Utf8(raw[idx]!.Value.a);
        for (var i = 1; i < cp.Count; i++)
        {
            var e = raw[i];
            if (e == null) { cp[i] = null; continue; }
            var (tag, a, b, text) = e.Value;
            cp[i] = tag switch
            {
                1 => new CpEntry(i, tag, "Utf8", text),
                7 => new CpEntry(i, tag, "Class", Utf8(a)),
                8 => new CpEntry(i, tag, "String", Utf8(a)),
                9 => new CpEntry(i, tag, "Fieldref", Ref(a, b)),
                10 => new CpEntry(i, tag, "Methodref", Ref(a, b)),
                11 => new CpEntry(i, tag, "InterfaceMethodref", Ref(a, b)),
                12 => new CpEntry(i, tag, "NameAndType", $"{Utf8(a)}:{Utf8(b)}"),
                3 => new CpEntry(i, tag, "Integer", "<int>"),
                4 => new CpEntry(i, tag, "Float", "<float>"),
                5 => new CpEntry(i, tag, "Long", "<long>"),
                6 => new CpEntry(i, tag, "Double", "<double>"),
                15 => new CpEntry(i, tag, "MethodHandle", $"kind={a} ref=#{b}"),
                16 => new CpEntry(i, tag, "MethodType", Utf8(a)),
                17 or 18 => new CpEntry(i, tag, tag == 17 ? "Dynamic" : "InvokeDynamic", $"#{a} #{b}"),
                19 => new CpEntry(i, tag, "Module", Utf8(a)),
                20 => new CpEntry(i, tag, "Package", Utf8(a)),
                _ => throw new FormatException($"tag {tag}")
            };
        }
        string Ref(int cls, int nt)
        {
            var ntE = raw[nt]!.Value;
            return $"{ClassName(cls)}|{Utf8(ntE.a)}|{Utf8(ntE.b)}";
        }

        r.U2(); // access flags
        var thisClass = ClassName(r.U2());
        r.U2(); // super
        var ifaces = r.U2();
        for (var i = 0; i < ifaces; i++) r.U2();
        SkipMembers(r); // fields
        var methods = new List<MethodInfo>();
        var mCount = r.U2();
        for (var i = 0; i < mCount; i++)
        {
            var acc = r.U2();
            var name = Utf8(r.U2());
            var desc = Utf8(r.U2());
            var mi = new MethodInfo { Name = name, Descriptor = desc, AccessFlags = acc };
            var aCount = r.U2();
            for (var j = 0; j < aCount; j++)
            {
                var aName = Utf8(r.U2());
                var len = (int)r.U4();
                if (aName == "Code")
                {
                    var end = r.Pos + len;
                    var maxStack = r.U2();
                    var maxLocals = r.U2();
                    var codeLen = (int)r.U4();
                    var code = r.Bytes(codeLen);
                    var exCount = r.U2();
                    var ex = new List<ExceptionHandler>();
                    for (var k = 0; k < exCount; k++)
                        ex.Add(new ExceptionHandler(r.U2(), r.U2(), r.U2(), r.U2()));
                    var frames = new List<StackMapFrame>();
                    var hasSmt = false;
                    var subCount = r.U2();
                    for (var k = 0; k < subCount; k++)
                    {
                        var sName = Utf8(r.U2());
                        var sLen = (int)r.U4();
                        if (sName == "StackMapTable")
                        {
                            hasSmt = true;
                            frames = ParseStackMapTable(r.Bytes(sLen), code.Length, cp);
                        }
                        else r.Skip(sLen);
                    }
                    r.Pos = end;
                    mi.Code = new CodeAttribute
                    {
                        MaxStack = maxStack, MaxLocals = maxLocals, Bytes = code,
                        ExceptionTable = ex, Frames = frames, HasStackMapTable = hasSmt
                    };
                }
                else r.Skip(len);
            }
            methods.Add(mi);
        }
        return new ClassFile
        {
            Minor = minor, Major = major, ConstantPool = cp, ThisClass = thisClass,
            Methods = methods, OriginalBytes = bytes
        };
    }

    private static void SkipMembers(Reader r)
    {
        var n = r.U2();
        for (var i = 0; i < n; i++)
        {
            r.U2(); r.U2(); r.U2();
            var a = r.U2();
            for (var j = 0; j < a; j++) { r.U2(); r.Skip((int)r.U4()); }
        }
    }

    private static List<StackMapFrame> ParseStackMapTable(byte[] data, int codeLength, List<CpEntry?> cp)
    {
        var r = new Reader(data);
        var count = r.U2();
        var frames = new List<StackMapFrame>();
        var offset = -1;
        var prevLocals = new List<VType>();
        for (var fi = 0; fi < count; fi++)
        {
            var ft = r.U1();
            int delta; var locals = new List<VType>(prevLocals); var stack = new List<VType>();
            if (ft <= 63) { delta = ft; }
            else if (ft <= 127) { delta = ft - 64; stack.Add(ReadVti(r, cp)); }
            else if (ft == 247) { delta = r.U2(); stack.Add(ReadVti(r, cp)); }
            else if (ft >= 248 && ft <= 250) { delta = r.U2(); locals.RemoveRange(locals.Count - (251 - ft), 251 - ft); }
            else if (ft == 251) { delta = r.U2(); }
            else if (ft >= 252 && ft <= 254) { delta = r.U2(); for (var k = 251; k <= ft; k++) locals.Add(ReadVti(r, cp)); }
            else { delta = r.U2(); var nl = r.U2(); locals = new List<VType>(); for (var k = 0; k < nl; k++) locals.Add(ReadVti(r, cp)); var ns = r.U2(); for (var k = 0; k < ns; k++) stack.Add(ReadVti(r, cp)); }
            offset += delta + 1;
            // A frame offset is valid only if it stays inside the code array; boundary
            // checks against the decoded instruction stream happen in the verifier.
            frames.Add(new StackMapFrame(fi, delta, offset, ft, locals, stack, offset < codeLength));
            prevLocals = locals;
        }
        return frames;
    }

    private static VType ReadVti(Reader r, List<CpEntry?> cp)
    {
        var tag = r.U1();
        return tag switch
        {
            0 => VType.Top,
            1 => VType.Int,
            2 => VType.Float,
            3 => VType.Double,
            4 => VType.Long,
            5 => VType.Null,
            6 => VType.UninitThisOf("?"),
            7 => VType.ObjectOf((cp[r.U2()] ?? throw new FormatException("bad cp in frame")).Display),
            8 => VType.UninitAt(r.U2(), "?"),
            _ => throw new FormatException($"bad verification_type_info tag {tag}")
        };
    }

    private sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) { _b = b; }
        public byte U1() => _b[Pos++];
        public ushort U2() => (ushort)(_b[Pos++] << 8 | _b[Pos++]);
        public uint U4() => (uint)(_b[Pos++] << 24 | _b[Pos++] << 16 | _b[Pos++] << 8 | _b[Pos++]);
        public byte[] Bytes(int n) { var a = new byte[n]; Array.Copy(_b, Pos, a, 0, n); Pos += n; return a; }
        public void Skip(int n) => Pos += n;
    }
}
