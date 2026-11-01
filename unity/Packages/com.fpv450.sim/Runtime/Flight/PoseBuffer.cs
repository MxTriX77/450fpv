using System.Threading;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The double-buffered pose (design 1, PHB-01): the flight thread publishes the last two steps' poses after every
    /// step, and the render thread reads the newest pair without ever blocking the writer. A wait-free triple buffer
    /// of PosePair: the writer fills its own slot and swaps it into the middle; the reader swaps the middle out when
    /// it is fresh. One writer thread, one reader thread; no allocation after construction.
    ///
    /// Beside the pair, the last HistorySteps poses by step (p7e-camera-feel design 2), so the renderer can take the
    /// camera at a PAL field's own time and along its read: a ring of slots, each under its own sequence number (the
    /// writer marks the slot, copies the pose, then publishes its step; a reader keeps a copy only when the slot held
    /// the step it asked for before and after the copy). The writer never waits.
    /// </summary>
    public sealed class PoseBuffer
    {
        const int Fresh = 4; // set on the middle index when it holds a pair the reader has not taken
        const int Index = 3;

        /// <summary>The poses the history keeps: 128 steps (design 2: a field and the longest exposure need 41, a 30 fps
        /// render frame adds 34; derived, with margin).</summary>
        public const int HistorySteps = 128;
        const int HistoryMask = HistorySteps - 1;

        readonly PosePair[] _slots = new PosePair[3];
        int _back = 0;   // the writer's
        int _middle = 1; // shared, swapped atomically
        int _front = 2;  // the reader's
        PoseFrame _last; // the writer's copy of the latest pose, the next pair's Previous
        long _startUs;

        readonly PoseFrame[] _history = new PoseFrame[HistorySteps];
        readonly long[] _held = new long[HistorySteps]; // each slot's step, −1 while the writer fills it
        long _latestStep = -1;

        public PoseBuffer()
        {
            for (int i = 0; i < HistorySteps; i++) _held[i] = -1;
        }

        /// <summary>Flight thread, once, before the first step: the start time and the initial pose (Previous = Current).</summary>
        public void Begin(long startUs, in PoseFrame initial, in FeedSignals signals)
        {
            _startUs = startUs;
            _last = initial;
            Publish(initial, signals);
        }

        /// <summary>Flight thread, after every step.</summary>
        public void Publish(in PoseFrame current, in FeedSignals signals)
        {
            ref PosePair slot = ref _slots[_back];
            slot.Previous = _last;
            slot.Current = current;
            slot.Signals = signals;
            slot.StartUs = _startUs;
            _last = current;
            _back = Interlocked.Exchange(ref _middle, _back | Fresh) & Index;
            Keep(current);
        }

        // The history's write: the slot marked (a full fence, so the mark lands before the copy), the pose, then its step.
        void Keep(in PoseFrame pose)
        {
            int i = (int)(pose.Step & HistoryMask);
            Interlocked.Exchange(ref _held[i], -1);
            _history[i] = pose;
            Volatile.Write(ref _held[i], pose.Step);
            Volatile.Write(ref _latestStep, pose.Step);
        }

        /// <summary>Render thread: the newest step the history holds, −1 before the first Begin.</summary>
        public long LatestStep => Volatile.Read(ref _latestStep);

        /// <summary>
        /// Render thread: the pose of a step the history still holds (from LatestStep − HistorySteps + 1 to LatestStep,
        /// less any slot the writer is refilling now); false for any other step. A flight's steps restart at 0, so a
        /// read across a new flight's Begin can return the last flight's step for that one call.
        /// </summary>
        public bool TryRead(long step, out PoseFrame pose)
        {
            pose = default;
            if (step < 0) return false;
            int i = (int)(step & HistoryMask);
            if (Volatile.Read(ref _held[i]) != step) return false;
            pose = _history[i];
            Interlocked.MemoryBarrier(); // the copy completes before the second look
            if (Volatile.Read(ref _held[i]) == step) return true;
            pose = default;
            return false;
        }

        /// <summary>
        /// Render thread: the newest published pair. The reference stays valid until this thread's next call; before
        /// the flight's Begin it is all zeros.
        /// </summary>
        public ref readonly PosePair Read()
        {
            if ((Volatile.Read(ref _middle) & Fresh) != 0)
                _front = Interlocked.Exchange(ref _middle, _front) & Index;
            return ref _slots[_front];
        }
    }
}
