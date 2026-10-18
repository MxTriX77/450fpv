"""Fixtures: a fake repo root in tmp_path with its own manifest, third-party folder and CREDITS.md, and a local HTTP
server standing in for the Poly Haven and ambientCG APIs and download hosts. No test touches the network or the
real manifest."""
import functools
import hashlib
import http.server
import json
import sys
import threading
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import fetch  # noqa: E402

THIRD_PARTY = Path("unity/Assets/_Project/Art/ThirdParty")
SET_ID = "fake_set"
MAPS = {"diffuse": f"{SET_ID}_diff_1k.png", "normal": f"{SET_ID}_nor_gl_1k.png"}


def content(name):
    """Deterministic fake file bytes, distinct per name."""
    return (name * 40).encode()


def run(*argv):
    """Run fetch.main in-process; returns its exit code."""
    try:
        return fetch.main([str(a) for a in argv])
    except SystemExit as e:  # argparse errors
        return e.code


def make_root(tmp_path, licence="CC0", base_url="https://dl.polyhaven.org/file/fake"):
    """A fake repo root with one pinned polyhaven-style set of two files, its manifest and a current CREDITS.md."""
    folder = tmp_path / THIRD_PARTY / "polyhaven" / SET_ID
    folder.mkdir(parents=True)
    files = []
    for map_name, name in MAPS.items():
        data = content(name)
        (folder / name).write_bytes(data)
        files.append({"map": map_name, "name": name, "url": f"{base_url}/{name}", "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest()})
    entry = {"source": "polyhaven", "id": SET_ID, "title": "Fake Set", "resolution": "1k", "licence": licence,
             "author": "A. Tester", "page": f"https://polyhaven.com/a/{SET_ID}",
             "destination": f"{THIRD_PARTY.as_posix()}/polyhaven/{SET_ID}", "files": files}
    (tmp_path / "tools" / "assets").mkdir(parents=True)
    (tmp_path / "tools" / "assets" / "manifest.json").write_text(json.dumps({"entries": [entry]}, indent=2) + "\n",
                                                                  encoding="utf-8")
    if licence in fetch.LICENCES:
        assert run("--credits", "--root", tmp_path) == 0
    return tmp_path


@pytest.fixture
def root(tmp_path):
    return make_root(tmp_path)


class Quiet(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


@pytest.fixture
def server(tmp_path_factory, monkeypatch):
    """Serves a www/ folder on 127.0.0.1 and points fetch's API constants and host allowlist at it.
    Yields (base_url, www_dir)."""
    www = tmp_path_factory.mktemp("www")
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Quiet, directory=str(www)))
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    base = f"http://127.0.0.1:{httpd.server_address[1]}"
    monkeypatch.setattr(fetch, "ALLOWED_HOSTS", {"127.0.0.1"})
    monkeypatch.setattr(fetch, "POLYHAVEN_API", base)
    monkeypatch.setattr(fetch, "AMBIENTCG_API", f"{base}/api/v2/full_json")
    monkeypatch.setattr(fetch, "TEXTURECAN", base)
    try:
        yield base, www
    finally:
        httpd.shutdown()
        httpd.server_close()
