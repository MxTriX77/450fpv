using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tether
{
    /// <summary>
    /// The optical link the line carries (p7d-tether design 8; PHY-20, PHY-21, PHY-25), each step after the line's bends
    /// (FiberCuts.Collect): the margin above the receiver's threshold is the launch margin (link.launch_margin_db: the budget
    /// less the connectors) less the attenuation of the line from the station to the exit (link.attenuation_db_per_m times s at
    /// the exit), less every bend's macrobend loss (the table's law at the bend's radius times its turn over a full one, the
    /// exit lip among them) and every pressing contact's microbend loss (c_m times the force pressing it, rough or smooth). The
    /// pack's own peel bend is not a loss here (assumed: inside the canister it is part of the budget the link was set up
    /// with). A line hanging free costs only its length and its lip; the margin never reads the attitude rate or rain on the
    /// lens (K§8.7). A dip is the margin below link.dip_threshold_db; it ends above the threshold plus
    /// link.dip_hysteresis_db. A break makes the state Broken and the margin −∞ in its step, for the rest of the flight.
    /// Engine-free, 0 B a step.
    /// </summary>
    public sealed class FiberLink
    {
        readonly double _launch, _attenuation, _threshold, _hysteresis;

        public FiberLink(TetherTable table)
        {
            _launch = table.Number("link.launch_margin_db");
            _attenuation = table.Number("link.attenuation_db_per_m");
            _threshold = table.Number("link.dip_threshold_db");
            _hysteresis = table.Number("link.dip_hysteresis_db");
            Margin = _launch;
        }

        /// <summary>The margin after the last step, dB (−∞ once broken), and its parts: the line's length's and the bends'.</summary>
        public double Margin { get; private set; }
        public double LengthLoss { get; private set; }
        public double BendLoss { get; private set; }
        public LinkState State { get; private set; }

        /// <summary>The bend with the largest loss after the last step (FiberCuts' index; −1 for none) and that loss, dB.</summary>
        public int Largest { get; private set; } = -1;
        public double LargestLoss { get; private set; }

        /// <summary>A dip is on; this step began one, or ended one. Dips counts those begun this flight.</summary>
        public bool InDip { get; private set; }
        public bool DipBegan { get; private set; }
        public bool DipEnded { get; private set; }
        public int Dips { get; private set; }

        /// <summary>The dip's start step, its least margin so far (dB), and the largest loss at that least (dB) with its bend's
        /// member and point (kept from the step of the least, as the bends move).</summary>
        public long DipStart { get; private set; }
        public double DipLeast { get; private set; }
        public double DipLoss { get; private set; }
        public byte DipKind { get; private set; }
        public ulong DipMember { get; private set; }
        public Double3 DipPoint { get; private set; }

        /// <summary>The margin's law for a line of length metres from the station with the bends of cuts: launch − α·length −
        /// Σ (macro + micro).</summary>
        public double MarginOf(double length, FiberCuts cuts)
        {
            double bends = 0;
            for (int i = 0; i < cuts.BendCount; i++) bends += cuts.Bend(i).Macro + cuts.Bend(i).Micro;
            return _launch - _attenuation * length - bends;
        }

        /// <summary>The step's margin and the dip's state, for a line of length metres from the station (s at the exit).</summary>
        public void Update(double length, FiberCuts cuts, long step)
        {
            DipBegan = DipEnded = false;
            if (State == LinkState.Broken) return;
            double bends = 0, most = 0;
            int largest = -1;
            for (int i = 0; i < cuts.BendCount; i++)
            {
                ref readonly FiberBend b = ref cuts.Bend(i);
                double loss = b.Macro + b.Micro;
                bends += loss;
                if (loss > most)
                {
                    most = loss;
                    largest = i;
                }
            }
            LengthLoss = _attenuation * length;
            BendLoss = bends;
            Margin = _launch - LengthLoss - bends;
            Largest = largest;
            LargestLoss = most;
            if (!InDip)
            {
                if (!(Margin < _threshold)) return;
                InDip = DipBegan = true;
                Dips++;
                DipStart = step;
                DipLeast = double.PositiveInfinity;
            }
            if (Margin < DipLeast)
            {
                DipLeast = Margin;
                DipLoss = most;
                if (largest >= 0)
                {
                    ref readonly FiberBend b = ref cuts.Bend(largest);
                    DipKind = b.Kind;
                    DipMember = b.Member;
                    DipPoint = b.Point;
                }
                else
                {
                    DipKind = (byte)FiberMember.Span;
                    DipMember = 0;
                }
            }
            if (Margin > _threshold + _hysteresis)
            {
                InDip = false;
                DipEnded = true;
            }
        }

        /// <summary>The break: Broken and −∞ from this step on (a dip on ends with it, unlogged: the break supersedes it).</summary>
        public void Break()
        {
            State = LinkState.Broken;
            Margin = double.NegativeInfinity;
            InDip = false;
            DipBegan = DipEnded = false;
        }
    }
}
