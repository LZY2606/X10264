using JvmVerifier.Core.Raw;

namespace JvmVerifier.Core.Binary;

/// <summary>
/// Minimal JVMS 4.1 class file reader focused on verifier inputs: version,
/// constant pool (verbatim), the Code attribute (instructions, exception
/// table, StackMapTable) and class hierarchy facts. Unknown attributes are
/// skipped by length but their raw presence is noted.
/// </summary>
public sealed class ClassFileReader
{
    private readonly byte[] _b;
    private int _p;
    private readonly List<RawDiagnostic> _diags = new();

    private ClassFileReader(byte[] bytes) { _b = bytes; }

    public static RawImport Read(byte[] bytes, string origin)
    {
        var r = new ClassFileReader(bytes);
        var cf = r.ReadClass(origin);
        return new RawImport { ClassFile = cf }.With(r._diags);
    }

    private byte U1() => _b[_p++];
    private int U2() { int v = (_b[_p] << 8) | _b[_p + 1]; _p += 2; return v; }
    private int S2() => (short)U2();
    private int U4() { int v = (_b[_p] << 24) | (_b[_p + 1] << 16) | (_b[_p + 2] << 8) | _b[_p + 3]; _p += 4; return v; }
    private long U8() { long hi = (uint)U4(); long lo = (uint)U4(); return (hi << 32) | lo; }

    private byte[] Take(int n) { var r = new byte[n]; Array.Copy(_b, _p, r, 0, n); _p += n; return r; }

    private RawClassFile ReadClass(string origin)
    {
        int magic = U4();
        if (magic != unchecked((int)0xCAFEBABE))
            _diags.Add(new RawDiagnostic("bad-magic", "classfile", $"magic 0x{magic:x8} is not CAFEBABE"));
        int minor = U2();
        int major = U2();

        var cp = ReadConstantPool();
        int accessFlags = U2();
        int thisIndex = U2();
        int superIndex = U2();

        var facts = new List<RawClassFact>();
        string thisName = cp.ClassName(thisIndex, _diags) ?? $"cp#{thisIndex}";
        string? superName = superIndex == 0 ? null : cp.ClassName(superIndex, _diags);

        int interfaceCount = U2();
        var ifaces = new List<string>();
        for (int i = 0; i < interfaceCount; i++)
        {
            var n = cp.ClassName(U2(), _diags);
            if (n is not null) ifaces.Add(n);
        }
        facts.Add(new RawClassFact { InternalName = thisName, SuperName = superName, Interfaces = ifaces });
        if (superName is not null)
            facts.Add(new RawClassFact { InternalName = superName, SuperName = "java/lang/Object" });

        int fieldCount = U2();
        for (int i = 0; i < fieldCount; i++) SkipMember();

        int methodCount = U2();
        var methods = new List<RawMethod>();
        for (int i = 0; i < methodCount; i++)
        {
            int mAccess = U2();
            int nameIdx = U2();
            int descIdx = U2();
            var m = ReadMethod(cp, mAccess, nameIdx, descIdx);
            if (m is not null) methods.Add(m);
        }

        return new RawClassFile
        {
            Origin = origin,
            MajorVersion = new Coords(major, DomainUnit.ClassfileMajorVersion),
            MinorVersion = new Coords(minor, DomainUnit.ClassfileMinorVersion),
            ConstantPool = cp,
            Methods = methods,
            ClassFacts = facts,
            ThisClassName = thisName,
            SuperClassName = superName,
            AccessFlags = accessFlags,
        };
    }

    private void SkipMember()
    {
        U2(); U2(); U2(); // access, name, desc
        int attrs = U2();
        for (int i = 0; i < attrs; i++) { U2(); int len = U4(); _p += len; }
    }

    private ConstantPool ReadConstantPool()
    {
        int count = U2();
        var cp = new ConstantPool { Count = count };
        cp.Entries.Add(new CpEntry { Index = 0 });
        for (int i = 1; i < count; i++)
        {
            int tag = U1();
            var e = new CpEntry { Index = i, Tag = (CpTag)tag };
            int save = _p;
            switch (tag)
            {
                case 1:
                {
                    int len = U2();
                    var bytes = Take(len);
                    e.RawBytes = bytes;
                    e.Utf8 = DecodeModifiedUtf8(bytes);
                    break;
                }
                case 3: e.RawIntegerBits = U4(); break;
                case 4: { var f = Take(4); e.RawBytes = f; e.RawFloatValue = BitConverter.Int32BitsToSingle(BitConverter.IsLittleEndian ? BitConverter.ToInt32(new[] { f[3], f[2], f[1], f[0] }, 0) : BitConverter.ToInt32(f, 0)); break; }
                case 5: e.RawLongBits = U8(); cp.Entries.Add(e); i++; cp.Entries.Add(new CpEntry { Index = i, IsSecondHalfOfWide = true }); break;
                case 6: { var h = Take(4); var l = Take(4); e.RawBytes = h.Concat(l).ToArray(); long bits = BitConverter.IsLittleEndian ? BitConverter.DoubleToInt64Bits(BitConverter.ToDouble(new byte[] { l[3], l[2], l[1], l[0], h[3], h[2], h[1], h[0] }, 0)) : 0; e.RawDoubleValue = BitConverter.Int64BitsToDouble(bits); cp.Entries.Add(e); i++; cp.Entries.Add(new CpEntry { Index = i, IsSecondHalfOfWide = true }); break; }
                case 7: e.NameIndex = U2(); break;
                case 8: e.StringIndex = U2(); break;
                case 9: case 10: case 11: case 17: case 18:
                    e.ClassIndex = U2(); e.NameAndTypeIndex = U2();
                    if (tag is 17 or 18) e.BootstrapMethodAttrIndex = e.ClassIndex;
                    break;
                case 12: e.NameIndex = U2(); e.DescriptorIndex = U2(); break;
                case 15: e.ReferenceKind = U1(); e.ReferenceIndex = U2(); break;
                case 16: e.DescriptorIndex = U2(); break;
                case 19: case 20: e.NameIndex = U2(); break;
                default:
                    _diags.Add(new RawDiagnostic("unknown-cp-tag", $"cp#{i}", $"unknown constant pool tag {tag}", $"cp#{i}"));
                    break;
            }
            e.RawBytes ??= _b[save.._p];
            cp.Entries.Add(e);
        }
        return cp;
    }

    internal static string DecodeModifiedUtf8(byte[] bytes)
    {
        // Modified UTF-8 differs only around null and supplementary chars;
        // standard UTF8 decoding covers the fixture corpus, and supplementary
        // pairs are decoded via the 0xED 0xA0.. path below.
        var sb = new System.Text.StringBuilder(bytes.Length);
        int k = 0;
        while (k < bytes.Length)
        {
            byte c = bytes[k++];
            if (c < 0x80) sb.Append((char)c);
            else if ((c & 0xE0) == 0xC0)
            {
                int u = ((c & 0x1F) << 6) | (bytes[k++] & 0x3F);
                sb.Append((char)u);
            }
            else
            {
                int u = ((c & 0x0F) << 12) | ((bytes[k] & 0x3F) << 6) | (bytes[k + 1] & 0x3F);
                k += 2;
                if (u >= 0xD800 && u <= 0xDBFF && k + 2 < bytes.Length && bytes[k] == 0xED)
                {
                    int lo = ((bytes[k + 1] & 0x0F) << 12) | ((bytes[k + 2] & 0x3F) << 6) | (bytes[k + 3] & 0x3F);
                    // encoded as 0xED 0xA0.. 0x80..
                    int cp2 = 0x10000 + ((u - 0xD800) << 10) + (lo - 0xDC00);
                    sb.Append(char.ConvertFromUtf32(cp2));
                    k += 4;
                    continue;
                }
                sb.Append((char)u);
            }
        }
        return sb.ToString();
    }

    private RawMethod? ReadMethod(ConstantPool cp, int access, int nameIdx, int descIdx)
    {
        string name = cp.Utf8Text(nameIdx, _diags) ?? $"cp#{nameIdx}";
        string desc = cp.Utf8Text(descIdx, _diags) ?? $"cp#{descIdx}";
        int attrCount = U2();
        for (int a = 0; a < attrCount; a++)
        {
            int attrNameIdx = U2();
            int attrLen = U4();
            int attrStart = _p;
            string? attrName = cp.Utf8Text(attrNameIdx);
            if (attrName == "Code")
            {
                var m = ReadCodeAttribute(cp, access, name, desc, attrStart);
                _p = attrStart + attrLen;
                return m;
            }
            _p = attrStart + attrLen;
        }
        // Abstract/native methods have no Code.
        return null;
    }

    private RawMethod ReadCodeAttribute(ConstantPool cp, int access, string name, string desc, int attrOffset)
    {
        int maxStack = U2();
        int maxLocals = U2();
        int codeLength = U4();
        byte[] code = Take(codeLength);
        int codeStart = _p - codeLength;

        var decoder = new CodeDecoder(code, _diags, $"m:{name}{desc}");
        var insns = decoder.Decode();

        int exCount = U2();
        var handlers = new List<RawExceptionHandler>();
        for (int i = 0; i < exCount; i++)
        {
            int s = U2(), e = U2(), h = U2(), t = U2();
            handlers.Add(new RawExceptionHandler
            {
                Id = $"exn:{name}{desc}#h{i}",
                StartPc = new Coords(s, DomainUnit.ByteOffsetInCode),
                EndPc = new Coords(e, DomainUnit.ByteOffsetInCode),
                HandlerPc = new Coords(h, DomainUnit.ByteOffsetInCode),
                CatchTypeCpIndex = t == 0 ? null : t,
            });
        }

        var frames = new List<RawStackMapFrame>();
        int codeAttrs = U2();
        for (int i = 0; i < codeAttrs; i++)
        {
            int anIdx = U2();
            int aLen = U4();
            int aStart = _p;
            if (cp.Utf8Text(anIdx) == "StackMapTable")
                frames = ReadStackMapTable(cp);
            _p = aStart + aLen;
        }

        return new RawMethod
        {
            Id = $"m:{name}{desc}",
            Name = name,
            Descriptor = desc,
            AccessFlags = access,
            MaxStack = maxStack,
            MaxLocals = maxLocals,
            CodeLength = codeLength,
            CodeAttributeOffset = new Coords(attrOffset, DomainUnit.AttributeByteOffset),
            Instructions = insns,
            ExceptionTable = handlers,
            StackMapTable = frames,
        };
    }

    private List<RawStackMapFrame> ReadStackMapTable(ConstantPool cp)
    {
        var frames = new List<RawStackMapFrame>();
        int n = U2();
        int prevPc = -1;
        for (int fi = 0; fi < n; fi++)
        {
            int frameType = U1();
            int offsetDelta;
            var locals = new List<RawVerificationTypeInfo>();
            var stack = new List<RawVerificationTypeInfo>();

            if (frameType <= 63)
            {
                offsetDelta = frameType;
            }
            else if (frameType <= 127)
            {
                offsetDelta = frameType - 64;
                stack.Add(ReadVerificationType(cp, fi, 0));
            }
            else if (frameType == 247)
            {
                offsetDelta = U2();
                stack.Add(ReadVerificationType(cp, fi, 0));
            }
            else if (frameType >= 248 && frameType <= 250)
            {
                offsetDelta = U2();
            }
            else if (frameType == 251)
            {
                offsetDelta = U2();
            }
            else if (frameType >= 252 && frameType <= 254)
            {
                offsetDelta = U2();
                int count = frameType - 251;
                for (int k = 0; k < count; k++) locals.Add(ReadVerificationType(cp, fi, k));
            }
            else // 255 full_frame
            {
                offsetDelta = U2();
                int nLocal = U2();
                for (int k = 0; k < nLocal; k++) locals.Add(ReadVerificationType(cp, fi, k));
                int nStack = U2();
                for (int k = 0; k < nStack; k++) stack.Add(ReadVerificationType(cp, fi, k));
            }

            int absolutePc = frameType == 247 || frameType >= 248
                ? prevPc + 1 + offsetDelta
                : (frames.Count == 0 ? offsetDelta : prevPc + 1 + offsetDelta);
            // Frame types 0..63 have implicit delta equal to frameType.
            prevPc = absolutePc;
            frames.Add(new RawStackMapFrame
            {
                Index = fi,
                FrameType = frameType,
                RawOffsetDelta = offsetDelta,
                AbsolutePc = absolutePc,
                Locals = locals,
                Stack = stack,
                Id = $"frame[{fi}]",
            });
        }
        return frames;
    }

    private RawVerificationTypeInfo ReadVerificationType(ConstantPool cp, int fi, int vi)
    {
        int tag = U1();
        var vtag = (VerificationTypeInfoTag)tag;
        var v = new RawVerificationTypeInfo { Tag = vtag, Id = $"frame[{fi}].v[{vi}]" };
        switch (tag)
        {
            case 7: v.CpoolIndex = U2(); break;
            case 8:
            {
                int off = U2();
                v.Offset = new Coords(off, DomainUnit.ByteOffsetInCode);
                break;
            }
            default:
                if (tag > 8)
                    _diags.Add(new RawDiagnostic("bad-verification-type", $"frame[{fi}].v[{vi}]", $"unknown verification_type_info tag {tag}"));
                break;
        }
        return v;
    }
}

internal static class RawImportExtensions
{
    public static RawImport With(this RawImport import, List<RawDiagnostic> more)
    {
        import.Diagnostics.AddRange(more);
        return import;
    }
}
