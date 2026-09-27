using Godot;

namespace DaggerCave;

/// <summary>
/// Quitting without hanging. Whenever something new is drawn, the renderer queues background
/// pipeline compiles at low priority, and Godot 4.4's worker pool can wait forever at shutdown
/// while any of them are still queued. So a quit first lets that queue drain: it posts its own
/// low-priority marker task (the queue is first in, first out, so the marker only runs once
/// every compile queued ahead of it has been picked up) and leaves once a marker finishes with
/// no new compiles queued meanwhile. Closing the window takes the same path.
/// </summary>
public partial class SafeQuit : Node
{
    /// <summary>A quit is under way: frame-driven scripts should stop.</summary>
    public static bool Quitting { get; private set; }

    // generous: a software renderer can take many seconds over a burst of compiles
    private const ulong GiveUpMs = 20000;
    private static readonly RenderingServer.RenderingInfo[] Counters =
    {
        RenderingServer.RenderingInfo.PipelineCompilationsCanvas, RenderingServer.RenderingInfo.PipelineCompilationsMesh,
        RenderingServer.RenderingInfo.PipelineCompilationsSurface, RenderingServer.RenderingInfo.PipelineCompilationsDraw,
        RenderingServer.RenderingInfo.PipelineCompilationsSpecialization,
    };

    private int _code;
    private long _marker = -1;
    private ulong _queued, _since;

    public static void Request(Node from, int code = 0)
    {
        if (Quitting || !from.IsInsideTree()) return;
        Quitting = true;
        var tree = from.GetTree();
        tree.Paused = true; // nothing new appears (and needs compiling) while we wait
        // deferred: a test may ask to quit from a _Ready, while the root is busy adding children
        tree.Root.CallDeferred(Node.MethodName.AddChild, new SafeQuit { _code = code, ProcessMode = ProcessModeEnum.Always });
    }

    public override void _Ready() => _since = Time.GetTicksMsec();

    public override void _Process(double delta)
    {
        // a task hogging the low-priority slot mustn't keep the game open forever
        if (Time.GetTicksMsec() - _since > GiveUpMs) { Leave(); return; }
        if (_marker < 0)
        {
            _queued = Queued();
            _marker = WorkerThreadPool.AddTask(Callable.From(() => { }), false, "quit marker");
            return;
        }
        if (!WorkerThreadPool.IsTaskCompleted(_marker)) return;
        WorkerThreadPool.WaitForTaskCompletion(_marker);
        _marker = -1;
        if (Queued() == _queued) Leave();
    }

    private void Leave()
    {
        SetProcess(false);
        GetTree().Quit(_code);
    }

    /// <summary>Pipeline compiles queued since launch (the renderer's running totals).</summary>
    private static ulong Queued()
    {
        ulong n = 0;
        foreach (var c in Counters) n += RenderingServer.GetRenderingInfo(c);
        return n;
    }
}
