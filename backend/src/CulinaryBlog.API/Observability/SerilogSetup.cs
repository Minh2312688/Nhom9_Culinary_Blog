using Serilog;
using Serilog.Events;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-002 Serilog configuration. Sets up the logging pipeline with JSON console output,
/// optional file sink (when Serilog:FilePath is configured), and optional Seq sink
/// (when Seq:ServerUrl is configured).
/// </summary>
public static class SerilogSetup
{
    /// <summary>
    /// Configures Serilog as the logging provider. Replaces the default console logger.
    /// Tests can inject an ILogEventSink via DI (ReadFrom.Services) to capture events.
    /// </summary>
    public static WebApplicationBuilder AddObservabilitySerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, lc) =>
        {
            var minimumLevelText = ctx.Configuration["Serilog:MinimumLevel"];
            var minimumLevel = Enum.TryParse<LogEventLevel>(minimumLevelText, ignoreCase: true, out var parsed)
                ? parsed
                : LogEventLevel.Information;

            lc.ReadFrom.Services(services)
              .Enrich.FromLogContext()
              .MinimumLevel.Is(minimumLevel)
              .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
              .MinimumLevel.Override("System", LogEventLevel.Warning);

            // Console sink: always enabled, JSON format
            lc.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());

            // File sink: only when Serilog:FilePath is configured
            var filePath = ctx.Configuration["Serilog:FilePath"];
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    // Probe write-ability with a unique, non-overwriting file so an
                    // existing file with the same name can never be clobbered.
                    // A relative path without a directory resolves against the
                    // current directory, which Directory.CreateDirectory handles
                    // as string.Empty => skip creation.
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var probePath = Path.Combine(
                        string.IsNullOrEmpty(directory) ? "." : directory,
                        $".serilog-probe-{Guid.NewGuid():N}.tmp");
                    var ownsProbe = false;
                    try
                    {
                        // FileMode.CreateNew throws if the file already exists.
                        // Only the run that successfully creates the file owns it
                        // and is allowed to delete it.
                        using (var probe = new FileStream(
                            probePath, FileMode.CreateNew, FileAccess.Write))
                        {
                            probe.Write([0x7B]);
                        }

                        ownsProbe = true;
                    }
                    finally
                    {
                        // Delete only if this run created the file.
                        if (ownsProbe)
                        {
                            try { File.Delete(probePath); }
                            catch (IOException) { /* best effort cleanup */ }
                        }
                    }

                    lc.WriteTo.File(
                        new Serilog.Formatting.Json.JsonFormatter(),
                        filePath,
                        rollingInterval: Serilog.RollingInterval.Day);
                }
                catch (Exception ex)
                {
                    // Skip File sink on error, do not crash the host
                    Console.Error.WriteLine($"[Serilog] Failed to configure File sink: {ex.Message}");
                }
            }

            // Seq sink: only when Seq:ServerUrl is configured
            var seqServerUrl = ctx.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrEmpty(seqServerUrl))
            {
                var seqApiKey = ctx.Configuration["Seq:ApiKey"];
                if (string.IsNullOrEmpty(seqApiKey))
                {
                    lc.WriteTo.Seq(seqServerUrl);
                }
                else
                {
                    lc.WriteTo.Seq(seqServerUrl, apiKey: seqApiKey);
                }
            }
        });

        return builder;
    }
}
