namespace JvmLab.Classfile;

/// <summary>
/// Parses a classfile, preserving original byte offsets, raw hex and
/// constant-pool indices for every structure. Nothing is normalized away:
/// the derived layer works on this evidence as-is.
/// </summary>
public sealed class ClassFileParser
{
    private byte[] _b = Array.Empty<byte>();
    private int _p;

    private int U1() => _b[_p++];
    private int U2() { var v = (_b[_p] << 8) | _b[_p + 1]; _p += 2; return v; }
    private uint U4() { var v = (uint)((_b[_p] << 24) | (_b[_p + 1] << 16) | (_b[_p + 2] << 8) | _b[_p + 3]); _p += 4; return v; }
    private string Hex(int offset, int length) => Convert.ToHexString(_b, offset, length);

    public ClassFile Parse(byte[] bytes)
    {
        _b = bytes; _p = 0;
        if (U4() != 0xCAFEBABE) throw new InvalidDataException("not a classfile (bad magic)");
        var minor = U2(); var major = U2();

        var cp = ParseConstantPool();
        var cf = new ClassFile
        {
            RawBytes = bytes, MinorVersion = minor, MajorVersion = major,
            ThisClass = "", SuperClass = ""
        };
        cf.ConstantPool.AddRange(cp);

        _ = U2(); // access flags
        var thisIdx = U2(); var superIdx = U2();
        cf = new ClassFile
        {
            RawBytes = bytes, MinorVersion = minor, MajorVersion = major,
            ThisClass = cf.CpClassName(thisIdx) ?? $"#{thisIdx}",
            SuperClass = cf.CpClassName(superIdx) ?? $"#{superIdx}"
        };
        cf.ConstantPool.AddRange(cp);

        var ifaceCount = U2();
        for (var i = 0; i < ifaceCount; i++) U2();

        var fieldCount = U2();
        for (var i = 0; i < fieldCount; i++)
        {
            _ = U2();
            var nameIdx = U2(); var descIdx = U2();
            cf.Fields.Add(new FieldInfo
            {
                Name = cf.CpUtf8(nameIdx) ?? $"#{nameIdx}",
                Descriptor = cf.CpUtf8(descIdx) ?? $"#{descIdx}",
                NameIndex = nameIdx, DescriptorIndex = descIdx
            });
            SkipAttributes();
        }

        var methodCount = U2();
        for (var i = 0; i < methodCount; i++)
        {
            var methodOffset = _p;
            _ = U2();
            var nameIdx = U2(); var descIdx = U2();
            var method = new MethodInfo
            {
                FileOffset = Quantity.Bytes(methodOffset, "classFileStart", ""),
                Name = cf.CpUtf8(nameIdx) ?? $"#{nameIdx}",
                Descriptor = cf.CpUtf8(descIdx) ?? $"#{descIdx}",
                NameIndex = nameIdx, DescriptorIndex = descIdx,
                RawHex = ""
            };
            var attrCount = U2();
            for (var a = 0; a < attrCount; a++)
            {
                var attrOffset = _p;
                var nameIndex = U2(); var length = (int)U4();
                var attrName = cf.CpUtf8(nameIndex);
                if (attrName == "Code")
                    method.Code = ParseCode(cf, attrOffset);
                else
                    _p += length;
            }
            method.RawHex = Hex(methodOffset, _p - methodOffset);
            cf.Methods.Add(method);
        }
        return cf;
    }

    private List<CpEntry> ParseConstantPool()
    {
        var list = new List<CpEntry>();
        var cpCount = U2();
        var index = 1;
        while (index < cpCount)
        {
            var offset = _p;
            var tag = U1();
            var entry = new CpEntry
            {
                Index = index,
                FileOffset = Quantity.Bytes(offset, "classFileStart", ""),
                Tag = (byte)tag, TagName = TagName(tag), RawHex = ""
            };
            switch (tag)
            {
                case 1: var len = U2(); entry.Values.Add(System.Text.Encoding.UTF8.GetString(_b, _p, len)); _p += len; break;
                case 3: entry.Values.Add((int)U4()); break;
                case 4: entry.Values.Add(BitConverter.Int32BitsToSingle((int)U4())); break;
                case 5: entry.Values.Add((long)((ulong)U4() << 32 | U4())); break;
                case 6: entry.Values.Add(BitConverter.Int64BitsToDouble((long)((ulong)U4() << 32 | U4()))); break;
                case 7 or 8 or 16 or 19 or 20: entry.Values.Add(U2()); break;
                case 9 or 10 or 11 or 12 or 17 or 18: entry.Values.Add(U2()); entry.Values.Add(U2()); break;
                case 15: entry.Values.Add(U1()); entry.Values.Add(U2()); break;
                default: throw new InvalidDataException($"unknown cp tag {tag} at file offset {offset}");
            }
            entry.RawHex = Hex(offset, _p - offset);
            list.Add(entry);
            index += tag is 5 or 6 ? 2 : 1;
        }
        return list;
    }

    private void SkipAttributes()
    {
        var count = U2();
        for (var i = 0; i < count; i++) { _ = U2(); var len = (int)U4(); _p += len; }
    }

    private CodeAttribute ParseCode(ClassFile cf, int attrOffset)
    {
        _p = attrOffset + 6; // skip attribute name index + length
        var maxStack = U2(); var maxLocals = U2();
        var codeLen = (int)U4();
        var codeOffset = _p;
        var code = _b.Skip(_p).Take(codeLen).ToArray();
        _p += codeLen;
        var attr = new CodeAttribute
        {
            FileOffset = Quantity.Bytes(attrOffset, "classFileStart", ""),
            MaxStack = maxStack, MaxLocals = maxLocals,
            CodeFileOffset = Quantity.Bytes(codeOffset, "classFileStart", ""),
            CodeBytes = code, RawHex = ""
        };
        attr.Instructions.AddRange(InstructionDecoder.Decode(code, codeOffset));

        var exCount = U2();
        for (var i = 0; i < exCount; i++)
        {
            var exOffset = _p;
            attr.ExceptionTable.Add(new ExceptionRange
            {
                FileOffset = Quantity.Bytes(exOffset, "classFileStart", ""),
                StartPc = U2(), EndPc = U2(), HandlerPc = U2(), CatchType = U2(),
                RawHex = Hex(exOffset, 8)
            });
        }

        var subCount = U2();
        for (var i = 0; i < subCount; i++)
        {
            var nameIdx = U2(); var len = (int)U4();
            if (cf.CpUtf8(nameIdx) == "StackMapTable")
                ParseStackMapTable(attr);
            else
                _p += len;
        }
        attr.RawHex = Hex(attrOffset, _p - attrOffset);
        return attr;
    }

    private void ParseStackMapTable(CodeAttribute attr)
    {
        var count = U2();
        var previous = -1;
        for (var i = 0; i < count; i++)
        {
            var frameOffset = _p;
            var frameType = U1();
            var frame = new StackMapFrame
            {
                FileOffset = Quantity.Bytes(frameOffset, "classFileStart", ""),
                FrameType = (byte)frameType,
                Kind = "same",
                OffsetDelta = frameType,
                RawHex = ""
            };
            if (frameType <= 63) { }
            else if (frameType <= 127)
            {
                frame.Kind = "same_locals_1";
                frame.Stack.Add(ParseVerificationType());
            }
            else if (frameType == 247)
            {
                frame.Kind = "same_locals_1_ext";
                frame.OffsetDelta = U2();
                frame.Stack.Add(ParseVerificationType());
            }
            else if (frameType is >= 248 and <= 250)
            {
                frame.Kind = $"chop({251 - frameType})";
                frame.OffsetDelta = U2();
            }
            else if (frameType == 251)
            {
                frame.Kind = "same_ext";
                frame.OffsetDelta = U2();
            }
            else if (frameType is >= 252 and <= 254)
            {
                frame.Kind = $"append({frameType - 251})";
                frame.OffsetDelta = U2();
                for (var j = 0; j < frameType - 251; j++) frame.Locals.Add(ParseVerificationType());
            }
            else
            {
                frame.Kind = "full";
                frame.OffsetDelta = U2();
                var locals = U2();
                for (var j = 0; j < locals; j++) frame.Locals.Add(ParseVerificationType());
                var stack = U2();
                for (var j = 0; j < stack; j++) frame.Stack.Add(ParseVerificationType());
            }
            frame.FrameOffset = previous == -1 ? frame.OffsetDelta : previous + frame.OffsetDelta + 1;
            previous = frame.FrameOffset;
            frame.RawHex = Hex(frameOffset, _p - frameOffset);
            attr.Frames.Add(frame);
        }
    }

    private VerificationTypeInfo ParseVerificationType()
    {
        var offset = _p;
        var tag = U1();
        var info = new VerificationTypeInfo
        {
            FileOffset = Quantity.Bytes(offset, "classFileStart", ""),
            Tag = (byte)tag, RawHex = ""
        };
        if (tag == 7) info.CpoolIndex = U2();
        else if (tag == 8) info.Offset = U2();
        info.RawHex = Hex(offset, _p - offset);
        return info;
    }

    private static string TagName(int tag) => tag switch
    {
        1 => "Utf8", 3 => "Integer", 4 => "Float", 5 => "Long", 6 => "Double",
        7 => "Class", 8 => "String", 9 => "Fieldref", 10 => "Methodref",
        11 => "InterfaceMethodref", 12 => "NameAndType", 15 => "MethodHandle",
        16 => "MethodType", 17 => "Dynamic", 18 => "InvokeDynamic",
        19 => "Module", 20 => "Package", _ => $"Tag{tag}"
    };
}
