"""Hand numbers for p7c-wind design 14 (task 1.5d): what moves a hovering drone's heading.

A linear hover at the nominal loadout (the 1 kg bottle), the builder's Betaflight 4.5 PIDs, the motors as drone.json's DC
model, and each candidate's yaw at 1-4 Hz (the angle's RMS over the band) and its yaw rate (RMS over 0.4-15 Hz), against
the footage's hover (C, D, E: 0.11-0.15 deg, 2.1-3.6 deg/s; ground-micro-motion.md section 6). The roll the own wake
gives is printed beside the sim's flown floor as the hand model's check, and the yaw loop's step beside the flown t90
(rate-tracking.md 2.3). Labels: published (P), derived (D), assumed (A), measured (M)."""
import cmath
import math

TWO_PI = 2 * math.pi
DEG = 180 / math.pi
G, RHO = 9.80665, 1.225                       # P: standard gravity, ISA air

# The drone (drone.json; the mass build at the 1 kg bottle, rate-tracking.md section 1)
MASS = 5.087                                  # D, kg
I_ROLL, I_YAW = 0.0448, 0.0604                # D, kg m^2 (body z and body y)
LEVER = 0.15981                               # D, m: each hub's offset on both axes (452 mm wheelbase, P)
R_HUB = math.sqrt(2) * LEVER                  # D, m: hub to centre, 0.226
RADIUS = 0.127                                # M, m (10-inch)
AREA = math.pi * RADIUS ** 2
K_T = 1.4e-5                                  # D from P (APC 10x4.5MR static table), N/(rad/s)^2
KAPPA = 0.0144                                # D from P (the same table), m: torque over thrust
KV = 900                                      # P, rpm/V
KT = 60 / (TWO_PI * KV)                       # D, N m/A (= Ke)
R_M = 0.11                                    # D from P (Flash Hobby 3115 bench), ohm
J_ROT = 6.0e-5                                # D, kg m^2 (prop and bell)
I0 = 1.4                                      # P, A (no-load current)
V_PACK = 23.7                                 # D, V: 6S at 0.9 charge (4.1 V a cell, the probe's StartPackCharge) less
                                              # the 6S4P pack's sag at the hover's ~73 A (r_cell 12 mOhm, A)
H_FORCE = 0.044                               # D (p7c-wind 1.6), N per m/s of in-plane air at 9000 rpm
WAKE_SIGMA, WAKE_CORNER = 0.017, 0.2          # D from P (Stack et al. 2004): thrust sigma, corner f R / v_h
INFLOW_J = 0.0405                             # P: the APC table's first inflow row
INFLOW_THRUST, INFLOW_TORQUE = 0.9612, 1.0026  # D from P: C_T and C_P over their static values there

# Betaflight 4.5 (pid_init.c scaling; the builder's PIDs, fc-profiles.md section 1; the sim default's rates)
P_SCALE, I_SCALE, D_SCALE, MIX = 0.032029, 0.244381, 0.000529, 1000.0   # P
KP_Y, KI_Y = P_SCALE * 45, I_SCALE * 30 * 2.5  # P: yaw P 45 (BF default), I 30 x 2.5 (no integrated yaw)
KP_R, KI_R, KD_R = P_SCALE * 65, I_SCALE * 20, D_SCALE * 60 * 0.85   # P: roll 65 / 20 / D min 60, TPA's 0.85 on D (D)
OUT = 1 - 0.055                               # P: duty per unit mix above the 5.5 % idle
FILTER_DELAY = 0.007                          # D: the gyro chain's group delay (two biquads at 60-90 Hz, the 50 Hz
                                              # notch, yaw P's 100 Hz PT1), a few ms each
YAW_CENTRE = 85.0                             # P: the sim default's yaw centre sensitivity, deg/s per full stick
DEADBANDS = (2, 3, 5)                         # P: yaw_deadband, us of 500: sim default, pilot's flown set, FpvPrep's

# The footage (M, ground-micro-motion.md 4.1 and 6): the hover's yaw at 1-4 Hz, its rate, E's real rate per band
FOOT_YAW_14 = (0.11, 0.15)
FOOT_RATE = (2.1, 3.6)
E_RATE_BANDS = (1.2, 1.3, 0.6)                # deg/s below 1 / 1-4 / 4-15 Hz
E_ROLL_14 = 0.12

# The hover (D): thrust, speed, torque, the induced velocity, the motor's linear constants
T_HOVER = MASS * G / 4
OMEGA = math.sqrt(T_HOVER / K_T)
Q_HOVER = KAPPA * T_HOVER
V_H = math.sqrt(T_HOVER / (2 * RHO * AREA))
B_DRIVE = KT * V_PACK / R_M                   # N m per unit duty: the drive's torque at once
DQ_DW = 2 * Q_HOVER / OMEGA                   # N m per rad/s: the prop's torque slope
C_MOTOR = KT * KT / R_M + DQ_DW               # N m per rad/s: the motor's damping
TAU_M = J_ROT / C_MOTOR                       # s: the motor's lag (rate-tracking.md: 42.7 ms)
DT_DW = 2 * K_T * OMEGA                       # N per rad/s
WAKE_TAU = RADIUS / (TWO_PI * WAKE_CORNER * V_H)


def yaw_actuator(s):
    """Body yaw torque per unit yaw mix: four motors' drive and spin-up reaction (J s + dQ/dw) b / (J s + c)."""
    return 4 * OUT * B_DRIVE * (J_ROT * s + DQ_DW) / (J_ROT * s + C_MOTOR)


def roll_actuator(s):
    """Body roll moment per unit roll mix: four thrusts on the lever through the motor's lag."""
    return 4 * OUT * LEVER * DT_DW * B_DRIVE / (J_ROT * s + C_MOTOR)


def loop(f, axis):
    s = 1j * TWO_PI * f
    per = DEG / MIX                            # PID units per (rad/s), mix per PID unit
    delay = cmath.exp(-s * FILTER_DELAY)
    if axis == "yaw":
        c = (KP_Y + KI_Y / s) * per
        return c * delay * yaw_actuator(s) / (I_YAW * s)
    c = (KP_R + KI_R / s + KD_R * s) * per
    return c * delay * roll_actuator(s) / (I_ROLL * s)


def sensitivity(f, axis):
    return abs(1 / (1 + loop(f, axis)))


def wake_psd(f, sigma):
    """One-sided PSD of a first-order process of variance sigma^2 with the own wake's corner."""
    return 4 * sigma * sigma * WAKE_TAU / (1 + (TWO_PI * f * WAKE_TAU) ** 2)


def band(fn, lo, hi, n=3000):
    """The integral of fn over [lo, hi] on a log grid (trapezoid)."""
    total, a = 0.0, math.log(lo)
    step = (math.log(hi) - a) / n
    prev_f, prev = lo, fn(lo)
    for k in range(1, n + 1):
        f = math.exp(a + k * step)
        v = fn(f)
        total += (v + prev) / 2 * (f - prev_f)
        prev_f, prev = f, v
    return total


def angle_and_rate(moment_psd, inertia, axis):
    """A moment's PSD (N^2 m^2 / Hz) into the angle's RMS at 1-4 Hz (deg) and the rate's RMS at 0.4-15 Hz (deg/s)."""
    def angle(f):
        w = TWO_PI * f
        return moment_psd(f) * sensitivity(f, axis) ** 2 / (inertia * inertia * w ** 4)

    def rate(f):
        w = TWO_PI * f
        return moment_psd(f) * sensitivity(f, axis) ** 2 / (inertia * inertia * w * w)
    return math.sqrt(band(angle, 1, 4)) * DEG, math.sqrt(band(rate, 0.4, 15)) * DEG


def yaw_step_t90():
    """The yaw loop's rate step (linear, the gyro chain as a first-order lag of the same delay): t90, s."""
    dt, r, lag, iterm, dw, t = 1e-5, 0.0, 0.0, 0.0, 0.0, 0.0
    target = 1.0                               # rad/s
    while t < 1:
        e = (target - lag) * DEG
        u = (KP_Y * e + iterm) / MIX
        iterm += KI_Y * e * dt
        dw_dot = (OUT * B_DRIVE * u - C_MOTOR * dw) / J_ROT
        torque = 4 * (J_ROT * dw_dot + DQ_DW * dw)
        dw += dw_dot * dt
        r += torque / I_YAW * dt
        lag += (r - lag) / FILTER_DELAY * dt
        t += dt
        if r >= 0.9 * target:
            return t
    return float("nan")


def needed_yaw_moment():
    """The yaw torque, flat over 1-4 Hz, that gives the footage's 1-4 Hz yaw: its RMS in the band, N m."""
    out = []
    for target in FOOT_YAW_14:
        unit = band(lambda f: sensitivity(f, "yaw") ** 2 / (I_YAW * I_YAW * (TWO_PI * f) ** 4), 1, 4)
        psd = (target / DEG) ** 2 / unit
        out.append(math.sqrt(psd * 3))
    return out


def main():
    print(f"hover: T {T_HOVER:.2f} N a rotor, {OMEGA:.0f} rad/s, Q {Q_HOVER:.4f} N m, v_h {V_H:.2f} m/s; "
          f"the motor's lag {TAU_M * 1000:.1f} ms; the own wake's corner {1 / (TWO_PI * WAKE_TAU):.1f} Hz")
    print("yaw loop: |S| at 0.5 / 1 / 2 / 4 Hz " + " / ".join(f"{sensitivity(f, 'yaw'):.2f}" for f in (0.5, 1, 2, 4))
          + f"; crossover {crossover('yaw'):.2f} Hz; rate step t90 {yaw_step_t90() * 1000:.0f} ms (flown: 147-154 ms)")
    print("roll loop: |S| at 1 / 2 / 4 Hz " + " / ".join(f"{sensitivity(f, 'roll'):.2f}" for f in (1, 2, 4))
          + f"; crossover {crossover('roll'):.2f} Hz")
    print(f"yaw authority: {abs(yaw_actuator(0)):.2f} N m per unit mix held, {abs(yaw_actuator(1e6j)):.2f} at once")

    sigma_t = WAKE_SIGMA * T_HOVER
    roll, roll_rate = angle_and_rate(lambda f: 4 * LEVER * LEVER * wake_psd(f, sigma_t), I_ROLL, "roll")
    print(f"\ncheck, the own wake's roll: {roll:.4f} deg at 1-4 Hz, {roll_rate:.2f} deg/s "
          "(flown, the noiseless human in still air: 0.061 deg, 1.78 deg/s)")

    flown_ratio = ((INFLOW_TORQUE - 1) / INFLOW_J) / ((1 - INFLOW_THRUST) / INFLOW_J)
    print("\n1. each rotor's torque unsteadiness, as a share r of the thrust's (sigma_Q/Q = r sigma_T/T):")
    for name, r in (("as flown: C_P(J)'s slope at J 0 over C_T's", flown_ratio), ("blade elements' bound", 0.2),
                    ("upper bound, in step with the thrust", 1.0)):
        sq = r * WAKE_SIGMA * Q_HOVER
        yaw, rate = angle_and_rate(lambda f, sq=sq: 4 * wake_psd(f, sq), I_YAW, "yaw")
        # Its in-plane twin: the same torque-producing forces, uneven around the disc, leave a net in-plane force of about
        # dQ / (0.7 R) on the hub's 0.226 m (an uneven share as large as the even one, A: the upper bound).
        twin = R_HUB / (0.7 * RADIUS)
        print(f"   {name}: r {r:.3f}, {yaw:.4f} deg, {rate:.2f} deg/s; with its in-plane twin at most "
              f"{yaw * math.sqrt(1 + twin * twin):.4f} deg")

    # 2. The spreads: the roll and pitch loops' per-motor thrust commands, each motor's yaw torque for them (its drive's
    # spin-up and the prop's torque), unmatched by eps between the motors that should cancel.
    def per_motor_torque_psd(f):
        s = 1j * TWO_PI * f
        t_cl = abs(loop(f, "roll") / (1 + loop(f, "roll")))   # the commands answer the disturbance in band
        dw = 1 / DT_DW                                         # rad/s per N
        return wake_psd(f, sigma_t) * t_cl ** 2 * abs((J_ROT * s + DQ_DW) * dw) ** 2
    print("\n2. the motors' spreads under the roll and pitch corrections (eps the unmatched share):")
    for eps in (0.03, 0.05):
        yaw, rate = angle_and_rate(lambda f, eps=eps: 2 * 4 * eps * eps * per_motor_torque_psd(f), I_YAW, "yaw")
        print(f"   eps {eps:.2f}: {yaw:.4f} deg, {rate:.2f} deg/s")

    print("\n3. the products of inertia: a roll moment yaws the body at 0.4-3.8 % of its roll rate (MassBuildTests):")
    print(f"   at the flown roll's 0.065 deg: {0.004 * 0.065:.4f}-{0.038 * 0.065:.4f} deg")

    need = needed_yaw_moment()
    per_ms = H_FORCE * R_HUB
    print(f"\nwhat the footage asks of the airframe: a yaw torque of {need[0]:.4f}-{need[1]:.4f} N m RMS within 1-4 Hz "
          f"({need[0] / 2 / Q_HOVER * 100:.1f}-{need[1] / 2 / Q_HOVER * 100:.1f} % of a rotor's torque from each, four apart)")
    print(f"4. the air's swirl: in-plane air across each hub turns the drone {per_ms:.4f} N m per m/s a rotor (H-force on "
          f"{R_HUB:.3f} m); the footage asks {need[0] / (2 * per_ms):.2f}-{need[1] / (2 * per_ms):.2f} m/s at 1-4 Hz at "
          "each hub, four apart")

    # 5. The human's yaw stick: what the footage asks, then tremor and crosstalk through the deadband.
    def t_yaw(f):
        l = loop(f, "yaw")
        return abs(l / (1 + l))
    t2 = t_yaw(2)
    cmd = E_RATE_BANDS[1] / t2
    print(f"\n5. the human's yaw stick: E's 1.3 deg/s at 1-4 Hz needs {cmd:.2f} deg/s of setpoint (|T| at 2 Hz {t2:.2f}),"
          f" {cmd / YAW_CENTRE * 500:.1f} us beyond the deadband ({cmd / YAW_CENTRE * 100:.1f} % of the half stick)")
    half_throw = 15.0                          # A, mm: the TX12 gimbal's stick end, about 30 mm over +-30 deg
    for tremor in (0.030, 0.060):              # P: 30 um mean physiological finger tremor, about 2x with the hand held out
        us = tremor / half_throw * 500
        print(f"   tremor {tremor * 1000:.0f} um at 8-12 Hz: {us:.1f} us, inside every deadband {DEADBANDS}; past it "
              f"{tremor / half_throw * YAW_CENTRE:.2f} deg/s, {tremor / half_throw * YAW_CENTRE / (TWO_PI * 10):.4f} deg at 10 Hz")
    collective = math.sqrt(band(lambda f: 4 * wake_psd(f, sigma_t), 1, 4))
    dt_dthrottle = 4 * OUT * DT_DW * B_DRIVE / C_MOTOR
    thr = collective / dt_dthrottle
    for c in (0.15, 0.41):                      # P: Wang 2012, multi-axis hand controllers (no thumb-gimbal data)
        print(f"   crosstalk {c:.2f} of a throttle that cancels the wake's {collective:.3f} N at 1-4 Hz "
              f"({thr * 100:.3f} % of the stick): {c * thr * 500:.2f} us of yaw, inside every deadband")
    for busy in (0.02, 0.05):
        us = 0.41 * busy * 1000
        past = max(0.0, us - DEADBANDS[0])
        print(f"   a busy throttle, {busy * 100:.0f} % at 1-4 Hz, at 0.41: {us:.1f} us, {past / 498 * YAW_CENTRE * t2:.2f}"
              f" deg/s past the sim's 2 us; it heaves the drone {busy * dt_dthrottle / MASS / (TWO_PI * 2) ** 2 * 100:.1f} cm at 2 Hz")

    # 6. The measurement: the camera's translation over a near scene reads as yaw (the rotation-only fit's degeneracy).
    w = 0.65                                    # D: the fit's weight, the mean cos^2 of the bearings over +-60 deg
    print("\n6. the camera's translation read as yaw (weight 0.65):")
    for z in (0.2, 1.0, 2.0, 5.0):
        x14 = G * E_ROLL_14 / DEG / (TWO_PI * 1.0) ** 2   # the lateral swing at 1 Hz from E's roll, a bound for the band
        print(f"   scene at {z:.1f} m: 1-4 Hz at most {w * x14 / z * DEG:.4f} deg; a drift of 2 / 5 cm/s reads "
              f"{w * 0.02 / z * DEG:.2f} / {w * 0.05 / z * DEG:.2f} deg/s")
    v_sim = G * 0.08 / DEG * 0.5                  # D: the sim's drift, its low-band roll held by tau_v 0.5 s
    print(f"   the sim's own drift, about {v_sim * 100:.1f} cm/s: {w * v_sim / 2 * DEG:.2f} deg/s at 2 m")


def crossover(axis):
    lo, hi = 0.05, 50.0
    for _ in range(80):
        mid = math.sqrt(lo * hi)
        if abs(loop(mid, axis)) > 1:
            lo = mid
        else:
            hi = mid
    return lo


if __name__ == "__main__":
    main()
