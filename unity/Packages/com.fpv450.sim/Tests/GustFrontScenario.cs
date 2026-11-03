using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The per-rotor gradient scenario (p7c-wind 1.4, PR-3, wind-turbulence's "Gusts are real forces with per-rotor
    /// gradients"): the repository drone holds its point in still air under the closed-loop pilot (SimPilot), and a gust
    /// front crosses it from behind, tail to nose: a step of Delta (a sideways gust and a downdraft, assumed) moving at
    /// Speed over a Width ramp. Each rotor meets the front when it reaches its hub, so the rear pair's inflow and
    /// in-plane drag change first: the thrust difference pitches the drone and the rear hubs' sideways drag, behind the
    /// centre of mass, yaws it, at once. The known-wrong field is the same front taken at the body's origin for every
    /// point (GustFront.Uniform): it pushes the drone but leaves the pitch and the yaw to the centre of mass's small
    /// offsets. The response is each axis's peak body rate in the passage less its peak before.
    /// </summary>
    public static class GustFrontScenario
    {
        /// <summary>The front: from 2 m behind the drone, at 6 m/s (assumed: a gust front advected by a Severe wind), a
        /// 4 m/s sideways gust from the left and a 1.5 m/s downdraft (assumed: about 4σ of Severe's turbulence) over
        /// 0.3 m (assumed: sharper than the 0.45 m between the hubs, so the rotors meet it in turn).</summary>
        public const double StartBehind = 2, Speed = 6, Side = 4, Down = 1.5, Width = 0.3;

        public const double Seconds = 6, FrontAt = 3; // the front is released after 3 s of hold

        /// <summary>The drone's rearmost air behind its origin, m (derived: the rear discs' rims, drone.json's hubs at
        /// z −0.160 with r 0.127, 0.287 m, rounded out). The hold's window ends when the front's ramp, Width/2 ahead of
        /// its plane, reaches it: the field's flight first leaves the uniform field's 8 ms later (model 36).</summary>
        public const double Rearmost = 0.3;

        public struct Result
        {
            /// <summary>Peak |pitch rate| and |yaw rate| over the passage, rad/s, less each one's wobble in the hold.</summary>
            public double Pitch, Yaw, Roll;

            /// <summary>The hold's own wobble: each axis's peak |rate| in the 500 ms before the front's ramp reaches the
            /// drone (Rearmost), rad/s, measured in this flight. The field's and the uniform field's flights are the same
            /// bits until then.</summary>
            public double HoldPitch, HoldYaw, HoldRoll;

            /// <summary>The steps of the pitch and the yaw peaks after the front reached the rear hubs.</summary>
            public int PitchStep, YawStep;
        }

        /// <summary>The scenario's floor over the hold's own wobble on each axis (assumed: an answer twice the wobble the
        /// hold shows before the front reaches the drone stands clear of it).</summary>
        public const double WobbleShare = 2;

        /// <summary>The scenario's pass: pitch and yaw both answer the front (each above WobbleShare × that axis's wobble
        /// in the hold before it, measured in the same flight), together (their peaks within 150 ms), each at least 3× the
        /// uniform field's (assumed).</summary>
        public static bool Coupled(in Result field, in Result uniform)
        {
            return field.Pitch > WobbleShare * field.HoldPitch && field.Yaw > WobbleShare * field.HoldYaw
                && System.Math.Abs(field.PitchStep - field.YawStep) <= 150
                && field.Pitch >= 3 * uniform.Pitch && field.Yaw >= 3 * uniform.Yaw;
        }

        public static Result Fly(DroneSpec spec, bool uniform)
        {
            FlightSetup setup = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still));
            setup.StartPosition = new Double3(0, 30, 0);
            // The front starts StartBehind behind the body's origin, with FrontAt of hold before it is released; it
            // moves north, along the drone's nose (+Z at the start attitude).
            setup.Wind.Front = new GustFront
            {
                Start = new Double3(0, 0, -StartBehind - Speed * FrontAt), Normal = new Double3(0, 0, 1), Speed = Speed,
                Delta = new Double3(Side, -Down, 0), Width = Width, Uniform = uniform,
            };
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            int steps = (int)(Seconds * 1000), arrive = (int)((FrontAt + (StartBehind - 0.2) / Speed) * 1000) - 16;
            int reached = (int)((FrontAt + (StartBehind - Rearmost - 0.5 * Width) / Speed) * 1000);
            double before = 0, beforeYaw = 0, beforeRoll = 0;
            var r = new Result();
            for (int k = 0; k < steps; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                Double3 w = core.State.BodyRate;
                double pitch = DetMath.Abs(w.X), yaw = DetMath.Abs(w.Y), roll = DetMath.Abs(w.Z);
                if (k < reached - 500) continue;
                if (k < reached)
                {
                    before = DetMath.Max(before, pitch);
                    beforeYaw = DetMath.Max(beforeYaw, yaw);
                    beforeRoll = DetMath.Max(beforeRoll, roll);
                    continue;
                }
                if (k < arrive) continue;
                if (k > arrive + 600) break;
                if (pitch > r.Pitch) { r.Pitch = pitch; r.PitchStep = k - arrive; }
                if (yaw > r.Yaw) { r.Yaw = yaw; r.YawStep = k - arrive; }
                r.Roll = DetMath.Max(r.Roll, roll);
            }
            r.HoldPitch = before;
            r.HoldYaw = beforeYaw;
            r.HoldRoll = beforeRoll;
            r.Pitch -= before;
            r.Yaw -= beforeYaw;
            r.Roll -= beforeRoll;
            return r;
        }
    }
}
