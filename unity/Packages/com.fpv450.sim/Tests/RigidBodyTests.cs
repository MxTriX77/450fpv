using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "The rigid body" (PHB-02). The bootstrap drone at its nominal loadout, stepped by RigidBody at
    // FlightCore.Dt with no gravity and no aero unless a test says so.
    public sealed class RigidBodyTests
    {
        const int Steps60s = 60000;

        static MassProperties Body() => BootstrapDrone.Spec().BuildMass(BootstrapDrone.Spec().Nominal).Properties;

        // The angular momentum about the CoM, in the world: q·(I·ω).
        static Double3 WorldMomentum(in FlightState s, in MassProperties b) => s.Attitude.Rotate(b.Inertia * s.BodyRate);

        static Double3 ComVelocity(in FlightState s, in MassProperties b) =>
            s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, b.CenterOfMass));

        // A spin about the CoM: the origin moves so that the CoM is at rest.
        static FlightState Spinning(in MassProperties b, Double3 rate) => new FlightState
        {
            Attitude = Quat.Identity,
            BodyRate = rate,
            Velocity = -Double3.Cross(rate, b.CenterOfMass),
        };

        static void Fly(ref FlightState s, in MassProperties b, int steps, out double worstMomentum, out double worstNorm, out double worstComSpeed)
        {
            Double3 l0 = WorldMomentum(s, b);
            worstMomentum = worstNorm = worstComSpeed = 0;
            for (int k = 0; k < steps; k++)
            {
                RigidBody.Step(ref s, b, default, default, FlightCore.Dt);
                worstMomentum = Math.Max(worstMomentum, (WorldMomentum(s, b) - l0).Length() / l0.Length());
                worstNorm = Math.Max(worstNorm, Math.Abs(s.Attitude.Norm() - 1));
                worstComSpeed = Math.Max(worstComSpeed, ComVelocity(s, b).Length());
            }
        }

        [Test]
        public void FreeFlightConservesWhatItShould([Values(0, 1, 2)] int axis)
        {
            MassProperties b = Body();
            Principal(b.Inertia, out double[] moments, out Double3[] axes);
            FlightState s = Spinning(b, axes[axis] * 2.0);
            Fly(ref s, b, Steps60s, out double momentum, out double norm, out double comSpeed);
            TestContext.WriteLine($"principal axis {axis} (I = {moments[axis]:F5} kg·m², axis {axes[axis]}): 60 s at 2 rad/s, "
                + $"angular momentum within {momentum:E2} (relative), quaternion norm within {norm:E2}, CoM speed {comSpeed:E2} m/s");
            Assert.That(momentum, Is.LessThan(1e-3), "angular momentum within 0.1 %");
            Assert.That(norm, Is.LessThan(1e-9), "the quaternion's norm within 1e-9 of 1");
            Assert.That(comSpeed, Is.LessThan(1e-9), "the CoM stays at rest");
        }

        // A spin off the principal axes, where ω × I·ω is not zero: the momentum still holds, the body rate wanders in
        // body axes (the torque-free tumble). A model without the gyroscopic coupling keeps ω fixed in the body and
        // passes the principal-axis spins above, but fails here.
        [Test]
        public void ATumbleNeedsTheGyroscopicCoupling()
        {
            MassProperties b = Body();
            Double3 rate = new Double3(1, 1, 1) * (2.0 / Math.Sqrt(3));
            FlightState s = Spinning(b, rate);
            Double3 l0 = WorldMomentum(s, b);
            double momentum = 0, norm = 0, wander = 0;
            for (int k = 0; k < Steps60s; k++)
            {
                RigidBody.Step(ref s, b, default, default, FlightCore.Dt);
                momentum = Math.Max(momentum, (WorldMomentum(s, b) - l0).Length() / l0.Length());
                norm = Math.Max(norm, Math.Abs(s.Attitude.Norm() - 1));
                wander = Math.Max(wander, (s.BodyRate - rate).Length());
            }
            TestContext.WriteLine($"tumble: angular momentum within {momentum:E2}, quaternion norm within {norm:E2}, body rate moved up to {wander:F3} rad/s");
            Assert.That(momentum, Is.LessThan(1e-3));
            Assert.That(norm, Is.LessThan(1e-9));
            Assert.That(wander, Is.GreaterThan(0.1), "a tumbling body's rate moves in its own axes");

            // The known-wrong model: the attitude turned by a fixed body rate.
            FlightState wrong = Spinning(b, rate);
            double worst = 0;
            for (int k = 0; k < Steps60s; k++)
            {
                wrong.Attitude = Quat.Integrate(wrong.Attitude, wrong.BodyRate, FlightCore.Dt);
                worst = Math.Max(worst, (WorldMomentum(wrong, b) - l0).Length() / l0.Length());
            }
            TestContext.WriteLine($"without the coupling: angular momentum off by {worst:E2}");
            Assert.That(worst, Is.GreaterThan(1e-2), "the check must fail a model without ω × I·ω");
        }

        // The translation is the CoM's: under gravity a tumbling drone's CoM follows semi-implicit Euler's exact
        // discrete parabola, x_n = x0 + n·dt·v0 + g·dt²·n(n+1)/2, while its origin swings round it.
        [Test]
        public void TheCentreOfMassFallsOnItsParabola()
        {
            MassProperties b = Body();
            Double3 v0 = new Double3(3, 2, -1);
            FlightState s = Spinning(b, new Double3(2, -1, 0.5));
            s.Position = new Double3(0, 100, 0);
            s.Velocity = s.Velocity + v0;
            Double3 x0 = s.Position + s.Attitude.Rotate(b.CenterOfMass);
            const int n = 10000;
            for (int k = 0; k < n; k++)
                RigidBody.Step(ref s, b, FlightCore.Gravity, default, FlightCore.Dt);
            double dt = FlightCore.Dt;
            Double3 expected = x0 + v0 * (n * dt) + FlightCore.Gravity * (dt * dt * n * (n + 1) / 2.0);
            double error = (s.Position + s.Attitude.Rotate(b.CenterOfMass) - expected).Length();
            TestContext.WriteLine($"CoM after 10 s: {error:E2} m from the exact discrete solution");
            Assert.That(error, Is.LessThan(1e-9));
        }

        // A moment about the CoM spins the body up as I⁻¹·M·dt from rest (to the gyroscopic term's dt·|ω| ≈ 2e-6): the
        // full tensor, so a roll moment also yaws the body by −I_yz/I_yy, the payload forward and below.
        [Test]
        public void AMomentAcceleratesByTheInverseInertia()
        {
            MassProperties b = Body();
            var moment = new Double3(0, 0, 0.1); // a roll moment, N·m
            FlightState s = Spinning(b, default);
            RigidBody.Step(ref s, b, default, moment, FlightCore.Dt);
            Double3 expected = b.InverseInertia * (moment * FlightCore.Dt);
            Assert.That((s.BodyRate - expected).Length(), Is.LessThan(1e-5 * expected.Length()));
            TestContext.WriteLine($"a roll moment: yaw rate / roll rate {s.BodyRate.Y / s.BodyRate.Z:F3}, −I_yz/I_yy {-b.Inertia.YZ / b.Inertia.YY:F3}");
            Assert.That(Math.Abs(s.BodyRate.Y), Is.GreaterThan(0.1 * Math.Abs(s.BodyRate.Z)), "roll couples into yaw");
        }

        // The principal moments and axes of a symmetric tensor, by cyclic Jacobi rotations (the test's own reference).
        static void Principal(Sym3 t, out double[] moments, out Double3[] axes)
        {
            var a = new double[3, 3] { { t.XX, t.XY, t.XZ }, { t.XY, t.YY, t.YZ }, { t.XZ, t.YZ, t.ZZ } };
            var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int sweep = 0; sweep < 50; sweep++)
            {
                double off = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
                if (off < 1e-40) break;
                for (int p = 0; p < 2; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    if (a[p, q] == 0) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double tan = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) tan = 1;
                    double c = 1 / Math.Sqrt(tan * tan + 1), s = tan * c;
                    for (int k = 0; k < 3; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
            }
            moments = new[] { a[0, 0], a[1, 1], a[2, 2] };
            axes = new Double3[3];
            for (int i = 0; i < 3; i++)
            {
                axes[i] = new Double3(v[0, i], v[1, i], v[2, i]);
                Double3 residual = t * axes[i] - axes[i] * moments[i];
                Assert.That(residual.Length(), Is.LessThan(1e-15), "the reference eigenvector " + i);
            }
        }
    }
}
