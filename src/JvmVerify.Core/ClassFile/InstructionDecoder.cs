using System.Collections.Generic;

namespace JvmVerify.Core.ClassFile;

public static class InstructionDecoder
{
    // Decodes the raw Code byte array. Falls back to single-byte unknown
    // instructions so later passes can still report precise offsets.
    public static IReadOnlyList<RawInstruction> Decode(byte[] code)
    {
        var result = new List<RawInstruction>();
        var r = new ByteReader(code);
        while (r.HasMore)
        {
            int start = r.Mark();
            int op = r.U1();
            var spec = Opcodes.Find(op);
            int len;
            var targets = new List<int>();
            if (op == Opcodes.Wide)
            {
                int inner = r.U1();
                bool iinc = inner == Opcodes.Iinc;
                r.U2();
                if (iinc) r.S2();
                len = r.Mark() - start;
            }
            else if (spec is null)
            {
                len = 1;
            }
            else if (spec.IsVariableLength)
            {
                int pad = 4 - (r.Position % 4);
                if (pad != 4) for (int i = 0; i < pad; i++) r.U1();
                int @default = r.S4(start);
                if (op == Opcodes.Tableswitch)
                {
                    int low = r.S4(start), high = r.S4(start);
                    targets.Add(start + @default);
                    for (int i = low; i <= high; i++) targets.Add(start + r.S4(start));
                }
                else
                {
                    int npairs = r.S4(start);
                    targets.Add(start + @default);
                    for (int i = 0; i < npairs; i++) { r.S4(start); targets.Add(start + r.S4(start)); }
                }
                len = r.Mark() - start;
            }
            else
            {
                for (int i = 0; i < spec.OperandLength; i++) r.U1();
                len = spec.OperandLength + 1;
                if (spec.Branch == BranchKind.Branch2) targets.Add(start + (sbyte)code[start + 1] << 0 | 0); // overwritten below
                if (spec.Branch == BranchKind.Branch2)
                {
                    targets.Clear();
                    targets.Add(start + (sbyte)(code[start + 1] << 24 >> 24) + (sbyte)(code[start + 2] << 24 >> 24) is { } _
                        ? System.BitConverter.ToInt16(new[] { code[start + 2], code[start + 1] }, 0) : 0);
                }
                if (spec.Branch == BranchKind.Branch4)
                {
                    targets.Clear();
                    targets.Add(start + (int)System.BitConverter.ToUInt32(new[] { code[start + 4], code[start + 3], code[start + 2], code[start + 1] }, 0));
                }
            }
            var operand = new byte[len - 1];
            for (int i = 0; i < operand.Length; i++) operand[i] = code[start + 1 + i];
            result.Add(new RawInstruction(start, op, spec?.Mnemonic ?? $"unknown_{op:X2}", operand, targets, len));
        }
        return result;
    }
}

internal static class ReaderSwitchExtensions
{
    public static int S4(this ByteReader r, int _)
    {
        var b = new byte[4];
        for (int i = 3; i >= 0; i--) b[i] = r.U1();
        return System.BitConverter.ToInt32(b, 0);
    }
}
