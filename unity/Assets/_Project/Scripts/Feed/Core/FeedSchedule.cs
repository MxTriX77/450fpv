using System;
using System.Globalization;

namespace Fpv450.Feed
{
    /// <summary>
    /// The schedule a seed gives, field by field: every value the feed pass reads from <see cref="FeedEvents"/> and
    /// its <see cref="FeedLoss"/>. It is how the port is A/B-checked against the Godot feed (FEED-00: the same seed,
    /// the same schedule), and how a still or a field sweep is aimed at an event.
    /// </summary>
    public static class FeedSchedule
    {
        public const string Header =
            "field state busy degrade n3amp n3roll n3spacing n4amp n4span split topstrip blacka blackb snowlevel "
            + "snowchroma tell tearamp teartop grainboost";

        /// Runs a fresh scheduler from `seed` over fields 1 to `fields` at a constant `motorCurrent` (0–1), firing a
        /// terminal loss at `cutField` (0 for none) the way the Godot feed's `--video-field a-b+cut|+hardcut` did,
        /// and hands every field to `visit`.
        public static void Run(int seed, int fields, float motorCurrent, int cutField, bool abrupt,
            Action<FeedEvents> visit)
        {
            var events = new FeedEvents(seed);
            for (int f = 1; f <= fields; f++)
            {
                if (f == cutField)
                    events.TriggerCut(abrupt);
                events.AdvanceTo(f, motorCurrent);
                visit(events);
            }
        }

        /// One field as text, in <see cref="Header"/>'s order, floats round-trippable.
        public static string Row(FeedEvents e)
        {
            FeedLoss l = e.Loss;
            return string.Join(" ", e.Field.ToString(CultureInfo.InvariantCulture), l.Current.ToString(),
                l.Busy ? "1" : "0", F(e.Degrade), F(e.N3Amp), F(e.N3Roll), F(e.N3Spacing), F(e.N4Amp), F(e.N4Span),
                F(l.Split), F(l.TopStrip), F(l.BlackA), F(l.BlackB), F(l.SnowLevel), F(l.SnowChroma), F(l.Tell),
                F(l.TearAmp), F(l.TearTop), F(l.GrainBoost));
        }

        /// A digest of the whole run over the same values as <see cref="Row"/>, taken from their bits rather than
        /// their text, so it doesn't depend on the runtime's number formatting: FNV-1a, 64-bit.
        public static ulong Digest(int seed, int fields, float motorCurrent, int cutField, bool abrupt)
        {
            ulong h = 14695981039346656037UL;
            Run(seed, fields, motorCurrent, cutField, abrupt, e =>
            {
                FeedLoss l = e.Loss;
                h = Mix(h, e.Field);
                h = Mix(h, (int)l.Current);
                h = Mix(h, l.Busy ? 1 : 0);
                foreach (float v in new[]
                         {
                             e.Degrade, e.N3Amp, e.N3Roll, e.N3Spacing, e.N4Amp, e.N4Span, l.Split, l.TopStrip,
                             l.BlackA, l.BlackB, l.SnowLevel, l.SnowChroma, l.Tell, l.TearAmp, l.TearTop, l.GrainBoost,
                         })
                    h = Mix(h, BitConverter.SingleToInt32Bits(v));
            });
            return h;
        }

        /// What the Godot feed printed when a field was pinned (VideoFeed.cs:233): the quality drops and recovering
        /// dropouts per minute, and the first fields of the first dropouts and deep drops, to aim a still at.
        public static string Summary(int seed, int fields, float motorCurrent)
        {
            int dropStarts = 0, dropoutStarts = 0;
            bool wasDrop = false, wasLoss = false;
            string dropouts = "", deep = "";
            int dropoutsListed = 0, deepListed = 0;
            Run(seed, fields, motorCurrent, 0, false, e =>
            {
                bool drop = e.Degrade > 0f, lost = e.Loss.Busy;
                if (drop && !wasDrop)
                    dropStarts++;
                if (lost && !wasLoss)
                {
                    dropoutStarts++;
                    if (dropoutsListed++ < 6) // VideoFeed.cs:247
                        dropouts += e.Field + " ";
                }
                wasDrop = drop;
                wasLoss = lost;
                if (e.Degrade > 0.75f && deepListed++ < 5) // VideoFeed.cs:251
                    deep += e.Field + " ";
            });
            double minutes = fields / 50.0 / 60.0;
            return string.Format(CultureInfo.InvariantCulture,
                "seed {0}, {1} fields ({2:0.0} min): {3:0.0} quality drops/min, {4:0.0} dropouts/min "
                + "(the pilot asked for 3-6); dropouts start at fields [{5}], deep drops at [{6}]",
                seed, fields, minutes, dropStarts / minutes, dropoutStarts / minutes, dropouts.Trim(), deep.Trim());
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        static ulong Mix(ulong h, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (byte)(v >> (8 * i));
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
