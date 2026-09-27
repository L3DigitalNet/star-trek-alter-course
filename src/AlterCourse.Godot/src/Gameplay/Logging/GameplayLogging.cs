using System.Globalization;
using AlterCourse.Core.Gameplay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Formatting.Json;

namespace AlterCourse.Godot.Gameplay.Logging;

/// <summary>Owns the scene-lifetime Serilog backend; no global logger or engine runtime is required.</summary>
internal sealed class GameplayLogging : IDisposable
{
    internal const long FileSizeLimitBytes = 4 * 1024 * 1024;
    internal const int RetainedFileCount = 7;
    private readonly ILoggerFactory? _factory;
    private readonly Action _fallback;
    private bool _disposed;

    private GameplayLogging(ILoggerFactory? factory, Action fallback)
    {
        _factory = factory;
        _fallback = fallback;
        Diagnostics = new GameDiagnostics(GetLogger<GameDiagnostics>(), fallback);
        SimulationLogger = GetLogger<GameSimulation>();
    }

    internal GameDiagnostics Diagnostics { get; }
    internal ILogger<GameSimulation> SimulationLogger { get; }

    internal static GameplayLogging Create(
        Func<string> logDirectory,
        Action fallback,
        Func<string, ILoggerFactory>? factory = null
    )
    {
        try
        {
            return new GameplayLogging((factory ?? CreateFactory)(logDirectory()), fallback);
        }
        catch (Exception)
        {
            SafeFallback(fallback);
            return new GameplayLogging(null, fallback);
        }
    }

    private static Serilog.Extensions.Logging.SerilogLoggerFactory CreateFactory(string directory)
    {
        Directory.CreateDirectory(directory);
        Serilog.Core.Logger backend = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("SessionCorrelation", Guid.NewGuid().ToString("N"))
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                new JsonFormatter(renderMessage: true, formatProvider: CultureInfo.InvariantCulture),
                Path.Combine(directory, "gameplay-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true
            )
            .CreateLogger();
        return new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: true);
    }

    private ILogger<T> GetLogger<T>()
    {
        try
        {
            return _factory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
        }
        catch (Exception)
        {
            SafeFallback(_fallback);
            return NullLogger<T>.Instance;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            _factory?.Dispose();
        }
        catch (Exception)
        {
            SafeFallback(_fallback);
        }
    }

    private static void SafeFallback(Action fallback)
    {
        // Serilog SelfLog can contain personal paths and exception payloads; it is never forwarded to this channel.
        try
        {
            fallback();
        }
        catch (Exception)
        { /* Cleanup and startup remain independent from diagnostic output. */
        }
    }
}
