namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Encoder negative pad: drop ConvLookahead frames from the front, append zeros.
/// Not present in the exported ONNX graph — applied in C# before the session.
/// </summary>
public static class PadFeat
{
    public const int ConvLookahead = 2;

    /// <summary>
    /// Batch: input/output are [T,F] row-major. For T=6: [0,1,2,3,4,5] → [2,3,4,5,0,0].
    /// </summary>
    public static void ApplyBatch(ReadOnlySpan<float> input, Span<float> output, int t, int f)
    {
        if (input.Length != t * f || output.Length != t * f)
            throw new ArgumentException("Shape mismatch.");

        int shift = Math.Min(ConvLookahead, t);
        for (int i = 0; i < t - shift; i++)
            input.Slice((i + shift) * f, f).CopyTo(output.Slice(i * f, f));
        for (int i = t - shift; i < t; i++)
            output.Slice(i * f, f).Clear();
    }

    /// <summary>
    /// Streaming: 2-frame delay on encoder input. First ConvLookahead pushes return false.
    /// </summary>
    public sealed class Streamer
    {
        private readonly float[][] _buffer;
        private int _count;

        public Streamer(int featureLength)
        {
            _buffer = new float[ConvLookahead + 1][];
            for (int i = 0; i < _buffer.Length; i++)
                _buffer[i] = new float[featureLength];
        }

        public bool TryPush(ReadOnlySpan<float> frame, Span<float> encoderInput)
        {
            frame.CopyTo(_buffer[_count]);
            _count++;
            if (_count <= ConvLookahead) return false;

            _buffer[0].AsSpan().CopyTo(encoderInput);
            var tmp = _buffer[0];
            for (int i = 0; i < _buffer.Length - 1; i++)
                _buffer[i] = _buffer[i + 1];
            _buffer[^1] = tmp;
            _count--;
            return true;
        }
    }
}
