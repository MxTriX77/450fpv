using System.Threading;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The double-buffered pose (design 1, PHB-01): the flight thread publishes the last two steps' poses after every
    /// step, and the render thread reads the newest pair without ever blocking the writer. A wait-free triple buffer
    /// of PosePair: the writer fills its own slot and swaps it into the middle; the reader swaps the middle out when
    /// it is fresh. One writer thread, one reader thread; no allocation after construction.
    /// </summary>
    public sealed class PoseBuffer
    {
        const int Fresh = 4; // set on the middle index when it holds a pair the reader has not taken
        const int Index = 3;

        readonly PosePair[] _slots = new PosePair[3];
        int _back = 0;   // the writer's
        int _middle = 1; // shared, swapped atomically
        int _front = 2;  // the reader's
        PoseFrame _last; // the writer's copy of the latest pose, the next pair's Previous
        long _startUs;

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
