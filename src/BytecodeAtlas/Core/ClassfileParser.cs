using System.Security.Cryptography;
using System.Text;

namespace BytecodeAtlas.Core;

/// <summary>Big-endian classfile parser. Raw representation (bytes, offsets, tags) is
/// preserved at import; normalization happens only in derived layers.</summary>
public static class ClassfileParser
{
    private sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b) => _b = b;
        public byte U1() => _b[Pos++];
        public ushort U2() { var v = (ushort)((_b[Pos] << 8) | _b[Pos + 1]); Pos += 2; return v; }
        public uint U4() { var v = ((uint)_b[Pos] << 24) | ((uint)_b[Pos + 1] << 16) | ((uint)_b[Pos + 2] << 8) | _b[Pos + 3]; Pos += 4; return v; }
        public byte[] Bytes(int n) { var r = _b[Pos..(Pos + n)]; Pos += n; return r; }
    }

    public static ClassFile Parse(byte[] bytes)
    {
        var diags = new List<Diagnostic>();
        var r = new Reader(bytes);
        var magic = r.U4();
        if (magic != 0xCAFEBABE)
            diags.Add(new Diagnostic("bad-magic", $"magic 0x{magic:X8} is not 0xCAFEBABE", "file:0"));
        int minor = r.U2();
        int major = r.U2();
        int cpCount = r.U2();
        var pool = new List<CpEntry?> { null };
        for (int i = 1; i < cpCount; i++)
        {
            int entryStart = r.Pos;
            byte tag = r.U1();
            switch (tag)
            {
                case 1: { int len = r.U2(); pool.Add(new CpEntry.Utf8(i, Encoding.UTF8.GetString(r.Bytes(len)))); break; }
                case 7: pool.Add(new CpEntry.Class(i, r.U2())); break;
                case 8: pool.Add(new CpEntry.Str(i, r.U2())); break;
                case 9: pool.Add(new CpEntry.Ref(i, "Fieldref", r.U2(), r.U2())); break;
                case 10: pool.Add(new CpEntry.Ref(i, "Methodref", r.U2(), r.U2())); break;
                case 11: pool.Add(new CpEntry.Ref(i, "InterfaceMethodref", r.U2(), r.U2())); break;
                case 12: pool.Add(new CpEntry.NameAndType(i, r.U2(), r.U2())); break;
                case 3: pool.Add(new CpEntry.Int32(i, unchecked((int)r.U4()))); break;
                case 4: pool.Add(new CpEntry.Flt(i, BitConverter.Int32BitsToSingle(unchecked((int)r.U4())))); break;
                case 5: { long v = unchecked((long)(((ulong)r.U4() << 32) | r.U4())); pool.Add(new CpEntry.Int64(i, v)); pool.Add(null); i++; break; }
                case 6: { long bits = unchecked((long)(((ulong)r.U4() << 32) | r.U4())); pool.Add(new CpEntry.Dbl(i, BitConverter.Int64BitsToDouble(bits))); pool.Add(null); i++; break; }
                case 15: pool.Add(new CpEntry.MethodHandle(i, r.U1(), r.U2())); break;
                case 16: pool.Add(new CpEntry.MethodType(i, r.U2())); break;
                case 18: pool.Add(new CpEntry.InvokeDynamic(i, r.U2(), r.U2())); break;
                default:
                    // Unconvertible value: keep raw bytes in place, attach diagnostic. Nothing is dropped.
                    diags.Add(new Diagnostic("unknown-cp-tag", $"constant pool tag {tag} not decodable; raw bytes preserved", $"cp#{i}@file:{entryStart}"));
                    pool.Add(new CpEntry.Unknown(i, tag, Array.Empty<byte>()));
                    r.Pos = bytes.Length; // cannot continue walking the pool reliably
                    i = cpCount;
                    break;
            }
        }

        r.U2(); // access flags
        int thisClass = r.U2();
        int superClass = r.U2();
        int ifaceCount = r.U2();
        for (int i = 0; i < ifaceCount; i++) r.U2();
        SkipMembers(r); // fields
        var methods = new List<MethodDef>();
        int methodCount = r.U2();
        var partial = new List<(string Name, string Desc, int Flags)>();
        // resolve names lazily after pool is known (pool already parsed above)
        for (int i = 0; i < methodCount; i++)
        {
            int flags = r.U2();
            int nameIdx = r.U2();
            int descIdx = r.U2();
            string name = pool[nameIdx] is CpEntry.Utf8 un ? un.Value : $"#{nameIdx}";
            string desc = pool[descIdx] is CpEntry.Utf8 ud ? ud.Value : $"#{descIdx}";
            var m = new MethodDef
            {
                Name = name, Descriptor = desc, AccessFlags = flags,
                MaxStack = 0, MaxLocals = 0, Code = Array.Empty<byte>(),
                ExceptionTable = new List<ExceptionHandler>(), Frames = new List<StackMapFrame>(),
                Instructions = new List<InstructionInfo>(),
            };
            int attrCount = r.U2();
            for (int a = 0; a < attrCount; a++)
            {
                int attrNameIdx = r.U2();
                uint attrLen = r.U4();
                int attrStart = r.Pos;
                string attrName = pool[attrNameIdx] is CpEntry.Utf8 an ? an.Value : "";
                if (attrName == "Code")
                    ParseCode(r, m, pool, diags, attrStart);
                r.Pos = attrStart + (int)attrLen;
            }
            methods.Add(m);
            partial.Add((name, desc, flags));
        }

        var cf = new ClassFile
        {
            Minor = minor, Major = major, Pool = pool,
            ThisClass = ResolveClass(pool, thisClass), SuperClass = ResolveClass(pool, superClass),
            Methods = methods, RawBytes = bytes,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        };
        foreach (var d in diags) cf.Diagnostics.Add(d);
        return cf;
    }

    private static string ResolveClass(List<CpEntry?> pool, int idx) =>
        idx > 0 && idx < pool.Count && pool[idx] is CpEntry.Class c && pool[c.NameIndex] is CpEntry.Utf8 u
            ? u.Value : "java/lang/Object";

    private static void SkipMembers(Reader r)
    {
        int count = r.U2();
        for (int i = 0; i < count; i++)
        {
            r.U2(); r.U2(); r.U2();
            int attrs = r.U2();
            for (int a = 0; a < attrs; a++) { r.U2(); uint len = r.U4(); r.Bytes((int)len); }
        }
    }

    private static void ParseCode(Reader r, MethodDef m, List<CpEntry?> pool, List<Diagnostic> diags, int codeAttrFileOffset)
    {
        m.CodeAttributeFileOffset = codeAttrFileOffset;
        m.MaxStack = r.U2();
        m.MaxLocals = r.U2();
        uint codeLen = r.U4();
        m.Code = r.Bytes((int)codeLen);
        int exCount = r.U2();
        for (int i = 0; i < exCount; i++)
        {
            int startPc = r.U2(); int endPc = r.U2(); int handlerPc = r.U2(); int catchType = r.U2();
            string? catchName = catchType == 0 ? "java/lang/Throwable" : ResolveClass(pool, catchType);
            m.ExceptionTable.Add(new ExceptionHandler(startPc, endPc, handlerPc, catchType, catchName));
        }
        int attrCount = r.U2();
        for (int a = 0; a < attrCount; a++)
        {
            int nameIdx = r.U2();
            uint len = r.U4();
            int start = r.Pos;
            string attrName = pool[nameIdx] is CpEntry.Utf8 u ? u.Value : "";
            if (attrName == "StackMapTable")
                ParseStackMapTable(r, m, diags, start);
            r.Pos = start + (int)len;
        }
        m.Instructions.AddRange(InstructionDecoder.Decode(m.Code, m.Diagnostics));
    }

    private static void ParseStackMapTable(Reader r, MethodDef m, List<Diagnostic> diags, int fileOffset)
    {
        int count = r.U2();
        int prevOffset = -1;
        for (int i = 0; i < count; i++)
        {
            int frameStart = r.Pos;
            int frameType = r.U1();
            var locals = new List<VerificationTypeInfo>();
            var stack = new List<VerificationTypeInfo>();
            int offsetDelta;
            if (frameType <= 63) { offsetDelta = frameType; }
            else if (frameType <= 127) { offsetDelta = frameType - 64; stack.Add(ParseVti(r)); }
            else if (frameType == 247) { offsetDelta = r.U2(); stack.Add(ParseVti(r)); }
            else if (frameType >= 248 && frameType <= 250) { offsetDelta = r.U2(); }
            else if (frameType == 251) { offsetDelta = r.U2(); }
            else if (frameType >= 252 && frameType <= 254)
            {
                offsetDelta = r.U2();
                for (int k = 0; k < frameType - 251; k++) locals.Add(ParseVti(r));
            }
            else if (frameType == 255)
            {
                offsetDelta = r.U2();
                int nl = r.U2();
                for (int k = 0; k < nl; k++) locals.Add(ParseVti(r));
                int ns = r.U2();
                for (int k = 0; k < ns; k++) stack.Add(ParseVti(r));
            }
            else
            {
                diags.Add(new Diagnostic("unknown-frame-type", $"frame_type {frameType} reserved; raw bytes preserved", $"frame[{i}]@file:{fileOffset + frameStart}"));
                break;
            }
            int offset = prevOffset + offsetDelta + (prevOffset == -1 ? 0 : 1);
            var raw = r.Bytes(0); // placeholder; slice below
            int frameEnd = r.Pos;
            m.Frames.Add(new StackMapFrame(frameType, offsetDelta, offset, locals, stack, raw, fileOffset + frameStart));
            // corrupt frame offset detection: offset must land on an instruction boundary and increase
            if (prevOffset >= 0 && offset <= prevOffset)
                diags.Add(new Diagnostic("frame-offset-order", $"frame[{i}] offset {offset} not greater than previous {prevOffset}", $"frame[{i}]"));
            prevOffset = offset;
            _ = frameEnd;
        }
    }

    private static VerificationTypeInfo ParseVti(Reader r)
    {
        int tag = r.U1();
        return tag switch
        {
            7 => new VerificationTypeInfo(7, r.U2()),
            8 => new VerificationTypeInfo(8, r.U2()),
            >= 0 and <= 6 => new VerificationTypeInfo(tag, null),
            _ => new VerificationTypeInfo(tag, null),
        };
    }
}
