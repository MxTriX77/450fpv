"""A stalk clamped upright at its base, laid over by a vertical jet whose load is normal to it, w*sin^2(theta) per length
(cross-flow principle), its free end resting on the ground (moment 0, a vertical reaction only). Units: EI = 1, w = 1, so
lengths are in the bending length l_b = (EI/w)^(1/3). Optional self weight g = mu*g/w (downward). Prints the crest height
(the highest point of the arch) for long stalks, the number design.md 2.12 uses."""
import math, sys


def shoot(m0, nx0, ny0, L, g, n=4000, full=False):
    ds = L / n
    x = y = th = 0.0
    M = m0          # M_z, EI th' = -M_z, so th'(0) = -m0
    Nx, Ny = nx0, ny0
    ymax = 0.0
    path = []

    def deriv(x, y, th, M, Nx, Ny):
        s, c = math.sin(th), math.cos(th)
        fx, fy = s * s * c, -s * s * s - g
        return (s, c, -M, -(s * Ny - c * Nx), -fx, -fy)

    st = [x, y, th, M, Nx, Ny]
    for i in range(n):
        k1 = deriv(*st)
        k2 = deriv(*[a + 0.5 * ds * b for a, b in zip(st, k1)])
        k3 = deriv(*[a + 0.5 * ds * b for a, b in zip(st, k2)])
        k4 = deriv(*[a + ds * b for a, b in zip(st, k3)])
        st = [a + ds / 6 * (b + 2 * c + 2 * d + e) for a, b, c, d, e in zip(st, k1, k2, k3, k4)]
        ymax = max(ymax, st[1])
        if full:
            path.append((st[0], st[1], st[2]))
    return st, ymax, path


def solve(L, g, guess):
    p = list(guess)
    for it in range(60):
        st, ymax, _ = shoot(*p, L, g)
        r = [st[3], st[4], st[1]]  # M(L) = 0, Nx(L) = 0, y(L) = 0
        if max(abs(v) for v in r) < 1e-10:
            break
        J = []
        for j in range(3):
            q = list(p)
            h = 1e-7 * max(1, abs(q[j]))
            q[j] += h
            s2, _, _ = shoot(*q, L, g)
            J.append([(s2[3] - st[3]) / h, (s2[4] - st[4]) / h, (s2[1] - st[1]) / h])
        # solve J^T d = -r (J rows are d/dp_j)
        A = [[J[j][i] for j in range(3)] for i in range(3)]
        b = [-v for v in r]
        d = gauss(A, b)
        lam = 1.0
        while lam > 1e-3:
            q = [a + lam * c for a, c in zip(p, d)]
            s2, _, _ = shoot(*q, L, g)
            r2 = [s2[3], s2[4], s2[1]]
            if sum(v * v for v in r2) < sum(v * v for v in r):
                break
            lam *= 0.5
        p = q
    st, ymax, path = shoot(*p, L, g, full=True)
    return p, ymax, max(abs(v) for v in (st[3], st[4], st[1])), path


def gauss(A, b):
    n = len(b)
    M = [row[:] + [bb] for row, bb in zip(A, b)]
    for i in range(n):
        piv = max(range(i, n), key=lambda r: abs(M[r][i]))
        M[i], M[piv] = M[piv], M[i]
        for r in range(i + 1, n):
            f = M[r][i] / M[i][i]
            for c in range(i, n + 1):
                M[r][c] -= f * M[i][c]
    x = [0.0] * n
    for i in reversed(range(n)):
        x[i] = (M[i][n] - sum(M[i][c] * x[c] for c in range(i + 1, n))) / M[i][i]
    return x


if __name__ == "__main__":
    g = float(sys.argv[1]) if len(sys.argv) > 1 else 0.0
    guess = (-1.0, 0.0, 1.0)
    # continuation in L from a short stalk to a long one
    for L in [1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0, 6.0, 8.0]:
        guess, ymax, res, path = solve(L, g, guess)
        thmax = max(t for _, _, t in path)
        print(f"g {g}  L/l_b {L:4.1f}  crest {ymax:.4f} l_b  base moment {-guess[0]:.4f}  tip reaction {shoot(*guess, L, g)[0][5]:.4f}  span {path[-1][0]:.3f}  max angle {math.degrees(thmax):.1f}  residual {res:.1e}")
