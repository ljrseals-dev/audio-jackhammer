# Listening verdicts — construction_sample.wav

## Protocol
Play `null_baseline.wav` then `cleaned_construction.wav` back-to-back on
headphones. Answer for the cleaned version:

1. **Intelligibility**: understand the words without a transcript? (yes / mostly / no)
2. **Noise removal**: construction noise meaningfully reduced? (yes / slightly / no)
3. **Call-usable**: would you take a phone call on this? (yes / borderline / no)

Two independent listeners required.

## Outputs (generate with real weights)

```bash
export DEEP_FILTER_NET_MODEL=... DEEP_FILTER_NET_AUX=...
dotnet run --project Denoiser.Cli -- \
  --input Denoiser.Tests/Fixtures/construction_sample.wav \
  --output Denoiser.Tests/Fixtures/listening/cleaned_construction.wav \
  --model deepfilternet --ola auto --lookahead true

dotnet run --project Denoiser.Cli -- \
  --input Denoiser.Tests/Fixtures/construction_sample.wav \
  --output Denoiser.Tests/Fixtures/listening/null_baseline.wav \
  --model null
```

## Verdicts

### Listener A (initials, date)
- Intelligibility:
- Noise removal:
- Call-usable:
- Comments:

### Listener B (initials, date)
- Intelligibility:
- Noise removal:
- Call-usable:
- Comments:

## Gate
- Either listener "no" on intelligibility → **do not ship DFN for speech cleanup**
- Both "yes" intelligibility + yes/slightly noise → **C-prep passes**; proceed to C
- Conditional pass if intelligibility yes but noise only slightly reduced

**Gate outcome: _pending human listening with real weights_**
