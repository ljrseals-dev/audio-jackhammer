using System.CommandLine;
using Denoiser.Codecs;
using Denoiser.Core;
using Denoiser.Models;
using Denoiser.Realtime;
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

        var olaOption = new Option<string>(
            aliases: new[] { "--ola" },
            getDefaultValue: () => "auto",
            description: "Overlap-add: auto | on | off (default auto — on iff model requires it).");

        var lookAheadOption = new Option<bool>(
            aliases: new[] { "--lookahead" },
            getDefaultValue: () => false,
            description: "Enable look-ahead limiter (default off).");

        var snrCsvOption = new Option<string?>(
            aliases: new[] { "--snr-csv" },
            description: "Write per-block SNR estimates to this CSV path.");

        var dfnModeOption = new Option<string>(
            aliases: new[] { "--dfn-mode" },
            getDefaultValue: () => "full",
            description: "DeepFilterNet mode: full | mask-only (mask-only is diagnostic only).");

        var captureDeviceOption = new Option<int>(
            aliases: new[] { "--capture-device" },
            getDefaultValue: () => 0,
            description: "WASAPI capture device index (realtime mode).");

        var renderDeviceOption = new Option<int>(
            aliases: new[] { "--render-device" },
            getDefaultValue: () => 0,
            description: "WASAPI render device index (realtime mode).");

        var captureBufferOption = new Option<int>(
            aliases: new[] { "--capture-buffer-ms" },
            getDefaultValue: () => 10,
            description: "Capture buffer milliseconds (realtime).");

        var renderBufferOption = new Option<int>(
            aliases: new[] { "--render-buffer-ms" },
            getDefaultValue: () => 10,
            description: "Render buffer milliseconds (realtime).");

        var durationOption = new Option<int>(
            aliases: new[] { "--duration" },
            getDefaultValue: () => 0,
            description: "Realtime duration seconds (0 = until Ctrl+C).");

        var root = new RootCommand("Environmental Denoiser for Construction-Site Audio")
        {
            inputOption, outputOption, modelOption, modeOption,
            highPassOption, formatOption, deviceOption, modelPathOption, logLevelOption,
            olaOption, lookAheadOption, snrCsvOption, dfnModeOption,
            captureDeviceOption, renderDeviceOption, captureBufferOption, renderBufferOption, durationOption
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
            var ola = context.ParseResult.GetValueForOption(olaOption)!;
            var lookAhead = context.ParseResult.GetValueForOption(lookAheadOption);
            var snrCsv = context.ParseResult.GetValueForOption(snrCsvOption);
            var dfnMode = context.ParseResult.GetValueForOption(dfnModeOption)!;
            var captureDev = context.ParseResult.GetValueForOption(captureDeviceOption);
            var renderDev = context.ParseResult.GetValueForOption(renderDeviceOption);
            var captureBuf = context.ParseResult.GetValueForOption(captureBufferOption);
            var renderBuf = context.ParseResult.GetValueForOption(renderBufferOption);
            var duration = context.ParseResult.GetValueForOption(durationOption);

            context.ExitCode = Run(input, output, model, mode, highPass, format, device,
                modelPath, logLevel, ola, lookAhead, snrCsv, dfnMode,
                captureDev, renderDev, captureBuf, renderBuf, duration);
        });

        return await root.InvokeAsync(args);
    }

    static int Run(
        string? input, string? output, string model, string mode,
        float highPass, string format, int device, string? modelPath, string logLevel,
        string ola, bool lookAhead, string? snrCsv, string dfnMode = "full",
        int captureDevice = 0, int renderDevice = 0, int captureBufferMs = 10, int renderBufferMs = 10,
        int durationSec = 0)
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
            ola = ola.ToLowerInvariant();

            if (mode == "realtime")
            {
                return RunRealtime(model, modelPath, dfnMode, lookAhead,
                    captureDevice, renderDevice, captureBufferMs, renderBufferMs, durationSec, logger);
            }

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
                if (dfnMode.Equals("mask-only", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        "WARNING: --dfn-mode mask-only is a diagnostic setting. " +
                        "Output is the ERB mask without the deep filter and is not production quality.");
                }
                denoiser = DenoiserFactory.Create(model, modelPath, dfnMode: dfnMode);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or DllNotFoundException)
            {
                Console.Error.WriteLine($"Error loading model '{model}': {ex.Message}");
                return 2;
            }

            bool useOla = ola switch
            {
                "on" => true,
                "off" => false,
                _ => denoiser.RequiresOverlapAdd // auto
            };
            if (useOla)
            {
                if (!denoiser.RequiresOverlapAdd && ola == "on")
                {
                    // Explicit --ola on against a self-synthesizing model is a category error.
                    Console.Error.WriteLine(
                        $"Error: {denoiser.Name} handles its own STFT/ISTFT synthesis; " +
                        "do not wrap it in external OLA. Use --ola auto or --ola off.");
                    return 2;
                }
                if (denoiser.RequiresOverlapAdd)
                {
                    denoiser = new OverlapAddAdapter(denoiser);
                    logger.LogInformation("Overlap-add enabled");
                }
            }

            logger.LogInformation("Model: {Name}, FrameSize: {Fs}, HighPass: {Hp} Hz, LookAhead: {La}",
                denoiser.Name, denoiser.FrameSize, highPass > 0 ? highPass : 0, lookAhead);

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

            var snr = new SnrEstimator();
            if (!string.IsNullOrWhiteSpace(snrCsv))
            {
                sink = new SnrCsvSink(sink, snrCsv, sampleRate: 48000, snr: snr);
                logger.LogInformation("SNR CSV sidecar: {Path}", snrCsv);
            }

            var limiter = new SoftLimiter(lookAhead: lookAhead, releaseMs: 50f);
            bool enableHp = highPass > 0;
            using var pipeline = new DenoiserPipeline(
                source, denoiser, sink,
                highPassHz: enableHp ? highPass : 90f,
                enableHighPass: enableHp,
                limiter: limiter,
                snr: snr,
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
    static int RunRealtime(
        string model, string? modelPath, string dfnMode, bool lookAhead,
        int captureDevice, int renderDevice, int captureBufferMs, int renderBufferMs,
        int durationSec, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Console.Error.WriteLine("Error: --mode realtime requires Windows (WASAPI).");
            return 2;
        }

        IDenoiser denoiser;
        try
        {
            if (dfnMode.Equals("mask-only", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    "WARNING: --dfn-mode mask-only is a diagnostic setting.");
            }
            denoiser = DenoiserFactory.Create(model, modelPath, dfnMode: dfnMode);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading model '{model}': {ex.Message}");
            return 2;
        }

        // RNNoise/DFN algorithmic delay estimate
        int algorithmicMs = model.Contains("deepfilter") ? 40 : (model.Contains("rnnoise") ? 10 : 0);
        var budget = new LatencyBudget(captureBufferMs, algorithmicMs, renderBufferMs);
        Console.WriteLine($"[realtime] model={model} lookahead={lookAhead}");
        Console.WriteLine($"[realtime] budget: {budget.Describe()}");
        if (budget.TotalMs > 60)
            Console.WriteLine("[realtime] WARNING: budget exceeds 60 ms signed target — revisit model/buffers.");

        SoftLimiter? limiter = lookAhead
            ? new SoftLimiter(lookAhead: true, lookAheadMs: 5f, releaseMs: 50f)
            : new SoftLimiter(lookAhead: false);

        try
        {
            using var capture = new WasapiCaptureDevice(captureDevice, captureBufferMs);
            using var render = new WasapiRenderDevice(renderDevice, renderBufferMs);
            using var pipeline = new RealtimePipeline(capture, render, denoiser, budget, limiter);

            using var exit = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };

            pipeline.Start();
            if (durationSec > 0)
            {
                if (!exit.Wait(TimeSpan.FromSeconds(durationSec)))
                    logger.LogInformation("Duration elapsed.");
            }
            else
            {
                Console.WriteLine("[realtime] running — Ctrl+C to stop");
                exit.Wait();
            }
            pipeline.Stop();
            return 0;
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Realtime failed: {ex.Message}");
            return 3;
        }
    }

    }
}
