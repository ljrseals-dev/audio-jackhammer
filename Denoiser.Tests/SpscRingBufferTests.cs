using Denoiser.Realtime;
using Xunit;

namespace Denoiser.Tests;

public sealed class SpscRingBufferTests
{
    [Fact]
    public void WriteRead_RoundTripsInOrder()
    {
        var ring = new SpscRingBuffer(8);
        var input = new float[] { 1, 2, 3, 4, 5 };
        Assert.Equal(5, ring.Write(input));
        var output = new float[5];
        Assert.Equal(5, ring.Read(output));
        Assert.Equal(input, output);
    }

    [Fact]
    public void Write_WrapsAround()
    {
        var ring = new SpscRingBuffer(4);
        ring.Write(new float[] { 1, 2, 3 });
        var discard = new float[3];
        ring.Read(discard);
        ring.Write(new float[] { 4, 5, 6, 7 });
        var output = new float[4];
        ring.Read(output);
        Assert.Equal(new float[] { 4, 5, 6, 7 }, output);
    }

    [Fact]
    public void Write_WhenFull_WritesPartialAndReportsCount()
    {
        var ring = new SpscRingBuffer(4);
        ring.Write(new float[] { 1, 2, 3 });
        int written = ring.Write(new float[] { 4, 5, 6 });
        Assert.Equal(1, written);
        Assert.Equal(4, ring.AvailableToRead);
    }

    [Fact]
    public void ConcurrentProducerConsumer_NoLossNoReordering()
    {
        const int total = 100_000;
        var ring = new SpscRingBuffer(1024);
        var received = new float[total];
        int receivedCount = 0;
        var done = new ManualResetEventSlim(false);

        var producer = new Thread(() =>
        {
            int sent = 0;
            while (sent < total)
            {
                Span<float> chunk = stackalloc float[64];
                int n = Math.Min(64, total - sent);
                for (int i = 0; i < n; i++) chunk[i] = sent + i;
                int written = ring.Write(chunk.Slice(0, n));
                sent += written;
                if (written == 0) Thread.SpinWait(50);
            }
            done.Set();
        });

        var consumer = new Thread(() =>
        {
            Span<float> buffer = stackalloc float[128];
            while (Volatile.Read(ref receivedCount) < total)
            {
                int n = ring.Read(buffer);
                for (int i = 0; i < n; i++)
                    received[Interlocked.Increment(ref receivedCount) - 1] = buffer[i];
                if (n == 0) Thread.SpinWait(50);
            }
        });

        producer.Start();
        consumer.Start();
        Assert.True(done.Wait(TimeSpan.FromSeconds(30)));
        Assert.True(consumer.Join(TimeSpan.FromSeconds(30)));

        for (int i = 0; i < total; i++)
            Assert.Equal(i, received[i]);
    }

    [Fact]
    public void Capacity_MustBePowerOfTwo()
    {
        Assert.Throws<ArgumentException>(() => new SpscRingBuffer(6));
    }
}
