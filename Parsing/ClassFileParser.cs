using System.Text;
using JvmVerifier.Domain;

namespace JvmVerifier.Parsing;

public sealed class ClassFileParser
{
    public RawClassFile Parse(byte[] bytes, string caseId, string originalSource = "binary")
    {
        var diagnostics = new List<ImportDiagnostic>();
        var reader = new BeReader(bytes, 0, bytes.Length);
        try
        {
            var magic = reader.U4();
            if (magic != 0xCAFEBABE) diagnostics.Add(Diag("CF-MAGIC", "classfile magic is not CAFEBABE", null));
            var minor = reader.U2();
            var major = reader.U2();
            var cpCount = reader.U2();
            var cp = ParseConstantPool(reader, cpCount, diagnostics);
            var access = reader.U2();
            var thisClass = reader.U2();
            var superClass = reader.U2();
            var interfaces = Enumerable.Range(0, reader.U2()).Select(_ => (int)reader.U2()).ToArray();
            var fields = reader.U2();
            for (var i = 0; i < fields; i++) SkipMember(reader);
            var methodCount = reader.U2();
            var methods = new List<RawMethod>();
            for (var i = 0; i < methodCount; i++) methods.Add(ParseMethod(reader, i, cp, diagnostics));
            var classAttributes = ParseAttributes(reader, cp, diagnostics);
            return new RawClassFile(caseId, magic, minor, major, cp, thisClass, superClass, interfaces, methods, classAttributes, originalSource, Convert.ToBase64String(bytes), diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Diag("CF-TRUNCATED", ex.Message, $"byte@{Math.Max(0, reader.Position)}"));
            return new RawClassFile(caseId, 0, 0, 0, Array.Empty<ConstantPoolEntry>(), 0, 0, Array.Empty<int>(), Array.Empty<RawMethod>(), Array.Empty<RawAttribute>(), originalSource, Convert.ToBase64String(bytes), diagnostics);
        }
    }

    private static List<ConstantPoolEntry> ParseConstantPool(BeReader reader, ushort count, List<ImportDiagnostic> diagnostics)
    {
        var result = new List<ConstantPoolEntry> { new EmptyConstantPoolSlot(0) };
        for (var i = 1; i < count; i++)
        {
            var offset = reader.Position;
            var tag = reader.U1();
            ConstantPoolEntry entry = tag switch
            {
                1 => new Utf8Entry(i, ReadModifiedUtf8(reader)),
                3 => new IntegerEntry(i, unchecked((int)reader.U4())),
                4 => new FloatEntry(i, unchecked((int)reader.U4())),
                5 => new LongEntry(i, unchecked((long)reader.U4() << 32 | reader.U4())),
                6 => new DoubleEntry(i, unchecked((long)reader.U4() << 32 | reader.U4())),
                7 => new ClassEntry(i, reader.U2()),
                8 => new StringEntry(i, reader.U2()),
                9 => new RefEntry(i, tag, reader.U2(), reader.U2(), "Fieldref"),
                10 => new RefEntry(i, tag, reader.U2(), reader.U2(), "Methodref"),
                11 => new RefEntry(i, tag, reader.U2(), reader.U2(), "InterfaceMethodref"),
                12 => new NameAndTypeEntry(i, reader.U2(), reader.U2()),
                15 => new SimpleEntry(i, tag, "MethodHandle", new[] { (int)reader.U1(), reader.U2() }),
                16 => new SimpleEntry(i, tag, "MethodType", new[] { reader.U2() }),
                17 => new SimpleEntry(i, tag, "Dynamic", new[] { reader.U2(), reader.U2() }),
                18 => new SimpleEntry(i, tag, "InvokeDynamic", new[] { reader.U2(), reader.U2() }),
                19 => new SimpleEntry(i, tag, "Module", new[] { reader.U2() }),
                20 => new SimpleEntry(i, tag, "Package", new[] { reader.U2() }),
                _ => throw new FormatException($"unknown constant pool tag {tag} at cp#{i} byte@{offset}")
            };
            result.Add(entry);
            if (tag is 5 or 6)
            {
                result.Add(new EmptyConstantPoolSlot(i + 1));
                i++;
            }
        }
        return result;
    }

    private static string ReadModifiedUtf8(BeReader reader)
    {
        var length = reader.U2();
        var data = reader.Bytes(length);
        try { return Encoding.UTF8.GetString(data); }
        catch { return Convert.ToBase64String(data); }
    }

    private static RawMethod ParseMethod(BeReader reader, int index, IReadOnlyList<ConstantPoolEntry> cp, List<ImportDiagnostic> diagnostics)
    {
        var access = reader.U2();
        var nameIndex = reader.U2();
        var descriptorIndex = reader.U2();
        var attrs = ParseAttributes(reader, cp, diagnostics);
        var codeAttr = attrs.FirstOrDefault(a => a.Name == "Code");
        var maxStack = 0;
        var maxLocals = 0;
        var code = Array.Empty<byte>();
        var exceptions = Array.Empty<RawExceptionEntry>();
        var frames = Array.Empty<RawStackMapFrame>();
        var codeAttributes = Array.Empty<RawAttribute>();
        if (codeAttr is not null)
        {
            var bytes = Convert.FromBase64String(codeAttr.RawBytesBase64);
            var codeReader = new BeReader(bytes, 0, bytes.Length);
            maxStack = codeReader.U2();
            maxLocals = codeReader.U2();
            var codeLength = (int)codeReader.U4();
            code = codeReader.Bytes(codeLength);
            var exceptionCount = codeReader.U2();
            exceptions = Enumerable.Range(0, exceptionCount).Select(n =>
            {
                var start = codeReader.U2();
                var end = codeReader.U2();
                var handler = codeReader.U2();
                var catchType = codeReader.U2();
                var raw = BitConverter.GetBytes(start).Concat(BitConverter.GetBytes(end)).Concat(BitConverter.GetBytes(handler)).Concat(BitConverter.GetBytes(catchType)).ToArray();
                return new RawExceptionEntry(n, start, end, handler, catchType, Convert.ToBase64String(raw.ReversePairs()));
            }).ToArray();
            codeAttributes = ParseAttributes(codeReader, cp, diagnostics);
            frames = ParseStackMapTable(codeAttributes, code, diagnostics);
        }
        var instructions = InstructionDecoder.Decode(code, diagnostics);
        return new RawMethod(index, access, nameIndex, descriptorIndex, Utf8(cp, nameIndex), Utf8(cp, descriptorIndex), maxStack, maxLocals, Convert.ToBase64String(code), instructions, exceptions, frames, codeAttributes);
    }

    private static List<RawAttribute> ParseAttributes(BeReader reader, IReadOnlyList<ConstantPoolEntry> cp, List<ImportDiagnostic> diagnostics)
    {
        var count = reader.U2();
        var result = new List<RawAttribute>();
        for (var i = 0; i < count; i++)
        {
            var nameIndex = reader.U2();
            var length = (int)reader.U4();
            var data = reader.Bytes(length);
            result.Add(new RawAttribute(Utf8(cp, nameIndex), Convert.ToBase64String(data)));
        }
        return result;
    }

    private static RawStackMapFrame[] ParseStackMapTable(IReadOnlyList<RawAttribute> attrs, byte[] code, List<ImportDiagnostic> diagnostics)
    {
        var attr = attrs.FirstOrDefault(a => a.Name == "StackMapTable");
        if (attr is null) return Array.Empty<RawStackMapFrame>();
        var bytes = Convert.FromBase64String(attr.RawBytesBase64);
        var reader = new BeReader(bytes, 0, bytes.Length);
        var count = reader.U2();
        var frames = new List<RawStackMapFrame>();
        var previousOffset = -1;
        for (var i = 0; i < count; i++)
        {
            var frameDiags = new List<string>();
            var rawStart = reader.Position;
            var frameType = reader.U1();
            var offsetDelta = 0;
            var locals = Array.Empty<VerificationTypeInfo>();
            var stack = Array.Empty<VerificationTypeInfo>();
            int? target = null;
            try
            {
                if (frameType <= 63)
                {
                    offsetDelta = frameType;
                }
                else if (frameType <= 127)
                {
                    offsetDelta = frameType - 64;
                    stack = new[] { ReadVerificationType(reader) };
                }
                else if (frameType == 247)
                {
                    offsetDelta = reader.U2();
                    stack = new[] { ReadVerificationType(reader) };
                }
                else if (frameType is >= 248 and <= 250)
                {
                    offsetDelta = reader.U2();
                }
                else if (frameType == 251)
                {
                    offsetDelta = reader.U2();
                }
                else if (frameType is >= 252 and <= 254)
                {
                    offsetDelta = reader.U2();
                    locals = Enumerable.Range(0, frameType - 251).Select(_ => ReadVerificationType(reader)).ToArray();
                }
                else
                {
                    offsetDelta = reader.U2();
                    locals = Enumerable.Range(0, reader.U2()).Select(_ => ReadVerificationType(reader)).ToArray();
                    stack = Enumerable.Range(0, reader.U2()).Select(_ => ReadVerificationType(reader)).ToArray();
                }
                target = previousOffset < 0 ? offsetDelta : previousOffset + offsetDelta + 1;
                previousOffset = target.Value;
                if (target.Value < 0 || target.Value > code.Length || !InstructionDecoder.IsInstructionStart(code, target.Value))
                    frameDiags.Add($"frame target byte@{target.Value} is not an instruction boundary");
            }
            catch (Exception ex)
            {
                frameDiags.Add(ex.Message);
            }
            var rawEnd = reader.Position;
            var raw = bytes.Skip(rawStart).Take(rawEnd - rawStart).ToArray();
            if (frameDiags.Count > 0) diagnostics.Add(Diag("FRAME-CORRUPT", string.Join("; ", frameDiags), $"frame#{i}", new[] { $"frame#{i}", $"byte@{target?.ToString() ?? "?"}" }));
            frames.Add(new RawStackMapFrame(i, frameType, offsetDelta, target, locals, stack, Convert.ToBase64String(raw), frameDiags));
        }
        return frames.ToArray();
    }

    private static VerificationTypeInfo ReadVerificationType(BeReader reader)
    {
        var start = reader.Position;
        var tag = reader.U1();
        return tag switch
        {
            0 => new TopVariableInfo(start, 1),
            1 => new IntegerVariableInfo(start, 1),
            2 => new FloatVariableInfo(start, 1),
            3 => new DoubleVariableInfo(start, 1),
            4 => new LongVariableInfo(start, 1),
            5 => new NullVariableInfo(start, 1),
            6 => new UninitializedThisVariableInfo(start, 1),
            7 => new ObjectVariableInfo(reader.U2(), start, 3),
            8 => new UninitializedVariableInfo(reader.U2(), start, 3),
            _ => throw new FormatException($"unknown verification type tag {tag}")
        };
    }

    private static void SkipMember(BeReader reader)
    {
        reader.Skip(6);
        var count = reader.U2();
        for (var i = 0; i < count; i++) { reader.Skip(2); var len = reader.U4(); reader.Skip((int)len); }
    }

    private static string Utf8(IReadOnlyList<ConstantPoolEntry> cp, int index) => cp.Count > index && cp[index] is Utf8Entry utf8 ? utf8.Value : $"#{index}";
    private static ImportDiagnostic Diag(string code, string message, string? coordinate, IReadOnlyList<string>? evidence = null) => new(code, message, coordinate, evidence ?? Array.Empty<string>());
}

internal static class ByteArrayExtensions
{
    public static byte[] ReversePairs(this byte[] input)
    {
        for (var i = 0; i + 1 < input.Length; i += 2) (input[i], input[i + 1]) = (input[i + 1], input[i]);
        return input;
    }
}
