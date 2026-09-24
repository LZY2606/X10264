using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

/// <summary>
/// Data-flow / abstract interpretation verifier. A worklist of blocks is
/// propagated; joins at leaders either accept a declared StackMapTable frame
/// (authoritative evidence) or apply version-specific rules. Missing frames are
/// NEVER reconstructed from later frames.
/// </summary>
public sealed partial class BytecodeVerifier
{
    private readonly MethodContext _c;
    private readonly List<VerifyEvent> _events = new();
    private Cfg _cfg = null!;
    private readonly Dictionary<int, VFrame> _blockIn = new();
    private readonly Dictionary<string, FrameSnapshot> _edgeStates = new();
    private readonly Dictionary<int, int> _bciBlock = new();
    private int _eventSeq;
    private int[] _depth = null!;

    public BytecodeVerifier(MethodContext ctx) => _c = ctx;

    public static VerificationResult Verify(MethodContext ctx) => new BytecodeVerifier(ctx).Run();

    private VerifyEvent Emit(VerifyEventCode code, Severity sev, string msg,
        int? bci = null, int? block = null, string? edge = null, int? frame = null,
        int? ex = null, string? fragment = null, string? rule = null,
        IReadOnlyList<int>? path = null, IReadOnlyList<string>? candidates = null,
        FrameSnapshot? expected = null, FrameSnapshot? actual = null)
    {
        var ev = new VerifyEvent
        {
            EventId = $"ev{_eventSeq++}:{code}:b{bci?.ToString() ?? "-"}",
            Code = code, Severity = sev, Message = msg, Bci = bci, BlockIndex = block,
            EdgeId = edge, FrameOrdinal = frame, ExceptionOrdinal = ex,
            SourceFragment = fragment, Rule = rule, EarliestPath = path,
            Candidates = candidates, ExpectedFrame = expected, ActualFrame = actual
        };
        _events.Add(ev);
        return ev;
    }

    private VerificationResult Run()
    {
        foreach (var d in _c.Decoded.Diagnostics)
            Emit(VerifyEventCode.InstructionDecode, Severity.Failure,
                $"{d.Code}: {d.Message}", bci: d.Offset, fragment: "code", rule: "decode");

        (_cfg, var cfgEvents) = CfgBuilder.Build(_c);
        _events.AddRange(cfgEvents);
        foreach (var b in _cfg.Blocks)
            for (int pc = b.Start; pc < b.EndExclusive; pc++) _bciBlock[pc] = b.Index;

        CheckStackMapFrames();
        if (!_c.Profile.IsTypeChecking)
            Emit(VerifyEventCode.LegacyInference, Severity.Info,
                _c.Profile.IsSplit
                    ? "major 50 split verifier: declared frames honored strictly, absent frames inferred"
                    : $"major {_c.Profile.MajorVersion} legacy inference verifier: missing frames tolerated",
                rule: "version-semantics");

        var entry = InitialFrame();
        _blockIn[0] = entry;
        var work = new Queue<int>();
        work.Enqueue(0);
        _depth = ComputePathDepth();

        // Fixpoint iteration. JoinInto re-enqueues a block only when its input
        // strictly moves up the finite abstract lattice, so this terminates.
        int guard = 0;
        int maxSteps = _cfg.Blocks.Count * 64 + 64;
        while (work.Count > 0 && guard++ < maxSteps)
        {
            int bi = work.Dequeue();
            var block = _cfg.Blocks[bi];
            if (!_blockIn.ContainsKey(block.Start)) continue;
            var frame = _blockIn[block.Start].Clone();
            block.Reachable = true;
            AnalyzeBlock(block, frame, work);
        }

        ReportUnreachableAndMissingFrames();
        AttachEarliestPaths();

        var diffs = BuildFrameDiffs();
        bool success = !_events.Any(e => e.IsFailure);
        var first = _events.Where(e => e.IsFailure)
            .OrderBy(e => e.EarliestPath is null ? int.MaxValue : e.EarliestPath.Count)
            .ThenBy(e => e.Bci ?? int.MaxValue).FirstOrDefault();

        return new VerificationResult
        {
            Success = success,
            Events = _events.ToList(),
            Cfg = _cfg,
            BlockInputs = _blockIn.ToDictionary(kv => kv.Key, kv => FrameSnapshot.From(kv.Value)),
            EdgeStates = _edgeStates,
            FrameDiffs = diffs,
            EarliestFailurePath = first?.EarliestPath ?? Array.Empty<int>()
        };
    }

    private int[] ComputePathDepth()
    {
        var depth = new int[_cfg.Blocks.Count];
        Array.Fill(depth, -1);
        depth[0] = 0;
        var q = new Queue<int>(); q.Enqueue(0);
        while (q.Count > 0)
        {
            int bi = q.Dequeue();
            foreach (var e in _cfg.Blocks[bi].OutEdges)
            {
                var tb = _cfg.BlockAt(e.ToBci);
                if (tb is null) continue;
                if (depth[tb.Index] < 0) { depth[tb.Index] = depth[bi] + 1; q.Enqueue(tb.Index); }
            }
        }
        return depth;
    }

    private VFrame InitialFrame()
    {
        var pars = DescriptorUtil.ParseParameters(_c.MethodDescriptor);
        int maxLocals = _c.Code.MaxLocals;
        var f = new VFrame(maxLocals);
        int slot = 0;
        if (!_c.IsStatic)
        {
            // Constructors begin with uninitializedThis; other methods with this.
            f.SetLocal(slot++, _c.IsConstructor ? (VType)VType.UninitThis : VType.Ref(_c.ClassFile.ThisName));
        }
        foreach (var p in pars)
        {
            var v = FieldVType(p);
            if (slot >= maxLocals)
            {
                Emit(VerifyEventCode.LocalOutOfRange, Severity.Failure,
                    $"Parameter {p} requires local {slot} but max_locals={maxLocals}",
                    bci: 0, fragment: "descriptor+max_locals", rule: "entry-frame");
                break;
            }
            f.SetLocal(slot, v);
            slot += v.Slots;
        }
        return f;
    }

    private static VType FieldVType(string fieldType) => DescriptorUtil.KindOf(fieldType) switch
    {
        JvmKind.Int => VType.Int,
        JvmKind.Long => VType.Long,
        JvmKind.Float => VType.Float,
        JvmKind.Double => VType.Double,
        JvmKind.Ref => fieldType.StartsWith("L")
            ? VType.Ref(fieldType.Substring(1, fieldType.Length - 2))
            : VType.Ref(fieldType), // arrays: keep the full descriptor as the name
        _ => VType.Top
    };
}
