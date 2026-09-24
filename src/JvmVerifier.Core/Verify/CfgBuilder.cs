using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public static class CfgBuilder
{
    public static (Cfg Cfg, List<VerifyEvent> Events) Build(MethodContext ctx)
    {
        var decoded = ctx.Decoded;
        var code = ctx.Code.Code;
        var events = new List<VerifyEvent>();
        var leaders = new SortedSet<int> { 0 };
        var insByStart = decoded.Instructions.ToDictionary(i => i.Offset);

        foreach (var ins in decoded.Instructions)
        {
            foreach (var t in InstructionDecoder.BranchTargets(ins))
                if (t >= 0 && t <= code.Length) leaders.Add(t);
                else events.Add(Fail(VerifyEventCode.BranchOutOfRange, ins.Offset,
                    $"Branch target {t} outside code [0,{code.Length})", "code", "bounds"));

            if (ins.Spec.IsBranch && !ins.Spec.IsUnconditional && ins.EndOffset < code.Length)
                leaders.Add(ins.EndOffset);
            if ((ins.Spec.IsUnconditional || ins.Spec.IsReturn || ins.Spec.IsThrow) && ins.EndOffset < code.Length)
                leaders.Add(ins.EndOffset);
        }
        foreach (var ex in ctx.Code.ExceptionTable)
        {
            if (ex.HandlerPc >= 0 && ex.HandlerPc < code.Length) leaders.Add(ex.HandlerPc);
            else events.Add(Fail(VerifyEventCode.BranchOutOfRange, ex.HandlerPc,
                $"Exception handler_pc {ex.HandlerPc} out of range", $"exception_table[{ex.Ordinal}]", "bounds"));
        }

        var leaderList = leaders.ToList();
        var blocks = new List<BasicBlock>();
        for (int i = 0; i < leaderList.Count; i++)
        {
            int start = leaderList[i];
            int end = i + 1 < leaderList.Count ? leaderList[i + 1] : code.Length;
            blocks.Add(new BasicBlock
            {
                Index = i, Start = start, EndExclusive = end,
                OutEdges = new(), InEdges = new()
            });
        }

        var edges = new List<CfgEdge>();
        int edgeOrdinal = 0;
        void AddEdge(int fromBlock, int toBci, EdgeKind kind, string? catchType, int? exOrd)
        {
            var e = new CfgEdge
            {
                FromBlock = fromBlock, ToBci = toBci, Kind = kind,
                CatchType = catchType, ExceptionOrdinal = exOrd, Ordinal = edgeOrdinal++
            };
            edges.Add(e);
            blocks[fromBlock].OutEdges.Add(e);
            var to = blocks.FirstOrDefault(b => b.Start == toBci);
            to?.InEdges.Add(e);
        }

        foreach (var block in blocks)
        {
            int last = block.Start;
            Instruction? lastIns = null;
            foreach (var ins in decoded.Instructions)
                if (ins.Offset >= block.Start && ins.Offset < block.EndExclusive) { last = ins.Offset; lastIns = ins; }

            if (lastIns is null) continue;

            if (lastIns.Spec.IsBranch)
            {
                foreach (var t in InstructionDecoder.BranchTargets(lastIns))
                {
                    bool isUncond = lastIns.Spec.IsUnconditional;
                    AddEdge(block.Index, t,
                        lastIns.Spec.Opcode is 170 or 171 ? EdgeKind.Switch :
                        isUncond ? EdgeKind.Unconditional : EdgeKind.Branch, null, null);
                }
                if (!lastIns.Spec.IsUnconditional && lastIns.EndOffset < code.Length)
                    AddEdge(block.Index, lastIns.EndOffset, EdgeKind.Fallthrough, null, null);
            }
            else if (lastIns.Spec.IsReturn || lastIns.Spec.IsThrow)
            {
                // terminal: no normal successor
            }
            else if (lastIns.EndOffset < code.Length)
            {
                AddEdge(block.Index, lastIns.EndOffset, EdgeKind.Fallthrough, null, null);
            }
        }

        // Exception edges: every block whose range overlaps [start_pc,end_pc).
        foreach (var ex in ctx.Code.ExceptionTable)
        {
            string? catchName = ex.CatchType == 0 ? null : ctx.ClassFile.ConstantPool.ClassName(ex.CatchType) ?? $"#{ex.CatchType}";
            foreach (var block in blocks)
            {
                int bStart = block.Start;
                int bEnd = block.EndExclusive;
                if (bStart >= ex.StartPc && bEnd <= ex.EndPc && ex.HandlerPc < code.Length &&
                    block.Start != ex.HandlerPc)
                {
                    AddEdge(block.Index, ex.HandlerPc, EdgeKind.Exception, catchName, ex.Ordinal);
                }
            }
        }

        var cfg = new Cfg { Blocks = blocks, Edges = edges };
        return (cfg, events);
    }

    internal static VerifyEvent Fail(VerifyEventCode code, int bci, string msg, string? fragment, string? rule,
        Severity sev = Severity.Failure) => new()
    {
        EventId = $"ev:{code}:b{bci}", Code = code, Severity = sev, Message = msg,
        Bci = bci, SourceFragment = fragment, Rule = rule
    };
}
