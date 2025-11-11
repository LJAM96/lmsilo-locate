# EXIF Metadata System

## Overview

This document details the comprehensive EXIF data extraction and display system for GeoLens, including GPS coordinates, camera information, capture settings, and timestamps.

---

## EXIF Data Categories

### 1. GPS Data (Priority)
- Latitude / Longitude
- Altitude
- GPS timestamp
- GPS accuracy (DOP)
- Direction/heading

### 2. Camera Information
- Camera make (e.g., "Canon")
- Camera model (e.g., "EOS R5")
- Lens make
- Lens model
- Serial number (optional)

### 3. Capture Settings
- Date/Time original
- ISO speed
- Exposure time (shutter speed)
- F-number (aperture)
- Focal length
- Flash fired
- White balance
- Exposure program

### 4. Image Details
- Image width
- Image height
- Resolution (DPI)
- Color space
- Orientation

### 5. Copyright & Attribution
- Artist/Photographer
- Copyright
- Software used

---

## Complete EXIF Extractor

```csharp
// Services/ExifMetadataExtractor.cs
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

public class ExifMetadataExtractor
{
    // GPS Property IDs
    private const int PropertyTagGpsLatitude = 0x0002;
    private const int PropertyTagGpsLatitudeRef = 0x0001;
    private const int PropertyTagGpsLongitude = 0x0004;
    private const int PropertyTagGpsLongitudeRef = 0x0003;
    private const int PropertyTagGpsAltitude = 0x0006;
    private const int PropertyTagGpsAltitudeRef = 0x0005;
    private const int PropertyTagGpsTimeStamp = 0x0007;
    private const int PropertyTagGpsDateStamp = 0x001D;

    // Camera Property IDs
    private const int PropertyTagEquipMake = 0x010F;
    private const int PropertyTagEquipModel = 0x0110;
    private const int PropertyTagLensMake = 0xA433;
    private const int PropertyTagLensModel = 0xA434;

    // Capture Settings Property IDs
    private const int PropertyTagDateTime = 0x0132;
    private const int PropertyTagDateTimeOriginal = 0x9003;
    private const int PropertyTagDateTimeDigitized = 0x9004;
    private const int PropertyTagISOSpeed = 0x8827;
    private const int PropertyTagExposureTime = 0x829A;
    private const int PropertyTagFNumber = 0x829D;
    private const int PropertyTagFocalLength = 0x920A;
    private const int PropertyTagFlash = 0x9209;
    private const int PropertyTagWhiteBalance = 0xA403;
    private const int PropertyTagExposureProgram = 0x8822;

    // Image Details Property IDs
    private const int PropertyTagImageWidth = 0x0100;
    private const int PropertyTagImageHeight = 0x0101;
    private const int PropertyTagOrientation = 0x0112;
    private const int PropertyTagXResolution = 0x011A;
    private const int PropertyTagYResolution = 0x011B;
    private const int PropertyTagColorSpace = 0xA001;

    // Copyright Property IDs
    private const int PropertyTagArtist = 0x013B;
    private const int PropertyTagCopyright = 0x8298;
    private const int PropertyTagSoftware = 0x0131;

    public ExifMetadata? ExtractMetadata(string imagePath)
    {
        try
        {
            using var image = Image.FromFile(imagePath);

            if (image.PropertyIdList.Length == 0)
                return null;

            var metadata = new ExifMetadata();

            // Extract GPS data
            metadata.GpsData = ExtractGpsData(image);

            // Extract camera info
            metadata.CameraInfo = ExtractCameraInfo(image);

            // Extract capture settings
            metadata.CaptureSettings = ExtractCaptureSettings(image);

            // Extract image details
            metadata.ImageDetails = ExtractImageDetails(image);

            // Extract copyright
            metadata.Copyright = ExtractCopyrightInfo(image);

            return metadata;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract EXIF: {ex.Message}");
            return null;
        }
    }

    private GpsData? ExtractGpsData(Image image)
    {
        if (!HasProperty(image, PropertyTagGpsLatitude) ||
            !HasProperty(image, PropertyTagGpsLongitude))
            return null;

        var latRef = GetPropertyString(image, PropertyTagGpsLatitudeRef);
        var lonRef = GetPropertyString(image, PropertyTagGpsLongitudeRef);

        var latitude = ParseGpsCoordinate(image, PropertyTagGpsLatitude, latRef);
        var longitude = ParseGpsCoordinate(image, PropertyTagGpsLongitude, lonRef);

        if (latitude == null || longitude == null)
            return null;

        var gpsData = new GpsData
        {
            Latitude = latitude.Value,
            Longitude = longitude.Value
        };

        // Optional: Altitude
        if (HasProperty(image, PropertyTagGpsAltitude))
        {
            var altitudeRef = GetPropertyByte(image, PropertyTagGpsAltitudeRef);
            var altitude = ParseRational(image, PropertyTagGpsAltitude);
            gpsData.Altitude = altitude * (altitudeRef == 1 ? -1 : 1);
        }

        // Optional: Timestamp
        if (HasProperty(image, PropertyTagGpsDateStamp) &&
            HasProperty(image, PropertyTagGpsTimeStamp))
        {
            var date = GetPropertyString(image, PropertyTagGpsDateStamp);
            var time = ParseGpsTime(image, PropertyTagGpsTimeStamp);
            gpsData.Timestamp = ParseGpsDateTime(date, time);
        }

        return gpsData;
    }

    private CameraInfo ExtractCameraInfo(Image image)
    {
        return new CameraInfo
        {
            Make = GetPropertyString(image, PropertyTagEquipMake)?.Trim(),
            Model = GetPropertyString(image, PropertyTagEquipModel)?.Trim(),
            LensMake = GetPropertyString(image, PropertyTagLensMake)?.Trim(),
            LensModel = GetPropertyString(image, PropertyTagLensModel)?.Trim(),
            Software = GetPropertyString(image, PropertyTagSoftware)?.Trim()
        };
    }

    private CaptureSettings ExtractCaptureSettings(Image image)
    {
        var settings = new CaptureSettings();

        // Date/Time
        var dateTimeOriginal = GetPropertyString(image, PropertyTagDateTimeOriginal);
        if (dateTimeOriginal != null)
        {
            settings.DateTimeOriginal = ParseExifDateTime(dateTimeOriginal);
        }

        // ISO
        if (HasProperty(image, PropertyTagISOSpeed))
        {
            settings.IsoSpeed = GetPropertyUInt16(image, PropertyTagISOSpeed);
        }

        // Exposure Time
        if (HasProperty(image, PropertyTagExposureTime))
        {
            var exposureTime = ParseRational(image, PropertyTagExposureTime);
            settings.ExposureTime = exposureTime;
            settings.ShutterSpeedDisplay = FormatShutterSpeed(exposureTime);
        }

        // F-Number
        if (HasProperty(image, PropertyTagFNumber))
        {
            var fNumber = ParseRational(image, PropertyTagFNumber);
            settings.FNumber = fNumber;
            settings.ApertureDisplay = $"f/{fNumber:F1}";
        }

        // Focal Length
        if (HasProperty(image, PropertyTagFocalLength))
        {
            var focalLength = ParseRational(image, PropertyTagFocalLength);
            settings.FocalLength = focalLength;
            settings.FocalLengthDisplay = $"{focalLength:F0}mm";
        }

        // Flash
        if (HasProperty(image, PropertyTagFlash))
        {
            var flash = GetPropertyUInt16(image, PropertyTagFlash);
            settings.FlashFired = (flash & 0x01) != 0;
        }

        // White Balance
        if (HasProperty(image, PropertyTagWhiteBalance))
        {
            var wb = GetPropertyUInt16(image, PropertyTagWhiteBalance);
            settings.WhiteBalance = wb switch
            {
                0 => "Auto",
                1 => "Manual",
                _ => "Unknown"
            };
        }

        // Exposure Program
        if (HasProperty(image, PropertyTagExposureProgram))
        {
            var program = GetPropertyUInt16(image, PropertyTagExposureProgram);
            settings.ExposureProgram = program switch
            {
                0 => "Not defined",
                1 => "Manual",
                2 => "Program AE",
                3 => "Aperture priority",
                4 => "Shutter priority",
                5 => "Creative program",
                6 => "Action program",
                7 => "Portrait mode",
                8 => "Landscape mode",
                _ => "Unknown"
            };
        }

        return settings;
    }

    private ImageDetails ExtractImageDetails(Image image)
    {
        var details = new ImageDetails
        {
            Width = image.Width,
            Height = image.Height
        };

        // Orientation
        if (HasProperty(image, PropertyTagOrientation))
        {
            var orientation = GetPropertyUInt16(image, PropertyTagOrientation);
            details.Orientation = orientation switch
            {
                1 => "Normal",
                2 => "Flip horizontal",
                3 => "Rotate 180",
                4 => "Flip vertical",
                5 => "Transpose",
                6 => "Rotate 90 CW",
                7 => "Transverse",
                8 => "Rotate 270 CW",
                _ => "Unknown"
            };
        }

        // Resolution
        if (HasProperty(image, PropertyTagXResolution))
        {
            details.XResolution = ParseRational(image, PropertyTagXResolution);
            details.YResolution = ParseRational(image, PropertyTagYResolution);
        }

        // Color Space
        if (HasProperty(image, PropertyTagColorSpace))
        {
            var colorSpace = GetPropertyUInt16(image, PropertyTagColorSpace);
            details.ColorSpace = colorSpace switch
            {
                1 => "sRGB",
                2 => "Adobe RGB",
                0xFFFF => "Uncalibrated",
                _ => "Unknown"
            };
        }

        return details;
    }

    private CopyrightInfo ExtractCopyrightInfo(Image image)
    {
        return new CopyrightInfo
        {
            Artist = GetPropertyString(image, PropertyTagArtist)?.Trim(),
            Copyright = GetPropertyString(image, PropertyTagCopyright)?.Trim()
        };
    }

    // Helper methods
    private bool HasProperty(Image image, int propertyId)
    {
        return image.PropertyIdList.Contains(propertyId);
    }

    private string? GetPropertyString(Image image, int propertyId)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            if (prop?.Value == null) return null;

            // Remove null terminators
            return Encoding.UTF8.GetString(prop.Value).TrimEnd('\0');
        }
        catch { return null; }
    }

    private ushort GetPropertyUInt16(Image image, int propertyId)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            return BitConverter.ToUInt16(prop.Value, 0);
        }
        catch { return 0; }
    }

    private byte GetPropertyByte(Image image, int propertyId)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            return prop.Value[0];
        }
        catch { return 0; }
    }

    private double ParseRational(Image image, int propertyId)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            uint numerator = BitConverter.ToUInt32(prop.Value, 0);
            uint denominator = BitConverter.ToUInt32(prop.Value, 4);
            return denominator == 0 ? 0 : (double)numerator / denominator;
        }
        catch { return 0; }
    }

    private double? ParseGpsCoordinate(Image image, int propertyId, string? reference)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            var values = prop.Value;

            var degrees = ToRational(values, 0);
            var minutes = ToRational(values, 8);
            var seconds = ToRational(values, 16);

            double coordinate = degrees + (minutes / 60.0) + (seconds / 3600.0);

            if (reference == "S" || reference == "W")
                coordinate = -coordinate;

            return coordinate;
        }
        catch { return null; }
    }

    private double ToRational(byte[] bytes, int offset)
    {
        uint numerator = BitConverter.ToUInt32(bytes, offset);
        uint denominator = BitConverter.ToUInt32(bytes, offset + 4);
        return denominator == 0 ? 0 : (double)numerator / denominator;
    }

    private string FormatShutterSpeed(double exposureTime)
    {
        if (exposureTime >= 1)
            return $"{exposureTime:F1}s";

        // Convert to fraction
        int denominator = (int)Math.Round(1.0 / exposureTime);
        return $"1/{denominator}s";
    }

    private DateTime? ParseExifDateTime(string exifDateTime)
    {
        try
        {
            // Format: "YYYY:MM:DD HH:MM:SS"
            var parts = exifDateTime.Split(' ');
            if (parts.Length != 2) return null;

            var dateParts = parts[0].Split(':');
            var timeParts = parts[1].Split(':');

            if (dateParts.Length != 3 || timeParts.Length != 3) return null;

            return new DateTime(
                int.Parse(dateParts[0]),
                int.Parse(dateParts[1]),
                int.Parse(dateParts[2]),
                int.Parse(timeParts[0]),
                int.Parse(timeParts[1]),
                int.Parse(timeParts[2])
            );
        }
        catch { return null; }
    }

    private string ParseGpsTime(Image image, int propertyId)
    {
        try
        {
            var prop = image.GetPropertyItem(propertyId);
            var hours = ToRational(prop.Value, 0);
            var minutes = ToRational(prop.Value, 8);
            var seconds = ToRational(prop.Value, 16);

            return $"{hours:F0}:{minutes:F0}:{seconds:F0}";
        }
        catch { return ""; }
    }

    private DateTime? ParseGpsDateTime(string date, string time)
    {
        try
        {
            // Date format: "YYYY:MM:DD"
            var dateParts = date.Split(':');
            var timeParts = time.Split(':');

            return new DateTime(
                int.Parse(dateParts[0]),
                int.Parse(dateParts[1]),
                int.Parse(dateParts[2]),
                int.Parse(timeParts[0]),
                int.Parse(timeParts[1]),
                (int)double.Parse(timeParts[2]),
                DateTimeKind.Utc
            );
        }
        catch { return null; }
    }
}

// Models
public record ExifMetadata
{
    public GpsData? GpsData { get; init; }
    public CameraInfo CameraInfo { get; init; } = new();
    public CaptureSettings CaptureSettings { get; init; } = new();
    public ImageDetails ImageDetails { get; init; } = new();
    public CopyrightInfo CopyrightInfo { get; init; } = new();

    public bool HasGps => GpsData != null;
    public bool HasCameraInfo => !string.IsNullOrEmpty(CameraInfo.Make) ||
                                  !string.IsNullOrEmpty(CameraInfo.Model);
    public bool HasCaptureSettings => CaptureSettings.DateTimeOriginal != null ||
                                       CaptureSettings.IsoSpeed > 0;
}

public record GpsData
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double? Altitude { get; init; }
    public DateTime? Timestamp { get; init; }
    public string LocationSummary { get; set; } = "";
}

public record CameraInfo
{
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? LensMake { get; init; }
    public string? LensModel { get; init; }
    public string? Software { get; init; }

    public string FullCameraName =>
        string.IsNullOrEmpty(Make) || string.IsNullOrEmpty(Model)
            ? "Unknown Camera"
            : $"{Make} {Model}";

    public string? FullLensName =>
        string.IsNullOrEmpty(LensModel)
            ? null
            : string.IsNullOrEmpty(LensMake)
                ? LensModel
                : $"{LensMake} {LensModel}";
}

public record CaptureSettings
{
    public DateTime? DateTimeOriginal { get; init; }
    public int IsoSpeed { get; init; }
    public double ExposureTime { get; init; }
    public string? ShutterSpeedDisplay { get; init; }
    public double FNumber { get; init; }
    public string? ApertureDisplay { get; init; }
    public double FocalLength { get; init; }
    public string? FocalLengthDisplay { get; init; }
    public bool FlashFired { get; init; }
    public string? WhiteBalance { get; init; }
    public string? ExposureProgram { get; init; }
}

public record ImageDetails
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Orientation { get; init; }
    public double XResolution { get; init; }
    public double YResolution { get; init; }
    public string? ColorSpace { get; init; }

    public string Resolution => $"{Width} × {Height}";
    public string Megapixels => $"{(Width * Height / 1_000_000.0):F1} MP";
    public string AspectRatio
    {
        get
        {
            int gcd = GCD(Width, Height);
            return $"{Width / gcd}:{Height / gcd}";
        }
    }

    private int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);
}

public record CopyrightInfo
{
    public string? Artist { get; init; }
    public string? Copyright { get; init; }
}
```

---

## UI Display Component

```xml
<!-- Views/ExifMetadataPanel.xaml -->
<UserControl
    x:Class="GeoLens.Views.ExifMetadataPanel"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="using:Microsoft.UI.Xaml.Controls">

    <Expander Header="Image Metadata"
             IsExpanded="True"
             HorizontalAlignment="Stretch"
             HorizontalContentAlignment="Stretch">

        <Expander.HeaderTemplate>
            <DataTemplate>
                <StackPanel Orientation="Horizontal" Spacing="12">
                    <FontIcon Glyph="&#xE946;"
                             Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                    <TextBlock Text="Image Metadata" FontWeight="SemiBold"/>
                </StackPanel>
            </DataTemplate>
        </Expander.HeaderTemplate>

        <StackPanel Spacing="16" Margin="0,12,0,0">

            <!-- GPS Section -->
            <StackPanel Spacing="8" Visibility="{x:Bind HasGps, Mode=OneWay}">
                <TextBlock Text="📍 GPS Location"
                          FontWeight="SemiBold"
                          Foreground="{ThemeResource SystemFillColorSuccessBrush}"/>

                <Grid ColumnSpacing="16" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>

                    <FontIcon Grid.Row="0" Grid.Column="0"
                             Glyph="&#xE81D;" FontSize="14"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="0" Grid.Column="1"
                              Text="{x:Bind Metadata.GpsData.LocationSummary, Mode=OneWay}"/>

                    <TextBlock Grid.Row="1" Grid.Column="0"
                              Text="Lat:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="1" Grid.Column="1"
                              Text="{x:Bind Metadata.GpsData.Latitude, Mode=OneWay}"
                              FontFamily="Consolas"/>

                    <TextBlock Grid.Row="2" Grid.Column="0"
                              Text="Lon:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="2" Grid.Column="1"
                              Text="{x:Bind Metadata.GpsData.Longitude, Mode=OneWay}"
                              FontFamily="Consolas"/>

                    <TextBlock Grid.Row="3" Grid.Column="0"
                              Text="Alt:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                              Visibility="{x:Bind HasAltitude, Mode=OneWay}"/>
                    <TextBlock Grid.Row="3" Grid.Column="1"
                              Text="{x:Bind AltitudeDisplay, Mode=OneWay}"
                              Visibility="{x:Bind HasAltitude, Mode=OneWay}"/>
                </Grid>
            </StackPanel>

            <!-- Camera Section -->
            <StackPanel Spacing="8" Visibility="{x:Bind HasCameraInfo, Mode=OneWay}">
                <TextBlock Text="📷 Camera" FontWeight="SemiBold"/>

                <Grid ColumnSpacing="16" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>

                    <FontIcon Grid.Row="0" Grid.Column="0"
                             Glyph="&#xE722;" FontSize="14"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="0" Grid.Column="1"
                              Text="{x:Bind Metadata.CameraInfo.FullCameraName, Mode=OneWay}"/>

                    <FontIcon Grid.Row="1" Grid.Column="0"
                             Glyph="&#xE91B;" FontSize="14"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                             Visibility="{x:Bind HasLens, Mode=OneWay}"/>
                    <TextBlock Grid.Row="1" Grid.Column="1"
                              Text="{x:Bind Metadata.CameraInfo.FullLensName, Mode=OneWay}"
                              Visibility="{x:Bind HasLens, Mode=OneWay}"/>

                    <FontIcon Grid.Row="2" Grid.Column="0"
                             Glyph="&#xE90F;" FontSize="14"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                             Visibility="{x:Bind HasSoftware, Mode=OneWay}"/>
                    <TextBlock Grid.Row="2" Grid.Column="1"
                              Text="{x:Bind Metadata.CameraInfo.Software, Mode=OneWay}"
                              Visibility="{x:Bind HasSoftware, Mode=OneWay}"/>
                </Grid>
            </StackPanel>

            <!-- Capture Settings Section -->
            <StackPanel Spacing="8" Visibility="{x:Bind HasCaptureSettings, Mode=OneWay}">
                <TextBlock Text="⚙️ Capture Settings" FontWeight="SemiBold"/>

                <Grid ColumnSpacing="16" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>

                    <!-- Date/Time -->
                    <FontIcon Grid.Row="0" Grid.Column="0"
                             Glyph="&#xE787;" FontSize="14"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="0" Grid.Column="1"
                              Text="{x:Bind DateTimeDisplay, Mode=OneWay}"/>

                    <!-- Exposure Triangle (ISO, Shutter, Aperture) -->
                    <StackPanel Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2"
                               Orientation="Horizontal" Spacing="16">

                        <!-- ISO -->
                        <Border Background="{ThemeResource SubtleFillColorSecondaryBrush}"
                               CornerRadius="4" Padding="8,4"
                               Visibility="{x:Bind HasIso, Mode=OneWay}">
                            <TextBlock>
                                <Run Text="ISO" FontWeight="SemiBold"/>
                                <Run Text="{x:Bind Metadata.CaptureSettings.IsoSpeed, Mode=OneWay}"/>
                            </TextBlock>
                        </Border>

                        <!-- Shutter Speed -->
                        <Border Background="{ThemeResource SubtleFillColorSecondaryBrush}"
                               CornerRadius="4" Padding="8,4"
                               Visibility="{x:Bind HasShutter, Mode=OneWay}">
                            <TextBlock Text="{x:Bind Metadata.CaptureSettings.ShutterSpeedDisplay, Mode=OneWay}"/>
                        </Border>

                        <!-- Aperture -->
                        <Border Background="{ThemeResource SubtleFillColorSecondaryBrush}"
                               CornerRadius="4" Padding="8,4"
                               Visibility="{x:Bind HasAperture, Mode=OneWay}">
                            <TextBlock Text="{x:Bind Metadata.CaptureSettings.ApertureDisplay, Mode=OneWay}"/>
                        </Border>

                        <!-- Focal Length -->
                        <Border Background="{ThemeResource SubtleFillColorSecondaryBrush}"
                               CornerRadius="4" Padding="8,4"
                               Visibility="{x:Bind HasFocalLength, Mode=OneWay}">
                            <TextBlock Text="{x:Bind Metadata.CaptureSettings.FocalLengthDisplay, Mode=OneWay}"/>
                        </Border>
                    </StackPanel>

                    <!-- Flash -->
                    <StackPanel Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
                               Orientation="Horizontal" Spacing="8"
                               Visibility="{x:Bind ShowFlash, Mode=OneWay}">
                        <FontIcon Glyph="&#xE793;" FontSize="14"
                                 Foreground="{ThemeResource SystemFillColorCautionBrush}"/>
                        <TextBlock Text="Flash Fired"
                                  Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    </StackPanel>
                </Grid>
            </StackPanel>

            <!-- Image Details Section -->
            <StackPanel Spacing="8">
                <TextBlock Text="🖼️ Image Details" FontWeight="SemiBold"/>

                <Grid ColumnSpacing="16" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0"
                              Text="Size:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="0" Grid.Column="1">
                        <Run Text="{x:Bind Metadata.ImageDetails.Resolution, Mode=OneWay}"/>
                        <Run Text="(" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                        <Run Text="{x:Bind Metadata.ImageDetails.Megapixels, Mode=OneWay}"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                        <Run Text=")" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    </TextBlock>

                    <TextBlock Grid.Row="1" Grid.Column="0"
                              Text="Ratio:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    <TextBlock Grid.Row="1" Grid.Column="1"
                              Text="{x:Bind Metadata.ImageDetails.AspectRatio, Mode=OneWay}"/>

                    <TextBlock Grid.Row="2" Grid.Column="0"
                              Text="Space:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                              Visibility="{x:Bind HasColorSpace, Mode=OneWay}"/>
                    <TextBlock Grid.Row="2" Grid.Column="1"
                              Text="{x:Bind Metadata.ImageDetails.ColorSpace, Mode=OneWay}"
                              Visibility="{x:Bind HasColorSpace, Mode=OneWay}"/>
                </Grid>
            </StackPanel>

            <!-- Copyright Section -->
            <StackPanel Spacing="8" Visibility="{x:Bind HasCopyright, Mode=OneWay}">
                <TextBlock Text="©️ Copyright" FontWeight="SemiBold"/>

                <Grid ColumnSpacing="16" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0"
                              Text="Artist:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                              Visibility="{x:Bind HasArtist, Mode=OneWay}"/>
                    <TextBlock Grid.Row="0" Grid.Column="1"
                              Text="{x:Bind Metadata.CopyrightInfo.Artist, Mode=OneWay}"
                              Visibility="{x:Bind HasArtist, Mode=OneWay}"/>

                    <TextBlock Grid.Row="1" Grid.Column="0"
                              Text="©:"
                              Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                              Visibility="{x:Bind HasCopyrightText, Mode=OneWay}"/>
                    <TextBlock Grid.Row="1" Grid.Column="1"
                              Text="{x:Bind Metadata.CopyrightInfo.Copyright, Mode=OneWay}"
                              TextWrapping="Wrap"
                              Visibility="{x:Bind HasCopyrightText, Mode=OneWay}"/>
                </Grid>
            </StackPanel>

        </StackPanel>
    </Expander>
</UserControl>
```

```csharp
// Views/ExifMetadataPanel.xaml.cs
public sealed partial class ExifMetadataPanel : UserControl, INotifyPropertyChanged
{
    private ExifMetadata? _metadata;

    public ExifMetadata? Metadata
    {
        get => _metadata;
        set
        {
            _metadata = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGps));
            OnPropertyChanged(nameof(HasCameraInfo));
            OnPropertyChanged(nameof(HasCaptureSettings));
            // ... notify all computed properties
        }
    }

    public bool HasGps => Metadata?.HasGps ?? false;
    public bool HasCameraInfo => Metadata?.HasCameraInfo ?? false;
    public bool HasCaptureSettings => Metadata?.HasCaptureSettings ?? false;
    public bool HasAltitude => Metadata?.GpsData?.Altitude != null;
    public bool HasLens => !string.IsNullOrEmpty(Metadata?.CameraInfo.FullLensName);
    public bool HasSoftware => !string.IsNullOrEmpty(Metadata?.CameraInfo.Software);
    public bool HasIso => (Metadata?.CaptureSettings.IsoSpeed ?? 0) > 0;
    public bool HasShutter => !string.IsNullOrEmpty(Metadata?.CaptureSettings.ShutterSpeedDisplay);
    public bool HasAperture => !string.IsNullOrEmpty(Metadata?.CaptureSettings.ApertureDisplay);
    public bool HasFocalLength => !string.IsNullOrEmpty(Metadata?.CaptureSettings.FocalLengthDisplay);
    public bool ShowFlash => Metadata?.CaptureSettings.FlashFired ?? false;
    public bool HasColorSpace => !string.IsNullOrEmpty(Metadata?.ImageDetails.ColorSpace);
    public bool HasCopyright => HasArtist || HasCopyrightText;
    public bool HasArtist => !string.IsNullOrEmpty(Metadata?.CopyrightInfo.Artist);
    public bool HasCopyrightText => !string.IsNullOrEmpty(Metadata?.CopyrightInfo.Copyright);

    public string DateTimeDisplay =>
        Metadata?.CaptureSettings.DateTimeOriginal?.ToString("g") ?? "Unknown";

    public string AltitudeDisplay =>
        Metadata?.GpsData?.Altitude != null
            ? $"{Metadata.GpsData.Altitude:F0}m"
            : "";

    public ExifMetadataPanel()
    {
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

---

## Integration in Right Panel

```xml
<!-- Add EXIF panel above predictions -->
<StackPanel Spacing="16" Padding="16">

    <!-- EXIF Metadata -->
    <local:ExifMetadataPanel x:Name="ExifPanel"
                            Metadata="{x:Bind SelectedImage.ExifMetadata, Mode=OneWay}"
                            Visibility="{x:Bind HasSelectedImage, Mode=OneWay}"/>

    <!-- ... rest of panels ... -->
</StackPanel>
```

---

This completes the EXIF metadata system. Now let me create the final implementation order document.
