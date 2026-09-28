namespace JvmBytecodeVerifier.Core.Raw;

// Decodes a method's code array into instructions. Operand layout follows JVMS §6.5.
// Branch targets and constant pool references are resolved here so the verifier and UI
// both consume the same coordinate-bearing model.
public static class InstructionDecoder
{
    private const int NoOperands = 0;
    private const int CpIndex = -1;
    private const int Byte = -2;
    private const int ShortBranch = -3;
    private const int WideShortBranch = -4;
    private const int WideCpIndex = -5;
    private const int TableSwitch = -6;
    private const int LookupSwitch = -7;
    private const int WidePrefix = -8;
    private const int Short = -9;
    private const int WideShort = -10;

    private static readonly (string Name, int Layout)[] Specs = BuildTable();

    public static DecodeResult Decode(byte[] code, IReadOnlyList<CpEntry> pool)
    {
        var instructions = new List<Instruction>();
        var errors = new List<DecodeError>();
        var pos = 0;
        while (pos < code.Length)
        {
            var start = pos;
            var op = code[pos];
            if (op == 0xAA) { pos = DecodeTableswitch(code, start, instructions, errors, pool); continue; }
            if (op == 0xAB) { pos = DecodeLookupswitch(code, start, instructions, errors, pool); continue; }
            if (op == 0xC4) { pos = DecodeWide(code, start, instructions, errors, pool); continue; }

            var (name, layout) = Specs[op];
            if (layout == int.MinValue)
            {
                errors.Add(new DecodeError(start, "V010",
                    $"Unknown or unsupported opcode 0x{op:X2} at byte offset {start}", $"code:{start}"));
                pos++;
                continue;
            }

            pos++;
            var operands = new List<InstructionOperand>();
            var branches = new List<int>();
            int? cpIndex = null;
            string note = "";
            switch (layout)
            {
                case NoOperands:
                    break;
                case CpIndex:
                {
                    var idx = ReadU2(code, ref pos, start, errors, operands, pool);
                    cpIndex = idx;
                    break;
                }
                case WideCpIndex:
                {
                    var idx = ReadU2(code, ref pos, start, errors, operands, pool);
                    ReadU1(code, ref pos, start, errors);
                    cpIndex = idx;
                    break;
                }
                case Byte:
                {
                    var v = ReadS1(code, ref pos, start, errors);
                    operands.Add(new InstructionOperand("byte", $"0x{v:X2}", v, start + 1));
                    break;
                }
                case Short:
                {
                    var v = ReadS2(code, ref pos, start, errors);
                    operands.Add(new InstructionOperand("s2", v.ToString(), v, start + 1));
                    break;
                }
                case WideShort:
                {
                    var v = ReadS2(code, ref pos, start, errors);
                    operands.Add(new InstructionOperand("s2", v.ToString(), v, start + 1));
                    ReadU1(code, ref pos, start, errors);
                    break;
                }
                case ShortBranch:
                {
                    var delta = ReadS2(code, ref pos, start, errors);
                    operands.Add(new InstructionOperand("branch", delta >= 0 ? $"+{delta}" : delta.ToString(), delta, start + 1));
                    branches.Add(start + delta);
                    break;
                }
                case WideShortBranch:
                {
                    var delta = ReadS4(code, ref pos, start, errors);
                    operands.Add(new InstructionOperand("branch32", delta >= 0 ? $"+{delta}" : delta.ToString(), delta, start + 1));
                    branches.Add(start + delta);
                    break;
                }
                default:
                {
                    for (var i = 0; i < layout; i++) ReadU1(code, ref pos, start, errors);
                    break;
                }
            }

            if (pos > code.Length)
            {
                errors.Add(new DecodeError(start, "V010",
                    $"Instruction {name} at {start} runs past end of code", $"code:{start}"));
                pos = code.Length;
            }
            var terminator = op is 0xAC or 0xB1 or 0xB0 or 0xC9 or 0xC8 or 0xAA or 0xAB
                or (>= 0x99 and <= 0xA8) or 0xA9;
            instructions.Add(new Instruction(op, name, start, pos - start, operands,
                branches, cpIndex, terminator, note));
        }
        return new DecodeResult(instructions, errors);
    }

    private static int DecodeTableswitch(byte[] code, int start, List<Instruction> instructions,
        List<DecodeError> errors, IReadOnlyList<CpEntry> pool)
    {
        var pos = start + 1;
        while (pos % 4 != 0) pos++;
        var def = ReadS4(code, ref pos, start, errors);
        var low = ReadS4(code, ref pos, start, errors);
        var high = ReadS4(code, ref pos, start, errors);
        var count = high - low + 1L;
        var branches = new List<int>();
        var operands = new List<InstructionOperand>
        {
            new("default", def >= 0 ? $"+{def}" : def.ToString(), def, -1),
            new("low", low.ToString(), low, -1),
            new("high", high.ToString(), high, -1)
        };
        if (count is < 0 or > 65535)
            errors.Add(new DecodeError(start, "V010", "tableswitch high < low or range too large", $"code:{start}"));
        else
        {
            for (var i = 0; i < count; i++)
            {
                var delta = ReadS4(code, ref pos, start, errors);
                operands.Add(new InstructionOperand($"case[{i + low}]", delta >= 0 ? $"+{delta}" : delta.ToString(), delta, -1));
                branches.Add(start + delta);
            }
        }
        branches.Add(start + def);
        instructions.Add(new Instruction(0xAA, "tableswitch", start, pos - start, operands,
            branches, null, true, ""));
        return pos;
    }

    private static int DecodeLookupswitch(byte[] code, int start, List<Instruction> instructions,
        List<DecodeError> errors, IReadOnlyList<CpEntry> pool)
    {
        var pos = start + 1;
        while (pos % 4 != 0) pos++;
        var def = ReadS4(code, ref pos, start, errors);
        var npairs = ReadS4(code, ref pos, start, errors);
        var branches = new List<int> { start + def };
        var operands = new List<InstructionOperand>
        {
            new("default", def >= 0 ? $"+{def}" : def.ToString(), def, -1),
            new("npairs", npairs.ToString(), npairs, -1)
        };
        if (npairs < 0 || npairs > 65535)
            errors.Add(new DecodeError(start, "V010", "lookupswitch npairs out of range", $"code:{start}"));
        else
        {
            for (var i = 0; i < npairs; i++)
            {
                var match = ReadS4(code, ref pos, start, errors);
                var delta = ReadS4(code, ref pos, start, errors);
                operands.Add(new InstructionOperand($"match{i}", match.ToString(), match, -1));
                branches.Add(start + delta);
            }
        }
        instructions.Add(new Instruction(0xAB, "lookupswitch", start, pos - start, operands,
            branches, null, true, ""));
        return pos;
    }

    private static int DecodeWide(byte[] code, int start, List<Instruction> instructions,
        List<DecodeError> errors, IReadOnlyList<CpEntry> pool)
    {
        var pos = start + 1;
        var sub = ReadU1(code, ref pos, start, errors);
        var (subName, _) = sub < Specs.Length ? Specs[sub] : ("?", int.MinValue);
        var idx = ReadU2(code, ref pos, start, errors, new List<InstructionOperand>(), pool);
        var operands = new List<InstructionOperand> { new("local", idx.ToString(), idx, start + 2) };
        var length = 4;
        if (sub is 0x84 /*iinc*/)
        {
            var v = ReadS2(code, ref pos, start, errors);
            operands.Add(new InstructionOperand("s2", v.ToString(), v, start + 4));
            length = 6;
        }
        instructions.Add(new Instruction(0xC4, $"wide/{subName}", start, length, operands,
            Array.Empty<int>(), null, false, $"wide {subName} {idx}"));
        return pos;
    }

    private static int ReadU1(byte[] code, ref int pos, int start, List<DecodeError> errors)
    {
        if (pos >= code.Length)
        {
            errors.Add(new DecodeError(start, "V010", "Truncated instruction operand", $"code:{start}"));
            pos++;
            return 0;
        }
        return code[pos++];
    }

    private static sbyte ReadS1(byte[] code, ref int pos, int start, List<DecodeError> errors)
    {
        var v = ReadU1(code, ref pos, start, errors);
        return unchecked((sbyte)v);
    }

    private static short ReadS2(byte[] code, ref int pos, int start, List<DecodeError> errors)
    {
        if (pos + 2 > code.Length)
        {
            errors.Add(new DecodeError(start, "V010", "Truncated instruction operand", $"code:{start}"));
            pos += 2;
            return 0;
        }
        var v = (short)((code[pos] << 8) | code[pos + 1]);
        pos += 2;
        return v;
    }

    private static int ReadS4(byte[] code, ref int pos, int start, List<DecodeError> errors)
    {
        if (pos + 4 > code.Length)
        {
            errors.Add(new DecodeError(start, "V010", "Truncated instruction operand", $"code:{start}"));
            pos += 4;
            return 0;
        }
        var v = (code[pos] << 24) | (code[pos + 1] << 16) | (code[pos + 2] << 8) | code[pos + 3];
        pos += 4;
        return v;
    }

    private static ushort ReadU2(byte[] code, ref int pos, int start, List<DecodeError> errors,
        List<InstructionOperand> operands, IReadOnlyList<CpEntry> pool)
    {
        if (pos + 2 > code.Length)
        {
            errors.Add(new DecodeError(start, "V010", "Truncated constant pool index", $"code:{start}"));
            pos += 2;
            return 0;
        }
        var idx = (ushort)((code[pos] << 8) | code[pos + 1]);
        pos += 2;
        operands.Add(new InstructionOperand("cp_index", $"#{idx}", idx, start + 1));
        return idx;
    }
