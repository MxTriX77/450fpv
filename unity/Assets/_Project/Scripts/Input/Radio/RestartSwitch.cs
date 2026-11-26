using System;

namespace Fpv450.Input.Radio
{
    public enum SwitchStep { On, Off, Done }

    /// <summary>
    /// The restart switch's choice in Calibrate (the pilot, 2026-11-25: the restart "is also on the Tx controller - A
    /// button"), engine-free and on the radio alone, as the wizard's arm step: the axis or button that changes by a
    /// quarter of its range or more when the pilot flips a switch on, held 0.2 s, then back off for 0.2 s. A radio in
    /// joystick mode sends its channels, not its switches (a TX12: channels 1–8 as axes, 9–32 as buttons), so the switch
    /// is whichever channel the radio's mixer gives it. The profile's sticks and its arm switch are never taken: a flip
    /// of the arm switch is named in the prompt, so a flip never both arms and restarts.
    /// </summary>
    public sealed class SwitchChoice
    {
        readonly CalibrationProfile _profile;
        readonly RadioAxis[] _axes;
        RadioReport _rest;
        int _candidate = -1;
        long _since;

        /// <param name="profile">The profile the switch is chosen for: its sticks and arm switch are not offered.</param>
        public SwitchChoice(CalibrationProfile profile, RadioAxis[] axes)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _axes = axes ?? throw new ArgumentNullException(nameof(axes));
        }

        public SwitchStep Step { get; private set; } = SwitchStep.On;

        /// <summary>The switch, once Step is Off.</summary>
        public ArmCalibration Result { get; } = new ArmCalibration();

        /// <summary>The arm switch was flipped while a switch was asked for.</summary>
        public bool ArmFlipped { get; private set; }

        /// <summary>What the pilot does now, in plain words.</summary>
        public string Prompt
        {
            get
            {
                switch (Step)
                {
                    case SwitchStep.On:
                        return ArmFlipped
                            ? "That is the arm switch (" + _profile.Arm.Where + "). Flip it back off, and flip another switch for Restart."
                            : "Flip the switch you want for Restart (SA) on.";
                    case SwitchStep.Off: return "Flip it back off.";
                    default: return "Restart on " + Result.Where + ".";
                }
            }
        }

        /// <summary>Takes one report, in arrival order: the first is the switches' rest.</summary>
        public void Feed(RadioReport r)
        {
            if (Step == SwitchStep.Done) return;
            if (_rest == null)
            {
                _rest = new RadioReport(r.Axes.Length);
                r.CopyTo(_rest);
                return;
            }
            if (Step == SwitchStep.Off)
            {
                if (CalibratedStickMap.Armed(r, Result)) _since = r.HostUs;
                else if (r.HostUs - _since >= CalibrationWizard.ArmHoldUs) Step = SwitchStep.Done;
                return;
            }
            ArmCalibration arm = _profile.Arm;
            if (arm.Control != ArmControl.None && CalibratedStickMap.Armed(r, arm) != CalibratedStickMap.Armed(_rest, arm)) ArmFlipped = true;

            int candidate = -1, best = 0;
            for (int i = 0; i < r.Axes.Length && i < _axes.Length; i++)
            {
                if (!Free(_profile, ArmControl.Axis, i)) continue;
                int d = Math.Abs(r.Axes[i] - _rest.Axes[i]);
                if (d >= CalibrationWizard.ArmChange * (_axes[i].Max - _axes[i].Min) && d > best)
                {
                    best = d;
                    candidate = i;
                }
            }
            ulong changed = r.Buttons ^ _rest.Buttons;
            for (int b = 0; candidate < 0 && b < 64; b++)
                if (((changed >> b) & 1) != 0 && Free(_profile, ArmControl.Button, b)) candidate = 1000 + b;
            if (candidate != _candidate)
            {
                _candidate = candidate;
                _since = r.HostUs;
                return;
            }
            if (candidate < 0 || r.HostUs - _since < CalibrationWizard.ArmHoldUs) return;

            if (candidate < 1000)
            {
                Result.Control = ArmControl.Axis;
                Result.Index = candidate;
                Result.Threshold = (_rest.Axes[candidate] + r.Axes[candidate]) / 2;
                Result.OnHigh = r.Axes[candidate] > _rest.Axes[candidate];
            }
            else
            {
                Result.Control = ArmControl.Button;
                Result.Index = candidate - 1000;
                Result.OnHigh = ((r.Buttons >> Result.Index) & 1) != 0;
            }
            Step = SwitchStep.Off;
            _since = r.HostUs;
        }

        /// <summary>The switch is one, and none of the profile's sticks or its arm switch.</summary>
        public static bool Free(CalibrationProfile p, ArmCalibration sw) => sw.Control != ArmControl.None && Free(p, sw.Control, sw.Index);

        static bool Free(CalibrationProfile p, ArmControl control, int index)
        {
            if (p.Arm.Control == control && p.Arm.Index == index) return false;
            return control != ArmControl.Axis || (index != p.Roll.Axis && index != p.Pitch.Axis && index != p.Yaw.Axis && index != p.Throttle.Axis);
        }
    }

    /// <summary>
    /// The restart switch in flight (FlightScene): a flip restarts the flight once, as R does. The switch seen on after it
    /// was off for OffUs or more, on the reports' arrival stamps, is a flip. Held on, it never repeats; on when first seen
    /// (never seen off), it is none; a bounce, off for less than OffUs, is none. No switch bound, nothing.
    /// </summary>
    public sealed class SwitchFlip
    {
        /// <summary>Over two of a radio's updates at 50 Hz (EdgeTX's slowest RF module period) and a switch's contact
        /// bounce, and well under a thumb's flip off and back on. Assumed, as the wizard's thresholds, until the pilot's
        /// TX12 says otherwise.</summary>
        public const long OffUs = 50000;

        long _offSince = -1;

        /// <summary>Forgets the switch (the radio gone): the next flip needs it seen off again.</summary>
        public void Reset() => _offSince = -1;

        /// <summary>The latest report, and the switch it is read for; true once for each flip.</summary>
        public bool Feed(RadioReport report, ArmCalibration binding)
        {
            if (binding == null || binding.Control == ArmControl.None)
            {
                _offSince = -1;
                return false;
            }
            if (!CalibratedStickMap.Armed(report, binding))
            {
                if (_offSince < 0) _offSince = report.HostUs;
                return false;
            }
            bool flip = _offSince >= 0 && report.HostUs - _offSince >= OffUs;
            _offSince = -1;
            return flip;
        }
    }
}
