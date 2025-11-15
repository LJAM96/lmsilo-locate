using Serilog;
using System;
using System.IO;
using Xunit;

namespace GeoLens.IntegrationTests.TestFixtures
{
    /// <summary>
    /// Fixture for managing temporary test data directories.
    /// Creates isolated temp directories for each test run and cleans up afterward.
    /// </summary>
    public class TestDataFixture : IDisposable
    {
        private readonly string _tempRootDirectory;
        private bool _disposed;

        /// <summary>
        /// Temporary directory for cache databases
        /// </summary>
        public string CacheDirectory { get; }

        /// <summary>
        /// Temporary directory for audit log databases
        /// </summary>
        public string AuditLogDirectory { get; }

        /// <summary>
        /// Temporary directory for export outputs
        /// </summary>
        public string ExportDirectory { get; }

        /// <summary>
        /// Temporary directory for recent files tracking
        /// </summary>
        public string RecentFilesDirectory { get; }

        /// <summary>
        /// Root temporary directory (parent of all test directories)
        /// </summary>
        public string TempRootDirectory => _tempRootDirectory;

        public TestDataFixture()
        {
            // Create a unique temp directory for this test run
            _tempRootDirectory = Path.Combine(
                Path.GetTempPath(),
                "GeoLens.IntegrationTests",
                Guid.NewGuid().ToString("N"));

            // Create subdirectories
            CacheDirectory = Path.Combine(_tempRootDirectory, "Cache");
            AuditLogDirectory = Path.Combine(_tempRootDirectory, "AuditLogs");
            ExportDirectory = Path.Combine(_tempRootDirectory, "Exports");
            RecentFilesDirectory = Path.Combine(_tempRootDirectory, "RecentFiles");

            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(AuditLogDirectory);
            Directory.CreateDirectory(ExportDirectory);
            Directory.CreateDirectory(RecentFilesDirectory);

            Log.Information("Created test data directories in {TempRoot}", _tempRootDirectory);
        }

        /// <summary>
        /// Get a unique file path in the export directory
        /// </summary>
        public string GetExportFilePath(string extension)
        {
            return Path.Combine(ExportDirectory, $"test-export-{Guid.NewGuid():N}.{extension}");
        }

        /// <summary>
        /// Get the cache database path
        /// </summary>
        public string GetCacheDatabasePath()
        {
            return Path.Combine(CacheDirectory, "predictions.db");
        }

        /// <summary>
        /// Get the audit log database path
        /// </summary>
        public string GetAuditLogDatabasePath()
        {
            return Path.Combine(AuditLogDirectory, "audit.db");
        }

        /// <summary>
        /// Clean up temporary directories
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (Directory.Exists(_tempRootDirectory))
                {
                    Directory.Delete(_tempRootDirectory, recursive: true);
                    Log.Information("Cleaned up test data directories: {TempRoot}", _tempRootDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to clean up test data directory: {TempRoot}", _tempRootDirectory);
            }

            _disposed = true;
        }
    }
}
