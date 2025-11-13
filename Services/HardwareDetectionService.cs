using System;
using System.Linq;
using System.Management;

namespace GeoLens.Services
{
    /// <summary>
    /// Hardware type detected
    /// </summary>
    public enum HardwareType
    {
        Unknown,
        CpuOnly,
        NvidiaGpu,
        AmdGpu
    }

    /// <summary>
    /// Hardware detection result
    /// </summary>
    public class HardwareInfo
    {
        public HardwareType Type { get; set; }
        public string GpuName { get; set; } = string.Empty;
        public string DeviceChoice { get; set; } = "cpu";
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for detecting GPU hardware using WMI
    /// </summary>
    public class HardwareDetectionService
    {
        /// <summary>
        /// Detect available GPU hardware
        /// </summary>
        public HardwareInfo DetectHardware()
        {
            try
            {
                var gpuName = GetGpuName();

                if (string.IsNullOrEmpty(gpuName))
                {
                    return new HardwareInfo
                    {
                        Type = HardwareType.CpuOnly,
                        DeviceChoice = "cpu",
                        Description = "No discrete GPU detected - using CPU"
                    };
                }

                // Check for NVIDIA GPU
                if (gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                    gpuName.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                    gpuName.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                    gpuName.Contains("GTX", StringComparison.OrdinalIgnoreCase))
                {
                    return new HardwareInfo
                    {
                        Type = HardwareType.NvidiaGpu,
                        GpuName = gpuName,
                        DeviceChoice = "cuda",
                        Description = $"NVIDIA GPU detected: {gpuName}"
                    };
                }

                // Check for AMD GPU
                if (gpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    gpuName.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                    gpuName.Contains("RX ", StringComparison.OrdinalIgnoreCase))
                {
                    return new HardwareInfo
                    {
                        Type = HardwareType.AmdGpu,
                        GpuName = gpuName,
                        DeviceChoice = "rocm",
                        Description = $"AMD GPU detected: {gpuName}"
                    };
                }

                // Unknown GPU - default to CPU
                return new HardwareInfo
                {
                    Type = HardwareType.Unknown,
                    GpuName = gpuName,
                    DeviceChoice = "cpu",
                    Description = $"Unknown GPU detected: {gpuName} - defaulting to CPU"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hardware detection failed: {ex.Message}");
                return new HardwareInfo
                {
                    Type = HardwareType.CpuOnly,
                    DeviceChoice = "cpu",
                    Description = "Hardware detection failed - defaulting to CPU"
                };
            }
        }

        private string GetGpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                var gpus = searcher.Get()
                    .Cast<ManagementObject>()
                    .Select(gpu => gpu["Name"]?.ToString() ?? string.Empty)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                // Filter out virtual/generic display adapters
                var filteredGpus = gpus.Where(name =>
                    !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Remote", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Prioritize NVIDIA, AMD, then Intel Arc, then any other GPU
                var nvidiaGpu = filteredGpus.FirstOrDefault(name =>
                    name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("GTX", StringComparison.OrdinalIgnoreCase));

                if (nvidiaGpu != null) return nvidiaGpu;

                var amdGpu = filteredGpus.FirstOrDefault(name =>
                    name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("RX ", StringComparison.OrdinalIgnoreCase));

                if (amdGpu != null) return amdGpu;

                var intelArcGpu = filteredGpus.FirstOrDefault(name =>
                    name.Contains("Arc", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("Intel", StringComparison.OrdinalIgnoreCase));

                if (intelArcGpu != null) return intelArcGpu;

                // Return first non-Intel integrated GPU, or any GPU if nothing else
                return filteredGpus.FirstOrDefault(name =>
                    !name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    ?? filteredGpus.FirstOrDefault()
                    ?? gpus.FirstOrDefault()
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get hardware information as a formatted string for display
        /// </summary>
        public string GetHardwareInfoString()
        {
            var info = DetectHardware();
            return $"{info.Description} (Device: {info.DeviceChoice})";
        }
    }
}
