using System.CommandLine;
using Denoiser.Codecs;
using Denoiser.Core;
using Denoiser.Models;
using Microsoft.Extensions.Logging;

namespace Denoiser.Cli;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Input audio file path (required for offline mode).");

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path (required for offline mode).");

        var modelOption = new Option<string>(
            aliases: new[] { "--model", "-m" },
            getDefaultValue: () => "rnnoise",
            description: "Denoising model: rnnoise | deepfilternet | null");

        var modeOption = new Option<string>(
            aliases: new[] { "--mode" },
            getDefaultValue: () => "offline",
            description: "Processing mode: offline | realtime");

        var highPassOption = new Option<float>(
            aliases: new[] { "--highpass" },
            getDefaultValue: () => 90f,
            description: "High-pass cutoff in Hz (default 90). Set 0 to disable.");

        var formatOption = new Option<string>(
            aliases: new[] { "--output-format", "-f" },
            getDefaultValue: () => "wav",
            description: "Output format: wav | mp3 | aac (currently only wav is fully implemented).");

        var deviceOption = new Option<int>(
            aliases: new[] { "--device" },
            getDefaultValue: () => -1,
            description: "Playback / capture device index (realtime mode).");

        var modelPathOption = new Option<string?>(
            aliases: new[] { "--model-path" },
            description: "Path to DeepFilterNet ONNX model file.");

        var logLevelOption = new Option<string>(
            aliases: new[] { "--log-level" },
            getDefaultValue: () => "info",
            description: "Log level: info | debug");

        var root = new RootCommand("Environmental Denoiser for Construction-Site Audio")
        {
            inputOption, outputOption, modelOption, modeOption,
            highPassOption, formatOption, deviceOption, modelPathOption, logLevelOption
        };

        root.SetHandler((context) =>
        {
            var input = context.ParseResult.GetValueForOption(inputOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var mode = context.ParseResult.GetValueForOption(modeOption)!;
            var highPass = context.ParseResult.GetValueForOption(highPassOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var device = context.ParseResult.GetValueForOption(deviceOption);
            var modelPath = context.ParseResult.GetValueForOption(modelPathOption);
            var logLevel = context.ParseResult.GetValueForOption(logLevelOption)!;

            context.ExitCode = Run(input, output, model, mode, highPass, format, device, modelPath, logLevel);
        });

        return await root.InvokeAsync(args);
    }

    static int Run(
        string? input, string? output, string model, string mode,
        float highPass, string format, int device, string? modelPath, string logLevel)
    {
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
            b.SetMinimumLevel(logLevel.Equals("debug", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Debug : LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("Denoiser");

        try
        {
            mode = mode.ToLowerInvariant();
            model = model.ToLowerInvariant();
            format = format.ToLowerInvariant();

            if (mode == "offline")
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.Error.WriteLine("Error: --input is required in offline mode.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(output))
                {
                    Console.Error.WriteLine("Error: --output is required in offline mode.");
                    return 1;
                }
            }

            IDenoiser denoiser;
            try
            {
                denoiser = DenoiserFactory.Create(model, modelPath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or DllNotFoundException)
            {
                Console.Error.WriteLine($"Error loading model '{model}': {ex.Message}");
                return 2;
            }

            logger.LogInformation("Model: {Name}, FrameSize: {Fs}, HighPass: {Hp} Hz",
                denoiser.Name, denoiser.FrameSize, highPass > 0 ? highPass : 0);

            IAudioSource source;
            IAudioSink sink;

            if (mode == "realtime")
            {
                logger.LogInformation("Realtime mode — capturing from device {Device}", device);
                source = new MicrophoneSource(deviceNumber: Math.Max(0, device));
                sink = new LivePlaybackSink(deviceNumber: device);
            }
            else
            {
                logger.LogInformation("Offline mode: {Input} → {Output}", input, output);
                source = new AudioFileSource(input!);

                if (format is "mp3" or "aac")
                {
                    logger.LogWarning("Compressed output ({Format}) is not yet fully implemented; writing WAV instead.", format);
                    output = Path.ChangeExtension(output, ".wav");
                }
                sink = new WavFileSink(output!);
            }

            bool enableHp = highPass > 0;
            using var pipeline = new DenoiserPipeline(
                source, denoiser, sink,
                highPassHz: enableHp ? highPass : 90f,
                enableHighPass: enableHp,
                logger: logger);

            var (samples, elapsed) = pipeline.Run();
            double seconds = samples / 48000.0;
            double rtf = elapsed.TotalSeconds / Math.Max(seconds, 1e-9);

            logger.LogInformation(
                "Done. Samples={Samples}, Audio={Audio:F2}s, Wall={Wall:F2}s, RTF={Rtf:F3}",
                samples, seconds, elapsed.TotalSeconds, rtf);

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
