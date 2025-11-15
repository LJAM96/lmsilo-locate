using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace GeoLens.Services
{
    /// <summary>
    /// Centralized logging service using Serilog for production-quality structured logging
    /// </summary>
    public static class LoggingService
    {
        /// <summary>
        /// Initialize Serilog with console, debug, and file sinks
        /// </summary>
        public static void Initialize()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GeoLens",
                "Logs",
                "geolens-.log"
            );

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.WithProperty("Application", "GeoLens")
                .Enrich.WithProperty("Version", "2.4.0")
                .WriteTo.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("GeoLens logging initialized");
        }

        /// <summary>
        /// Shutdown Serilog and flush any pending log entries
        /// </summary>
        public static void Shutdown()
        {
            Log.Information("GeoLens shutting down");
            Log.CloseAndFlush();
        }
    }
}
