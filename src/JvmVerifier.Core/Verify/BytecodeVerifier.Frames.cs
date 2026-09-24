using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed partial class BytecodeVerifier
{
    private readonly Dictionary<int, StackMapFrame> _declared = new();
    private readonly List<FrameDiff> _frameDiffs = new();

    private StackMapFrame? DeclaredFrameAt(int bci) =>
        _declared.TryGetValue(bci, out var f) ? f : null;

    private void CheckStackMapFrames()
    {
        var smt = _c.Code.StackMapTable;
        if (smt is null) return;

        if (smt.DecodeError)
        {
            Emit(VerifyEventCode.CorruptFrameOffset, Severity.Failure,
                $"StackMapTable attribute is structurally corrupt: {smt.DecodeErrorMessage}",
                fragment: "StackMapTable", rule: "frame-structure");
            return;
        }

        var starts = _c.Decoded.Instructions.Select(i => i.Offset).ToHashSet();
        int previous = -1;
        foreach (var frame in smt.Frames)
        {
            // Monotonic, strictly increasing offsets are a hard structural rule.
            if (frame.Offset <= previous)
            {
                Emit(VerifyEventCode.CorruptFrameOffset, Severity.Failure,
                    $"StackMap frame #{frame.Ordinal} offset {frame.Offset} is not after " +
                    $"previous offset {previous} (encoded delta={frame.OffsetDelta})",
                    frame: frame.Ordinal, fragment: $"StackMapTable[{frame.Ordinal}].offset_delta",
                    rule: "frame-offset-order");
            }
            previous = frame.Offset;

            // A frame must certify an actual instruction. Missing frames are never
            // reconstructed; a wrong offset is corrupt evidence, kept in place.
            if (!starts.Contains(frame.Offset))
            {
                Emit(VerifyEventCode.CorruptFrameOffset, Severity.Failure,
                    $"StackMap frame #{frame.Ordinal} points at bci {frame.Offset}, " +
                    "which is not an instruction boundary",
                    bci: frame.Offset, frame: frame.Ordinal,
                    fragment: $"StackMapTable[{frame.Ordinal}].offset_delta",
                    rule: "frame-offset-target");
                continue;
            }

            // A frame only has meaning at a branch/exception target (leader).
            var block = _cfg.BlockAt(frame.Offset);
            if (block is not null && block.InEdges.All(e => e.Kind == EdgeKind.Fallthrough) && frame.Offset != 0)
            {
                Emit(VerifyEventCode.CorruptFrameOffset, Severity.Warning,
                    $"StackMap frame #{frame.Ordinal} at bci {frame.Offset} is not at a join target",
                    bci: frame.Offset, frame: frame.Ordinal,
                    fragment: $"StackMapTable[{frame.Ordinal}]", rule: "frame-placement");
            }
            _declared[frame.Offset] = frame;
        }
    }

    private void CheckDeclaredVsComputed(StackMapFrame frame, VFrame computed, int bci, int blockIndex)
    {
        var declaredIn = frame.ToIncoming(_c);
        bool match = FramesEqual(declaredIn, computed);
        var diff = new FrameDiff
        {
            FrameOrdinal = frame.Ordinal, Bci = bci,
            Declared = FrameSnapshot.From(declaredIn),
            Computed = FrameSnapshot.From(computed), Matches = match
        };
        _frameDiffs.Add(diff);
        if (!match)
        {
            Emit(VerifyEventCode.JoinConflict, Severity.Failure,
                $"Declared StackMap frame #{frame.Ordinal} at bci {bci} disagrees with " +
                "computed incoming frame",
                bci, blockIndex, frame: frame.Ordinal, fragment: $"StackMapTable[{frame.Ordinal}]",
                rule: "frame-evidence", expected: diff.Declared, actual: diff.Computed);
        }
    }

    private void CheckHandlerDeclaredFrame(int handlerBci, VFrame handlerFrame, CfgEdge edge)
    {
        var frame = DeclaredFrameAt(handlerBci);
        if (frame is null)
        {
            // Missing frame at an exception entry is reported separately only when
            // the handler is reachable; never infer a frame to stand in for it.
            return;
        }
        var declaredIn = frame.ToIncoming(_c);
        bool match = FramesEqual(declaredIn, handlerFrame);
        if (!match)
        {
            Emit(VerifyEventCode.ExceptionFrameMismatch, Severity.Failure,
                $"Exception handler at bci {handlerBci} declares a frame inconsistent " +
                "with the exception-entry state [throwable] + preserved locals",
                handlerBci, edge: edge.StableId, frame: frame.Ordinal,
                ex: edge.ExceptionOrdinal, fragment: $"StackMapTable[{frame.Ordinal}]+exception_table[{edge.ExceptionOrdinal}]",
                rule: "exception-entry-strict",
                expected: FrameSnapshot.From(handlerFrame), actual: FrameSnapshot.From(declaredIn));
        }
    }

    private void ReportUnreachableAndMissingFrames()
    {
        bool attrPresent = _c.Code.StackMapTable is not null;
        foreach (var block in _cfg.Blocks)
        {
            if (!block.Reachable)
            {
                Emit(VerifyEventCode.UnreachableBlock, Severity.Info,
                    $"Block bb{block.Index} at bci {block.Start} is not reachable from entry; " +
                    "its types are not checked and it needs no stack frame",
                    block.Start, block.Index, fragment: "code", rule: "reachability");
                continue;
            }

            bool needsFrame = block.Start != 0 && block.InEdges.Any(e =>
                e.Kind is EdgeKind.Branch or EdgeKind.Switch or EdgeKind.Unconditional or EdgeKind.Exception);
            if (!needsFrame) continue;
            if (DeclaredFrameAt(block.Start) is not null) continue;

            if (_c.Profile.MissingFrameIsError(attrPresent))
            {
                var edgeKinds = string.Join("/", block.InEdges
                    .Where(e => e.Kind != EdgeKind.Fallthrough).Select(e => e.Kind.ToString()));
                Emit(VerifyEventCode.MissingFrame, Severity.Failure,
                    $"Reachable join/handler at bci {block.Start} ({edgeKinds}) has no " +
                    $"StackMapTable frame (major {_c.Profile.MajorVersion}); no later frame is " +
                    "used to infer it",
                    block.Start, block.Index, fragment: "StackMapTable", rule: "missing-frame");
            }
            else
            {
                Emit(VerifyEventCode.LegacyInference, Severity.Info,
                    $"Reachable target bci {block.Start} has no declared frame; legacy " +
                    "inference verifier computes it (not recorded as frame evidence)",
                    block.Start, block.Index, fragment: "StackMapTable", rule: "legacy-inference");
            }
        }
    }

    private List<FrameDiff> BuildFrameDiffs() => _frameDiffs;

    /// <summary>BFS over the CFG records an ordered list of bcis from entry to
    /// the block of every failure; this is the "earliest failure path".</summary>
    private void AttachEarliestPaths()
    {
        var prev = new Dictionary<int, int> { [0] = -1 };
        var q = new Queue<int>(); q.Enqueue(0);
        while (q.Count > 0)
        {
            int bi = q.Dequeue();
            foreach (var e in _cfg.Blocks[bi].OutEdges)
            {
                var tb = _cfg.BlockAt(e.ToBci);
                if (tb is null || prev.ContainsKey(tb.Index)) continue;
                prev[tb.Index] = bi;
                q.Enqueue(tb.Index);
            }
        }

        foreach (var ev in _events)
        {
            if (!ev.IsFailure || ev.Bci is null) continue;
            var block = _cfg.Blocks.FirstOrDefault(b => ev.Bci.Value >= b.Start && ev.Bci.Value < b.EndExclusive);
            if (block is null || !prev.ContainsKey(block.Index)) continue;
            var chain = new List<int>();
            int cur = block.Index;
            while (cur != -1) { chain.Add(_cfg.Blocks[cur].Start); cur = prev[cur]; }
            chain.Reverse();
            if (chain[^1] != ev.Bci.Value) chain.Add(ev.Bci.Value);
            ev.GetType().GetProperty(nameof(VerifyEvent.EarliestPath))?.SetValue(ev, chain);
        }
    }
}
