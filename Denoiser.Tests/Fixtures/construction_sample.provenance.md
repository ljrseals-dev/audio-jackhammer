# construction_sample.wav — provenance

## Current status

The committed `construction_sample.wav` is a **synthetic** CC0 stand-in used for
pipeline correctness tests until real sources are mixed.

To replace with a real FSD50K + LibriSpeech mix:

```bash
python tools/build_construction_fixture.py \
  --noise path/to/fsd50k_jackhammer.wav \
  --speech path/to/librispeech_utterance.flac \
  --out Denoiser.Tests/Fixtures/construction_sample.wav
```

Then fill in the SHA-256 values below from the script output and commit.

## Target sources (CC-BY 4.0)

- **Noise**: FSD50K — https://zenodo.org/record/4060432 — labels such as Jackhammer, Power_drill
  - Citation: Fonseca et al., "FSD50K: an open dataset of human-labeled sound events", 2020
  - sha256: _(run build script)_
- **Speech**: LibriSpeech dev-clean — https://openslr.org/12
  - Citation: Panayotov et al., "LibriSpeech: an ASR corpus based on public domain audio books", 2015
  - sha256: _(run build script)_

## Mix parameters

- Duration: 2.0 s @ 48 kHz mono float32
- Layout: 0–1 s noise only; 1–2 s speech + noise
- SNR: 0 dB; peak normalized to −1 dBFS
- Rebuild is deterministic given the same source files (no RNG)
