using System;
using Serilog;

namespace UnrealAssetScout.Logging;

// UnrealAssetScout-owned logging facade that isolates the application's logs from the global
// Serilog logger used by dependencies like CUE4Parse. Configured by RuntimeLogging and called
// throughout UnrealAssetScout so dependency logs can be suppressed without hiding our own output.
internal static class AppLog
{
    private static ILogger _logger = new LoggerConfiguration().CreateLogger();

    internal static ILogger Logger => _logger;

    internal static void SetLogger(ILogger logger) => _logger = logger;

    internal static ILogger ForContext(string propertyName, object? value, bool destructureObjects = false) =>
        _logger.ForContext(propertyName, value, destructureObjects);

    internal static void Information(string messageTemplate, params object?[]? propertyValues) =>
        _logger.Information(messageTemplate, propertyValues ?? []);

    internal static void Warning(string messageTemplate, params object?[]? propertyValues) =>
        _logger.Warning(messageTemplate, propertyValues ?? []);

    internal static void Warning(Exception exception, string messageTemplate, params object?[]? propertyValues) =>
        _logger.Warning(exception, messageTemplate, propertyValues ?? []);

    internal static void Error(string messageTemplate, params object?[]? propertyValues) =>
        _logger.Error(messageTemplate, propertyValues ?? []);

    internal static void Error(Exception exception, string messageTemplate, params object?[]? propertyValues) =>
        _logger.Error(exception, messageTemplate, propertyValues ?? []);
}
