Copyright © 2026 Lewis James Seals Jr. All rights reserved.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT.
# Environmental Denoiser for Construction-Site Audio

Standalone .NET 8 application that ingests compressed audio, applies speech / environmental denoising tuned for construction-site noise (jackhammers, rock drills, machinery rumble, broadband traffic), and writes cleaned audio or routes it to a playback device.

This is **speech/environmental enhancement**, not active noise cancellation (ANC).

## Solution structure

| Project | Role |
|---------|------|
| `Denoiser.Core` | Interfaces (`IAudioSource`, `IDenoiser`, `IAudioSink`), pipeline, high-pass filter, soft limiter, SNR estimator, `NullDenoiser` |
| `Denoiser.Codecs` | NAudio-based file source, WAV sink, microphone source, live playback sink |
| `Denoiser.Models` | `RnNoiseDenoiser` (native), `DeepFilterNetOnnxDenoiser` (ONNX Runtime), factory |
| `Denoiser.Cli` | Command-line front-end |
| `Denoiser.Tests` | Unit & pipeline tests |

## Requirements

- .NET 8 SDK (or later)
- For RNNoise: native `librnnoise` shared library on the library path
- For DeepFilterNet: an exported ONNX model file

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

## Model setup

### RNNoise (Option A – baseline, pure CPU)

1. Build or download `librnnoise`:
   - Source: https://github.com/xiph/rnnoise
   - Or use a prebuilt package for your platform.
2. Place the shared library next to the executable (or on `LD_LIBRARY_PATH` / system path):
   - Linux: `librnnoise.so`
   - Windows: `rnnoise.dll`
   - macOS: `librnnoise.dylib`

If the library is missing the CLI fails loudly with placement instructions.

### DeepFilterNet2 (Option B – higher quality)

1. Export DeepFilterNet2 to ONNX (see the upstream project: https://github.com/Rikorose/DeepFilterNet).
2. Place the `.onnx` file at `models/deepfilternet2.onnx` relative to the working directory, or pass `--model-path`.

If the file is missing the CLI fails loudly with placement instructions.

## Codec support matrix

| Format | Decode (NAudio) | Encode (output) |
|--------|-----------------|-----------------|
| WAV    | Yes             | Yes (primary)   |
| MP3    | Yes*            | Partial†        |
| AAC/M4A| Yes*            | Partial†        |
| Opus/Ogg | Yes*          | No              |
| FLAC   | Yes*            | No              |
| WMA    | Yes*            | No              |

\* Platform codec support required (Windows Media Foundation on Windows; may need extra packages on Linux).  
† CLI currently falls back to WAV when `--output-format mp3|aac` is requested.

All audio is converted to **mono 48 kHz float32** at the pipeline boundary.

## CLI

```
denoiser --input <path> --output <path>
         --model rnnoise|deepfilternet|null
         --mode offline|realtime
         --highpass 90
         --output-format wav|mp3|aac
         --device <playback device index>   # realtime only
         --model-path <path-to-onnx>        # deepfilternet only
         --log-level info|debug
```

### Examples

**Offline WAV cleanup (null model – pipeline smoke test):**
```bash
dotnet run --project Denoiser.Cli -- --input sample.wav --output clean.wav --model null --highpass 90
```

**Offline with RNNoise:**
```bash
dotnet run --project Denoiser.Cli -- --input construction.mp3 --output enhanced.wav --model rnnoise
```

**Realtime microphone monitoring (null model):**
```bash
dotnet run --project Denoiser.Cli -- --mode realtime --model null --device 0
```

## Architecture

```
IAudioSource  →  HighPassFilter (80–100 Hz)  →  IDenoiser  →  SoftLimiter  →  IAudioSink
```

- **High-pass** is mandatory in the default config (machinery rumble).
- **Soft limiter** prevents clipping on impact transients after denoising.
- **SNR estimate** (rough RMS-based) is logged per block for visibility.
- Processing is always chunked in model-frame multiples (480 for RNNoise).

## Benchmark / RTF

After building, a simple RTF measurement is printed by the CLI at the end of every offline run:

```
Done. Samples=..., Audio=X.XXs, Wall=Y.YYs, RTF=Z.ZZZ
```

Target: RTF &lt; 1.0 for RNNoise on a standard laptop CPU. DeepFilterNet RTF is typically ~0.04 on a notebook CPU when the ONNX model is present.

## Tests

```bash
dotnet test
```

Coverage includes:

- NullDenoiser bit-exact round-trip
- High-pass frequency response sanity (DC attenuation, 1 kHz preservation)
- Soft limiter peak clamping
- End-to-end pipeline on a 2-second synthetic tone
- SNR estimator smoke test

## License notes

See `THIRD_PARTY_NOTICES.md` for RNNoise, DeepFilterNet, NAudio, and ONNX Runtime licenses (all permissive).

## Design notes

- No reference to any Kinect / pose-estimation repository.
- No Python runtime in the final deliverable (ONNX path is pure managed + native ORT).
- Fail loudly if a model cannot be loaded; never silently fall back to pass-through.
- Output is described as “enhanced” / “denoised”, never “noise-cancelled”.
