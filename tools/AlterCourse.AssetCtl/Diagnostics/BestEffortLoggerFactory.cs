using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlterCourse.AssetCtl.Diagnostics;

/// <summary>Scopes optional Microsoft logging so diagnostic faults cannot replace command outcomes or cancellation.</summary>
internal sealed class BestEffortLoggerFactory(ILoggerFactory factory) : ILoggerFactory
{
    public static ILoggerFactory Create(Func<ILoggerFactory> create)
    {
        try
        {
            return new BestEffortLoggerFactory(create() ?? NullLoggerFactory.Instance);
        }
        catch (Exception exception) when (IsNonfatal(exception))
        {
            ReportDegradation();
            return NullLoggerFactory.Instance;
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        try
        {
            return new BestEffortLogger(factory.CreateLogger(categoryName));
        }
        catch (Exception exception) when (IsNonfatal(exception))
        {
            ReportDegradation();
            return NullLogger.Instance;
        }
    }

    public void AddProvider(ILoggerProvider provider)
    {
        try
        {
            factory.AddProvider(provider);
        }
        catch (Exception exception) when (IsNonfatal(exception))
        {
            ReportDegradation();
        }
    }

    public void Dispose()
    {
        try
        {
            factory.Dispose();
        }
        catch (Exception exception) when (IsNonfatal(exception))
        {
            ReportDegradation();
        }
    }

    internal static bool IsNonfatal(Exception exception) =>
        exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    internal static void ReportDegradation() => WriteDiagnostic("assetctl: diagnostics-unavailable.");

    internal static void WriteDiagnostic(string trustedMessage)
    {
        try
        {
            Console.Error.WriteLine(trustedMessage);
        }
        catch (Exception exception) when (IsNonfatal(exception))
        {
            // Diagnostic fallback has no further fallback: stderr failure must not recurse or affect command stdout.
        }
    }

    private sealed class BestEffortLogger(ILogger logger) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            try
            {
                IDisposable? scope = logger.BeginScope(state);
                return scope is null ? null : new BestEffortScope(scope);
            }
            catch (Exception exception) when (IsNonfatal(exception))
            {
                return null;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            try
            {
                return logger.IsEnabled(logLevel);
            }
            catch (Exception exception) when (IsNonfatal(exception))
            {
                return false;
            }
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            try
            {
                // Callers own allowlisted state. Exception objects carry uncontrolled prose, stack paths and Data to sinks.
                logger.Log(logLevel, eventId, state, exception: null, formatter);
            }
            catch (Exception failure) when (IsNonfatal(failure))
            {
                ReportDegradation();
            }
        }
    }

    private sealed class BestEffortScope(IDisposable scope) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                scope.Dispose();
            }
            catch (Exception exception) when (IsNonfatal(exception))
            {
                ReportDegradation();
            }
        }
    }
}
