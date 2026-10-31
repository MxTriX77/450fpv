using System;
using System.IO;
using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Log
{
    /// <summary>
    /// The flight log's writer (design 10, LOG-01). On the flight thread it is the scheduler's IFlightRecorder: each
    /// step's record and events are encoded into a preallocated ring, with no allocation, no lock and no disk. A flush
    /// worker thread writes the ring to the stream every few ms.
    ///
    /// The step never waits on the disk: the default ring holds 4 MiB, 4.6 s of flight at 916 B a step. Only a disk
    /// stalled for longer fills it, and then the flight thread waits for room. It logs that wait as a LogFull hitch
    /// before the step's record, and the scheduler's next pass logs the late steps as a Late one. Nothing is dropped,
    /// so a replay stays exact. A failed write (a full disk) stops the log and never the flight: the file then ends
    /// cut, and the summary reader says where.
    ///
    /// Threads: RecordStep and RecordHitch on the flight thread only; Close once the flight has stopped.
    /// </summary>
    public sealed class FlightRecorder : IFlightRecorder, IDisposable
    {
        public const int DefaultCapacity = 1 << 22;

        /// <summary>The smallest ring: a few steps with a full step's events (StepEvents' 64).</summary>
        public const int MinCapacity = 1 << 14;

        const int FlushPeriodMs = 2;
        const int StepBlockSize = LogFormat.BlockHeaderSize + LogFormat.StepSize;
        const int EventBlockSize = LogFormat.BlockHeaderSize + LogFormat.EventSize;

        readonly Stream _stream;
        readonly bool _ownsStream;
        readonly byte[] _ring;
        readonly long _mask;
        readonly byte[] _block = new byte[StepBlockSize]; // the block being encoded, then copied into the ring
        readonly Thread _worker;
        readonly long _headerLength;
        long _cursor;  // the flight thread's: the end of what it has put in the ring
        long _written; // published by the flight thread after each step, read by the worker
        long _read;    // published by the worker after each write, read by the flight thread
        Exception _fault;
        volatile bool _closing;
        bool _closed;
        LogEnd _counts;

        /// <summary>
        /// Writes the header to stream at once, then starts the flush worker. capacity is a power of two, at least
        /// MinCapacity. The stream is disposed at Close when ownsStream.
        /// </summary>
        public FlightRecorder(Stream stream, LogHeader header, int capacity = DefaultCapacity, bool ownsStream = true)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (capacity < MinCapacity || (capacity & (capacity - 1)) != 0)
                throw new ArgumentException("the ring's capacity must be a power of two, at least " + MinCapacity, nameof(capacity));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _ownsStream = ownsStream;
            _ring = new byte[capacity];
            _mask = capacity - 1;
            byte[] head = header.Encode();
            _stream.Write(head, 0, head.Length);
            _stream.Flush();
            _headerLength = head.Length;
            _worker = new Thread(Flush) { Name = "fpv450 log", IsBackground = true };
            _worker.Start();
        }

        /// <summary>A new log file at path (never an existing one), from LogStore.NewPath for a flight.</summary>
        public static FlightRecorder Create(string path, LogHeader header, int capacity = DefaultCapacity)
        {
            var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1 << 16);
            try
            {
                return new FlightRecorder(file, header, capacity, true);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        /// <summary>What has been recorded so far (SticksDropped is set at Close).</summary>
        public LogEnd Counts => _counts;

        /// <summary>Bytes on the stream: the header and everything flushed.</summary>
        public long Bytes => _headerLength + Volatile.Read(ref _read);

        /// <summary>The write that stopped the log, if one failed; the flight goes on without it.</summary>
        public Exception Fault => Volatile.Read(ref _fault);

        public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
        {
            if (_closed || Volatile.Read(ref _fault) != null) return;
            int count = events == null ? 0 : events.Count;
            int lost = events == null ? 0 : events.Lost;
            if (!Reserve(StepBlockSize + (count + (lost > 0 ? 1 : 0)) * EventBlockSize, state.Step)) return;

            LogFormat.EncodeBlockHeader(_block, LogFormat.StepBlock, LogFormat.StepSize);
            LogFormat.EncodeStep(_block.AsSpan(LogFormat.BlockHeaderSize), state, stick, trace);
            Put(StepBlockSize);
            _counts.Steps++;
            for (int i = 0; i < count; i++)
                PutEvent(state.Step, events[i]);
            if (lost > 0)
            {
                FlightEvent e = FlightEvent.Of(EventKind.EventsLost);
                e.A = lost;
                PutEvent(state.Step, e);
                _counts.EventsLost += lost;
            }
            Volatile.Write(ref _written, _cursor);
        }

        /// <summary>A late scheduler pass, stamped with the state its first late step made.</summary>
        public void RecordHitch(in Hitch hitch)
        {
            if (_closed || Volatile.Read(ref _fault) != null) return;
            long step = hitch.Step + 1;
            if (!Reserve(EventBlockSize, step)) return;
            FlightEvent e = FlightEvent.Of(EventKind.Hitch);
            e.Index = (byte)HitchCause.Late;
            e.A = hitch.Steps;
            e.B = hitch.LateUs;
            PutEvent(step, e);
            _counts.Hitches++;
            Volatile.Write(ref _written, _cursor);
        }

        /// <summary>
        /// A pause (HitchCause.Pause) before the step that makes state step: no step ran for pausedUs. Only while the
        /// flight thread is stopped (the app's pause), so it stands where a hitch would, before the steps it delayed.
        /// </summary>
        public void RecordPause(long step, long pausedUs)
        {
            if (_closed || Volatile.Read(ref _fault) != null) return;
            if (!Reserve(EventBlockSize, step)) return;
            FlightEvent e = FlightEvent.Of(EventKind.Hitch);
            e.Index = (byte)HitchCause.Pause;
            e.B = pausedUs;
            PutEvent(step, e);
            Volatile.Write(ref _written, _cursor);
        }

        /// <summary>
        /// Drains the ring, writes the end block and closes the stream (if owned). Once, after the flight thread has
        /// stopped; sticksDropped is the stick inbox's count (StickInbox.Dropped).
        /// </summary>
        public void Close(long sticksDropped = 0)
        {
            if (_closed) return;
            _closed = true;
            _closing = true;
            _worker.Join();
            try
            {
                if (_fault == null)
                {
                    _counts.SticksDropped = sticksDropped;
                    var end = new byte[LogFormat.BlockHeaderSize + LogFormat.EndSize];
                    LogFormat.EncodeBlockHeader(end, LogFormat.EndBlock, LogFormat.EndSize);
                    LogFormat.EncodeEnd(end.AsSpan(LogFormat.BlockHeaderSize), _counts);
                    _stream.Write(end, 0, end.Length);
                    _stream.Flush();
                }
            }
            catch (Exception e)
            {
                _fault = e;
            }
            finally
            {
                if (_ownsStream) _stream.Dispose();
            }
        }

        public void Dispose() => Close();

        // Room for bytes more, plus one hitch event. A full ring makes the flight thread wait for the worker, in 1 ms
        // sleeps, and logs the wait; false when the worker has failed and the log is over.
        bool Reserve(int bytes, long step)
        {
            bytes += EventBlockSize;
            if (Room() >= bytes) return true;
            long waits = 0;
            while (Room() < bytes)
            {
                if (Volatile.Read(ref _fault) != null) return false;
                waits++;
                Thread.Sleep(1);
            }
            _counts.LogWaits++;
            FlightEvent e = FlightEvent.Of(EventKind.Hitch);
            e.Index = (byte)HitchCause.LogFull;
            e.A = waits;
            PutEvent(step, e);
            return true;
        }

        long Room() => _ring.Length - (_cursor - Volatile.Read(ref _read));

        void PutEvent(long step, in FlightEvent e)
        {
            LogFormat.EncodeBlockHeader(_block, LogFormat.EventBlock, LogFormat.EventSize);
            LogFormat.EncodeEvent(_block.AsSpan(LogFormat.BlockHeaderSize), step, e);
            Put(EventBlockSize);
            _counts.Events++;
        }

        // Copies the encoded block into the ring at the cursor, wrapping at its end.
        void Put(int length)
        {
            int at = (int)(_cursor & _mask);
            int first = _ring.Length - at;
            if (first >= length)
            {
                Buffer.BlockCopy(_block, 0, _ring, at, length);
            }
            else
            {
                Buffer.BlockCopy(_block, 0, _ring, at, first);
                Buffer.BlockCopy(_block, first, _ring, 0, length - first);
            }
            _cursor += length;
        }

        // The flush worker: whatever the flight thread has published goes to the stream, then the room is handed back.
        // After Close is asked, it drains what is left and stops.
        void Flush()
        {
            try
            {
                while (true)
                {
                    bool closing = _closing;
                    long w = Volatile.Read(ref _written);
                    long r = _read;
                    if (w > r)
                    {
                        int at = (int)(r & _mask);
                        int count = (int)(w - r);
                        int first = _ring.Length - at;
                        if (first >= count)
                        {
                            _stream.Write(_ring, at, count);
                        }
                        else
                        {
                            _stream.Write(_ring, at, first);
                            _stream.Write(_ring, 0, count - first);
                        }
                        _stream.Flush();
                        Volatile.Write(ref _read, w);
                    }
                    else if (closing)
                    {
                        return;
                    }
                    else
                    {
                        Thread.Sleep(FlushPeriodMs);
                    }
                }
            }
            catch (Exception e)
            {
                Volatile.Write(ref _fault, e);
            }
        }
    }
}
