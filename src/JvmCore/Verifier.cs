namespace JvmCore;

/// <summary>Versioned verification semantics. Major version 50 (Java 6) is the boundary.</summary>
public interface IVerificationSemantics
{
    int Version { get; }
    string Name { get; }
    /// <summary>StackMapTable frames are authoritative and required at jump targets.</summary>
    bool UsesStackMapFrames { get; }
    /// <summary>Legacy type-inference verifier accepts jsr/ret subroutines.</summary>
    bool AllowsJsrRet { get; }
    /// <summary>Legacy verifier type-checks code that is not reachable from the entry.</summary>
    bool VerifiesUnreachableCode { get; }
}

public sealed record LegacySemantics(int Version = 49) : IVerificationSemantics
{
    public string Name => "legacy-type-inference";
    public bool UsesStackMapFrames => false;
    public bool AllowsJsrRet => true;
    public bool VerifiesUnreachableCode => true;
}

public sealed record StackMapSemantics(int Version = 52) : IVerificationSemantics
{
    public string Name => "stackmap-type-checking";
    public bool UsesStackMapFrames => true;
    public bool AllowsJsrRet => false;
    public bool VerifiesUnreachableCode => false;
}

public static class SemanticsFactory
{
    public static IVerificationSemantics ForMajorVersion(int major) =>
        major < 50 ? new LegacySemantics(major) : new StackMapSemantics(major);
}

/// <summary>Machine-readable judgment explaining why one rule fired (basis of a decision).</summary>
public sealed record Judgment(
    string Subject, int Offset, string Rule, string Version, string Decision,
    IReadOnlyDictionary<string, string> Evidence)
{
    public static Judgment Make(string subject, int offset, string rule, int version, string decision, object evidence)
        => new(subject, offset, rule, version.ToString(), decision,
            evidence.GetType().GetProperties().ToDictionary(p => p.Name, p => p.Value?.ToString() ?? ""));
}

public sealed record VerifyError(int Offset, string Code, string Message, IReadOnlyList<int> Path);
public sealed record JoinConflictRecord(int Offset, string Where, int Slot, IReadOnlyList<string> Candidates);
public sealed record CfgEdge(int From, int To, string Kind, FrameState? State);
public sealed record StateSnapshot(int Offset, FrameState In, FrameState? Out, bool Derived);

public sealed class VerificationResult
{
    public required string SemanticsName { get; init; }
    public required int Version { get; init; }
    public List<StateSnapshot> States { get; } = new();
    public List<VerifyError> Errors { get; } = new();
    public List<JoinConflictRecord> Conflicts { get; } = new();
    public List<Judgment> Judgments { get; } = new();
    public List<int> Unreachable { get; } = new();
    public List<string> Diagnostics { get; } = new();
    public List<CfgEdge> Edges { get; } = new();
    public List<StackMapFrameRaw> Frames { get; } = new();
    public bool Valid => Errors.Count == 0;
    public VerifyError? EarliestError =>
        Errors.OrderBy(e => e.Path.Count).ThenBy(e => e.Offset).FirstOrDefault();
}

/// <summary>
/// Abstract interpretation of one method's bytecode. In-states and out-states
/// are recorded per instruction. Each semantic rule that may differ by
/// classfile version produces a Judgment; ambiguous joins keep all candidates.
/// </summary>
public static class Verifier
{
    public static VerificationResult Verify(ClassFile cls, MethodModel method, int? forcedVersion = null)
    {
        var version = forcedVersion ?? (int)cls.MajorVersion.Value!;
        var sem = SemanticsFactory.ForMajorVersion(version);
        var instructions = InstructionDecoder.Decode(method.Code);
        var byOffset = instructions.ToDictionary(i => i.Offset);
        var boundaries = new HashSet<int>(instructions.Select(i => i.Offset));
        var result = new VerificationResult { SemanticsName = sem.Name, Version = version };
        var maxLocals = (int)method.MaxLocals.Value!;
        var maxStack = (int)method.MaxStack.Value!;

        var entry = EntryFrame(cls, method, maxLocals);

        var states = new Dictionary<int, FrameState>();   // current in-state per offset
        var outs = new Dictionary<int, FrameState>();
        var derived = new HashSet<int>();
        var pending = new HashSet<int>();
        var queue = new Queue<int>();
        var pred = new Dictionary<int, int>();
        var visited = new HashSet<int>();

        var mapFrames = new Dictionary<int, StackMapFrameRaw>();
        var frameLocalPad = maxLocals;
        foreach (var f in method.StackMapFrames)
        {
            var off = (int)f.Offset.Value!;
            result.Frames.Add(f);
            if (!boundaries.Contains(off))
            {
                result.Errors.Add(new VerifyError(off, "corrupted-frame-offset",
                    $"stack map frame offset {off} is not an instruction boundary",
                    PathTo(pred, off, 0)));
                result.Judgments.Add(Judgment.Make("corrupted-frame-offset", off,
                    "JVMS §4.7.4 frame offset_delta must name an instruction", version,
                    "rejected-frame-kept-raw", new { frameKind = f.FrameType, rawTag = f.RawTag }));
                continue;
            }
            mapFrames[off] = f;
        }

        var handlers = method.ExceptionTable
            .Select(h => (start: (int)h.StartPc.Value!, end: (int)h.EndPc.Value!,
                          handler: (int)h.HandlerPc.Value!, catchName: h.CatchTypeName))
            .ToList();

        FrameState Padded(VType[] locals)
        {
            if (locals.Length >= frameLocalPad) return new FrameState(locals, Array.Empty<VType>());
            var padded = new VType[frameLocalPad];
            Array.Copy(locals, padded, locals.Length);
            for (var i = locals.Length; i < frameLocalPad; i++) padded[i] = VType.Top;
            return new FrameState(padded, Array.Empty<VType>());
        }

        List<int> CurrentPath(int offset) => PathTo(pred, offset, 0);

        void AddError(int off, string code, string msg)
        {
            if (!result.Errors.Any(e => e.Offset == off && e.Code == code))
                result.Errors.Add(new VerifyError(off, code, msg, CurrentPath(off)));
        }

        void Enqueue(int offset, FrameState state, bool derivedState, int? predecessor)
        {
            if (offset < 0 || offset >= method.Code.Length)
            { AddError(offset, "branch-out-of-range", $"branch leaves code array (offset {offset})"); return; }
            if (!boundaries.Contains(offset))
            { AddError(offset, "corrupted-branch-target", $"branch target {offset} is not an instruction boundary"); return; }

            if (sem.UsesStackMapFrames && mapFrames.TryGetValue(offset, out var map))
            {
                // Frame is authoritative evidence: incoming state must be assignable to it.
                var mapState = Padded(map.State.Locals);
                var stackCopy = map.State.Stack.ToArray();
                mapState = new FrameState(mapState.Locals, stackCopy);
                if (visited.Contains(offset) && state != null)
                {
                    if (!state.AssignableTo(mapState, out var why))
                    {
                        AddError(offset, "stackmap-mismatch", $"state not assignable to stack map frame: {why}");
                        result.Judgments.Add(Judgment.Make("branch-join", offset,
                            "JVMS §4.10.1 type-checking with declared frame", version,
                            "frame-prevails", new { computed = state.LocalsText() + " " + state.StackText(), frame = mapState.LocalsText() + " " + mapState.StackText() }));
                    }
                    else
                    {
                        result.Judgments.Add(Judgment.Make("branch-join", offset,
                            "JVMS §4.10.1 type-checking with declared frame", version,
                            "agrees-with-frame", new { frame = mapState.LocalsText() }));
                    }
                }
                state = mapState; // never substitute anything for a declared frame
                derivedState = false;
            }
            else if (sem.UsesStackMapFrames && IsJumpTargetOrHandler(offset, instructions, handlers))
            {
                AddError(offset, "missing-stackmap-frame",
                    $"jump target/handler at {offset} has no StackMapTable frame");
                result.Judgments.Add(Judgment.Make("missing-frame", offset,
                    "JVMS §4.10.1 (major>=50): every jump target must declare a frame", version,
                    "rejected-no-back-inference",
                    new { nextDeclaredFrame = method.StackMapFrames.Select(f => (int)f.Offset.Value!)
                        .Where(o => o > offset).Select(o => o.ToString()).FirstOrDefault() ?? "none" }));
                derivedState = true; // computed state is a diagnostic continuation, not evidence
            }

            if (states.TryGetValue(offset, out var existing))
            {
                var (merged, slots, heightError) = FrameState.Merge(existing, state);
                if (heightError != null)
                {
                    AddError(offset, "stack-height-at-join", heightError);
                    return;
                }
                RecordJoin(offset, slots, result, version);
                var changed = !StatesEqual(existing, merged!);
                states[offset] = merged!;
                if (changed && !pending.Contains(offset)) { queue.Enqueue(offset); pending.Add(offset); }
            }
            else
            {
                states[offset] = state;
                queue.Enqueue(offset); pending.Add(offset);
            }
            if (predecessor != null && !pred.ContainsKey(offset) && offset != predecessor) pred[offset] = predecessor.Value;
            if (derivedState) derived.Add(offset);
        }

        Enqueue(0, entry, false, null);

        while (queue.Count > 0)
        {
            var offset = queue.Dequeue(); pending.Remove(offset);
            var instr = byOffset[offset];
            var inState = states[offset].Clone();
            visited.Add(offset);

            // Exception edges: handler enters with stack exactly [catchType] (§4.10.1).
            foreach (var h in handlers.Where(hh => offset >= hh.start && offset < hh.end))
            {
                var entryState = new FrameState((VType[])inState.Locals.Clone(),
                    new[] { h.catchName == null ? VType.RefOf("java/lang/Throwable") : VType.RefOf(h.catchName) });
                result.Edges.Add(new CfgEdge(offset, h.handler, "exception", entryState));
                result.Judgments.Add(Judgment.Make("exception-handler-entry", h.handler,
                    "JVMS §4.10.1 exception handler initial frame", version,
                    "stack-replaced-with-catch-type",
                    new { protectedPc = offset, catchType = h.catchName ?? "any" }));
                Enqueue(h.handler, entryState, false, offset);
            }

            var successors = Stepper.Step(cls, method, instr, inState, maxStack, sem, result, version);
            outs[offset] = inState;
            foreach (var (target, st, kind) in successors)
            {
                if (target == null)
                    result.Edges.Add(new CfgEdge(offset, -1, kind, inState));
                else
                {
                    result.Edges.Add(new CfgEdge(offset, target.Value, kind, st));
                    Enqueue(target.Value, st, derived.Contains(offset), offset);
                }
            }
        }

        // Legacy semantics: unreachable code is still type-checked.
        if (sem.VerifiesUnreachableCode)
        {
            foreach (var instr in instructions)
            {
                if (visited.Contains(instr.Offset)) continue;
                result.Unreachable.Add(instr.Offset);
                result.Judgments.Add(Judgment.Make("unreachable-block", instr.Offset,
                    "JVMS §4.10.1 legacy inference verifies unreachable code", version,
                    "verified-with-top-locals", new { majorVersion = version }));
                var loose = new FrameState(Enumerable.Range(0, maxLocals).Select(_ => VType.Top).ToArray(), Array.Empty<VType>());
                var succs = Stepper.Step(cls, method, instr, loose, maxStack, sem, result, version);
                if (!states.ContainsKey(instr.Offset))
                {
                    states[instr.Offset] = loose; outs[instr.Offset] = loose;
                }
                foreach (var (target, st, kind) in succs)
                    if (target != null && !visited.Contains(target.Value) && !states.ContainsKey(target.Value))
                        states[target.Value] = st;
            }
        }
        else
        {
            foreach (var instr in instructions.Where(i => !visited.Contains(i.Offset)))
                result.Unreachable.Add(instr.Offset);
            if (result.Unreachable.Count > 0)
                result.Judgments.Add(Judgment.Make("unreachable-block", result.Unreachable[0],
                    "JVMS §4.10.1 stackmap verifier only checks frame-reachable code", version,
                    "skipped-not-verified", new { offsets = string.Join(",", result.Unreachable) }));
        }

        foreach (var instr in instructions)
        {
            if (!states.TryGetValue(instr.Offset, out var inState)) continue;
            outs.TryGetValue(instr.Offset, out var outState);
            result.States.Add(new StateSnapshot(instr.Offset, inState, outState, derived.Contains(instr.Offset)));
        }
        result.Diagnostics.AddRange(cls.Diagnostics);
        return result;
    }

    private static bool StatesEqual(FrameState a, FrameState b)
    {
        if (a.Locals.Length != b.Locals.Length || a.Stack.Length != b.Stack.Length) return false;
        for (var i = 0; i < a.Locals.Length; i++) if (!a.Locals[i].Equals(b.Locals[i])) return false;
        for (var i = 0; i < a.Stack.Length; i++) if (!a.Stack[i].Equals(b.Stack[i])) return false;
        return true;
    }

    private static void RecordJoin(int offset, List<SlotMerge> slots, VerificationResult result, int version)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            var m = slots[i];
            if (m.Candidates.Count <= 1) continue;
            var localCount = Math.Min(i, int.MaxValue);
            var where = "local"; var slot = i;
            result.Conflicts.Add(new JoinConflictRecord(offset, where, slot,
                m.Candidates.Select(c => c.ToString()).Distinct().ToList()));
            result.Judgments.Add(Judgment.Make("branch-join", offset,
                "slot-wise merge at control-flow join", version,
                m.Conflict ? "conflict-kept-all-candidates" : "merged-candidates-retained",
                new { slot, candidates = string.Join("|", m.Candidates.Select(c => c.ToString()).Distinct()) }));
        }
    }

    private static FrameState EntryFrame(ClassFile cls, MethodModel m, int maxLocals)
    {
        var paramTypes = DescriptorNormalizer.ParseParameterTypes(m.DescriptorRaw, m.IsStatic, cls.ThisClass, m.IsConstructor);
        var locals = new VType[maxLocals];
        for (var i = 0; i < maxLocals; i++) locals[i] = VType.Top;
        var slot = 0;
        foreach (var t in paramTypes)
        {
            locals[slot] = t;
            if (t.IsCategory2) { locals[slot + 1] = VType.SecondSlot; slot += 2; }
            else slot++;
        }
        return new FrameState(locals, Array.Empty<VType>());
    }

    private static bool IsJumpTargetOrHandler(int offset, List<Instruction> instrs,
        List<(int start, int end, int handler, string? catchName)> handlers)
    {
        if (handlers.Any(h => h.handler == offset)) return true;
        foreach (var i in instrs)
        {
            if (InstructionDecoder.IsBranch(i.Opcode))
            {
                if (i.Opcode == 168) continue; // jsr targets are not ordinary join points
                if (InstructionDecoder.ShortBranch(i) == offset) return true;
                if (i.Opcode != 167 && i.Opcode != 168 && i.NextOffset == offset) return true;
            }
        }
        return false;
    }

    private static List<int> PathTo(Dictionary<int, int> pred, int offset, int root)
    {
        var path = new List<int>();
        var seen = new HashSet<int>();
        while (offset != root && seen.Add(offset) && pred.TryGetValue(offset, out var p))
        {
            path.Add(offset);
            offset = p;
        }
        path.Add(root);
        path.Reverse();
        return path;
    }
}
