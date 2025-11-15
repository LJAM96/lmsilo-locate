using GeoLens.Services;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GeoLens.IntegrationTests.TestFixtures
{
    /// <summary>
    /// Fixture for managing Python service lifecycle across integration tests.
    /// Implements IAsyncLifetime to start/stop service once per test collection.
    /// </summary>
    public class PythonServiceFixture : IAsyncLifetime
    {
        private PythonRuntimeManager? _runtimeManager;
        private bool _serviceStartedSuccessfully;

        public PythonRuntimeManager RuntimeManager => _runtimeManager
            ?? throw new InvalidOperationException("PythonRuntimeManager not initialized");

        public bool IsServiceAvailable => _serviceStartedSuccessfully && RuntimeManager.IsRunning;

        public string BaseUrl => RuntimeManager.BaseUrl;

        public PythonServiceFixture()
        {
            // Configure Serilog for test logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("integration-tests.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("PythonServiceFixture created");
        }

        /// <summary>
        /// Called before any tests run - starts the Python service
        /// </summary>
        public async Task InitializeAsync()
        {
            Log.Information("Initializing Python service for integration tests...");

            try
            {
                // Use default Python executable (will find python from PATH)
                _runtimeManager = new PythonRuntimeManager("python");

                // Start service with auto device detection
                var progress = new Progress<int>(p =>
                    Log.Debug("Python service startup progress: {Progress}%", p));

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                _serviceStartedSuccessfully = await _runtimeManager.StartAsync(
                    device: "auto",
                    progress: progress,
                    cancellationToken: cts.Token);

                if (_serviceStartedSuccessfully)
                {
                    Log.Information("Python service started successfully on {BaseUrl}", BaseUrl);
                }
                else
                {
                    Log.Warning("Python service failed to start - some tests will be skipped");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Python service");
                _serviceStartedSuccessfully = false;
            }
        }

        /// <summary>
        /// Called after all tests complete - stops the Python service
        /// </summary>
        public async Task DisposeAsync()
        {
            Log.Information("Disposing Python service fixture...");

            if (_runtimeManager != null)
            {
                try
                {
                    await _runtimeManager.StopAsync();
                    _runtimeManager.Dispose();
                    Log.Information("Python service stopped successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing Python service");
                }
            }

            Log.CloseAndFlush();
        }

        /// <summary>
        /// Restart the Python service (for testing recovery scenarios)
        /// </summary>
        public async Task<bool> RestartServiceAsync()
        {
            Log.Information("Restarting Python service...");

            if (_runtimeManager != null)
            {
                await _runtimeManager.StopAsync();
                await Task.Delay(1000); // Wait for cleanup
            }

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _serviceStartedSuccessfully = await _runtimeManager!.StartAsync(
                device: "auto",
                cancellationToken: cts.Token);

            return _serviceStartedSuccessfully;
        }
    }

    /// <summary>
    /// Defines a test collection that shares the Python service fixture.
    /// All tests in this collection will use the same Python service instance.
    /// </summary>
    [CollectionDefinition("PythonService")]
    public class PythonServiceCollection : ICollectionFixture<PythonServiceFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
