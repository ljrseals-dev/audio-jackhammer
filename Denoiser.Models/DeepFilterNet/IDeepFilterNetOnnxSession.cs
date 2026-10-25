namespace Denoiser.Models.DeepFilterNet;

public interface IDeepFilterNetOnnxSession : IDisposable
{
    (float[] erbMask, float[] dfCoefs) Run(float[] featErb, float[] featSpec, int t);
}
