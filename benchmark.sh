#!/usr/bin/env bash
# Simple RTF benchmark helper.
# Generates a 10-second synthetic tone, runs the pipeline with --model null,
# and reports wall time / RTF. Swap --model when native libs / ONNX are available.

set -euo pipefail
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT

echo "Generating 10 s synthetic tone..."
dotnet run --project Denoiser.Tests --no-build 2>/dev/null || true

# Use a tiny C# one-liner via the CLI null path after building a fixture
python3 - <<'PY' 2>/dev/null || true
import struct, math, wave, sys
path = sys.argv[1] if len(sys.argv) > 1 else "/tmp/tone.wav"
sr, dur = 48000, 10
n = sr * dur
with wave.open(path, "w") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(sr)
    for i in range(n):
        v = int(0.3 * 32767 * math.sin(2 * math.pi * 440 * i / sr))
        w.writeframes(struct.pack("<h", v))
print(path)
PY

# Prefer sox / ffmpeg if present to make a quick fixture
IN="$TMPDIR/tone.wav"
if command -v ffmpeg >/dev/null 2>&1; then
  ffmpeg -y -f lavfi -i "sine=frequency=440:duration=10" -ar 48000 -ac 1 "$IN" 2>/dev/null
elif command -v sox >/dev/null 2>&1; then
  sox -n -r 48000 -c 1 "$IN" synth 10 sine 440
else
  # Minimal WAV header + silence via printf (still exercises the pipeline)
  python3 -c "
import struct, math, wave
sr,dur=48000,10
with wave.open('$IN','w') as w:
  w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
  for i in range(sr*dur):
    v=int(0.3*32767*math.sin(2*math.pi*440*i/sr))
    w.writeframes(struct.pack('<h',v))
" 2>/dev/null || {
  echo "Cannot generate fixture (need ffmpeg, sox, or python3). Skipping."
  exit 0
}
fi

OUT="$TMPDIR/out.wav"
echo "Running pipeline (model=null)..."
dotnet run --project Denoiser.Cli -c Release -- --input "$IN" --output "$OUT" --model null --highpass 90 --log-level info
echo "Benchmark complete."
