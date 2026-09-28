namespace Verifier.Core.Model;

/// <summary>
/// Raw classfile model. Every domain quantity keeps its original
/// representation (byte offsets, constant-pool indices, raw hex) — the
/// derived layer normalizes, the raw layer is never rewritten.
/// </summary>
public sealed record CpEntry(int Index, string Tag, string RawHex, string Display);

public sealed record ExceptionHandlerModel(
    int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string? CatchType);

public sealed record StackMapFrameModel(
    int Offset,           // absolute bytecode offset the frame applies to
    int OffsetDelta,      // raw offset_delta as stored in the attribute
    string FrameType,     // same / same_locals_1 / chop / append / full
    IReadOnlyList<string> Locals,   // verification-type display strings
    IReadOnlyList<string> Stack,
    string RawHex);

public sealed record InstructionModel(
    int Offset,           // original byte offset in the code array
    byte Opcode,
    string Mnemonic,
    string Operands,      // decoded operand text (constant-pool refs keep #index)
    int Size,             // bytes occupied, opcode included
    IReadOnlyList<int> BranchTargets,
    string RawHex);

public sealed record MethodModel(
    string Name,
    string Descriptor,    // raw descriptor string, e.g. "(J[I)V"
    int MaxStack,
    int MaxLocals,
    bool IsStatic,
    IReadOnlyList<InstructionModel> Instructions,
    IReadOnlyList<ExceptionHandlerModel> Exceptions,
    IReadOnlyList<StackMapFrameModel> Frames,
    string CodeRawHex);

public sealed record ClassFileModel(
    int MinorVersion,
    int MajorVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    string ThisClass,
    string SuperClass,
    IReadOnlyList<MethodModel> Methods,
    string RawHex);

/// <summary>Descriptor parsing kept separate so raw descriptors stay untouched.</summary>
public static class DescriptorParser
{
    public static List<VerificationType> ParseArgumentTypes(string descriptor)
    {
        var result = new List<VerificationType>();
        int i = 1; // skip '('
        while (descriptor[i] != ')')
        {
            var (t, next) = ParseOne(descriptor, i);
            result.Add(t);
            i = next;
        }
        return result;
    }

    public static VerificationType ParseReturnType(string descriptor)
    {
        int close = descriptor.IndexOf(')');
        var (t, _) = ParseOne(descriptor, close + 1);
        return t;
    }

    private static (VerificationType, int) ParseOne(string d, int i)
    {
        switch (d[i])
        {
            case 'B' or 'C' or 'I' or 'S' or 'Z': return (VerificationType.Int, i + 1);
            case 'F': return (VerificationType.Float, i + 1);
            case 'J': return (VerificationType.Long, i + 1);
            case 'D': return (VerificationType.Double, i + 1);
            case 'V': return (VerificationType.Top, i + 1);
            case 'L':
                int semi = d.IndexOf(';', i);
                return (new VerificationType.RefType(d[(i + 1)..semi]), semi + 1);
            case '[':
                int j = i;
                while (d[j] == '[') j++;
                if (d[j] == 'L') j = d.IndexOf(';', j);
                return (new VerificationType.RefType(d[i..(j + 1)]), j + 1);
            default:
                throw new FormatException($"bad descriptor '{d}' at {i}");
        }
    }
}
