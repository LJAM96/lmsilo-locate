using GeoLens.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace GeoLens.Services
{
    /// <summary>
    /// Extracts EXIF metadata from images including GPS coordinates and camera information
    /// </summary>
    public class ExifMetadataExtractor
    {
        /// <summary>
        /// Extract GPS coordinates from an image file
        /// </summary>
        /// <param name="imagePath">Full path to the image file</param>
        /// <returns>ExifGpsData with GPS information, or null if extraction fails</returns>
        public async Task<ExifGpsData?> ExtractGpsDataAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    Debug.WriteLine($"[ExifExtractor] File not found: {imagePath}");
                    return null;
                }

                // Load the file as StorageFile
                var file = await StorageFile.GetFileFromPathAsync(imagePath);

                // Open the image stream
                using var stream = await file.OpenAsync(FileAccessMode.Read);

                // Create decoder
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // Get properties
                var properties = decoder.BitmapProperties;

                // Try to get GPS data
                var gpsData = await TryGetGpsDataAsync(properties);

                if (gpsData != null && gpsData.HasGps)
                {
                    Debug.WriteLine($"[ExifExtractor] GPS found: {gpsData.Latitude:F6}, {gpsData.Longitude:F6}");

                    // Try to get altitude
                    gpsData.Altitude = await TryGetAltitudeAsync(properties);
                }
                else
                {
                    Debug.WriteLine($"[ExifExtractor] No GPS data found in: {Path.GetFileName(imagePath)}");
                }

                return gpsData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExifExtractor] Error extracting GPS: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract extended EXIF metadata including camera info and capture settings
        /// </summary>
        public async Task<ExifMetadata?> ExtractExtendedMetadataAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    return null;
                }

                var file = await StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var properties = decoder.BitmapProperties;

                var metadata = new ExifMetadata
                {
                    GpsData = await TryGetGpsDataAsync(properties)
                };

                // Camera make and model
                metadata.CameraMake = await TryGetPropertyValueAsync(properties, "/app1/ifd/exif/{ushort=271}") // Make
                                   ?? await TryGetPropertyValueAsync(properties, "/app1/ifd/{ushort=271}");

                metadata.CameraModel = await TryGetPropertyValueAsync(properties, "/app1/ifd/exif/{ushort=272}") // Model
                                    ?? await TryGetPropertyValueAsync(properties, "/app1/ifd/{ushort=272}");

                // Lens information
                metadata.LensModel = await TryGetPropertyValueAsync(properties, "/app1/ifd/exif/{ushort=42036}"); // LensModel

                // Capture settings
                metadata.Iso = await TryGetUshortPropertyAsync(properties, "/app1/ifd/exif/{ushort=34855}"); // ISOSpeedRatings
                metadata.FNumber = await TryGetRationalPropertyAsync(properties, "/app1/ifd/exif/{ushort=33437}"); // FNumber
                metadata.ExposureTime = await TryGetRationalPropertyAsync(properties, "/app1/ifd/exif/{ushort=33434}"); // ExposureTime
                metadata.FocalLength = await TryGetRationalPropertyAsync(properties, "/app1/ifd/exif/{ushort=37386}"); // FocalLength

                // Timestamp
                var dateTimeStr = await TryGetPropertyValueAsync(properties, "/app1/ifd/exif/{ushort=36867}") // DateTimeOriginal
                               ?? await TryGetPropertyValueAsync(properties, "/app1/ifd/exif/{ushort=36868}"); // DateTimeDigitized

                if (!string.IsNullOrEmpty(dateTimeStr))
                {
                    if (DateTime.TryParseExact(dateTimeStr, "yyyy:MM:dd HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime dt))
                    {
                        metadata.DateTaken = dt;
                    }
                }

                // Image dimensions
                metadata.Width = decoder.PixelWidth;
                metadata.Height = decoder.PixelHeight;

                return metadata;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExifExtractor] Error extracting extended metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to extract GPS data from EXIF properties
        /// </summary>
        private async Task<ExifGpsData?> TryGetGpsDataAsync(BitmapPropertiesView properties)
        {
            try
            {
                // GPS property paths
                const string gpsLatPath = "/app1/ifd/gps/{ushort=2}"; // GPSLatitude
                const string gpsLatRefPath = "/app1/ifd/gps/{ushort=1}"; // GPSLatitudeRef
                const string gpsLonPath = "/app1/ifd/gps/{ushort=4}"; // GPSLongitude
                const string gpsLonRefPath = "/app1/ifd/gps/{ushort=3}"; // GPSLongitudeRef

                // Get latitude
                var latitudeData = await properties.GetPropertiesAsync(new[] { gpsLatPath });
                var latitudeRefData = await properties.GetPropertiesAsync(new[] { gpsLatRefPath });

                if (!latitudeData.ContainsKey(gpsLatPath) || latitudeData[gpsLatPath].Value == null)
                {
                    return new ExifGpsData { HasGps = false };
                }

                // Get longitude
                var longitudeData = await properties.GetPropertiesAsync(new[] { gpsLonPath });
                var longitudeRefData = await properties.GetPropertiesAsync(new[] { gpsLonRefPath });

                if (!longitudeData.ContainsKey(gpsLonPath) || longitudeData[gpsLonPath].Value == null)
                {
                    return new ExifGpsData { HasGps = false };
                }

                // Parse latitude
                var latArray = (BitmapTypedValue[])latitudeData[gpsLatPath].Value;
                var latRef = latitudeRefData.ContainsKey(gpsLatRefPath) ?
                    latitudeRefData[gpsLatRefPath].Value?.ToString() : "N";
                double latitude = ConvertGpsCoordinate(latArray, latRef ?? "N");

                // Parse longitude
                var lonArray = (BitmapTypedValue[])longitudeData[gpsLonPath].Value;
                var lonRef = longitudeRefData.ContainsKey(gpsLonRefPath) ?
                    longitudeRefData[gpsLonRefPath].Value?.ToString() : "E";
                double longitude = ConvertGpsCoordinate(lonArray, lonRef ?? "E");

                return new ExifGpsData
                {
                    HasGps = true,
                    Latitude = latitude,
                    Longitude = longitude
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExifExtractor] Error parsing GPS data: {ex.Message}");
                return new ExifGpsData { HasGps = false };
            }
        }

        /// <summary>
        /// Try to get altitude from GPS data
        /// </summary>
        private async Task<double?> TryGetAltitudeAsync(BitmapPropertiesView properties)
        {
            try
            {
                const string gpsAltPath = "/app1/ifd/gps/{ushort=6}"; // GPSAltitude
                const string gpsAltRefPath = "/app1/ifd/gps/{ushort=5}"; // GPSAltitudeRef

                var altitudeData = await properties.GetPropertiesAsync(new[] { gpsAltPath });
                if (!altitudeData.ContainsKey(gpsAltPath) || altitudeData[gpsAltPath].Value == null)
                {
                    return null;
                }

                var altRefData = await properties.GetPropertiesAsync(new[] { gpsAltRefPath });
                var altRef = altRefData.ContainsKey(gpsAltRefPath) ?
                    (byte)(altRefData[gpsAltRefPath].Value ?? 0) : (byte)0;

                // Altitude is stored as a rational
                var altValue = (BitmapTypedValue)altitudeData[gpsAltPath].Value;
                double altitude = ConvertRational(altValue);

                // AltitudeRef: 0 = above sea level, 1 = below sea level
                if (altRef == 1)
                {
                    altitude = -altitude;
                }

                return altitude;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert GPS coordinate from degrees, minutes, seconds to decimal degrees
        /// </summary>
        private double ConvertGpsCoordinate(BitmapTypedValue[] coordinate, string reference)
        {
            if (coordinate == null || coordinate.Length < 3)
            {
                return 0;
            }

            double degrees = ConvertRational(coordinate[0]);
            double minutes = ConvertRational(coordinate[1]);
            double seconds = ConvertRational(coordinate[2]);

            double result = degrees + (minutes / 60.0) + (seconds / 3600.0);

            // Apply reference (N/S for latitude, E/W for longitude)
            if (reference == "S" || reference == "W")
            {
                result = -result;
            }

            return result;
        }

        /// <summary>
        /// Convert rational value (numerator/denominator) to double
        /// </summary>
        private double ConvertRational(BitmapTypedValue value)
        {
            try
            {
                if (value.Type == PropertyType.UInt32Array)
                {
                    var array = (uint[])value.Value;
                    if (array.Length >= 2 && array[1] != 0)
                    {
                        return (double)array[0] / array[1];
                    }
                }
                else if (value.Type == PropertyType.Int32Array)
                {
                    var array = (int[])value.Value;
                    if (array.Length >= 2 && array[1] != 0)
                    {
                        return (double)array[0] / array[1];
                    }
                }
            }
            catch
            {
                // Ignore conversion errors
            }

            return 0;
        }

        /// <summary>
        /// Try to get a string property value
        /// </summary>
        private async Task<string?> TryGetPropertyValueAsync(BitmapPropertiesView properties, string propertyPath)
        {
            try
            {
                var data = await properties.GetPropertiesAsync(new[] { propertyPath });
                if (data.ContainsKey(propertyPath) && data[propertyPath].Value != null)
                {
                    return data[propertyPath].Value.ToString();
                }
            }
            catch
            {
                // Property not found or error reading
            }
            return null;
        }

        /// <summary>
        /// Try to get a ushort property value
        /// </summary>
        private async Task<ushort?> TryGetUshortPropertyAsync(BitmapPropertiesView properties, string propertyPath)
        {
            try
            {
                var data = await properties.GetPropertiesAsync(new[] { propertyPath });
                if (data.ContainsKey(propertyPath) && data[propertyPath].Value != null)
                {
                    if (data[propertyPath].Value is ushort value)
                    {
                        return value;
                    }
                    else if (ushort.TryParse(data[propertyPath].Value.ToString(), out ushort parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                // Property not found or error reading
            }
            return null;
        }

        /// <summary>
        /// Try to get a rational property value as double
        /// </summary>
        private async Task<double?> TryGetRationalPropertyAsync(BitmapPropertiesView properties, string propertyPath)
        {
            try
            {
                var data = await properties.GetPropertiesAsync(new[] { propertyPath });
                if (data.ContainsKey(propertyPath) && data[propertyPath].Value is BitmapTypedValue value)
                {
                    return ConvertRational(value);
                }
            }
            catch
            {
                // Property not found or error reading
            }
            return null;
        }
    }

    /// <summary>
    /// Extended EXIF metadata including camera info and capture settings
    /// </summary>
    public class ExifMetadata
    {
        public ExifGpsData? GpsData { get; set; }
        public string? CameraMake { get; set; }
        public string? CameraModel { get; set; }
        public string? LensModel { get; set; }
        public ushort? Iso { get; set; }
        public double? FNumber { get; set; }
        public double? ExposureTime { get; set; }
        public double? FocalLength { get; set; }
        public DateTime? DateTaken { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }

        public string CameraInfo
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(CameraMake)) parts.Add(CameraMake);
                if (!string.IsNullOrEmpty(CameraModel)) parts.Add(CameraModel);
                return parts.Count > 0 ? string.Join(" ", parts) : "Unknown Camera";
            }
        }

        public string CaptureSettings
        {
            get
            {
                var parts = new List<string>();
                if (Iso.HasValue) parts.Add($"ISO {Iso}");
                if (FNumber.HasValue) parts.Add($"f/{FNumber:F1}");
                if (ExposureTime.HasValue)
                {
                    if (ExposureTime.Value < 1)
                        parts.Add($"1/{(int)(1 / ExposureTime.Value)}s");
                    else
                        parts.Add($"{ExposureTime:F1}s");
                }
                if (FocalLength.HasValue) parts.Add($"{FocalLength:F0}mm");
                return parts.Count > 0 ? string.Join(" • ", parts) : "No capture data";
            }
        }
    }
}
