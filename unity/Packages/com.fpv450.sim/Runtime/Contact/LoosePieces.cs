using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The pieces a damaged leg drops (p7g-legs 1.3, design 4), engine-free and allocation-free in a step. A piece leaves the
    /// drone with the drone's motion at its own parts (the drone's origin velocity and turn, so the body keeps its momentum
    /// less the piece's, RigidBody's rule), and flies as a free rigid body under gravity and the air's drag across its tubes,
    /// and lies on the ground through the contact model's laws (each of its shapes a body shape on the ground, the soil and
    /// the world's objects, at the leg tube's own penetration spring). It is stepped at the drone's 1 ms after the drone's
    /// step, never part of it: its state is in the side record at 50 Hz (LogFormat.LooseBlock), and the drone never touches
    /// it. At rest (LegDamageSpec.RestSpeed, RestRate, RestSteps) it stops stepping and lies where it is.
    ///
    /// Slots: up to 6 at once (each skid's two legs' tubes and the skid), each with its contacts and its mass model built at
    /// the flight's start over every leg-side part and shape, the piece's own taken in (ContactModel.Reuse, MassModel.SetPart).
    /// </summary>
    public sealed class LoosePieces
    {
        public const int Slots = 6;

        /// <summary>The side record's payload: step, slot, at rest, parts, then position, attitude, velocity and turn.</summary>
        public const int RecordSize = 120;

        // The mass models' parts, in order: the four legs, the four T-joints, the right skid, the left skid.
        const int Parts = 10;

        /// <summary>A piece's fixed sub-steps in the drone's 1 ms (derived): an 18 g tube's turn about its own axis (I 9e-7
        /// kg·m²) on its contacts' springs and anchors runs at about 1400 rad/s, ω·dt 1.4 at 1 ms, where semi-implicit Euler
        /// keeps it undamped (a 250 Hz cycle at rest, traced); at 250 µs it is 0.35 and the contacts' dampers take it out.</summary>
        public const int SubSteps = 4;

        readonly LegDamageSpec _legs;
        readonly FlightWorld _world;
        readonly ContactModel[] _contacts = new ContactModel[Slots];
        readonly MassModel[] _mass = new MassModel[Slots];
        readonly FlightState[] _state = new FlightState[Slots];
        readonly int[] _parts = new int[Slots], _still = new int[Slots];
        readonly bool[] _active = new bool[Slots], _asleep = new bool[Slots];
        readonly int[] _drone = new int[Parts];         // each mass part's index in the drone's
        readonly int[] _looseOf;                         // the drone's shape index → its loose shape, else −1
        readonly int _tjointShapes;                      // the T-joints' shapes: a capsule and its two end spheres a T-joint
        readonly int _tubeEnds;                          // the leg tubes' end spheres: two a leg, in Corners order
        readonly DronePart[] _empty = new DronePart[Parts];
        readonly int[] _shapes = new int[48];
        readonly ContactShape[] _geometry = new ContactShape[48];
        readonly StepEvents _scratch = new StepEvents(64);
        StepTrace _trace;
        int _count;

        /// <summary>The slots for a flight whose contacts have the legs' law: drone's its contact spec, mass its build at the
        /// start (the pieces' parts are read from it as they leave).</summary>
        public LoosePieces(ContactSpec drone, FlightWorld world, MassModel mass)
        {
            if (drone?.Legs == null) throw new ArgumentException("loose pieces come from the legs' law", nameof(drone));
            _legs = drone.Legs.Clone();
            _world = world ?? throw new ArgumentNullException(nameof(world));
            for (int c = 0; c < 4; c++)
            {
                _drone[c] = _legs.Parts[c];
                _drone[4 + c] = _legs.TJointParts[c];
            }
            for (int c = 0; c < 4; c++)
            {
                if (_legs.Bore[c].X >= 0) _drone[8] = _legs.SkidParts[c];
                else _drone[9] = _legs.SkidParts[c];
            }

            // Every leg-side shape as a body shape of its tube's own penetration spring and damping (the legs' and skids' one
            // stock, drone.json's leg shapes); the drone's index of each.
            double k = 0, zeta = 0;
            foreach (ContactShape s in drone.Shapes)
                if (s.Role == ContactRole.Body && s.Part == _legs.Parts[0])
                {
                    k = s.Stiffness;
                    zeta = s.DampingRatio;
                }
            _looseOf = new int[drone.Shapes.Length];
            int n = 0;
            for (int i = 0; i < drone.Shapes.Length; i++)
            {
                _looseOf[i] = -1;
                if (IsLegSide(drone.Shapes[i])) _looseOf[i] = n++;
            }
            // Each T-joint as a capsule in its box along the skid's bore (the box's narrower side its diameter), with a sphere at
            // each core end, and each leg tube with a sphere at each end: a capsule lying on a face touches at one core end,
            // which flips with the least tilt (the skids' P3 rule: a capsule with load-bearing spheres at its ends). On the drone
            // the skid's supports stand for the T-joints; a loose piece rests on them.
            _tjointShapes = n;
            n += 12;
            _tubeEnds = n;
            n += 8;
            string polymer = null;
            foreach (ContactShape s in drone.Shapes)
                if (s.Role == ContactRole.Body && s.Part == _legs.Parts[0]) polymer = s.Material;
            var spec = drone.Clone();
            spec.Legs = null;
            spec.LegSideStiffness = spec.LegSideDampingRatio = 0;
            spec.LegTubeSideStiffness = spec.LegJawSideStiffness = spec.LegSwayLoad = spec.LegTubeAxialStiffness = 0;
            spec.CrashAccel = double.PositiveInfinity;
            spec.CrashPropSpeed = double.PositiveInfinity;
            spec.Shapes = new ContactShape[n];
            for (int i = 0; i < drone.Shapes.Length; i++)
            {
                if (_looseOf[i] < 0) continue;
                ContactShape s = drone.Shapes[i];
                s.Role = ContactRole.Body;
                s.Corner = -1;
                s.Stiffness = s.StiffnessUp = k;
                s.DampingRatio = zeta;
                spec.Shapes[_looseOf[i]] = s;
            }
            for (int c = 0; c < 4; c++)
            {
                DronePart joint = mass.Part(_legs.TJointParts[c]);
                double r = (joint.Size.X < joint.Size.Y ? joint.Size.X : joint.Size.Y) / 2, half = joint.Size.Z / 2 - r;
                if (half < 0) half = 0;
                Double3 a = joint.Position - new Double3(0, 0, half), b = joint.Position + new Double3(0, 0, half);
                for (int e = 0; e < 3; e++)
                    spec.Shapes[_tjointShapes + 3 * c + e] = new ContactShape
                    {
                        Name = joint.Name, Part = _legs.TJointParts[c], Role = ContactRole.Body, Corner = -1, Material = polymer,
                        A = e == 2 ? b : a, B = e == 1 ? a : b, Radius = r, Stiffness = k, StiffnessUp = k, DampingRatio = zeta,
                    };
                ContactShape tube = default;
                foreach (ContactShape t in drone.Shapes)
                    if (t.Role == ContactRole.Body && t.Part == _legs.Parts[c]) tube = t;
                for (int e = 0; e < 2; e++)
                {
                    ContactShape end = tube;
                    end.Corner = -1;
                    end.A = end.B = e == 0 ? tube.A : tube.B;
                    end.Stiffness = end.StiffnessUp = k;
                    end.DampingRatio = zeta;
                    spec.Shapes[_tubeEnds + 2 * c + e] = end;
                }
            }
            var parts = new DronePart[Parts];
            for (int p = 0; p < Parts; p++) parts[p] = mass.Part(_drone[p]);
            for (int slot = 0; slot < Slots; slot++)
            {
                _contacts[slot] = new ContactModel(spec, world, mass.Properties.Mass, null, FlightCore.Dt / SubSteps);
                _mass[slot] = new MassModel(parts);
            }
            for (int p = 0; p < Parts; p++)
            {
                _empty[p] = parts[p];
                _empty[p].Mass = 0;
            }
        }

        bool IsLegSide(in ContactShape s)
        {
            if (s.Role == ContactRole.Support || s.Role == ContactRole.Skid) return true;
            if (s.Role != ContactRole.Body) return false;
            for (int c = 0; c < 4; c++)
                if (s.Part == _legs.Parts[c]) return true;
            return false;
        }

        /// <summary>The pieces flying or lying now.</summary>
        public int Count => _count;

        public bool Active(int slot) => _active[slot];
        public bool AtRest(int slot) => _asleep[slot];

        /// <summary>A piece's parts (LegChange's mask) and its state (its frame the drone's body frame as it left).</summary>
        public int PartsOf(int slot) => _parts[slot];
        public ref readonly FlightState State(int slot) => ref _state[slot];
        public ref readonly MassProperties Body(int slot) => ref _mass[slot].Properties;

        /// <summary>
        /// A leg's tube cut at its clamp's edge (the drone's build, body): the part below the edge (the piece) and the stub above
        /// it (in the clamp), each the tube's cylinder over its own length, its mass the tube's by length (the stub's the rest,
        /// so the two sum to the tube's mass exactly).
        /// </summary>
        public static void Cut(DronePart tube, Double3 edge, out DronePart lower, out DronePart stub)
        {
            double length = tube.Length, mass = tube.Mass;
            Double3 a = tube.Axis, top = tube.Position + a * (length / 2), bottom = tube.Position - a * (length / 2);
            double below = Double3.Dot(edge - bottom, a);
            if (below < 0) below = 0;
            if (below > length) below = length;
            double piece = mass * (below / length);
            lower = tube;
            lower.Length = below;
            lower.Position = bottom + a * (below / 2);
            lower.Mass = piece;
            stub = tube;
            stub.Length = length - below;
            stub.Position = top - a * ((length - below) / 2);
            stub.Mass = mass - piece;
        }

        /// <summary>
        /// Piece p of a structure change leaves the drone now: its parts read from the drone's build (mass) as they are, turned
        /// by their legs' sets (drone, its contacts), its shapes as they left, its state the drone's s. Its PieceDropped event
        /// goes into events. Allocates nothing.
        /// </summary>
        public void Spawn(LegChange change, int p, in FlightState s, MassModel mass, ContactModel drone, StepEvents events)
        {
            int slot = 0;
            while (slot < Slots && _active[slot]) slot++;
            if (slot == Slots) return; // cannot happen: two tubes and a skid a side
            int bits = change.Parts[p];
            MassModel m = _mass[slot];
            LegDamage law = drone.Legs;
            int main = -1;
            for (int q = 0; q < Parts; q++)
            {
                DronePart part = _empty[q];
                if (q < 4 && (bits & (1 << q | 1 << (4 + q))) != 0)
                {
                    DronePart tube = mass.Part(_drone[q]);
                    if ((bits & 1 << (4 + q)) != 0)
                    {
                        Cut(tube, _legs.Clamp[q], out DronePart lower, out _);
                        tube = lower;
                    }
                    part = Turned(tube, q, law);
                    main = _drone[q];
                }
                else if (q >= 4 && q < 8 && (bits & 1 << (8 + q - 4)) != 0)
                {
                    int c = q - 4;
                    part = mass.Part(_drone[q]);
                    if (law.SkidMode(c) != LegMode.TJointFracture) part = Turned(part, c, law);
                }
                else if (q >= 8 && (bits & 1 << (12 + q - 8)) != 0)
                {
                    part = mass.Part(_drone[q]);
                    if (main < 0) main = _drone[q];
                }
                m.SetPart(q, part, false);
            }
            m.Rebuild();
            int count = change.ShapeCount[p];
            int shapes = count;
            for (int k = 0; k < shapes; k++)
            {
                ContactShape g = change.Geometry[p][k];
                _shapes[k] = _looseOf[change.Shapes[p][k]];
                _geometry[k] = g;
                for (int c = 0; c < 4; c++)
                {
                    if (g.Role != ContactRole.Body || g.Part != _legs.Parts[c]) continue;
                    for (int j = 0; j < 2; j++)
                    {
                        ContactShape end = _contacts[slot].Base(_tubeEnds + 2 * c + j);
                        end.A = end.B = j == 0 ? g.A : g.B;
                        _shapes[count] = _tubeEnds + 2 * c + j;
                        _geometry[count++] = end;
                    }
                }
            }
            for (int c = 0; c < 4; c++)
            {
                if ((bits & 1 << (8 + c)) == 0) continue;
                for (int j = 0; j < 3; j++)
                {
                    ContactShape g = _contacts[slot].Base(_tjointShapes + 3 * c + j);
                    if (law.SkidMode(c) != LegMode.TJointFracture)
                    {
                        g.A = TurnedPoint(g.A, c, law);
                        g.B = TurnedPoint(g.B, c, law);
                    }
                    _shapes[count] = _tjointShapes + 3 * c + j;
                    _geometry[count++] = g;
                }
            }
            _contacts[slot].Reuse(m.Properties.Mass, _shapes, count, _geometry);
            _state[slot] = new FlightState
            {
                Step = s.Step, Position = s.Position, Velocity = s.Velocity, Attitude = s.Attitude, BodyRate = s.BodyRate,
            };
            _scratch.Clear();
            _contacts[slot].Sense(_state[slot], ref _state[slot], _scratch);
            _parts[slot] = bits;
            _active[slot] = true;
            _asleep[slot] = false;
            _still[slot] = 0;
            _count++;

            ref readonly MassProperties body = ref m.Properties;
            FlightEvent e = FlightEvent.Of(EventKind.PieceDropped);
            e.Part = (ushort)(main >= 0 ? main : FlightEvent.NoPart);
            e.Index = (byte)slot;
            e.Point = s.Position + s.Attitude.Rotate(body.CenterOfMass);
            e.A = body.Mass;
            e.B = (s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, body.CenterOfMass))).Length();
            e.C = bits;
            events.Add(e);
        }

        Double3 TurnedPoint(Double3 point, int c, LegDamage law)
        {
            var part = new DronePart { Kind = PartKind.Box, Position = point };
            return Turned(part, c, law).Position;
        }

        // A part of leg c's tube or foot turned with its set about its clamp's edge (a cylinder's axis turned with it; a box,
        // the T-joint, only moved).
        DronePart Turned(DronePart part, int c, LegDamage law)
        {
            Double3 rho = law.SetRotation(c);
            double t = rho.Length();
            if (!(t > 0)) return part;
            DetMath.SinCos(t / 2, out double sin, out double cos);
            double k = sin / t;
            var q = new Quat(rho.X * k, rho.Y * k, rho.Z * k, cos);
            Double3 pivot = _legs.Clamp[c];
            part.Position = pivot + q.Rotate(part.Position - pivot);
            if (part.Kind == PartKind.Cylinder) part.Axis = q.Rotate(part.Axis);
            return part;
        }

        /// <summary>
        /// Every flying piece's 1 ms step after the drone's (its contacts' events stay its own, never the drone's): gravity,
        /// the air's drag across its tubes at the wind there, its contacts, the rigid body; then the rest check.
        /// </summary>
        public void Step(WindField wind)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                if (!_active[slot] || _asleep[slot]) continue;
                ref FlightState s = ref _state[slot];
                MassModel m = _mass[slot];
                ref readonly MassProperties body = ref m.Properties;
                long step = s.Step;
                for (int sub = 0; sub < SubSteps; sub++)
                {
                    Double3 force = default, moment = default;
                    if (_legs.AirDensity > 0) Drag(slot, s, body, wind, ref force, ref moment);
                    _scratch.Clear();
                    _contacts[slot].Forces(ref s, body, ref force, ref moment, ref _trace, _scratch);
                    FlightState was = s;
                    RigidBody.Step(ref s, in body, FlightCore.Gravity + force * body.InverseMass, moment, FlightCore.Dt / SubSteps);
                    _contacts[slot].Sense(was, ref s, _scratch);
                }
                s.Step = step + 1;
                Double3 v = s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, body.CenterOfMass));
                bool still = _trace.ContactsTouching > 0 && v.Length() < _legs.RestSpeed && s.BodyRate.Length() < _legs.RestRate;
                _still[slot] = still ? _still[slot] + 1 : 0;
                if (_still[slot] >= _legs.RestSteps) _asleep[slot] = true;
            }
        }

        // The air's drag across each tube of the piece (the independence principle): −½ρ·C_d·D·ℓ·|u⊥|·u⊥ at its centre, u⊥ the
        // air's velocity across its axis there.
        void Drag(int slot, in FlightState s, in MassProperties body, WindField wind, ref Double3 force, ref Double3 moment)
        {
            MassModel m = _mass[slot];
            for (int q = 0; q < Parts; q++)
            {
                ref readonly DronePart part = ref m.Part(q);
                if (part.Kind != PartKind.Cylinder || !(part.Mass > 0)) continue;
                Double3 r = part.Position;
                Double3 vb = s.Attitude.InverseRotate(s.Velocity - wind.At(s.Position + s.Attitude.Rotate(r))) + Double3.Cross(s.BodyRate, r);
                Double3 across = vb - part.Axis * Double3.Dot(part.Axis, vb);
                double size = across.Length();
                if (!(size > 0)) continue;
                Double3 f = across * (-0.5 * _legs.AirDensity * _legs.LooseDrag * part.Diameter * part.Length * size);
                force = force + s.Attitude.Rotate(f);
                moment = moment + Double3.Cross(r - body.CenterOfMass, f);
            }
        }

        /// <summary>One piece's side record (RecordSize bytes, little-endian): step i64, slot u8, at rest u8, parts u16, 4 zero
        /// bytes, then its frame's position, attitude (x, y, z, w), velocity (world) and turn (body), f64 each.</summary>
        public void Encode(int slot, Span<byte> dst)
        {
            ref readonly FlightState s = ref _state[slot];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(dst, s.Step);
            dst[8] = (byte)slot;
            dst[9] = (byte)(_asleep[slot] ? 1 : 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst.Slice(10), (ushort)_parts[slot]);
            dst[12] = dst[13] = dst[14] = dst[15] = 0;
            double[] values = Values(s);
            for (int i = 0; i < values.Length; i++)
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(dst.Slice(16 + 8 * i), BitConverter.DoubleToInt64Bits(values[i]));
        }

        readonly double[] _values = new double[13];

        double[] Values(in FlightState s)
        {
            double[] v = _values;
            v[0] = s.Position.X; v[1] = s.Position.Y; v[2] = s.Position.Z;
            v[3] = s.Attitude.X; v[4] = s.Attitude.Y; v[5] = s.Attitude.Z; v[6] = s.Attitude.W;
            v[7] = s.Velocity.X; v[8] = s.Velocity.Y; v[9] = s.Velocity.Z;
            v[10] = s.BodyRate.X; v[11] = s.BodyRate.Y; v[12] = s.BodyRate.Z;
            return v;
        }
    }
}
