namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The FPV camera at a PAL field's own sim time (p7e-camera-feel design 2, PHY-25), render-side as PosePair.Interpolate:
    /// no step calls it. Field k is the flight's steps 20k … 20k + 19 (FlightCore.FieldSteps, aligned to the flight), so it
    /// is whole once step 20(k + 1) is published. The camera sits on the flexing frame: the body's attitude times the
    /// frame's elastic turn at the camera (PoseFrame.CameraTurn). Never smoothed: any instant of the history is the two
    /// steps around it, blended as PoseFrame.Lerp blends them.
    /// </summary>
    public static class FieldCamera
    {
        /// <summary>The newest field whose read is complete when the latest published step is latestStep; −1 before the first.</summary>
        public static long LatestField(long latestStep) =>
            latestStep < FlightCore.FieldSteps ? -1 : latestStep / FlightCore.FieldSteps - 1;

        /// <summary>Field k's start, µs of sim time since the flight's step 0.</summary>
        public static long FieldStartUs(long field) => field * FlightCore.FieldSteps * FlightCore.StepUs;

        /// <summary>The camera's attitude, camera (body) axes to world: Attitude × (½·CameraTurn, 1), normalised, the
        /// first-order rotation of Quat.Integrate.</summary>
        public static Quat Attitude(in PoseFrame pose) => (pose.Attitude * Turn(pose.CameraTurn)).Normalized();

        /// <summary>The frame's elastic turn as a rotation, camera axes on the body's: (½·θ, 1), normalised.</summary>
        public static Quat Turn(in Double3 turn) => new Quat(0.5 * turn.X, 0.5 * turn.Y, 0.5 * turn.Z, 1).Normalized();

        /// <summary>
        /// The pose at sim time timeUs (µs since step 0) from the history: the step at or before it, blended toward the next;
        /// false when the history no longer holds them or does not hold them yet.
        /// </summary>
        public static bool At(PoseBuffer poses, long timeUs, out PoseFrame pose)
        {
            pose = default;
            if (timeUs < 0) return false;
            long step = timeUs / FlightCore.StepUs, rest = timeUs - step * FlightCore.StepUs;
            if (!poses.TryRead(step, out PoseFrame a)) return false;
            if (rest == 0)
            {
                pose = a;
                return true;
            }
            if (!poses.TryRead(step + 1, out PoseFrame b)) return false;
            PoseFrame.Lerp(a, b, rest / (double)FlightCore.StepUs, out pose);
            return true;
        }
    }
}
