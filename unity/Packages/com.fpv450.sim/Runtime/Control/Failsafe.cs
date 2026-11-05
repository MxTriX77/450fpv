using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Control
{
    /// <summary>The failsafe's stages (EventKind.Failsafe's Index).</summary>
    public enum FailsafeStage : byte { SignalLost = 0, Stage1 = 1, Stage2 = 2, Disarmed = 3 }

    /// <summary>
    /// Betaflight 4.5's receiver failsafe on a lost uplink (p7d-tether design 8, PHY-21, D-023): the fiber's break is the
    /// receiver's signal loss (the uplink is the same line, assumed: the class flies fiber for its jamming immunity), so from the
    /// step after the break no frame reaches the flight controller. Published (the Betaflight 4.5 documentation, Failsafe):
    /// - the receiver declares the signal lost RxLossSeconds after the last good frame, and the controller holds the last good
    ///   values HoldSeconds more (so the sticks it flies are the last frame's until then);
    /// - stage 1 then flies the channel fallbacks, the sticks centred and the throttle off (1000 µs), the switches held, until
    ///   failsafe_delay (tenths of a second) after the last good frame;
    /// - stage 2 runs failsafe_procedure: DROP disarms at once; AUTO-LAND holds failsafe_throttle (µs) with the sticks centred for
    ///   failsafe_off_delay (tenths), then disarms (the sim's controller has no self-level mode, so the landing holds the sticks
    ///   centred in acro: assumed); GPS-RESCUE is refused (no GPS).
    /// The uplink never comes back (the line is broken). Each stage is a Failsafe event. 0 B a step.
    /// </summary>
    public sealed class Failsafe
    {
        /// <summary>The receiver's signal-loss timeout and the hold after it, s (published, Betaflight 4.5).</summary>
        public const double RxLossSeconds = 0.1, HoldSeconds = 0.3;

        readonly double _delay, _offDelay, _throttle, _dt;
        readonly FailsafeProcedure _procedure;
        long _lost = -1;
        StickSample _last;
        int _stage = -1;

        public Failsafe(in FcSettings settings, double dt)
        {
            if (settings.failsafe_procedure == FailsafeProcedure.GPS_RESCUE)
                throw new ArgumentException("failsafe_procedure GPS-RESCUE is not modelled (no GPS): use DROP or AUTO-LAND");
            if (!(settings.failsafe_delay >= 2)) throw new ArgumentException("failsafe_delay is at least 2 (200 ms, Betaflight's floor)");
            _delay = settings.failsafe_delay / 10;
            _offDelay = settings.failsafe_off_delay / 10;
            _throttle = (settings.failsafe_throttle - 1000) / 1000.0;
            if (_throttle < 0) _throttle = 0;
            _procedure = settings.failsafe_procedure;
            _dt = dt;
        }

        /// <summary>The uplink is lost: frames stopped after the step that made the state's step count lostAt.</summary>
        public bool Active => _lost >= 0;
        public long LostAt => _lost;
        public FailsafeStage Stage => (FailsafeStage)(_stage < 0 ? 0 : _stage);

        /// <summary>The uplink is lost from the step whose state count is step; last is the last good frame's sample.</summary>
        public void Lose(long step, in StickSample last)
        {
            if (_lost >= 0) return;
            _lost = step;
            _last = last;
        }

        /// <summary>The sample the controller flies at the step that starts at the state's count step, the input's in force
        /// (its HostUs kept); the stages' events into events.</summary>
        public StickSample Filter(long step, in StickSample input, StepEvents events)
        {
            if (_lost < 0) return input;
            double t = (step - _lost) * _dt;
            StickSample k = _last;
            k.HostUs = input.HostUs;
            k.Failsafe = false;
            if (t >= RxLossSeconds) Enter(FailsafeStage.SignalLost, t, 0, events);
            double stage1 = RxLossSeconds + HoldSeconds < _delay ? RxLossSeconds + HoldSeconds : _delay;
            if (t >= stage1)
            {
                k.Roll = k.Pitch = k.Yaw = 0;
                k.Throttle = 0;
                Enter(FailsafeStage.Stage1, t, 0, events);
            }
            if (t >= _delay)
            {
                bool land = _procedure == FailsafeProcedure.AUTO_LAND;
                k.Throttle = land ? _throttle : 0;
                Enter(FailsafeStage.Stage2, t, k.Throttle, events);
                if (!land || t >= _delay + _offDelay)
                {
                    k.Throttle = 0;
                    k.Arm = false;
                    Enter(FailsafeStage.Disarmed, t, 0, events);
                }
            }
            return k;
        }

        void Enter(FailsafeStage stage, double t, double throttle, StepEvents events)
        {
            if ((int)stage <= _stage) return;
            _stage = (int)stage;
            FlightEvent e = FlightEvent.Of(EventKind.Failsafe);
            e.Index = (byte)stage;
            e.A = t;
            e.B = (double)(byte)_procedure;
            e.C = throttle;
            events.Add(e);
        }
    }
}
