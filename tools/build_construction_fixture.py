#!/usr/bin/env python3
"""Build construction_sample.wav from FSD50K noise + LibriSpeech speech.
Requires: soundfile, scipy, numpy
"""
import argparse
import hashlib
import sys
from math import gcd

import numpy as np

SAMPLE_RATE = 48000
DURATION_S = 2.0
NOISE_PORTION_S = 1.0
SNR_DB = 0.0

def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()

def load_resampled(path, target_len):
    import soundfile as sf
    from scipy.signal import resample_poly
    data, sr = sf.read(path, dtype="float32", always_2d=False)
    if data.ndim > 1:
        data = data.mean(axis=1)
    if sr != SAMPLE_RATE:
        g = gcd(SAMPLE_RATE, sr)
        up, down = SAMPLE_RATE // g, sr // g
        data = resample_poly(data, up, down).astype(np.float32)
    if len(data) < target_len:
        data = np.pad(data, (0, target_len - len(data)))
    return data[:target_len]

def rms(x):
    return float(np.sqrt(np.mean(x * x))) if len(x) else 0.0

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--noise", required=True)
    p.add_argument("--speech", required=True)
    p.add_argument("--out", required=True)
    args = p.parse_args()

    try:
        import soundfile as sf
    except ImportError:
        sys.exit("pip install soundfile scipy numpy")

    n_total = int(SAMPLE_RATE * DURATION_S)
    n_noise = int(SAMPLE_RATE * NOISE_PORTION_S)
    n_speech_region = n_total - n_noise

    noise = load_resampled(args.noise, n_total)
    speech = load_resampled(args.speech, n_speech_region)

    noise_rms = rms(noise[n_noise:])
    speech_rms = rms(speech)
    if speech_rms == 0:
        sys.exit("speech clip is silent")
    speech_target = noise_rms * (10 ** (SNR_DB / 20.0))
    speech = speech * (speech_target / speech_rms)

    mixed = np.empty(n_total, dtype=np.float32)
    mixed[:n_noise] = noise[:n_noise]
    mixed[n_noise:] = noise[n_noise:] + speech

    peak = float(np.max(np.abs(mixed)))
    if peak > 0:
        mixed *= (10 ** (-1.0 / 20.0)) / peak

    sf.write(args.out, mixed, SAMPLE_RATE, subtype="FLOAT")
    print(f"wrote {args.out}")
    print(f"  noise sha256={sha256(args.noise)}")
    print(f"  speech sha256={sha256(args.speech)}")
    print(f"  SNR_DB={SNR_DB}")

if __name__ == "__main__":
    main()
