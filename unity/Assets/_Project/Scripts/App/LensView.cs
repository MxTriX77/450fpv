using System;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.App
{
    /// <summary>
    /// The water on the FPV camera's lens, drawn (w21-rain-on-camera 1.2, FEED-13, D-126): the physics' LensWater (1.1) stepped
    /// on the flight's published poses, once a render frame, and its drops and trails put on the feed's LensMap as they are, for
    /// the feed's lens pass. Drawing only: the lens reads the poses and never feeds back, so the flight and its log are the same
    /// with it on or off, and a replay draws the same drops. Sunny (rate 0) keeps the lens dry and the map empty.
    /// </summary>
    public sealed class LensView
    {
        LensWater _water;
        LensMap _map;
        ulong _drawn;
        long _field = -1;

        /// <summary>The physics' lens (null before Begin).</summary>
        public LensWater Water => _water;

        /// <summary>The map the feed draws (null before Begin); its Wet says whether any water is on it.</summary>
        public LensMap Map => _map;

        /// <summary>The flight this lens is for (FlightSession.Flights), −1 before any.</summary>
        public int Flight { get; private set; } = -1;

        /// <summary>
        /// A new flight's lens, dry: drone.json's lens and airframe as built, the flight's rain rate (mm/h, FeedSignals.RainRate,
        /// 0 when Sunny) and seed (the lens's own stream).
        /// </summary>
        public void Begin(int flight, DroneSpec spec, MassModel airframe, double rateMmPerHour, ulong seed)
        {
            Flight = flight;
            _water = new LensWater(spec, airframe, rateMmPerHour, seed);
            LensSpec glass = _water.Spec;
            _map = new LensMap((float)glass.ClearRadius, (float)glass.CurvatureRadius, (float)glass.RecedingAngle);
            _drawn = 0;
            _field = -1;
        }

        /// <summary>
        /// Steps the lens to the newest whole field the flight has published and redraws the map when its water moved: true
        /// when the map changed (the rig uploads it then).
        /// </summary>
        public bool Advance(PoseBuffer poses)
        {
            if (_water == null) return false;
            _water.Advance(poses);
            if (_water.Field == _field) return false;
            _field = _water.Field;
            ulong state = State(_water);
            if (state == _drawn) return false;
            _drawn = state;
            Draw(_water, _map);
            return true;
        }

        /// <summary>The lens's drops and their trails on the map, as the physics gives them.</summary>
        public static void Draw(LensWater water, LensMap map)
        {
            map.Clear();
            ReadOnlySpan<LensDrop> drops = water.Drops;
            for (int i = 0; i < drops.Length; i++)
            {
                LensDrop d = drops[i];
                map.Add(new LensBlob
                {
                    X = (float)d.X, Y = (float)d.Y, Radius = (float)d.Radius, Height = (float)d.Height,
                    Elongation = (float)d.Elongation, AxisX = (float)d.AxisX, AxisY = (float)d.AxisY,
                });
                ReadOnlySpan<LensTrailPoint> trail = water.TrailOf(i);
                for (int p = 1; p < trail.Length; p++)
                    map.Add(new LensFilm
                    {
                        X0 = (float)trail[p - 1].X, Y0 = (float)trail[p - 1].Y, X1 = (float)trail[p].X, Y1 = (float)trail[p].Y,
                        Width = (float)trail[p].Width, Film = (float)trail[p].Film,
                    });
            }
        }

        // What the map is drawn from, hashed (FNV-1a over the doubles' bits): unchanged while every drop stays pinned.
        static ulong State(LensWater water)
        {
            ulong h = 14695981039346656037UL;
            void Mix(double v)
            {
                h ^= (ulong)BitConverter.DoubleToInt64Bits(v);
                h *= 1099511628211UL;
            }
            ReadOnlySpan<LensDrop> drops = water.Drops;
            Mix(drops.Length);
            for (int i = 0; i < drops.Length; i++)
            {
                LensDrop d = drops[i];
                Mix(d.Id);
                Mix(d.X);
                Mix(d.Y);
                Mix(d.Volume);
                Mix(d.Elongation);
                Mix(d.AxisX);
                Mix(d.TrailCount);
                ReadOnlySpan<LensTrailPoint> trail = water.TrailOf(i);
                if (trail.Length > 0) Mix(trail[0].X); // the oldest point: dropped as the drop lays more
            }
            return h;
        }
    }
}
