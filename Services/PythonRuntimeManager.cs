using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Manages the Python FastAPI service lifecycle
    /// </summary>
    public class PythonRuntimeManager : IDisposable
    {
        private Process? _pythonProcess;
        private readonly string _pythonExecutable;
        private readonly string _apiServiceScript;
        private readonly int _port;
        private readonly HttpClient _healthCheckClient;
        private bool _isDisposed;

        public string BaseUrl => $"http://localhost:{_port}";
        public bool IsRunning => _pythonProcess != null && !_pythonProcess.HasExited;

        public PythonRuntimeManager(string pythonExecutable = "python", int? port = null)
        {
            _pythonExecutable = pythonExecutable;
            _port = port ?? ConfigurationService.Instance.Config.GeoLens.Api.Port;

            var healthCheckTimeout = ConfigurationService.Instance.Config.GeoLens.Api.HealthCheckTimeoutSeconds;
            _healthCheckClient = new HttpClient { Timeout = TimeSpan.FromSeconds(healthCheckTimeout) };

            // Determine api_service.py path
            // In development: search upward from bin directory to find project root
            // In production: Core will be in a known location relative to executable
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var searchDir = new DirectoryInfo(baseDir);

            // Search up to 6 levels for the Core directory
            for (int i = 0; i < 6 && searchDir != null; i++)
            {
                var corePath = Path.Combine(searchDir.FullName, "Core", "api_service.py");
                if (File.Exists(corePath))
                {
                    _apiServiceScript = corePath;
                    break;
                }
                searchDir = searchDir.Parent;
            }

            // Fallback: assume Core is at same level as bin (production)
            if (string.IsNullOrEmpty(_apiServiceScript))
            {
                _apiServiceScript = Path.Combine(baseDir, "..", "Core", "api_service.py");
                _apiServiceScript = Path.GetFullPath(_apiServiceScript);
            }
        }

        /// <summary>
        /// Start the Python FastAPI service
        /// </summary>
        public async Task<bool> StartAsync(
            string device = "auto",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                Log.Information("Python service is already running");
                progress?.Report(100);
                return true;
            }

            progress?.Report(0);

            // Check if service is already running on the port (external process)
            Log.Information("Checking if service is already running on {BaseUrl}", BaseUrl);
            if (await IsServiceAlreadyRunningAsync())
            {
                Log.Information("Service is already running externally - skipping startup");
                progress?.Report(100);
                return true;
            }

            // Verify Python executable exists
            if (!CanFindPython())
            {
                Log.Error("Python executable not found: {PythonExecutable}", _pythonExecutable);
                return false;
            }

            progress?.Report(10);

            // Verify api_service.py exists
            if (!File.Exists(_apiServiceScript))
            {
                Log.Error("API service script not found: {ApiServiceScript}", _apiServiceScript);
                return false;
            }

            progress?.Report(20);

            try
            {
                // Working directory must be the parent of Core directory for proper module import
                var coreDir = Path.GetDirectoryName(_apiServiceScript) ?? string.Empty;
                var projectRoot = Path.GetDirectoryName(coreDir) ?? string.Empty;

                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExecutable,
                    Arguments = $"-m uvicorn Core.api_service:app --host 127.0.0.1 --port {_port}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = projectRoot
                };

                // Set environment variable for device if needed
                if (!string.IsNullOrEmpty(device) && device != "auto")
                {
                    startInfo.EnvironmentVariables["GEOCLIP_DEVICE"] = device;
                }

                _pythonProcess = new Process { StartInfo = startInfo };

                // Handle output for debugging
                _pythonProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Debug("[Python] {Output}", e.Data);
                    }
                };

                _pythonProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Log.Warning("[Python Error] {Output}", e.Data);
                    }
                };

                progress?.Report(30);

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                Log.Information("Python service starting on {BaseUrl}", BaseUrl);

                progress?.Report(40);

                // Wait for service to be ready (health check)
                var startupTimeout = ConfigurationService.Instance.Config.GeoLens.Api.StartupTimeoutSeconds;
                return await WaitForHealthyAsync(TimeSpan.FromSeconds(startupTimeout), cancellationToken, progress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start Python service");
                return false;
            }
        }

        /// <summary>
        /// Wait for the service to become healthy
        /// </summary>
        private async Task<bool> WaitForHealthyAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken,
            IProgress<int>? progress = null)
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime + timeout;
            var retryDelay = TimeSpan.FromMilliseconds(500);
            var totalSeconds = timeout.TotalSeconds;

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                if (await CheckHealthAsync(cancellationToken))
                {
                    Log.Information("Python service is healthy");
                    progress?.Report(100);
                    return true;
                }

                // Report progress (map time elapsed to 40-100% range)
                var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                var percentage = 40 + (int)((elapsed / totalSeconds) * 60);
                progress?.Report(Math.Min(percentage, 99)); // Cap at 99 until actually healthy

                await Task.Delay(retryDelay, cancellationToken);
            }

            Log.Error("Python service health check timed out");
            return false;
        }

        /// <summary>
        /// Check if the service is healthy
        /// </summary>
        public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var healthEndpoint = ConfigurationService.Instance.Config.GeoLens.Api.HealthCheckEndpoint;
                var response = await _healthCheckClient.GetAsync($"{BaseUrl}{healthEndpoint}", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stop the Python service
        /// </summary>
        public void Stop()
        {
            if (_pythonProcess == null || _pythonProcess.HasExited)
                return;

            try
            {
                Log.Information("Stopping Python service...");
                _pythonProcess.Kill(entireProcessTree: true);
                _pythonProcess.WaitForExit(5000);
                Log.Information("Python service stopped");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping Python service");
            }
            finally
            {
                _pythonProcess?.Dispose();
                _pythonProcess = null;
            }
        }

        /// <summary>
        /// Check if the service is already running on the port (external process)
        /// </summary>
        private async Task<bool> IsServiceAlreadyRunningAsync()
        {
            try
            {
                var healthEndpoint = ConfigurationService.Instance.Config.GeoLens.Api.HealthCheckEndpoint;
                var response = await _healthCheckClient.GetAsync($"{BaseUrl}{healthEndpoint}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Log.Debug("External service health check response: {Response}", content);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Health check failed (service not running)");
            }
            return false;
        }

        private bool CanFindPython()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExecutable,
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                process.Start();
                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _healthCheckClient?.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
