using System.Diagnostics.CodeAnalysis;

namespace Jbcv.Core.ClassFile;

public sealed record DecodeDiagnostic(int Offset, string Code, string Message)
{
    public string Id => $"diag@{Offset}:{Code}";
}

public sealed record DecodedCode(IReadOnlyList<RawInstruction> Instructions, IReadOnlyList<DecodeDiagnostic> Diagnostics)
{
    public RawInstruction? At(int offset) =>
        Instructions.FirstOrDefault(i => i.Offset == offset);
}

/// <summary>Decodes Code[] byte stream instruction-by-instruction. Truncated/unknown bytes are kept in place with diagnostics.</summary>
public static class CodeDecoder
{
    public static DecodedCode Decode(byte[] code)
    {
        var list = new List<RawInstruction>();
        var diags = new List<DecodeDiagnostic>();
        int pc = 0;
        while (pc < code.Length)
        {
            int start = pc;
            byte op = code[pc++];
            if (!InstructionSet.Known(op))
            {
                diags.Add(new DecodeDiagnostic(start, "UNKNOWN_OPCODE",
                    $"opcode 0x{op:X2} is not defined; byte retained, stream halted at this boundary"));
                list.Add(new RawInstruction(start, op, InstructionSet.Mnemonic(op), Array.Empty<byte>(), 1));
                // Unknown length: stop linear scan; remaining bytes stay as evidence in CodeHex.
                break;
            }

            int operandLen = ResolveOperandLength(op, code, start, ref pc, diags);
            if (operandLen < 0) break; // truncation diagnostic already emitted
            byte[] operands = new byte[operandLen];
            Array.Copy(code, start + 1, operands, 0, operandLen);
            list.Add(new RawInstruction(start, op, InstructionSet.Mnemonic(op), operands, 1 + operandLen));
            pc = start + 1 + operandLen;
        }
        return new DecodedCode(list, diags);
    }

    private static int ResolveOperandLength(byte op, byte[] code, int start, ref int pc, List<DecodeDiagnostic> diags)
    {
        switch (op)
        {
            case 0xaa: // tableswitch
                return ReadTableswitch(code, start, diags);
            case 0xab: // lookupswitch
                return ReadLookupswitch(code, start, diags);
            case 0xc4: // wide
                return ReadWide(code, start, diags);
            default:
                int fixedLen = InstructionSet.FixedOperandBytes(op);
                if (start + 1 + fixedLen > code.Length)
                {
                    diags.Add(new DecodeDiagnostic(start, "TRUNCATED_OPERANDS",
                        $"{InstructionSet.Mnemonic(op)} needs {fixedLen} operand byte(s), code ends at {code.Length}"));
                    return -1;
                }
                pc = start + 1 + fixedLen;
                return fixedLen;
        }
    }

    private static int ReadTableswitch(byte[] code, int start, List<DecodeDiagnostic> diags)
    {
        int dataStart = start + 1;
        int pad = (4 - dataStart % 4) % 4;
        int p = dataStart + pad;
        if (p + 12 > code.Length) { EmitTrunc(start, "tableswitch", code.Length, p + 12, diags); return -1; }
        int low = I32(code, p + 4);
        int high = I32(code, p + 8);
        long count = (long)high - low + 1;
        if (count < 0 || count > 1 << 16)
        {
            diags.Add(new DecodeDiagnostic(start, "BAD_SWITCH_RANGE", $"tableswitch low={low} high={high}"));
            count = Math.Max(0, count);
        }
        int total = pad + 12 + (int)count * 4;
        if (start + 1 + total > code.Length) { EmitTrunc(start, "tableswitch", code.Length, start + 1 + total, diags); return -1; }
        return total;
    }

    private static int ReadLookupswitch(byte[] code, int start, List<DecodeDiagnostic> diags)
    {
        int dataStart = start + 1;
        int pad = (4 - dataStart % 4) % 4;
        int p = dataStart + pad;
        if (p + 8 > code.Length) { EmitTrunc(start, "lookupswitch", code.Length, p + 8, diags); return -1; }
        int npairs = I32(code, p + 4);
        if (npairs < 0 || npairs > 1 << 16)
        {
            diags.Add(new DecodeDiagnostic(start, "BAD_SWITCH_PAIRS", $"lookupswitch npairs={npairs}"));
            npairs = Math.Max(0, npairs);
        }
        int total = pad + 8 + npairs * 8;
        if (start + 1 + total > code.Length) { EmitTrunc(start, "lookupswitch", code.Length, start + 1 + total, diags); return -1; }
        return total;
    }

    private static int ReadWide(byte[] code, int start, List<DecodeDiagnostic> diags)
    {
        if (start + 2 >= code.Length) { EmitTrunc(start, "wide", code.Length, start + 3, diags); return -1; }
        byte inner = code[start + 1];
        int total = inner == 0x84 ? 5 : 3;
        if (start + 1 + total > code.Length) { EmitTrunc(start, $"wide {inner:X2}", code.Length, start + 1 + total, diags); return -1; }
        return total;
    }

    private static void EmitTrunc(int start, string what, int actual, int needed, List<DecodeDiagnostic> diags) =>
        diags.Add(new DecodeDiagnostic(start, "TRUNCATED_OPERANDS", $"{what}: needs {needed} byte(s), code ends at {actual}"));

    public static int I32(byte[] b, int p) =>
        (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
}
