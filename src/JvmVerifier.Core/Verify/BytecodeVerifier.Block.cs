using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed partial class BytecodeVerifier
{
    private void AnalyzeBlock(BasicBlock block, VFrame frame, Queue<int> work)
    {
        var instructions = _c.Decoded.Instructions
            .Where(i => i.Offset >= block.Start && i.Offset < block.EndExclusive)
            .OrderBy(i => i.Offset).ToList();

        foreach (var ins in instructions)
        {
            int bci = ins.Offset;
            var declaredFrame = DeclaredFrameAt(bci);
            if (declaredFrame is not null)
            {
                // Declared StackMapTable frame is authoritative at this leader.
                // It must be compatible with the computed incoming state.
                CheckDeclaredVsComputed(declaredFrame, frame, bci, block.Index);
                frame = declaredFrame.ToIncoming(_c);
                _blockIn[bci] = frame.Clone();
            }

            try
            {
                Execute(ins, frame, block, bci, work);
            }
            catch (StackUnderflowException)
            {
                Emit(VerifyEventCode.StackUnderflow, Severity.Failure,
                    $"{ins.Mnemonic} at {bci} pops past operand stack bottom",
                    bci, block.Index, fragment: "code", rule: "abstract-execution");
            }
            catch (LocalAccessException ex)
            {
                Emit(VerifyEventCode.LocalOutOfRange, Severity.Failure,
                    $"{ins.Mnemonic} at {bci}: {ex.Message}",
                    bci, block.Index, fragment: "code+max_locals", rule: "abstract-execution");
            }

            if (frame.StackDepth > _c.Code.MaxStack)
                Emit(VerifyEventCode.MaxStackExceeded, Severity.Failure,
                    $"Stack depth {frame.StackDepth} exceeds max_stack {_c.Code.MaxStack} at {bci}",
                    bci, block.Index, fragment: "code+max_stack", rule: "abstract-execution");
        }

        // Successor propagation.
        var last = instructions.LastOrDefault();
        if (last is null)
        {
            // Empty block (can happen for dead padding): fall through.
            PropagateSuccessors(block, frame, work);
            return;
        }
        PropagateSuccessors(block, frame, work);
    }

    private void PropagateSuccessors(BasicBlock block, VFrame outFrame, Queue<int> work)
    {
        foreach (var edge in block.OutEdges)
        {
            var target = _cfg.BlockAt(edge.ToBci);
            if (target is null) continue;

            if (edge.Kind == EdgeKind.Exception)
            {
                var handlerFrame = HandlerEntryFrame(edge);
                _edgeStates[edge.StableId] = FrameSnapshot.From(outFrame);
                Emit(VerifyEventCode.ExceptionEdge, Severity.Info,
                    $"Exception edge {edge.StableId} -> handler bci {edge.ToBci} " +
                    $"({edge.CatchType ?? "finally/catch-all"}); handler stack = [throwable]",
                    bci: edge.ToBci, block: target.Index, edge: edge.StableId,
                    ex: edge.ExceptionOrdinal, fragment: $"exception_table[{edge.ExceptionOrdinal}]",
                    rule: "exception-entry");

                // Exception edges are strict on 50+: declared frame must equal handler frame.
                if (_c.Profile.ExceptionFrameStrict)
                    CheckHandlerDeclaredFrame(target.Start, handlerFrame, edge);

                if (JoinInto(target.Index, handlerFrame, work))
                    Emit(VerifyEventCode.ExceptionEdge, Severity.Info,
                        $"Handler block bb{target.Index}@bci{target.Start} (re)scheduled",
                        bci: target.Start, block: target.Index, edge: edge.StableId, rule: "exception-entry");
            }
            else
            {
                _edgeStates[edge.StableId] = FrameSnapshot.From(outFrame);
                JoinInto(target.Index, outFrame, work);
            }
        }
    }

    private VFrame HandlerEntryFrame(CfgEdge edge)
    {
        var f = new VFrame(_c.Code.MaxLocals);
        // Locals are preserved on exception entry; stack is exactly the throwable.
        if (_blockIn.TryGetValue(_cfg.Blocks[edge.FromBlock].Start, out var origin))
            Array.Copy(origin.Locals, f.Locals, f.Locals.Length);
        string throwable = edge.CatchType is null
            ? "java/lang/Throwable"
            : edge.CatchType.StartsWith("[") ? edge.CatchType : edge.CatchType;
        f.Push(VType.Ref(throwable));
        return f;
    }

    /// <summary>Join incoming state into block input. Returns true if changed (re-enqueue).</summary>
    private bool JoinInto(int blockIndex, VFrame incoming, Queue<int> work)
    {
        int start = _cfg.Blocks[blockIndex].Start;
        if (!_blockIn.TryGetValue(start, out var existing))
        {
            _blockIn[start] = incoming.Clone();
            work.Enqueue(blockIndex);
            return true;
        }
        var merged = MergeFrames(existing, incoming, start);
        if (!FramesEqual(existing, merged))
        {
            _blockIn[start] = merged;
            work.Enqueue(blockIndex);
            return true;
        }
        return false;
    }

    private bool FramesEqual(VFrame a, VFrame b)
    {
        if (a.Locals.Length != b.Locals.Length) return false;
        for (int i = 0; i < a.Locals.Length; i++)
            if (!a.Locals[i].Equals(b.Locals[i])) return false;
        if (a.Stack.Count != b.Stack.Count) return false;
        for (int i = 0; i < a.Stack.Count; i++)
            if (!a.Stack[i].Equals(b.Stack[i])) return false;
        return true;
    }

    private VFrame MergeFrames(VFrame a, VFrame b, int bci)
    {
        var locals = new VType[a.Locals.Length];
        for (int i = 0; i < locals.Length; i++)
        {
            var r = TypeMerge.Merge(a.Locals[i], b.Locals[i]);
            if (r.Candidates.Count == 0)
            {
                Emit(VerifyEventCode.JoinConflict, Severity.Failure,
                    $"Branch join at bci {bci} local {i}: {r.AmbiguityReason}",
                    bci, fragment: "code+stackmap", rule: "join",
                    candidates: Array.Empty<string>());
                locals[i] = VType.Top;
            }
            else
            {
                if (r.AmbiguityReason is not null)
                    Emit(VerifyEventCode.JoinAmbiguity, Severity.Info,
                        $"Branch join at bci {bci} local {i}: {r.AmbiguityReason}; " +
                        $"{r.Candidates.Count} legal candidate(s) retained",
                        bci, fragment: "code+stackmap", rule: "join",
                        candidates: r.Candidates.Select(c => c.Token).ToArray());
                locals[i] = r.Chosen;
            }
        }
        var stack = new List<VType>();
        if (a.Stack.Count != b.Stack.Count)
        {
            Emit(VerifyEventCode.JoinConflict, Severity.Failure,
                $"Branch join at bci {bci}: operand stack depth differs " +
                $"{a.Stack.Count} vs {b.Stack.Count} (logical values)",
                bci, fragment: "code+stackmap", rule: "join");
            int n = Math.Min(a.Stack.Count, b.Stack.Count);
            for (int i = 0; i < n; i++) stack.Add(TypeMerge.Merge(a.Stack[i], b.Stack[i]).Chosen);
        }
        else
        {
            for (int i = 0; i < a.Stack.Count; i++)
            {
                var r = TypeMerge.Merge(a.Stack[i], b.Stack[i]);
                if (r.Candidates.Count == 0)
                {
                    Emit(VerifyEventCode.JoinConflict, Severity.Failure,
                        $"Branch join at bci {bci} stack {i}: {r.AmbiguityReason}",
                        bci, fragment: "code+stackmap", rule: "join");
                    stack.Add(VType.Top);
                }
                else stack.Add(r.Chosen);
            }
        }
        return new VFrame(locals, stack);
    }
}
