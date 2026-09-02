using System.Globalization;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace AlterCourse.AssetCtl;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        try
        {
            string? repository = TryFindRepository();
            string? logRoot = ResolveLogRoot(arguments, repository);
            using ILoggerFactory loggerFactory = CreateLoggerFactory(repository, logRoot);
            using var httpClient = new HttpClient(CreateProviderHandler());
            return await RunAsync(arguments, loggerFactory, httpClient).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsProcessFatal(exception) && exception is not OperationCanceledException)
        {
            return ReportUnexpectedFailure(exception);
        }
    }

    internal static SocketsHttpHandler CreateProviderHandler() =>
        new() { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(15) };

    internal static string? ResolveLogRoot(string[] arguments, string? repository) =>
        string.Equals(arguments.FirstOrDefault(), "validate-config", StringComparison.Ordinal)
            ? null
            : TryLoadLogRoot(repository);

    internal static ILoggerFactory CreateLoggerFactory(string? repository, string? configuredLogRoot = ".assetctl/logs")
    {
        if (repository is null || configuredLogRoot is null)
        {
            return LoggerFactory.Create(builder => builder.AddProvider(new StderrLoggerProvider()));
        }

        try
        {
            string logRoot = PathPolicy.ResolveUnder(repository, configuredLogRoot, "log_root", allowMissing: true);
            Directory.CreateDirectory(logRoot);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(
                    formatProvider: CultureInfo.InvariantCulture,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    standardErrorFromLevel: LogEventLevel.Verbose
                )
                .WriteTo.File(
                    new JsonFormatter(renderMessage: true, formatProvider: CultureInfo.InvariantCulture),
                    Path.Combine(logRoot, "assetctl-.json"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 4 * 1024 * 1024,
                    rollOnFileSizeLimit: true
                )
                .CreateLogger();
            return new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, dispose: true);
        }
        catch (Exception exception) when (!IsProcessFatal(exception) && exception is not OperationCanceledException)
        {
            // Diagnostics must never control routing or publication; a broken sink degrades to the safe stderr fallback.
            Console.Error.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"assetctl: logging degraded: {Redactor.Sanitize(exception.Message)}"
                )
            );
            return LoggerFactory.Create(builder => builder.AddProvider(new StderrLoggerProvider()));
        }
    }

    internal static int ReportUnexpectedFailure(Exception exception)
    {
        _ = exception;
        Console.Error.WriteLine("assetctl: unexpected internal failure.");
        return 1;
    }

    private static bool IsProcessFatal(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException;

    private static async Task<int> RunAsync(string[] arguments, ILoggerFactory loggerFactory, HttpClient httpClient)
    {
        AdapterRegistry registry = new([
            new LocalPlaceholderGenerator(),
            new RecraftImageAdapter(httpClient),
            new OpenAiImageAdapter(httpClient),
            new XaiImageAdapter(httpClient),
            new OpenAiVisionReviewer(httpClient),
        ]);
        ConfigurationLoader loader = new(registry.Descriptors);
        AssetRouter router = new(registry);
        GenerationOrchestrator orchestrator = new(registry, router);
        CommandApp app = new(loader, router, orchestrator, loggerFactory.CreateLogger<CommandApp>());
        try
        {
            return await app.RunAsync(
                    arguments,
                    Console.IsInputRedirected ? CancellationToken.None : ConsoleCancelToken()
                )
                .ConfigureAwait(false);
        }
        catch (AssetCtlException exception)
        {
            Console.Error.WriteLine($"assetctl: {Redactor.Sanitize(exception.Message)}");
            return exception.ExitCode;
        }
        catch (ProviderException exception)
        {
            Console.Error.WriteLine($"assetctl: provider {exception.Category}: {Redactor.Sanitize(exception.Message)}");
            return exception.Category is ProviderErrorCategory.Authentication or ProviderErrorCategory.Authorization
                ? 3
                : 4;
        }
    }

    private static string? TryFindRepository()
    {
        try
        {
            return RepositoryLocator.Find(Environment.CurrentDirectory);
        }
        catch (AssetCtlException)
        {
            return null;
        }
    }

    private static string? TryLoadLogRoot(string? repository)
    {
        if (repository is null)
        {
            return null;
        }

        try
        {
            using var client = new HttpClient(CreateProviderHandler());
            AdapterRegistry registry = new([
                new LocalPlaceholderGenerator(),
                new RecraftImageAdapter(client),
                new OpenAiImageAdapter(client),
                new XaiImageAdapter(client),
                new OpenAiVisionReviewer(client),
            ]);
            return new ConfigurationLoader(registry.Descriptors).Load(repository).Paths.LogRoot;
        }
        catch (AssetCtlException)
        {
            return null;
        }
    }

    private static CancellationToken ConsoleCancelToken()
    {
        var source = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            source.Cancel();
        };
        return source.Token;
    }

    private sealed class StderrLoggerProvider : ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new StderrLogger(categoryName);

        public void Dispose() { }
    }

    private sealed class StderrLogger(string category) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (IsEnabled(logLevel))
            {
                Console.Error.WriteLine($"{category}: {Redactor.Sanitize(formatter(state, exception))}");
            }
        }
    }
}
