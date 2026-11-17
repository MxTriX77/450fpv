"""A tiny stand-in for the few numpy calls analyze.py makes (no numpy on this machine): 3-vectors as V, and the
polyline helpers done in plain Python."""
import math


class V(tuple):
    def __new__(cls, it):
        return tuple.__new__(cls, tuple(float(x) for x in it))

    def __add__(self, o): return V(a + b for a, b in zip(self, o))
    def __sub__(self, o): return V(a - b for a, b in zip(self, o))
    def __mul__(self, k): return V(a * k for a in self)
    __rmul__ = __mul__


class _Linalg:
    @staticmethod
    def norm(v): return math.sqrt(sum(x * x for x in v))


class _NP:
    linalg = _Linalg()

    @staticmethod
    def array(x):
        if x and isinstance(x[0], (tuple, list, V)):
            return [V(p) for p in x]
        return V(x)

    @staticmethod
    def zeros(n): return V([0.0] * n)

    @staticmethod
    def dot(a, b): return sum(x * y for x, y in zip(a, b))

    @staticmethod
    def clip(x, lo, hi): return lo if x < lo else hi if x > hi else x


np = _NP()
