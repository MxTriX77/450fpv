"""The wind tools' tests import them from tools/wind. No test opens the real reference/ folder."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
