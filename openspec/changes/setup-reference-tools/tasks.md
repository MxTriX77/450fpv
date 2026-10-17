# Tasks: setup-reference-tools

## 1. Tools

- [ ] 1.1 [reference-analyst] Add `tools/reference/extract_frames.py` and `tools/reference/contact_sheet.py` (ffmpeg for extraction, Pillow for tiling and labels; a path guard shared by both) with `tools/reference/tests/` on a synthetic ffmpeg `testsrc` clip in `tmp_path`, and verify `python -m pytest tools/reference` passes. Do not open or read any real clip or still in this task

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
