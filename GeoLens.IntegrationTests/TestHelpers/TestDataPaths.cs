using System;
using System.IO;
using System.Linq;
using Serilog;

namespace GeoLens.IntegrationTests.TestHelpers
{
    /// <summary>
    /// Provides paths to test data files and manages test data directory
    /// </summary>
    public static class TestDataPaths
    {
        /// <summary>
        /// Get the TestData directory path (resolves from current assembly location)
        /// </summary>
        public static string TestDataDirectory
        {
            get
            {
                // Start from assembly location and search upward for TestData directory
                var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
                var current = new DirectoryInfo(assemblyLocation);

                while (current != null)
                {
                    var testDataPath = Path.Combine(current.FullName, "TestData");
                    if (Directory.Exists(testDataPath))
                    {
                        return testDataPath;
                    }

                    // Also check in GeoLens.IntegrationTests/TestData
                    var projectTestDataPath = Path.Combine(current.FullName, "GeoLens.IntegrationTests", "TestData");
                    if (Directory.Exists(projectTestDataPath))
                    {
                        return projectTestDataPath;
                    }

                    current = current.Parent;
                }

                // Fallback: create in temp directory
                var tempPath = Path.Combine(Path.GetTempPath(), "GeoLens.IntegrationTests", "TestData");
                Directory.CreateDirectory(tempPath);
                Log.Warning("TestData directory not found, using temp path: {Path}", tempPath);
                return tempPath;
            }
        }

        /// <summary>
        /// Get all test image files in the TestData directory
        /// </summary>
        public static string[] GetAllTestImages()
        {
            if (!Directory.Exists(TestDataDirectory))
            {
                Log.Warning("TestData directory does not exist: {Path}", TestDataDirectory);
                return Array.Empty<string>();
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            var files = Directory.GetFiles(TestDataDirectory)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();

            Log.Debug("Found {Count} test images in {Directory}", files.Length, TestDataDirectory);
            return files;
        }

        /// <summary>
        /// Get a specific test image by filename
        /// </summary>
        public static string GetTestImage(string filename)
        {
            var path = Path.Combine(TestDataDirectory, filename);

            if (!File.Exists(path))
            {
                Log.Warning("Test image not found: {Path}", path);
            }

            return path;
        }

        /// <summary>
        /// Ensure TestData directory exists with at least one test image
        /// </summary>
        public static void EnsureTestDataExists()
        {
            Directory.CreateDirectory(TestDataDirectory);

            var existingImages = GetAllTestImages();
            if (existingImages.Length == 0)
            {
                Log.Information("No test images found, generating default set...");

                // Generate 5 default test images
                TestImageGenerator.GenerateColoredImage(
                    Path.Combine(TestDataDirectory, "test-red.jpg"), color: "red");
                TestImageGenerator.GenerateColoredImage(
                    Path.Combine(TestDataDirectory, "test-green.jpg"), color: "green");
                TestImageGenerator.GenerateColoredImage(
                    Path.Combine(TestDataDirectory, "test-blue.jpg"), color: "blue");
                TestImageGenerator.GenerateLandscapeImage(
                    Path.Combine(TestDataDirectory, "test-landscape.jpg"));
                TestImageGenerator.GeneratePortraitImage(
                    Path.Combine(TestDataDirectory, "test-portrait.jpg"));

                Log.Information("Generated 5 default test images in {Directory}", TestDataDirectory);
            }
            else
            {
                Log.Debug("Found {Count} existing test images", existingImages.Length);
            }
        }

        /// <summary>
        /// Get the first available test image (or generate one if none exist)
        /// </summary>
        public static string GetFirstTestImage()
        {
            EnsureTestDataExists();
            var images = GetAllTestImages();
            return images.Length > 0
                ? images[0]
                : throw new InvalidOperationException("No test images available");
        }

        /// <summary>
        /// Get a batch of N test images (generates if needed)
        /// </summary>
        public static string[] GetTestImageBatch(int count)
        {
            EnsureTestDataExists();
            var existing = GetAllTestImages();

            if (existing.Length >= count)
            {
                return existing.Take(count).ToArray();
            }

            // Generate additional images if needed
            var needed = count - existing.Length;
            var tempDir = Path.Combine(TestDataDirectory, "generated");
            Directory.CreateDirectory(tempDir);

            var generated = TestImageGenerator.GenerateTestImageBatch(tempDir, needed);

            return existing.Concat(generated).Take(count).ToArray();
        }
    }
}
