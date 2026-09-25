using JvmVerifier.Core.Raw;

namespace JvmVerifier.Core.Binary;

/// <summary>
/// Linear decode of a Code[] byte stream into RawInstructions, preserving every
/// operand and its byte offset. Switch padding and wide opcodes are handled
/// explicitly. Undecodable tails produce diagnostics but never throw away the
/// bytes.
/// </summary>
public sealed class CodeDecoder
{
    private readonly byte[] _code;
    private readonly List<RawDiagnostic> _diags;
    private readonly string _loc;

    public CodeDecoder(byte[] code, List<RawDiagnostic> diags, string methodId)
    {
        _code = code;
        _diags = diags;
        _loc = methodId;
    }

    public List<RawInstruction> Decode()
    {
        var list = new List<RawInstruction>();
        int pc = 0;
        while (pc < _code.Length)
        {
            int start = pc;
            int opcode = _code[pc++];
            var info = OpcodeTable.Get(opcode);
            if (info is null)
            {
                _diags.Add(new RawDiagnostic("unknown-opcode", $"{_loc}@pc{start}",
                    $"unknown opcode 0x{opcode:x2} at byte offset {start}", $"insn@{start}"));
            list.Add(new RawInstruction(
                opcode, $"unknown_{opcode}", new Coords(start, DomainUnit.ByteOffsetInCode), Array.Empty<byte>())
            { Length = 1 });
                continue;
            }

            switch (opcode)
            {
                case 0xaa: DecodeTableSwitch(list, start, opcode, ref pc); break;
                case 0xab: DecodeLookupSwitch(list, start, opcode, ref pc); break;
                case 0xc4: DecodeWide(list, start, ref pc); break;
                default: DecodeFixed(list, info, start, opcode, ref pc); break;
            }
        }
        return list;
    }

    private byte[] Slice(int from, int len)
    {
        var r = new byte[len];
        for (int k = 0; k < len; k++)
            r[k] = from + k < _code.Length ? _code[from + k] : (byte)0;
        return r;
    }

    private static int U1(byte[] b, int i) => b[i];
    private static int S1(byte[] b, int i) => (sbyte)b[i];
    private static int U2(byte[] b, int i) => (b[i] << 8) | b[i + 1];
    private static int S2(byte[] b, int i) => (short)U2(b, i);
    private static int S4(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    private void DecodeFixed(List<RawInstruction> list, OpcodeInfo info, int start, int opcode, ref int pc)
    {
        int len = info.OperandLength;
        if (pc + len > _code.Length)
        {
            _diags.Add(new RawDiagnostic("truncated-instruction", $"{_loc}@pc{start}",
                $"instruction {info.Mnemonic} at {start} overruns Code[] (needs {len} operand bytes)", $"insn@{start}"));
            len = Math.Max(0, _code.Length - pc);
        }
        var raw = Slice(pc, len);
        var insn = new RawInstruction
        {
            Opcode = opcode,
            Mnemonic = info.Mnemonic,
            Pc = new Coords(start, DomainUnit.ByteOffsetInCode),
            Length = 1 + len,
            Raw = raw,
        };

        switch (opcode)
        {
            case 0x10 or 0x12: insn.CpIndex = U1(raw, 0); break;
            case 0x11: insn.IncrementConst = S2(raw, 0); break;
            case 0x13 or 0x14 or 0xb2 or 0xb3 or 0xb4 or 0xb5 or 0xb6 or 0xb7 or 0xb8 or 0xbd or 0xc0 or 0xc1:
                insn.CpIndex = U2(raw, 0); break;
            case 0xba: insn.CpIndex = U2(raw, 0); break;
            case 0xbc: insn.ArrayTypeCode = U1(raw, 0); break;
            case 0x15 or 0x16 or 0x17 or 0x18 or 0x19 or 0x36 or 0x37 or 0x38 or 0x39 or 0x3a or 0xa9:
                insn.LocalIndex = U1(raw, 0); break;
            case 0x84:
                insn.LocalIndex = U1(raw, 0);
                insn.IncrementConst = S1(raw, 1);
                break;
            case 0xc5:
                insn.CpIndex = U2(raw, 0);
                insn.Dimensions = U1(raw, 2);
                break;
            case 0x99 or 0x9a or 0x9b or 0x9c or 0x9d or 0x9e or 0x9f or 0xa0 or 0xa1 or 0xa2 or 0xa3 or 0xa4 or 0xa5 or 0xa6 or 0xa7 or 0xa8 or 0xc6 or 0xc7:
            {
                int delta = S2(raw, 0);
                insn.BranchDelta = delta;
                insn.BranchTarget = start + delta;
                insn.BranchTargetCoord = new Coords(insn.BranchTarget.Value, DomainUnit.ByteOffsetInCode);
                break;
            }
            case 0xc8 or 0xc9:
            {
                int delta = S4(raw, 0);
                insn.BranchDelta = delta;
                insn.BranchTarget = start + delta;
                insn.BranchTargetCoord = new Coords(insn.BranchTarget.Value, DomainUnit.ByteOffsetInCode);
                break;
            }
        }
        pc += len;
        list.Add(insn);
    }

    private void DecodeWide(List<RawInstruction> list, int start, ref int pc)
    {
        if (pc >= _code.Length)
        {
            _diags.Add(new RawDiagnostic("truncated-wide", $"{_loc}@pc{start}", "wide opcode missing inner opcode", $"insn@{start}"));
            list.Add(new RawInstruction(0xc4, "wide", new Coords(start, DomainUnit.ByteOffsetInCode), Array.Empty<byte>()) { Length = 1 });
            return;
        }
        int inner = _code[pc];
        int opLen = inner == 0x84 ? 5 : 3; // iinc has index+const
        if (pc + opLen > _code.Length)
        {
            _diags.Add(new RawDiagnostic("truncated-wide", $"{_loc}@pc{start}", "wide instruction overruns Code[]", $"insn@{start}"));
            opLen = Math.Max(0, _code.Length - pc);
        }
        var raw = Slice(pc, opLen);
        var insn = new RawInstruction
        {
            Opcode = 0xc4,
            Mnemonic = "wide " + OpcodeTable.Mnemonic(inner),
            Pc = new Coords(start, DomainUnit.ByteOffsetInCode),
            Length = 1 + opLen,
            Raw = raw,
            LocalIndex = U2(raw, 0),
        };
        if (inner == 0x84) insn.IncrementConst = S2(raw, 2);
        pc += opLen;
        list.Add(insn);
    }

    private void DecodeTableSwitch(List<RawInstruction> list, int start, int opcode, ref int pc)
    {
        int pad = (4 - (pc % 4)) % 4;
        int data = pc + pad;
        if (data + 12 > _code.Length)
        {
            _diags.Add(new RawDiagnostic("truncated-switch", $"{_loc}@pc{start}", "tableswitch overruns Code[]", $"insn@{start}"));
            return;
        }
        int def = S4(_code, data);
        int low = S4(_code, data + 4);
        int high = S4(_code, data + 8);
        int n = high - low + 1;
        var insn = new RawInstruction
        {
            Opcode = opcode,
            Mnemonic = "tableswitch",
            Pc = new Coords(start, DomainUnit.ByteOffsetInCode),
            SwitchDefault = start + def,
            SwitchLow = low,
            SwitchHigh = high,
        };
        for (int k = 0; k < n; k++)
        {
            int off = S4(_code, data + 12 + k * 4);
            insn.SwitchOffsets.Add(start + off);
        }
        insn.Length = data + 12 + n * 4 - start;
        insn.Raw = Slice(pc, insn.Length - 1);
        pc = start + insn.Length;
        list.Add(insn);
    }

    private void DecodeLookupSwitch(List<RawInstruction> list, int start, int opcode, ref int pc)
    {
        int pad = (4 - (pc % 4)) % 4;
        int data = pc + pad;
        if (data + 8 > _code.Length)
        {
            _diags.Add(new RawDiagnostic("truncated-switch", $"{_loc}@pc{start}", "lookupswitch overruns Code[]", $"insn@{start}"));
            return;
        }
        int def = S4(_code, data);
        int npairs = S4(_code, data + 4);
        var insn = new RawInstruction
        {
            Opcode = opcode,
            Mnemonic = "lookupswitch",
            Pc = new Coords(start, DomainUnit.ByteOffsetInCode),
            SwitchDefault = start + def,
        };
        for (int k = 0; k < npairs; k++)
        {
            int key = S4(_code, data + 8 + k * 8);
            int off = S4(_code, data + 12 + k * 8);
            insn.SwitchKeys.Add(key);
            insn.SwitchOffsets.Add(start + off);
        }
        insn.Length = data + 8 + npairs * 8 - start;
        insn.Raw = Slice(pc, insn.Length - 1);
        pc = start + insn.Length;
        list.Add(insn);
    }
}
