using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace UnrealAssetScout.Logging;

// A Serilog ITextFormatter that switches between normal UnrealAssetScout log formatting, plain list
// output formatting, and prefixed external-dependency formatting based on event properties.
// Used by RuntimeLogging.ReConfigureLogger so file logs keep raw list output unprefixed while
// enabled dependency logs are clearly marked as external.
internal sealed class PlainAwareFormatter(string plainOutputProperty, string fileProgressProperty, string externalProperty) : ITextFormatter
{
    private readonly MessageTemplateTextFormatter _defaultFormatter =
        new($"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] {{{fileProgressProperty}}}{{Message:lj}}{{NewLine}}{{Exception}}",
            CultureInfo.InvariantCulture);
    private readonly MessageTemplateTextFormatter _externalFormatter =
        new($"{{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{Level:u3}}] [External] {{{fileProgressProperty}}}{{Message:lj}}{{NewLine}}{{Exception}}",
            CultureInfo.InvariantCulture);
    private readonly MessageTemplateTextFormatter _plainFormatter =
        new($"{{{fileProgressProperty}}}{{Message:lj}}{{NewLine}}{{Exception}}", CultureInfo.InvariantCulture);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        var formatter = logEvent.Properties.ContainsKey(plainOutputProperty)
            ? _plainFormatter
            : logEvent.Properties.ContainsKey(externalProperty)
                ? _externalFormatter
                : _defaultFormatter;
        formatter.Format(logEvent, output);
    }
}
