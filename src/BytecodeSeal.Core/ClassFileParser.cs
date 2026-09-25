using System.Security.Cryptography;
using System.Text;

namespace BytecodeSeal.Core;

/// <summary>Parses a .class file into <see cref="ClassFileModel"/>, preserving the
/// original representation (raw hex, constant-pool indices, byte offsets).</summary>
public static class ClassFileParser
{
    public static ClassFileModel Parse(byte[] bytes)
    {
        var r = new Reader(bytes);
        var diagnostics = new List<Diagnostic>();
        uint magic = r.U4();
        if (magic != 0xCAFEBABE) throw new InvalidDataException("not a classfile (bad magic)");
        int minor = r.U2();
        int major = r.U2();
        int cpCount = r.U2();
        var cp = new object?[cpCount]; // decoded values: string for utf8, CpRef tuples
        var entries = new List<CpEntry>();
        for (int i = 1; i < cpCount; i++)
        {
            int tag = r.U1();
            switch (tag)
            {
                case 1: // Utf8
                {
                    int len = r.U2();
                    var raw = r.Bytes(len);
                    var s = Encoding.UTF8.GetString(raw);
                    cp[i] = s;
                    entries.Add(new CpEntry(i, "Utf8", s, Convert.ToHexString(raw), Array.Empty<int>()));
                    break;
                }
                case 3: { var b = r.Bytes(4); cp[i] = BitConverter.ToInt32(Big(b)); entries.Add(new CpEntry(i, "Integer", cp[i]!.ToString()!, Convert.ToHexString(b), Array.Empty<int>())); break; }
                case 4: { var b = r.Bytes(4); cp[i] = BitConverter.ToSingle(Big(b)); entries.Add(new CpEntry(i, "Float", cp[i]!.ToString()!, Convert.ToHexString(b), Array.Empty<int>())); break; }
                case 5: { var b = r.Bytes(8); cp[i] = BitConverter.ToInt64(Big(b)); entries.Add(new CpEntry(i, "Long", cp[i]!.ToString()!, Convert.ToHexString(b), Array.Empty<int>())); i++; break; }
                case 6: { var b = r.Bytes(8); cp[i] = BitConverter.ToDouble(Big(b)); entries.Add(new CpEntry(i, "Double", cp[i]!.ToString()!, Convert.ToHexString(b), Array.Empty<int>())); i++; break; }
                case 7: { int ni = r.U2(); cp[i] = ("Class", ni); entries.Add(new CpEntry(i, "Class", $"#{ni}", $"07{ni:X4}", new[]{ni})); break; }
                case 8: { int si = r.U2(); cp[i] = ("String", si); entries.Add(new CpEntry(i, "String", $"#{si}", $"08{si:X4}", new[]{si})); break; }
                case 9: case 10: case 11:
                {
                    int c = r.U2(); int nt = r.U2();
                    cp[i] = (tag == 9 ? "Fieldref" : tag == 10 ? "Methodref" : "InterfaceMethodref", c, nt);
                    entries.Add(new CpEntry(i, ((ValueTuple<string, int, int>)cp[i]!).Item1, $"#{c}.#{nt}", $"{tag:X2}{c:X4}{nt:X4}", new[]{c, nt}));
                    break;
                }
                case 12: { int n = r.U2(); int d = r.U2(); cp[i] = ("NameAndType", n, d); entries.Add(new CpEntry(i, "NameAndType", $"#{n}.#{d}", $"0C{n:X4}{d:X4}", new[]{n, d})); break; }
                case 15: { int k = r.U1(); int ri = r.U2(); cp[i] = ("MethodHandle", k, ri); entries.Add(new CpEntry(i, "MethodHandle", $"{k}/#{ri}", $"0F{k:X2}{ri:X4}", new[]{ri})); break; }
                case 16: { int d = r.U2(); cp[i] = ("MethodType", d); entries.Add(new CpEntry(i, "MethodType", $"#{d}", $"10{d:X4}", new[]{d})); break; }
                case 17: case 18: { int b2 = r.U2(); int nt = r.U2(); cp[i] = (tag == 17 ? "Dynamic" : "InvokeDynamic", b2, nt); entries.Add(new CpEntry(i, tag == 17 ? "Dynamic" : "InvokeDynamic", $"#{b2}.#{nt}", $"{tag:X2}{b2:X4}{nt:X4}", new[]{b2, nt})); break; }
                case 19: case 20: { int n = r.U2(); cp[i] = (tag == 19 ? "Module" : "Package", n); entries.Add(new CpEntry(i, tag == 19 ? "Module" : "Package", $"#{n}", $"{tag:X2}{n:X4}", new[]{n})); break; }
                default:
                    diagnostics.Add(new Diagnostic($"cp#{i}", $"constant_pool[{i}]", $"unsupported constant-pool tag {tag}; entry left unparsed"));
                    throw new InvalidDataException($"unsupported constant pool tag {tag} at #{i}");
            }
        }

        string CpUtf8(int idx) => idx > 0 && idx < cp.Length && cp[idx] is string s ? s : $"#bad:{idx}";
        string ClassName(int idx)
        {
            if (idx == 0) return "java/lang/Throwable";
            if (idx < cp.Length && cp[idx] is ValueTuple<string, int> t && t.Item1 == "Class") return CpUtf8(t.Item2);
            diagnostics.Add(new Diagnostic($"cp#{idx}", $"constant_pool[{idx}]", "class index does not resolve; kept as raw index"));
            return $"#bad:{idx}";
        }

        r.U2(); // access flags
        string thisClass = ClassName(r.U2());
        string superClass = ClassName(r.U2());
        int ifCount = r.U2();
        for (int i = 0; i < ifCount; i++) r.U2();
        SkipMembers(r); // fields

        var methods = new List<MethodModel>();
        int mCount = r.U2();
        for (int m = 0; m < mCount; m++)
        {
            int acc = r.U2();
            string name = CpUtf8(r.U2());
            string desc = CpUtf8(r.U2());
            int aCount = r.U2();
            MethodModel? model = null;
            for (int a = 0; a < aCount; a++)
            {
                string aName = CpUtf8(r.U2());
                int aLen = (int)r.U4();
                if (aName == "Code")
                {
                    int maxStack = r.U2();
                    int maxLocals = r.U2();
                    int codeLen = (int)r.U4();
                    var code = r.Bytes(codeLen);
                    int exCount = r.U2();
                    var handlers = new List<ExceptionHandler>();
                    for (int e = 0; e < exCount; e++)
                    {
                        int sp = r.U2(); int ep = r.U2(); int hp = r.U2(); int ct = r.U2();
                        handlers.Add(new ExceptionHandler(sp, ep, hp, ct, ClassName(ct)));
                    }
                    var frames = new List<StackMapFrame>();
                    int caCount = r.U2();
                    for (int ca = 0; ca < caCount; ca++)
                    {
                        string caName = CpUtf8(r.U2());
                        int caLen = (int)r.U4();
                        if (caName == "StackMapTable")
                        {
                            var end = r.Pos + caLen;
                            int nFrames = r.U2();
                            int prev = -1;
                            for (int f = 0; f < nFrames; f++)
                            {
                                int ft = r.U1();
                                int offsetDelta; string kind;
                                var locals = new List<VerificationTypeInfo>();
                                var stack = new List<VerificationTypeInfo>();
                                if (ft <= 63) { offsetDelta = ft; kind = "same"; }
                                else if (ft <= 127) { offsetDelta = ft - 64; kind = "same_locals_1_stack"; stack.Add(Vti(r, ClassName)); }
                                else if (ft == 247) { offsetDelta = r.U2(); kind = "same_locals_1_stack_ext"; stack.Add(Vti(r, ClassName)); }
                                else if (ft >= 248 && ft <= 250) { offsetDelta = r.U2(); kind = $"chop_{251 - ft}"; }
                                else if (ft == 251) { offsetDelta = r.U2(); kind = "same_ext"; }
                                else if (ft <= 254)
                                {
                                    offsetDelta = r.U2(); kind = $"append_{ft - 251}";
                                    for (int k = 0; k < ft - 251; k++) locals.Add(Vti(r, ClassName));
                                }
                                else
                                {
                                    offsetDelta = r.U2(); kind = "full";
                                    int nl = r.U2();
                                    for (int k = 0; k < nl; k++) locals.Add(Vti(r, ClassName));
                                    int ns = r.U2();
                                    for (int k = 0; k < ns; k++) stack.Add(Vti(r, ClassName));
                                }
                                int abs = prev + offsetDelta + 1;
                                prev = abs;
                                frames.Add(new StackMapFrame(abs, offsetDelta, kind, locals, stack));
                            }
                            r.Pos = end;
                        }
                        else r.Pos += caLen;
                    }
                    model = new MethodModel(name, desc, acc, maxStack, maxLocals,
                        Convert.ToHexString(code), handlers, frames);
                }
                else r.Pos += aLen;
            }
            if (model != null) methods.Add(model);
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];
        return new ClassFileModel(minor, major, thisClass, superClass, entries, methods, diagnostics, hash);
    }

    private static VerificationTypeInfo Vti(Reader r, Func<int, string> className)
    {
        int tag = r.U1();
        return tag switch
        {
            0 => new("top", null, null, null),
            1 => new("int", null, null, null),
            2 => new("float", null, null, null),
            3 => new("long", null, null, null),
            4 => new("double", null, null, null),
            5 => new("null", null, null, null),
            6 => new("uninitializedThis", null, null, null),
            7 => ReadObject(r, className),
            8 => new("uninitialized", null, null, r.U2()),
            _ => new($"bad:{tag}", null, null, null),
        };
    }

    private static VerificationTypeInfo ReadObject(Reader r, Func<int, string> className)
    {
        int idx = r.U2();
        return new("object", idx, className(idx), null);
    }

    private static byte[] Big(byte[] b) { Array.Reverse(b); return b; }

    private static void SkipMembers(Reader r)
    {
        int count = r.U2();
        for (int i = 0; i < count; i++)
        {
            r.U2(); r.U2(); r.U2();
            int aCount = r.U2();
            for (int a = 0; a < aCount; a++) { r.U2(); int len = (int)r.U4(); r.Pos += len; }
        }
    }

    public sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) { _b = b; }
        public int U1() => _b[Pos++];
        public int U2() { int v = (_b[Pos] << 8) | _b[Pos + 1]; Pos += 2; return v; }
        public uint U4() { uint v = (uint)(_b[Pos] << 24 | _b[Pos + 1] << 16 | _b[Pos + 2] << 8 | _b[Pos + 3]); Pos += 4; return v; }
        public byte[] Bytes(int n) { var b = _b.AsSpan(Pos, n).ToArray(); Pos += n; return b; }
    }
}
