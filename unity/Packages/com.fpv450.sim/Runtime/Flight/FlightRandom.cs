using System;
using System.Collections.Generic;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// One named stream of a flight's randomness (design 9, K§6.9): counter-based, so draw i is a pure function of
    /// (Key, i), Key = FlightRandom.StreamKey(seed, name). Integer arithmetic only, the same bits everywhere.
    /// A value type held by the model that owns the stream; its Count is part of that model's state.
    /// </summary>
    public struct SeedStream
    {
        const ulong Gamma = 0x9E3779B97F4A7C15UL; // SplitMix64's increment, 2^64 / φ (published, Steele et al. 2014)

        public readonly string Name;
        public readonly ulong Key;

        /// <summary>Draws made so far.</summary>
        public ulong Count;

        public SeedStream(string name, ulong key)
        {
            Name = name;
            Key = key;
            Count = 0;
        }

        /// <summary>Draw i of the stream, without advancing it: SplitMix64's output at state Key + (i + 1)·γ.</summary>
        public readonly ulong BitsAt(ulong i) => unchecked(FlightRandom.Mix64(Key + (i + 1) * Gamma));

        /// <summary>The next 64 random bits.</summary>
        public ulong NextBits() => BitsAt(Count++);

        /// <summary>The next uniform double in [0, 1): the top 53 bits × 2^−53, exact.</summary>
        public double NextUnit() => (NextBits() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>The next uniform double in [lo, hi].</summary>
        public double NextUniform(double lo, double hi) => lo + (hi - lo) * NextUnit();
    }

    /// <summary>One per-flight draw, as the log header records it: the parameter, its stream, its bound and its value.</summary>
    public readonly struct ParameterDraw
    {
        public readonly string Stream, Parameter;
        public readonly double Lo, Hi, Value;

        /// <summary>The stream's draw index this value came from.</summary>
        public readonly ulong Index;

        public ParameterDraw(string stream, string parameter, double lo, double hi, double value, ulong index)
        {
            Stream = stream;
            Parameter = parameter;
            Lo = lo;
            Hi = hi;
            Value = value;
            Index = index;
        }
    }

    /// <summary>
    /// A flight's randomness (design 9, K§6.9): one 64-bit seed, shown and logged, and independent named streams from
    /// a 64-bit hash of (seed, name), so adding a stream never shifts another. Randomness only ever perturbs a named
    /// physical parameter within its bound (flight-randomness): per-flight draws go through Draw, which records
    /// them for the header; evolving streams (turbulence) are opened here and advanced by their model. Streams are
    /// opened and per-flight draws made at the flight's start, never in a step's steady state.
    /// </summary>
    public sealed class FlightRandom
    {
        readonly List<string> _opened = new List<string>();
        readonly List<ParameterDraw> _draws = new List<ParameterDraw>();

        public FlightRandom(ulong seed) => Seed = seed;

        public ulong Seed { get; }

        /// <summary>The stream names in the order they were opened.</summary>
        public IReadOnlyList<string> Opened => _opened;

        /// <summary>Every per-flight draw, in the order made.</summary>
        public IReadOnlyList<ParameterDraw> Draws => _draws;

        /// <summary>
        /// The stream named name, for one model. A name is printable ASCII (by convention "model.parameter", e.g.
        /// "wind.turbulence") and is opened once per flight: two models sharing one stream would correlate.
        /// </summary>
        public SeedStream Open(string name)
        {
            ulong key = StreamKey(Seed, name);
            if (_opened.Contains(name))
                throw new InvalidOperationException("stream already opened: " + name);
            _opened.Add(name);
            return new SeedStream(name, key);
        }

        /// <summary>A per-flight draw of a named physical parameter, uniform in [lo, hi], recorded for the header.</summary>
        public double Draw(ref SeedStream stream, string parameter, double lo, double hi)
        {
            if (!(lo <= hi))
                throw new ArgumentException("the bound must have lo <= hi: " + parameter);
            ulong index = stream.Count;
            double value = stream.NextUniform(lo, hi);
            _draws.Add(new ParameterDraw(stream.Name, parameter, lo, hi, value, index));
            return value;
        }

        /// <summary>
        /// A per-flight draw of a named physical parameter, nearly normal about 0 with standard deviation sigma: the
        /// sum of 12 uniform draws less 6 (Irwin–Hall: variance 1, kurtosis 2.9), times sigma, clamped to ±bound;
        /// recorded for the header with the bound and the index of its first draw.
        /// </summary>
        public double DrawNormal(ref SeedStream stream, string parameter, double sigma, double bound)
        {
            if (!(sigma >= 0 && bound >= 0))
                throw new ArgumentException("the spread and the bound are not negative: " + parameter);
            ulong index = stream.Count;
            double sum = -6;
            for (int i = 0; i < 12; i++) sum += stream.NextUnit();
            double value = sum * sigma;
            value = value > bound ? bound : value < -bound ? -bound : value;
            _draws.Add(new ParameterDraw(stream.Name, parameter, -bound, bound, value, index));
            return value;
        }

        /// <summary>
        /// The stream key of (seed, name): Mix64(seed + Mix64(FNV-1a 64 of the name's ASCII bytes)). Fixed forever,
        /// because old logs replay through it (a change is a superseding decision and a new log version).
        /// </summary>
        public static ulong StreamKey(ulong seed, string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("a stream needs a name", nameof(name));
            ulong h = 0xCBF29CE484222325UL; // FNV-1a 64 offset basis (published)
            foreach (char c in name)
            {
                if (c < 0x20 || c > 0x7E)
                    throw new ArgumentException("stream names are printable ASCII: " + name, nameof(name));
                h = unchecked((h ^ c) * 0x100000001B3UL); // FNV-1a 64 prime (published)
            }
            return Mix64(unchecked(seed + Mix64(h)));
        }

        /// <summary>SplitMix64's finaliser (published, Steele et al. 2014; Stafford's mix 13), a bijection on 64 bits.</summary>
        public static ulong Mix64(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
