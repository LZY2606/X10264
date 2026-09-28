namespace JvmBytecodeVerifier.Core.Raw;

public static class ClassFileParser
{
    public static ClassFile Parse(byte[] bytes)
    {
        var r = new BigEndianReader(bytes);
        var magic = r.U4("magic");
        if (magic != 0xCAFEBABE)
            throw new ClassFormatException("Invalid magic number; not a class file", "magic");
        var minor = r.U2("minor_version");
        var major = r.U2("major_version");

        var cpCount = r.U2("constant_pool_count");
        var pool = new CpEntry[cpCount == 0 ? 0 : cpCount - 1];
        for (ushort i = 1; i < cpCount; i++)
        {
            var tag = (CpTag)r.U1($"cp#{i}.tag");
            pool[i - 1] = ReadCpEntry(tag, ref r, pool, i);
            if (tag is CpTag.Long or CpTag.Double) i++;
        }

        var access = r.U2("access_flags");
        var thisIdx = r.U2("this_class");
        var superIdx = r.U2("super_class");
        var thisName = ClassName(pool, thisIdx);
        var superName = superIdx == 0 ? "" : ClassName(pool, superIdx);

        var ifaceCount = r.U2("interfaces_count");
        var interfaces = new string[ifaceCount];
        for (var i = 0; i < ifaceCount; i++)
            interfaces[i] = ClassName(pool, r.U2($"interfaces[{i}]"));

        var fields = ReadMembers(ref r, pool, "fields");
        var methods = ReadMethods(ref r, pool);
        var attrs = ReadAttributes(ref r, pool, "class");

        return new ClassFile
        {
            Magic = magic,
            MinorVersion = minor,
            MajorVersion = major,
            ConstantPoolCount = cpCount,
            ConstantPool = pool,
            AccessFlags = access,
            ThisClass = thisName,
            SuperClass = superName,
            Interfaces = interfaces,
            Fields = fields,
            Methods = methods,
            Attributes = attrs,
            OriginalBytes = bytes
        };
    }

    private static string Hex(byte[] all, int start, int len) =>
        Convert.ToHexString(new ReadOnlySpan<byte>(all, start, len));

    private static CpEntry ReadCpEntry(CpTag tag, ref BigEndianReader r, CpEntry[] pool, ushort index)
    {
        var bytes = r.Underlying;
        switch (tag)
        {
            case CpTag.Utf8:
            {
                var len = r.U2($"cp#{index}.length");
                return new CpUtf8(r.ModifiedUtf8(len));
            }
            case CpTag.Integer:
            {
                var start = r.Position;
                var v = unchecked((int)r.U4());
                return new CpInteger(v, Hex(bytes, start, 4));
            }
            case CpTag.Float:
            {
                var start = r.Position;
                var bits = r.U4();
                return new CpFloat(bits, Hex(bytes, start, 4));
            }
            case CpTag.Long:
            {
                var start = r.Position;
                var hi = r.U4(); var lo = r.U4();
                return new CpLong(unchecked((long)((hi << 32) | lo)), Hex(bytes, start, 8));
            }
            case CpTag.Double:
            {
                var start = r.Position;
                var hi = r.U4(); var lo = r.U4();
                return new CpDouble((hi << 32) | lo, Hex(bytes, start, 8));
            }
            case CpTag.Class:
            {
                var ni = r.U2();
                return new CpClass(ni, Utf8(pool, ni));
            }
            case CpTag.String:
            {
                var ui = r.U2();
                return new CpString(ui, Utf8(pool, ui));
            }
            case CpTag.Fieldref:
            case CpTag.Methodref:
            case CpTag.InterfaceMethodref:
            {
                var ci = r.U2(); var nt = r.U2();
                var (n, d) = NameAndType(pool, nt);
                return new CpRef(tag, ci, ClassName(pool, ci), nt, n, d);
            }
            case CpTag.NameAndType:
            {
                var ni = r.U2(); var di = r.U2();
                return new CpNameAndType(ni, Utf8(pool, ni), di, Utf8(pool, di));
            }
            case CpTag.MethodHandle:
            {
                var kind = r.U1(); var refIdx = r.U2();
                return new CpMethodHandle(kind, refIdx);
            }
            case CpTag.MethodType:
            {
                var di = r.U2();
                return new CpMethodType(di, Utf8(pool, di));
            }
            case CpTag.Dynamic:
            case CpTag.InvokeDynamic:
            {
                var bsm = r.U2(); var nt = r.U2();
                var (n, d) = NameAndType(pool, nt);
                return new CpDynamic(tag, bsm, nt, n, d);
            }
            case CpTag.Module:
            case CpTag.Package:
            {
                var ni = r.U2();
                return new CpModule(tag, ni, Utf8(pool, ni));
            }
            default:
                throw new ClassFormatException($"Unknown constant pool tag {(byte)tag} at #{index}", $"cp:#{index}");
        }
    }

    private static string Utf8(CpEntry[] pool, ushort index) =>
        index >= 1 && index <= pool.Length && pool[index - 1] is CpUtf8 u ? u.Value : $"#!{index}";

    private static string ClassName(CpEntry[] pool, ushort index) =>
        index >= 1 && index <= pool.Length && pool[index - 1] is CpClass c ? Utf8(pool, c.NameIndex) : $"#!{index}";

    private static (string Name, string Descriptor) NameAndType(CpEntry[] pool, ushort index)
    {
        if (index >= 1 && index <= pool.Length && pool[index - 1] is CpNameAndType nt)
            return (Utf8(pool, nt.NameIndex), Utf8(pool, nt.DescriptorIndex));
        return ($"#!{index}", "");
    }

    private static IReadOnlyList<RawFieldOrMethod> ReadMembers(ref BigEndianReader r, CpEntry[] pool, string where)
    {
        var count = r.U2($"{where}_count");
        var list = new List<RawFieldOrMethod>(count);
        for (var i = 0; i < count; i++)
        {
            var access = r.U2();
            var ni = r.U2(); var di = r.U2();
            var attrs = ReadAttributes(ref r, pool, $"{where}[{i}]");
            list.Add(new RawFieldOrMethod(access, Utf8(pool, ni), Utf8(pool, di), attrs));
        }
        return list;
    }

    private static IReadOnlyList<RawMethod> ReadMethods(ref BigEndianReader r, CpEntry[] pool)
    {
        var count = r.U2("methods_count");
        var list = new List<RawMethod>(count);
        for (var i = 0; i < count; i++)
        {
            var access = r.U2();
            var ni = r.U2(); var di = r.U2();
            var attrs = ReadAttributes(ref r, pool, $"methods[{i}]");
            var name = Utf8(pool, ni);
            var descriptor = Utf8(pool, di);
            RawCode code = null;
            foreach (var attr in attrs)
            {
                if (attr.Name != "Code") continue;
                code = ReadCode(attr, pool, i);
                break;
            }
            list.Add(new RawMethod(access, name, descriptor, attrs, code));
        }
        return list;
    }

    private static RawCode ReadCode(RawAttribute codeAttr, CpEntry[] pool, int methodOrdinal)
    {
        var ar = new BigEndianReader(codeAttr.Info);
        var maxStack = ar.U2("max_stack");
        var maxLocals = ar.U2("max_locals");
        var codeLength = (int)ar.U4("code_length");
        if (codeLength == 0)
            throw new ClassFormatException("code_length must be greater than zero", $"method[{methodOrdinal}].code_length");
        if (codeLength > 65535)
            throw new ClassFormatException("code_length must not exceed 65535", $"method[{methodOrdinal}].code_length");
        var bytecode = ar.Bytes(codeLength).ToArray();
        var bytecodeOffset = codeAttr.InfoOffset + ar.Position - codeLength;

        var exCount = ar.U2("exception_table_length");
        var exceptions = new List<RawExceptionEntry>(exCount);
        for (var i = 0; i < exCount; i++)
        {
            var sp = ar.U2(); var ep = ar.U2(); var hp = ar.U2(); var ct = ar.U2();
            var catchName = ct == 0 ? "<finally>" : ClassName(pool, ct);
            exceptions.Add(new RawExceptionEntry(sp, ep, hp, ct, catchName, i));
        }

        var codeAttrs = ReadAttributes(ref ar, pool, $"method[{methodOrdinal}].Code");
        List<RawStackMapFrame> frames = null;
        var other = new List<RawAttribute>();
        foreach (var attr in codeAttrs)
        {
            if (attr.Name == "StackMapTable")
                frames = ReadStackMapTable(attr, pool, methodOrdinal);
            else
                other.Add(attr);
        }

        return new RawCode(maxStack, maxLocals, bytecode, exceptions,
            frames ?? Array.Empty<RawStackMapFrame>(), other, codeAttr.InfoOffset, bytecodeOffset);
    }

    private static List<RawStackMapFrame> ReadStackMapTable(RawAttribute attr, CpEntry[] pool, int methodOrdinal)
    {
        var sr = new BigEndianReader(attr.Info);
        var n = sr.U2("number_of_entries");
        var frames = new List<RawStackMapFrame>(n);
        ushort previousOffset = 0;
        for (var i = 0; i < n; i++)
        {
            var frameStartFileOffset = attr.InfoOffset + sr.Position;
            var frameTypeByteOffset = frameStartFileOffset;
            var frameType = sr.U1($"stack_map_frame[{i}].frame_type");
            ushort delta;
            int? deltaFieldOffset = null;
            var locals = new List<VerificationTypeRaw>();
            var stack = new List<VerificationTypeRaw>();
            int appendCount;
            if (frameType <= 63)
            {
                delta = frameType;
            }
            else if (frameType is >= 64 and <= 126)
            {
                delta = (ushort)(frameType - 64);
                stack.Add(ReadVerificationType(ref sr, pool, attr.InfoOffset));
            }
            else if (frameType == 247)
            {
                deltaFieldOffset = attr.InfoOffset + sr.Position;
                delta = sr.U2();
                stack.Add(ReadVerificationType(ref sr, pool, attr.InfoOffset));
            }
            else if (frameType is >= 248 and <= 250)
            {
                deltaFieldOffset = attr.InfoOffset + sr.Position;
                delta = sr.U2();
            }
            else if (frameType == 251)
            {
                deltaFieldOffset = attr.InfoOffset + sr.Position;
                delta = sr.U2();
            }
            else if (frameType is >= 252 and <= 254)
            {
                deltaFieldOffset = attr.InfoOffset + sr.Position;
                delta = sr.U2();
                appendCount = frameType - 251;
                for (var k = 0; k < appendCount; k++)
                    locals.Add(ReadVerificationType(ref sr, pool, attr.InfoOffset));
            }
            else // 255 full frame
            {
                deltaFieldOffset = attr.InfoOffset + sr.Position;
                delta = sr.U2();
                var nl = sr.U2();
                for (var k = 0; k < nl; k++) locals.Add(ReadVerificationType(ref sr, pool, attr.InfoOffset));
                var ns = sr.U2();
                for (var k = 0; k < ns; k++) stack.Add(ReadVerificationType(ref sr, pool, attr.InfoOffset));
            }

            var rawLength = attr.InfoOffset + sr.Position - frameStartFileOffset;
            var kind = frameType switch
            {
                <= 63 => StackMapFrameKind.Same,
                <= 126 => StackMapFrameKind.SameLocals1StackItem,
                247 => StackMapFrameKind.SameLocals1StackItemExtended,
                >= 248 and <= 250 => StackMapFrameKind.Chop,
                251 => StackMapFrameKind.SameFrameExtended,
                >= 252 => StackMapFrameKind.Append,
                _ => StackMapFrameKind.FullFrame
            };
            // JVMS 4.10.1: first frame applies to offset_delta, later frames to previous + offset_delta + 1.
            var declaredOffset = (ushort)(i == 0 ? delta : previousOffset + delta + 1);
            frames.Add(new RawStackMapFrame(frameType, kind, delta, frameTypeByteOffset, deltaFieldOffset,
                locals, stack, declaredOffset, frameStartFileOffset, rawLength, false));
            previousOffset = declaredOffset;
        }
        return frames;
    }

    private static VerificationTypeRaw ReadVerificationType(ref BigEndianReader r, CpEntry[] pool, int baseOffset)
    {
        var offset = baseOffset + r.Position;
        var tag = (VerificationTypeTag)r.U1("verification_type_info.tag");
        ushort? payload = null;
        string name;
        switch (tag)
        {
            case VerificationTypeTag.Top: name = "top"; break;
            case VerificationTypeTag.Integer: name = "int"; break;
            case VerificationTypeTag.Float: name = "float"; break;
            case VerificationTypeTag.Double: name = "double"; break;
            case VerificationTypeTag.Long: name = "long"; break;
            case VerificationTypeTag.Null: name = "null"; break;
            case VerificationTypeTag.UninitializedThis: name = "uninitializedThis"; break;
            case VerificationTypeTag.Object:
            {
                var cpIndex = r.U2();
                payload = cpIndex;
                name = ClassName(pool, cpIndex);
                break;
            }
            case VerificationTypeTag.Uninitialized:
            {
                var newPc = r.U2();
                payload = newPc;
                name = $"uninitialized@{newPc}";
                break;
            }
            default:
                throw new ClassFormatException($"Unknown verification_type_info tag {(byte)tag}", $"stackmap:{offset}");
        }
        return new VerificationTypeRaw(tag, payload, name, offset);
    }

    private static IReadOnlyList<RawAttribute> ReadAttributes(ref BigEndianReader r, CpEntry[] pool, string where)
    {
        var count = r.U2($"{where}.attributes_count");
        var list = new List<RawAttribute>(count);
        for (var i = 0; i < count; i++)
        {
            var nameIdx = r.U2();
            var len = (int)r.U4("attribute_length");
            var infoOffset = r.Position;
            var info = r.Bytes(len).ToArray();
            list.Add(new RawAttribute(Utf8(pool, nameIdx), nameIdx, info, infoOffset));
        }
        return list;
    }
}
