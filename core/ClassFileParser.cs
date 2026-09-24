using System.Security.Cryptography;
using System.Text;

namespace JvmVerify.Core;

/// <summary>Parses a classfile, preserving raw offsets, constant pool entries,
/// descriptors, branch targets, exception ranges, stack map frames and the
/// classfile version exactly as found.</summary>
public static class ClassFileParser
{
    public const uint ExpectedMagic = 0xCAFEBABE;

    private sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) => _b = b;
        public byte U1() => _b[Pos++];
        public ushort U2() { var v = (ushort)((_b[Pos] << 8) | _b[Pos + 1]); Pos += 2; return v; }
        public uint U4() { var v = ((uint)_b[Pos] << 24) | ((uint)_b[Pos + 1] << 16) | ((uint)_b[Pos + 2] << 8) | _b[Pos + 3]; Pos += 4; return v; }
        public byte[] Bytes(int n) { var r = _b[Pos..(Pos + n)]; Pos += n; return r; }
        public byte[] Slice(int start, int len) => _b[start..(start + len)];
        public bool End => Pos >= _b.Length;
    }

    public static ParsedClassFile Parse(byte[] bytes)
    {
        var diagnostics = new List<Diagnostic>();
        var r = new Reader(bytes);
        var magic = r.U4();
        if (magic != ExpectedMagic)
            diagnostics.Add(new Diagnostic("BAD-MAGIC", "classfile magic mismatch", $"0x{magic:X8}", "classfile"));
        var minor = r.U2();
        var major = r.U2();
        var version = new ClassFileVersion(major, minor);

        var cpCount = r.U2();
        var cp = new CpEntry?[cpCount];
        for (var i = 1; i < cpCount; i++)
        {
            var tag = r.U1();
            switch (tag)
            {
                case 1: { var len = r.U2(); cp[i] = new CpEntry.Utf8(Encoding.UTF8.GetString(r.Bytes(len))); break; }
                case 3: cp[i] = new CpEntry.IntConst((int)r.U4()); break;
                case 4: cp[i] = new CpEntry.FloatConst(BitConverter.Int32BitsToSingle((int)r.U4())); break;
                case 5: { var hi = (long)r.U4(); cp[i] = new CpEntry.LongConst((hi << 32) | r.U4()); i++; break; }
                case 6: { var hi = (long)r.U4(); cp[i] = new CpEntry.DoubleConst(BitConverter.Int64BitsToDouble((hi << 32) | r.U4())); i++; break; }
                case 7: cp[i] = new CpEntry.ClassRef(r.U2()); break;
                case 8: cp[i] = new CpEntry.StringRef(r.U2()); break;
                case 9: cp[i] = new CpEntry.FieldRef(r.U2(), r.U2()); break;
                case 10: cp[i] = new CpEntry.MethodRef(r.U2(), r.U2()); break;
                case 11: cp[i] = new CpEntry.InterfaceMethodRef(r.U2(), r.U2()); break;
                case 12: cp[i] = new CpEntry.NameAndType(r.U2(), r.U2()); break;
                case 15: cp[i] = new CpEntry.MethodHandle(r.U1(), r.U2()); break;
                case 16: cp[i] = new CpEntry.MethodType(r.U2()); break;
                case 18: cp[i] = new CpEntry.InvokeDynamic(r.U2(), r.U2()); break;
                default:
                    diagnostics.Add(new Diagnostic("CP-UNKNOWN-TAG", $"unknown constant pool tag {tag} at index {i}", tag.ToString(), "constant-pool"));
                    cp[i] = new CpEntry.Raw(tag, Array.Empty<byte>());
                    i = cpCount; // cannot continue walking the pool safely
                    break;
            }
        }

        string CpUtf8(ushort idx) =>
            idx < cp.Length && cp[idx] is CpEntry.Utf8 u ? u.Value : $"<bad-utf8-ref:{idx}>";
        string CpClass(ushort idx) =>
            idx < cp.Length && cp[idx] is CpEntry.ClassRef c ? CpUtf8(c.NameIndex) : $"<bad-class-ref:{idx}>";

        r.U2(); // access flags
        var thisClass = CpClass(r.U2());
        var superIdx = r.U2();
        var superClass = superIdx == 0 ? null : CpClass(superIdx);

        var ifaceCount = r.U2();
        for (var i = 0; i < ifaceCount; i++) r.U2();
        SkipMembers(r); // interfaces already consumed; fields
        SkipMembers(r); // (kept symmetric: fields)

        var methodCount = r.U2();
        var methods = new List<ParsedMethod>();
        for (var i = 0; i < methodCount; i++)
        {
            var access = r.U2();
            var name = CpUtf8(r.U2());
            var rawDescriptor = CpUtf8(r.U2());
            var methodDiags = new List<Diagnostic>();
            var descriptor = MethodDescriptorParser.TryParse(rawDescriptor, out var d) ? d : null;
            if (descriptor is null)
                methodDiags.Add(new Diagnostic("DESC-UNPARSEABLE",
                    $"descriptor of {name} could not be normalized; raw value kept", rawDescriptor, "descriptor"));

            ushort maxStack = 0, maxLocals = 0;
            byte[] code = Array.Empty<byte>();
            var codeOffset = ByteOffset.ClassFile(-1);
            var exceptions = new List<ExceptionHandler>();
            var frames = new List<StackMapFrameInfo>();

            var attrCount = r.U2();
            for (var a = 0; a < attrCount; a++)
            {
                var attrName = CpUtf8(r.U2());
                var attrLen = (int)r.U4();
                var attrStart = r.Pos;
                if (attrName == "Code")
                {
                    maxStack = r.U2();
                    maxLocals = r.U2();
                    var codeLen = (int)r.U4();
                    codeOffset = ByteOffset.ClassFile(r.Pos);
                    code = r.Bytes(codeLen);
                    var exCount = r.U2();
                    for (var e = 0; e < exCount; e++)
                    {
                        var spc = r.U2(); var epc = r.U2(); var hpc = r.U2(); var ct = r.U2();
                        exceptions.Add(new ExceptionHandler(
                            ByteOffset.Code(spc), ByteOffset.Code(epc), ByteOffset.Code(hpc),
                            ct, ct == 0 ? null : CpClass(ct)));
                    }
                    var subCount = r.U2();
                    for (var s = 0; s < subCount; s++)
                    {
                        var subName = CpUtf8(r.U2());
                        var subLen = (int)r.U4();
                        var subStart = r.Pos;
                        if (subName == "StackMapTable")
                            frames.AddRange(ParseStackMapTable(r, CpClass));
                        r.Pos = subStart + subLen;
                    }
                }
                r.Pos = attrStart + attrLen;
            }

            methods.Add(new ParsedMethod(name, rawDescriptor, descriptor, methodDiags,
                access, maxStack, maxLocals, code, exceptions, frames, codeOffset));
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ParsedClassFile(magic, version, cp, thisClass, superClass, methods, diagnostics, bytes, sha);
    }

    private static void SkipMembers(Reader r)
    {
        var count = r.U2();
        for (var i = 0; i < count; i++)
        {
            r.U2(); r.U2(); r.U2();
            var attrs = r.U2();
            for (var a = 0; a < attrs; a++) { r.U2(); var len = (int)r.U4(); r.Bytes(len); }
        }
    }

    private static List<StackMapFrameInfo> ParseStackMapTable(Reader r, Func<ushort, string> cpClass)
    {
        var frames = new List<StackMapFrameInfo>();
        var count = r.U2();
        var prevOffset = -1;
        for (var i = 0; i < count; i++)
        {
            var start = r.Pos;
            var frameType = r.U1();
            int offsetDelta;
            var locals = new List<VerificationTypeInfo>();
            var stack = new List<VerificationTypeInfo>();
            if (frameType <= 63) offsetDelta = frameType;
            else if (frameType <= 127) { offsetDelta = frameType - 64; stack.Add(ParseVerificationType(r, cpClass)); }
            else if (frameType == 247) { offsetDelta = r.U2(); stack.Add(ParseVerificationType(r, cpClass)); }
            else if (frameType is >= 248 and <= 250) offsetDelta = r.U2();
            else if (frameType == 251) offsetDelta = r.U2();
            else if (frameType is >= 252 and <= 254)
            {
                offsetDelta = r.U2();
                for (var k = 0; k < frameType - 251; k++) locals.Add(ParseVerificationType(r, cpClass));
            }
            else
            {
                offsetDelta = r.U2();
                var nLocals = r.U2();
                for (var k = 0; k < nLocals; k++) locals.Add(ParseVerificationType(r, cpClass));
                var nStack = r.U2();
                for (var k = 0; k < nStack; k++) stack.Add(ParseVerificationType(r, cpClass));
            }
            var effective = prevOffset + offsetDelta + 1;
            frames.Add(new StackMapFrameInfo(frameType, ByteOffset.Code(effective), locals, stack,
                r.Slice(start, r.Pos - start)));
            prevOffset = effective;
        }
        return frames;
    }

    private static VerificationTypeInfo ParseVerificationType(Reader r, Func<ushort, string> cpClass)
    {
        var tag = r.U1();
        return tag switch
        {
            7 => new VerificationTypeInfo(tag, ClassName: cpClass(r.U2())),
            8 => new VerificationTypeInfo(tag, NewOffset: r.U2()),
            _ => new VerificationTypeInfo(tag),
        };
    }
}
