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
         --ola auto|on|off                  # default auto
         --lookahead                        # look-ahead limiter (default off)
         --snr-csv <path>                   # SNR sidecar CSV
         --log-level info|debug
```

### Framing and tail padding

All model calls use exact `FrameSize` samples (480 for RNNoise / Null).
A residual partial frame at end-of-stream is **zero-padded** and emitted, so
output length is `ceil(inputSamples / FrameSize) * FrameSize`.

### Overlap-add

`--ola auto` enables OLA only when the model reports `RequiresOverlapAdd`.
RNNoise and Null do not require it; DeepFilterNet may depending on export.
`--ola off` is bit-identical to the pre-OLA path for Null/RNNoise.

### Look-ahead limiter

`--lookahead` enables a short (5 ms) delay-line limiter that reduces post-impact
gating. Default is off for backward compatibility.

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


## OLA assumptions

`--ola on` / `auto` wraps the model in a 50% overlap-add adapter with a sqrt-Hann
synthesis window. The **model is assumed to apply its own sqrt-Hann analysis window**
internally (true for STFT-based denoisers like DeepFilterNet). RNNoise does not;
leave OLA off for it.

At end-of-stream the pipeline zero-pads any residual frame to FrameSize and, if the
denoiser is `IFlushableDenoiser`, drains its pending OLA tail.


## Trustworthiness fixtures (A-pass)

See `Denoiser.Tests/Fixtures/README.md` for:
- Synthetic golden input (`synthetic_2s_440_660_seed42.wav`)
- Construction-like sample (self-generated CC0)
- Null-path golden (`ola_off_golden_null.wav`) for `--ola off` regression
- Optional RNNoise golden when native lib is present

OLA window modes: `--ola auto|on|off` with synthesis windows `SqrtHann` (default), `Hann`, `Rectangular`
via `OlaWindowMode` on `OverlapAddAdapter`.


## DeepFilterNet DSP contract

DeepFilterNet's ONNX graph is **neural-only**. The C# layer implements:

| Stage | Spec |
|-------|------|
| Sample rate | 48 kHz |
| STFT | 960-point, hop 480, **Vorbis** window (Princen-Bradley) |
| Spectrum | 481 bins; deep filter on first 96 |
| ERB | 32 bands |
| Normalization | α=0.99; ERB mean −60…−90 dB; unit 0.001…0.0001 |
| Lookahead | 2 frames |
| Algorithmic delay | **1440 samples (30 ms)** |

`RequiresOverlapAdd = false` — DFN owns ISTFT/OLA. `--ola auto` resolves to Off.
Do **not** wrap DFN in `OverlapAddAdapter` (category error; double synthesis).

### Model files (ship gate)

```
export DEEP_FILTER_NET_MODEL=/path/to/deepfilter.onnx
export DEEP_FILTER_NET_AUX=/path/to/deepfilter-auxiliary.bin
```

Continuity test (`Dfn_SynthesisContinuity_AfterWarmup`) must pass with real weights
before enabling DFN for users. Coefficient layout is `[1,5,T,96,2]` (order, frame, freq, re/im).


## B-fix DSP semantics

- **Deep filter**: 5-tap centered complex FIR  
  `Y[t,k] = Σₙ c[n,t,k] · X[t−2+n,k]` for k &lt; 96 (not a scalar sum of taps).
- **pad_feat**: drop 2 frames from the front of feature tensors, zero-pad the tail (not in ONNX graph).
- **FFT**: FftSharp Bluestein (default) for N=960; `--fft naive` keeps the O(N²) reference.
- Continuity is necessary but not sufficient; kernel unit tests (`Tap0_UsesFrame_tMinus2`) prove the FIR taps.


## Realtime latency

DeepFilterNet has a **fixed algorithmic delay** that cannot be reduced by pipeline tuning:

| Component | Samples | ms @ 48 kHz | Source |
|-----------|---------|-------------|--------|
| STFT history | 480 | 10 | 960-pt FFT, hop 480 |
| Deep filter lookahead | 960 | 20 | `df_lookahead = 2` (training) |
| Streaming `pad_feat` | 960 | 20 | `conv_lookahead = 2` (encoder) |
| **Total (streaming)** | **2400** | **50** | |
| Offline batch | ~1440 | ~30 | no streaming pad_feat |

### Target use cases

| Use case | 50 ms one-way OK? | Status |
|----------|:-----------------:|--------|
| Recorded monitoring / post-hoc cleanup | Yes | Supported |
| Radio dispatch / push-to-talk | Yes | Supported |
| Live two-way conversation | Marginal | Test on real call before claiming |
| Hearing protection / ANC-adjacent | No | **Not this model** |

Latency floor is architectural. Lower latency needs a different model, not a faster pipeline.

**Signed off by:** _pending product owner — C-prep gate_  
**Date:** _

### Diagnostic flag

`--dfn-mode mask-only` applies the ERB mask only (skips the deep filter). Prints a stderr warning. For threshold calibration / ablation only — not production.


## Realtime status (pass C)

**Authorized.** Process owner cleared realtime work. C0 evidence still recommended for production:

| Gate | Status |
|------|--------|
| Real FSD50K+LibriSpeech fixture | Synthetic stand-in only |
| `ablation_results.json` complete | `status: pending` |
| Two human listening verdicts | Pending |
| Product-owner latency sign-off | Pending |

Scaffolding in `Denoiser.Realtime` (SPSC ring, device interfaces, latency budget)
is CI-tested and ready. WASAPI capture/render and `--mode realtime` ship only
after all four gates are closed and committed.


### Realtime CLI

```bash
denoiser --mode realtime --model null --duration 30
```
