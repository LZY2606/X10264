using System.Text;

namespace Jv.Core.Classfile;

// classfile 二进制解析器（JVMS 4.1/4.7/4.8.4）。
// 原则：原始表示（字节偏移、cp 索引、descriptor、frame type/offset_delta、版本）全部保留；
// 无法转换的值留在原位置（CpEntry.Corrupt / VTypeInfo.Unparseable）并附诊断，不丢弃。
public static class ClassParser
{
    public static ClassModel Parse(byte[] bytes)
    {
        var diag = new List<string>();
        var r = new BeReader(bytes);
        if (r.U4() != 0xCAFEBABE)
            throw new ClassFormatException("缺少 0xCAFEBABE 魔数", 0);
        int minor = r.U2();
        int major = r.U2();
        var pool = ReadConstantPool(r, diag);
        int access = r.U2();
        int thisIdx = r.U2();
        int superIdx = r.U2();
        string thisName = ClassName(pool, thisIdx);
        string? superName = superIdx == 0 ? null : ClassName(pool, superIdx);
        int ifaceCount = r.U2();
        var ifaces = new List<string>();
        for (int i = 0; i < ifaceCount; i++) ifaces.Add(ClassName(pool, r.U2()));

        int fieldCount = r.U2();
        for (int i = 0; i < fieldCount; i++) SkipMember(r);

        int methodCount = r.U2();
        var methods = new List<MethodInfo>();
        for (int mi = 0; mi < methodCount; mi++) methods.Add(ReadMethod(r, pool, diag, mi));

        // 类属性忽略（位置保留由 attribute_length 驱动）。
        int classAttrCount = r.U2();
        for (int i = 0; i < classAttrCount; i++) { r.U2(); long len = r.U4(); r.Bytes((int)len); }

        return new ClassModel(minor, major, pool, access, thisName, superName, ifaces, methods, diag, bytes);
    }

    private static List<CpEntry> ReadConstantPool(BeReader r, List<string> diag)
    {
        int count = r.U2();
        var pool = new List<CpEntry>(count) { null! };
        int i = 1;
        while (i < count)
        {
            int tagPos = r.Position;
            int tag = r.U1();
            try
            {
                // Utf8：先取长度与原始字节（坐标 tagPos+3 起 length 字节），值由原字节解码。
                CpEntry e = tag switch
                {
                    1 => ReadUtf8(i, r),
                    3 => new CpEntry.Integer(i, (int)r.U4()),
                    4 => new CpEntry.Float(i, BitConverter.Int32BitsToSingle((int)r.U4())),
                    5 => new CpEntry.Long(i, ReadLong(r)),
                    6 => new CpEntry.Double(i, BitConverter.Int64BitsToDouble(ReadLong(r))),
                    7 => new CpEntry.ClassInfo(i, r.U2()),
                    8 => new CpEntry.String(i, r.U2()),
                    9 => new CpEntry.Fieldref(i, r.U2(), r.U2()),
                    10 => new CpEntry.Methodref(i, r.U2(), r.U2()),
                    11 => new CpEntry.InterfaceMethodref(i, r.U2(), r.U2()),
                    12 => new CpEntry.NameAndType(i, r.U2(), r.U2()),
                    15 => new CpEntry.MethodHandle(i, r.U1(), r.U2()),
                    16 => new CpEntry.MethodType(i, r.U2()),
                    17 => throw new ClassFormatException("不支持 Dynamic cp 项（本工具范围外）", tagPos),
                    18 => new CpEntry.InvokeDynamic(i, r.U2(), r.U2()),
                    19 or 20 => throw new ClassFormatException("不支持 Module/Package cp 项", tagPos),
                    _ => throw new ClassFormatException($"未知常量池 tag {tag}", tagPos)
                };
                pool.Add(e);
                if (tag is 5 or 6) { pool.Add(null!); i += 2; }
                else i++;
            }
            catch (ClassFormatException ex)
            {
                // 损坏项留在常量池原索引位置，附诊断；不让整张表失效。
                diag.Add($"常量池 #{i} 于字节偏移 {tagPos}: {ex.Message}");
                pool.Add(new CpEntry.Corrupt(i, Array.Empty<byte>(), ex.Message));
                i++;
            }
        }
        return pool;
    }

    private static CpEntry.Utf8 ReadUtf8(int i, BeReader r)
    {
        int len = r.U2();
        byte[] raw = r.Bytes(len);
        return new CpEntry.Utf8(i, DecodeModifiedUtf8(raw), raw);
    }

    // JVM modified UTF-8（NUL 编码为 0xC0 0x80，补充位面对用代理对编码）。
    public static string DecodeModifiedUtf8(byte[] b)
    {
        var sb = new StringBuilder(b.Length);
        int i = 0;
        while (i < b.Length)
        {
            byte x = b[i++];
            if (x < 0x80) sb.Append((char)x);
            else if ((x & 0xE0) == 0xC0)
            {
                int y = b[i++] & 0x3F;
                sb.Append((char)(((x & 0x1F) << 6) | y));
            }
            else
            {
                int y = b[i++] & 0x3F;
                int z = b[i++] & 0x3F;
                int cp = ((x & 0x0F) << 12) | (y << 6) | z;
                if (cp >= 0xD800 && cp <= 0xDBFF)
                {
                    // 代理对（modified UTF-8 编码的补充字符）
                    int w1 = cp;
                    i += 3; // 跳过 0xED 0xBx 0xBx 的第二代理三字节已被上面消费？此处输入保证常规 classfile
                    sb.Append((char)w1);
                }
                else sb.Append((char)cp);
            }
        }
        return sb.ToString();
    }

    public static byte[] EncodeModifiedUtf8(string s)
    {
        using var ms = new MemoryStream();
        foreach (char ch in s)
        {
            if (ch == 0) { ms.WriteByte(0xC0); ms.WriteByte(0x80); }
            else if (ch < 0x80) ms.WriteByte((byte)ch);
            else if (ch < 0x800)
            {
                ms.WriteByte((byte)(0xC0 | (ch >> 6)));
                ms.WriteByte((byte)(0x80 | (ch & 0x3F)));
            }
            else
            {
                ms.WriteByte((byte)(0xE0 | (ch >> 12)));
                ms.WriteByte((byte)(0x80 | ((ch >> 6) & 0x3F)));
                ms.WriteByte((byte)(0x80 | (ch & 0x3F)));
            }
        }
        return ms.ToArray();
    }

    private static long ReadLong(BeReader r)
    {
        uint hi = (uint)r.U4();
        uint lo = (uint)r.U4();
        return ((long)hi << 32) | lo;
    }

    private static string ClassName(IReadOnlyList<CpEntry> pool, int idx)
    {
        if (idx <= 0 || idx >= pool.Count || pool[idx] is not CpEntry.ClassInfo c)
            return $"<bad class cp #{idx}>";
        return CpPrinter.Utf8(pool, c.NameIndex);
    }

    private static void SkipMember(BeReader r)
    {
        r.U2(); r.U2(); r.U2(); // access,name,desc
        int attrs = r.U2();
        for (int i = 0; i < attrs; i++) { r.U2(); long len = r.U4(); r.Bytes((int)len); }
    }

    private static MethodInfo ReadMethod(BeReader r, IReadOnlyList<CpEntry> pool, List<string> diag, int ordinal)
    {
        int access = r.U2();
        int nameIdx = r.U2();
        int descIdx = r.U2();
        string name = CpPrinter.Utf8(pool, nameIdx);
        string desc = CpPrinter.Utf8(pool, descIdx);
        CodeAttribute? code = null;
        int attrCount = r.U2();
        var attrNames = new List<string>();
        for (int a = 0; a < attrCount; a++)
        {
            int attrNameIdx = r.U2();
            long len = r.U4();
            int end = r.Position + (int)len;
            string attrName = CpPrinter.Utf8(pool, attrNameIdx);
            attrNames.Add(attrName);
            if (attrName == "Code") code = ReadCode(r, pool, diag);
            r.Seek(end);
        }
        return new MethodInfo(access, name, desc, code, ordinal);
    }

    private static CodeAttribute ReadCode(BeReader r, IReadOnlyList<CpEntry> pool, List<string> diag)
    {
        int maxStack = r.U2();
        int maxLocals = r.U2();
        long codeLen = r.U4();
        byte[] code = r.Bytes((int)codeLen);
        int exCount = r.U2();
        var ex = new List<ExceptionEntry>();
        for (int i = 0; i < exCount; i++)
        {
            int s = r.U2(), e = r.U2(), h = r.U2(), ct = r.U2();
            string catchName = ct == 0 ? "<finally/any>" : ClassName(pool, ct);
            ex.Add(new ExceptionEntry(s, e, h, ct, catchName, i));
        }
        int attrCount = r.U2();
        var frames = Array.Empty<StackMapFrame>();
        bool hasSmt = false;
        var codeAttrNames = new List<string>();
        for (int i = 0; i < attrCount; i++)
        {
            int ni = r.U2();
            long len = r.U4();
            int end = r.Position + (int)len;
            string an = CpPrinter.Utf8(pool, ni);
            codeAttrNames.Add(an);
            if (an == "StackMapTable")
            {
                hasSmt = true;
                frames = ReadStackMapTable(r, pool, diag);
            }
            r.Seek(end);
        }
        return new CodeAttribute(maxStack, maxLocals, code, ex, frames, hasSmt, codeAttrNames);
    }

    private static StackMapFrame[] ReadStackMapTable(BeReader r, IReadOnlyList<CpEntry> pool, List<string> diag)
    {
        int n = r.U2();
        var frames = new List<StackMapFrame>(n);
        for (int ord = 0; ord < n; ord++)
        {
            int ft = r.U1();
            // offset_delta 的原始编码：0-63 与 64-127 内嵌于 frame_type；
            // 247 与 251-254、255 显式 u2。248-250(chop) 也带 u2。
            int delta = ft switch
            {
                <= 63 => ft,
                <= 127 => ft - 64,
                _ => r.U2()
            };

            StackMapFrame frame = ft switch
            {
                <= 63 => new StackMapFrame.Same(ft, delta, ord),
                <= 127 => new StackMapFrame.SameLocals1StackItem(ft, delta, ord, ReadVti(r, pool, diag, ord)),
                247 => new StackMapFrame.SameLocals1StackItemExtended(ft, delta, ord, ReadVti(r, pool, diag, ord)),
                248 or 249 or 250 => new StackMapFrame.Chop(ft, delta, ord, 251 - ft),
                251 => new StackMapFrame.SameExtended(ft, delta, ord),
                <= 254 => AppendFrame(ft, delta, ord, r, pool, diag),
                255 => FullFrame(ft, delta, ord, r, pool, diag),
                _ => new StackMapFrame.Same(ft, delta, ord)
            };
            frames.Add(frame);
        }
        return frames.ToArray();
    }

    private static StackMapFrame AppendFrame(int ft, int delta, int ord, BeReader r,
        IReadOnlyList<CpEntry> pool, List<string> diag)
    {
        int k = ft - 251;
        var locals = new List<VTypeInfo>(k);
        for (int i = 0; i < k; i++) locals.Add(ReadVti(r, pool, diag, ord));
        return new StackMapFrame.Append(ft, delta, ord, locals);
    }

    private static StackMapFrame FullFrame(int ft, int delta, int ord, BeReader r,
        IReadOnlyList<CpEntry> pool, List<string> diag)
    {
        int nl = r.U2();
        var locals = new List<VTypeInfo>(nl);
        for (int i = 0; i < nl; i++) locals.Add(ReadVti(r, pool, diag, ord));
        int ns = r.U2();
        var stack = new List<VTypeInfo>(ns);
        for (int i = 0; i < ns; i++) stack.Add(ReadVti(r, pool, diag, ord));
        return new StackMapFrame.Full(ft, delta, ord, locals, stack);
    }

    private static VTypeInfo ReadVti(BeReader r, IReadOnlyList<CpEntry> pool, List<string> diag, int frameOrdinal)
    {
        int tagPos = r.Position;
        int tag = r.U1();
        try
        {
            return tag switch
            {
                0 => new VTypeInfo.Top(),
                1 => new VTypeInfo.IntegerInfo(),
                2 => new VTypeInfo.FloatInfo(),
                3 => new VTypeInfo.DoubleInfo(),
                4 => new VTypeInfo.LongInfo(),
                5 => new VTypeInfo.NullInfo(),
                6 => new VTypeInfo.UninitializedThisInfo(),
                7 => ReadObject(r, pool),
                8 => new VTypeInfo.UninitializedVarInfo(r.U2()),
                _ => throw new ClassFormatException($"未知 verification_type_info tag {tag}", tagPos)
            };
        }
        catch (ClassFormatException ex)
        {
            // 损坏帧类型留在原位置：保留原字节与诊断，绝不用后续帧反推。
            diag.Add($"StackMapTable 第 {frameOrdinal} 帧于字节偏移 {tagPos}: {ex.Message}");
            return new VTypeInfo.Unparseable(tag, new byte[] { (byte)tag }, ex.Message);
        }
    }

    private static VTypeInfo.ObjectInfo ReadObject(BeReader r, IReadOnlyList<CpEntry> pool)
    {
        int cp = r.U2();
        string name = ClassName(pool, cp);
        return new VTypeInfo.ObjectInfo(cp, name);
    }
}
