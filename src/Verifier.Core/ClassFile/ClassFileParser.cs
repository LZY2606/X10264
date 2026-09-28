using System.Text;
using Verifier.Core.Model;

namespace Verifier.Core.ClassFile;

/// <summary>
/// Minimal but faithful classfile parser (JVMS §4.1). Constant-pool entries,
/// code, exception tables and StackMapTable frames are decoded while every
/// original offset/index/raw byte sequence is retained on the model.
/// </summary>
public sealed class ClassFileParser
{
    private byte[] _b = [];
    private int _pos;
    private object?[] _cp = [];

    public ClassFileModel Parse(byte[] bytes)
    {
        _b = bytes;
        _pos = 0;
        if (U4() != 0xCAFEBABE) throw new FormatException("bad magic (not a classfile)");
        int minor = U2();
        int major = U2();
        int count = U2();
        _cp = new object?[count];
        var cpEntries = new List<CpEntry>(count - 1);
        int i = 1;
        while (i < count)
        {
            int start = _pos;
            int tag = _b[_pos++];
            object value;
            string display;
            switch (tag)
            {
                case 1: // Utf8
                    {
                        int len = U2();
                        var s = ModifiedUtf8(_b, _pos, len);
                        _pos += len;
                        value = s;
                        display = $"Utf8 \"{s}\"";
                        break;
                    }
                case 3: value = I4(); display = $"Integer {value}"; break;
                case 4: value = BitConverter.Int32BitsToSingle(I4()); display = $"Float {value}"; break;
                case 5: value = I8(); display = $"Long {value}"; break;
                case 6: value = BitConverter.DoubleToInt64Bits(0) is _ ? ReadDouble() : 0; display = $"Double {value}"; break;
                case 7: { int n = U2(); value = ("Class", n); display = $"Class #{n}"; break; }
                case 8: { int n = U2(); value = ("String", n); display = $"String #{n}"; break; }
                case 9: { int a = U2(); int c = U2(); value = ("Fieldref", a, c); display = $"Fieldref #{a}.#{c}"; break; }
                case 10: { int a = U2(); int c = U2(); value = ("Methodref", a, c); display = $"Methodref #{a}.#{c}"; break; }
                case 11: { int a = U2(); int c = U2(); value = ("InterfaceMethodref", a, c); display = $"InterfaceMethodref #{a}.#{c}"; break; }
                case 12: { int a = U2(); int c = U2(); value = ("NameAndType", a, c); display = $"NameAndType #{a}:#{c}"; break; }
                case 15: { int k = U1(); int n = U2(); value = ("MethodHandle", k, n); display = $"MethodHandle {k}:#{n}"; break; }
                case 16: { int n = U2(); value = ("MethodType", n); display = $"MethodType #{n}"; break; }
                case 17: { int a = U2(); int c = U2(); value = ("InvokeDynamic", a, c); display = $"InvokeDynamic #{a}:#{c}"; break; }
                case 18: { int n = U2(); value = ("Dynamic", n); display = $"Dynamic #{n}"; break; }
                case 19: { int n = U2(); value = ("Module", n); display = $"Module #{n}"; break; }
                case 20: { int n = U2(); value = ("Package", n); display = $"Package #{n}"; break; }
                default: throw new FormatException($"unknown constant pool tag {tag} at #{i}");
            }
            _cp[i] = value;
            cpEntries.Add(new CpEntry(i, tag switch
            {
                1 => "Utf8", 3 => "Integer", 4 => "Float", 5 => "Long", 6 => "Double",
                7 => "Class", 8 => "String", 9 => "Fieldref", 10 => "Methodref",
                11 => "InterfaceMethodref", 12 => "NameAndType", 15 => "MethodHandle",
                16 => "MethodType", 17 => "InvokeDynamic", 18 => "Dynamic",
                19 => "Module", 20 => "Package", _ => $"{tag}"
            }, ToHex(bytes, start, _pos - start), display));
            if (tag is 5 or 6) { i += 2; } else i++;
        }

        int accessFlags = U2();
        int thisIdx = U2();
        int superIdx = U2();
        string thisClass = ClassName(thisIdx);
        string superClass = superIdx == 0 ? "" : ClassName(superIdx);

        int ifCount = U2();
        _pos += ifCount * 2;

        var methods = new List<MethodModel>();
        int methodCount = U2();
        for (int m = 0; m < methodCount; m++) methods.Add(ReadMethod(U2()));

        return new ClassFileModel(minor, major, cpEntries, thisClass, superClass, methods,
            Convert.ToHexString(bytes));

        int U1() => _b[_pos++];
        int U2() => (_b[_pos++] << 8) | _b[_pos++];
        int I4()
        {
            int v = BinaryPrimitivesReadInt32(_b, _pos);
            _pos += 4;
            return v;
        }
        long I8()
        {
            long v = BinaryPrimitivesReadInt64(_b, _pos);
            _pos += 8;
            return v;
        }
        double ReadDouble() => BitConverter.Int64BitsToDouble(I8());
    }

    private MethodModel ReadMethod(int accessFlags)
    {
        string name = Utf8At(U2());
        string descriptor = Utf8At(U2());
        int attrCount = U2();
        byte[] code = [];
        int maxStack = 0, maxLocals = 0;
        var exceptions = new List<ExceptionHandlerModel>();
        var frames = new List<StackMapFrameModel>();
        for (int a = 0; a < attrCount; a++)
        {
            string attrName = Utf8At(U2());
            int len = ReadU4();
            int attrStart = _pos;
            if (attrName == "Code")
            {
                maxStack = ReadU2();
                maxLocals = ReadU2();
                int codeLen = ReadU4();
                code = _b[_pos..(_pos + codeLen)];
                _pos += codeLen;
                int exCount = ReadU2();
                for (int e = 0; e < exCount; e++)
                {
                    int s = ReadU2(), en = ReadU2(), h = ReadU2(), c = ReadU2();
                    exceptions.Add(new ExceptionHandlerModel(s, en, h, c, c == 0 ? null : ClassNameFromRef(c)));
                }
                int codeAttrCount = ReadU2();
                for (int ca = 0; ca < codeAttrCount; ca++)
                {
                    string can = Utf8At(ReadU2());
                    int clen = ReadU4();
                    if (can == "StackMapTable") ReadStackMapTable(frames, clen);
                    else _pos += clen;
                }
            }
            else _pos = attrStart + len;
        }

        var instructions = InstructionDecoder.Decode(code);
        return new MethodModel(name, descriptor, maxStack, maxLocals,
            (accessFlags & 0x0008) != 0, instructions, exceptions, frames,
            Convert.ToHexString(code));
    }

    private void ReadStackMapTable(List<StackMapFrameModel> frames, int length)
    {
        int end = _pos + length;
        int entryCount = ReadU2();
        int prevOffset = -1;
        for (int k = 0; k < entryCount; k++)
        {
            int frameStart = _pos;
            int frameType = ReadU1();
            string kind;
            int offsetDelta;
            var locals = new List<string>();
            var stack = new List<string>();
            switch (frameType)
            {
                case <= 63:
                    kind = "same";
                    offsetDelta = frameType;
                    break;
                case <= 127:
                    kind = "same_locals_1_stack_item";
                    offsetDelta = frameType - 64;
                    stack.Add(ReadVerificationType());
                    break;
                case 247:
                    kind = "same_locals_1_stack_item_extended";
                    offsetDelta = ReadU2();
                    stack.Add(ReadVerificationType());
                    break;
                case <= 246:
                    kind = $"reserved({frameType})";
                    offsetDelta = 0;
                    break;
                case <= 250:
                    kind = "chop";
                    offsetDelta = ReadU2();
                    break;
                case 251:
                    kind = "append";
                    offsetDelta = ReadU2();
                    locals.Add(ReadVerificationType());
                    break;
                case 252:
                    kind = "append";
                    offsetDelta = ReadU2();
                    locals.Add(ReadVerificationType());
                    locals.Add(ReadVerificationType());
                    break;
                case 253:
                    kind = "append";
                    offsetDelta = ReadU2();
                    for (int z = 0; z < 3; z++) locals.Add(ReadVerificationType());
                    break;
                default: // 255
                    kind = "full_frame";
                    offsetDelta = ReadU2();
                    int nl = ReadU2();
                    for (int z = 0; z < nl; z++) locals.Add(ReadVerificationType());
                    int ns = ReadU2();
                    for (int z = 0; z < ns; z++) stack.Add(ReadVerificationType());
                    break;
            }
            int absolute = prevOffset + 1 + offsetDelta;
            prevOffset = absolute;
            frames.Add(new StackMapFrameModel(absolute, offsetDelta, kind, locals, stack,
                ToHex(_b, frameStart, _pos - frameStart)));
        }
        _pos = end;
    }

    private string ReadVerificationType()
    {
        int tag = ReadU1();
        return tag switch
        {
            0 => "top(unusable)",
            1 => "int",
            2 => "float",
            3 => "double",
            4 => "long",
            5 => "null",
            6 => "uninitializedThis",
            7 => $"ref:{ClassNameFromRef(ReadU2())}",
            8 => $"uninitialized@{ReadU2()}",
            _ => throw new FormatException($"bad verification type tag {tag}")
        };
    }

    private string ClassName(int classCpIndex) => ClassNameFromRef(classCpIndex);

    private string ClassNameFromRef(int classCpIndex)
    {
        var v = _cp[classCpIndex] as (string, int)?;
        int nameIndex = v!.Value.Item2;
        return (string)_cp[nameIndex]!;
    }

    private string Utf8At(int cpIndex) => (string)_cp[cpIndex]!;

    private static string ModifiedUtf8(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder(length);
        int end = offset + length;
        while (offset < end)
        {
            int b1 = data[offset++] & 0xFF;
            if (b1 < 0x80) sb.Append((char)b1);
            else if ((b1 & 0xE0) == 0xC0)
            {
                int b2 = data[offset++] & 0x3F;
                sb.Append((char)(((b1 & 0x1F) << 6) | b2));
            }
            else
            {
                int b2 = data[offset++] & 0x3F;
                int b3 = data[offset++] & 0x3F;
                sb.Append((char)(((b1 & 0x0F) << 12) | (b2 << 6) | b3));
            }
        }
        return sb.ToString();
    }

    private static string ToHex(byte[] b, int start, int len) =>
        Convert.ToHexString(b, start, len);

    private int ReadU1() => _b[_pos++];
    private int ReadU2() => (_b[_pos++] << 8) | _b[_pos++];
    private int ReadU4()
    {
        int v = BinaryPrimitivesReadInt32(_b, _pos);
        _pos += 4;
        return v;
    }

    private static int BinaryPrimitivesReadInt32(byte[] b, int o)
    {
        return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
    }

    private static long BinaryPrimitivesReadInt64(byte[] b, int o)
    {
        return ((long)b[o] << 56) | ((long)b[o + 1] << 48) |
               ((long)b[o + 2] << 40) | ((long)b[o + 3] << 32) |
               ((long)b[o + 4] << 24) | ((long)b[o + 5] << 16) |
               ((long)b[o + 6] << 8) | b[o + 7];
    }
}
