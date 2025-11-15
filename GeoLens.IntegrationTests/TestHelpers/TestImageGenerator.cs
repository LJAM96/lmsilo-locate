using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System;
using System.IO;
using System.Security.Cryptography;
using Serilog;

namespace GeoLens.IntegrationTests.TestHelpers
{
    /// <summary>
    /// Generates test images with known properties for integration testing
    /// </summary>
    public static class TestImageGenerator
    {
        /// <summary>
        /// Generate a simple colored rectangle image
        /// </summary>
        public static string GenerateColoredImage(string outputPath, int width = 800, int height = 600, string color = "red")
        {
            var imageColor = color.ToLower() switch
            {
                "red" => Color.Red,
                "green" => Color.Green,
                "blue" => Color.Blue,
                "yellow" => Color.Yellow,
                "purple" => Color.Purple,
                _ => Color.Gray
            };

            using var image = new Image<Rgba32>(width, height);
            image.Mutate(ctx => ctx.Fill(imageColor));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            image.SaveAsJpeg(outputPath);

            Log.Debug("Generated test image: {Path} ({Width}x{Height}, {Color})",
                outputPath, width, height, color);

            return outputPath;
        }

        /// <summary>
        /// Generate multiple test images with different colors
        /// </summary>
        public static string[] GenerateTestImageBatch(string outputDirectory, int count = 10)
        {
            var colors = new[] { "red", "green", "blue", "yellow", "purple" };
            var paths = new string[count];

            for (int i = 0; i < count; i++)
            {
                var color = colors[i % colors.Length];
                var path = Path.Combine(outputDirectory, $"test-image-{i:D3}-{color}.jpg");
                paths[i] = GenerateColoredImage(path, color: color);
            }

            Log.Information("Generated {Count} test images in {Directory}", count, outputDirectory);
            return paths;
        }

        /// <summary>
        /// Generate an image with text overlay (useful for identifying specific test images)
        /// </summary>
        public static string GenerateImageWithText(string outputPath, string text, int width = 800, int height = 600)
        {
            using var image = new Image<Rgba32>(width, height);

            // Fill with a gradient background
            image.Mutate(ctx =>
            {
                ctx.Fill(Color.DarkBlue);
                // Note: Text rendering requires SixLabors.Fonts package
                // For now, just create a solid color image
            });

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            image.SaveAsJpeg(outputPath);

            Log.Debug("Generated text image: {Path} with text '{Text}'", outputPath, text);
            return outputPath;
        }

        /// <summary>
        /// Calculate MD5 hash of an image file (for cache verification)
        /// </summary>
        public static string CalculateMD5Hash(string imagePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(imagePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Calculate XXHash64 fingerprint (matches the cache service implementation)
        /// </summary>
        public static ulong CalculateXXHash64(string imagePath)
        {
            using var stream = File.OpenRead(imagePath);
            var hash = new System.IO.Hashing.XxHash64();

            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.Append(buffer.AsSpan(0, bytesRead));
            }

            return BitConverter.ToUInt64(hash.GetHashAndReset());
        }

        /// <summary>
        /// Create a copy of an image with modified content (different hash)
        /// </summary>
        public static string CreateModifiedCopy(string sourcePath, string destinationPath)
        {
            using var image = Image.Load<Rgba32>(sourcePath);

            // Add a small modification (change one pixel to ensure different hash)
            image.Mutate(ctx =>
            {
                // Add a single white pixel in the corner
                ctx.Fill(Color.White, new Rectangle(0, 0, 1, 1));
            });

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            image.SaveAsJpeg(destinationPath);

            Log.Debug("Created modified copy: {Source} -> {Destination}", sourcePath, destinationPath);
            return destinationPath;
        }

        /// <summary>
        /// Generate a landscape-oriented image (typical for photos)
        /// </summary>
        public static string GenerateLandscapeImage(string outputPath)
        {
            return GenerateColoredImage(outputPath, width: 1920, height: 1080, color: "blue");
        }

        /// <summary>
        /// Generate a portrait-oriented image
        /// </summary>
        public static string GeneratePortraitImage(string outputPath)
        {
            return GenerateColoredImage(outputPath, width: 1080, height: 1920, color: "green");
        }

        /// <summary>
        /// Generate a square image
        /// </summary>
        public static string GenerateSquareImage(string outputPath)
        {
            return GenerateColoredImage(outputPath, width: 1024, height: 1024, color: "red");
        }
    }
}
