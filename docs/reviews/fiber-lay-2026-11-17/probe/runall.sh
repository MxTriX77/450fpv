#!/bin/bash
# Probe-replay the pilot's 30 flights one after another (one dotnet process at a time). $1: this folder's built probe
# (dotnet build -c Release in a folder holding lay.cs, lay.csproj and rt/: a copy of the sim's Runtime with hooks.patch
# applied); $2: the triage's log (triage.log, the flights by tag and seed); $3: the output folder.
PROBE="$1"; TRIAGE="$2"; OUT="$3"
grep -o "^f[0-9]* seed 0x[0-9A-F]*" "$TRIAGE" | while read tag _ seed; do
  dotnet "$PROBE" "$(git rev-parse --show-toplevel)" "$LOCALAPPDATA/450fpv/flights" "$seed" "$tag" "$OUT" 2>&1 | tail -1
done
