namespace JvmVerifier.Core.ClassFile;

public sealed record DecodeDiagnostic(int Offset, string Code, string Message);

public sealed class DecodeResult
{
    public required IReadOnlyList<Instruction> Instructions { get; init; }
    public required IReadOnlyList<DecodeDiagnostic> Diagnostics { get; init; }
    public Instruction? At(int offset) => Instructions.FirstOrDefault(i => i.Offset == offset);
}

public static class InstructionDecoder
{
    public static DecodeResult Decode(byte[] code)
    {
        var list = new List<Instruction>();
        var diags = new List<DecodeDiagnostic>();
        int pc = 0;
        while (pc < code.Length)
        {
            int start = pc;
            byte op = code[pc++];
            var spec = InstructionSet.Of(op);
            if (spec is null)
            {
                diags.Add(new(start, "bad-opcode", $"Unknown opcode 0x{op:X2}"));
                break;
            }

            byte[] operands;
            if (spec.Opcode == 170 || spec.Opcode == 171) // tableswitch / lookupswitch
            {
                int padded = (pc + 3) & ~3;
                operands = ReadSwitch(code, pc, padded, spec.Opcode == 170, out var targets, out string? err);
                foreach (var t in targets) listTarget(start, t);
                if (err is not null) diags.Add(new(start, "bad-switch", err));
                pc = padded + operands.Length;
            }
            else if (spec.Opcode == 196) // wide
            {
                byte inner = code[pc];
                int extra = inner is 132 ? 4 : 2;
                operands = code.AsSpan(pc, extra).ToArray();
                pc += extra;
            }
            else
            {
                int size = spec.OperandSize;
                if (pc + size > code.Length)
                {
                    diags.Add(new(start, "truncated", $"Operand for {spec.Mnemonic} runs past code end"));
                    operands = code.AsSpan(pc).ToArray();
                    pc = code.Length;
                }
                else
                {
                    operands = code.AsSpan(pc, size).ToArray();
                    pc += size;
                }
            }

            list.Add(new Instruction
            {
                Offset = start, Spec = spec, OperandBytes = operands
            });
            if (spec.Opcode is 170 or 171)
            {
                // switch target bookkeeping kept below via operand parse
            }
        }
        return new DecodeResult { Instructions = list, Diagnostics = diags };

        void listTarget(int _, int __) { }
    }

    private static byte[] ReadSwitch(byte[] code, int pc, int dataStart, bool table,
        out List<int> targets, out string? error)
    {
        targets = new(); error = null;
        var ms = new MemoryStream();
        // default + low/high are s4; gather offsets relative to instruction start
        int ReadS4(int at) =>
            (code[at] << 24) | (code[at + 1] << 16) | (code[at + 2] << 8) | code[at + 3];
        int instrStart = pc - 1;
        int defaultOff = ReadS4(dataStart);
        targets.Add(instrStart + defaultOff);
        if (table)
        {
            int low = ReadS4(dataStart + 4), high = ReadS4(dataStart + 8);
            long n = (long)high - low + 1;
            if (n < 0 || n > 1 << 20) { error = "tableswitch high < low or absurd"; n = Math.Max(0, n); }
            int bytes = 12 + (int)Math.Min(n, 1 << 20) * 4 + (dataStart - pc);
            for (int i = 0; i < n && dataStart + 12 + i * 4 + 4 <= code.Length; i++)
                targets.Add(instrStart + ReadS4(dataStart + 12 + i * 4));
            return code.AsSpan(pc, Math.Max(0, Math.Min(bytes, code.Length - pc))).ToArray();
        }
        else
        {
            int npairs = ReadS4(dataStart + 4);
            if (npairs < 0 || npairs > 1 << 20) { error = "lookupswitch absurd pair count"; npairs = 0; }
            for (int i = 0; i < npairs && dataStart + 8 + i * 8 + 8 <= code.Length; i++)
                targets.Add(instrStart + ReadS4(dataStart + 12 + i * 8));
            int bytes = (dataStart - pc) + 8 + npairs * 8;
            return code.AsSpan(pc, Math.Max(0, Math.Min(bytes, code.Length - pc))).ToArray();
        }
    }

    /// <summary>All branch/switch targets as absolute byte offsets, used by CFG leader detection.</summary>
    public static IEnumerable<int> BranchTargets(Instruction ins)
    {
        if (ins.Spec.Kind is OperandKind.BranchOffset or OperandKind.BranchOffsetW)
        {
            yield return ins.BranchTarget;
            yield break;
        }
        if (ins.Spec.Opcode is 170 or 171)
        {
            int pc = ins.Offset + 1;
            int dataStart = (pc + 3) & ~3;
            int ReadS4(int at) =>
                (ins.OperandBytes[at - pc] << 24) | (ins.OperandBytes[at - pc + 1] << 16) |
                (ins.OperandBytes[at - pc + 2] << 8) | ins.OperandBytes[at - pc + 3];
            yield return ins.Offset + ReadS4(dataStart);
            if (ins.Spec.Opcode == 170)
            {
                int low = ReadS4(dataStart + 4), high = ReadS4(dataStart + 8);
                for (int v = low, i = 0; v <= high && i < 1 << 20; v++, i++)
                    yield return ins.Offset + ReadS4(dataStart + 12 + i * 4);
            }
            else
            {
                int n = ReadS4(dataStart + 4);
                for (int i = 0; i < n; i++)
                    yield return ins.Offset + ReadS4(dataStart + 12 + i * 8);
            }
        }
    }
}
