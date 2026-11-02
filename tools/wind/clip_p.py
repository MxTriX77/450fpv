"""Clip P's published numbers, for the Gaussian check (wind.md section 6.5) on a machine without reference/.

Every number is measured on P by the first attempt's wind_stats.py and published in
docs/reference-notes/wind.md; nothing here is read from reference/. summary() returns the part of
wind_stats.analyse's output that wind_stats.gauss_check reads: the frame rate, the frame count, the
belt segment, the mean lean, the residual spread over the belt and the field, and the Welch PSD.

The PSD is rebuilt from the band table (wind.md section 3.1): each band's published RMS spread flat
over the band's Welch bins (derived), so every band keeps P's power. The measured PSD's shape
inside a band (section 3.2) is not kept; with reference/ present, run the check on P's own log
(--letter P --segment 121-330 --gauss N) as the original did.
"""
FPS = 29.917                       # measured: the recording's frame rate (wind.md sections 1.1, 6.2)
FRAMES = 1417                      # measured: P's frames (section 1.6)
BELT = (121, 330)                  # measured: set B, the tree-belt segment (section 1.6)
MEAN_LEAN = dict(roll=-9.11, pitch=-22.71)        # measured, deg: section 2, angle means
RES_STD = dict(                     # measured, deg: section 4.4, residual std over B (belt) and F (field)
    inside=dict(roll=1.26, pitch=0.43, yaw=1.67),
    outside=dict(roll=0.51, pitch=0.28, yaw=0.44))
BANDS = ((0.23, 0.5), (0.5, 1.0), (1.0, 2.0), (2.0, 4.0), (4.0, 8.0), (8.0, 15.0))   # wind_stats.BANDS
BAND_RMS = dict(                    # measured, deg: section 3.1, band RMS of the angles (heading for yaw)
    roll=(0.90, 0.50, 0.20, 0.114, 0.091, 0.086),
    pitch=(0.27, 0.155, 0.179, 0.121, 0.044, 0.031),
    yaw=(1.36, 0.59, 0.27, 0.066, 0.038, 0.039))
WELCH_N = 128                      # wind_stats.WELCH_N


def psd(axis):
    """[(f, deg^2/Hz)] on the Welch bins, DC first: each band's RMS^2 spread evenly over its bins."""
    df = FPS / WELCH_N
    f = [k * df for k in range(WELCH_N // 2 + 1)]
    out = [(0.0, 0.0)]
    for fi in f[1:]:
        j = next(i for i, (lo, hi) in enumerate(BANDS) if lo <= fi < hi)
        nbins = sum(1 for x in f if BANDS[j][0] <= x < BANDS[j][1])
        out.append((fi, BAND_RMS[axis][j] ** 2 / (nbins * df)))
    return out


def summary():
    return dict(fps=FPS, frames=FRAMES,
                angle=[dict(mean=MEAN_LEAN["roll"]), dict(mean=MEAN_LEAN["pitch"])],
                segment=dict(range=BELT, **{side: {k: dict(res_std=v) for k, v in s.items()}
                                             for side, s in RES_STD.items()}),
                spectra={k: dict(psd=psd(k)) for k in BAND_RMS})
