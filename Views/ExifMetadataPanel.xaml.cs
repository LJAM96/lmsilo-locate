using GeoLens.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GeoLens.Views
{
    public sealed partial class ExifMetadataPanel : UserControl
    {
        public ExifMetadataPanel()
        {
            InitializeComponent();
        }

        public void LoadMetadata(ExifMetadata? metadata)
        {
            if (metadata == null)
            {
                ShowNoExifMessage();
                return;
            }

            // Hide "no data" message
            NoExifInfoBar.IsOpen = false;

            // GPS Section
            if (metadata.GpsData?.HasGps == true)
            {
                GpsExpander.Visibility = Visibility.Visible;
                LatitudeText.Text = FormatLatitude(metadata.GpsData.Latitude);
                LongitudeText.Text = FormatLongitude(metadata.GpsData.Longitude);

                if (metadata.GpsData.Altitude.HasValue)
                {
                    AltitudeLabel.Visibility = Visibility.Visible;
                    AltitudeText.Visibility = Visibility.Visible;
                    AltitudeText.Text = $"{metadata.GpsData.Altitude:F1} m";
                }
                else
                {
                    AltitudeLabel.Visibility = Visibility.Collapsed;
                    AltitudeText.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrEmpty(metadata.GpsData.LocationName))
                {
                    LocationLabel.Visibility = Visibility.Visible;
                    LocationNameText.Visibility = Visibility.Visible;
                    LocationNameText.Text = metadata.GpsData.LocationName;
                }
                else
                {
                    LocationLabel.Visibility = Visibility.Collapsed;
                    LocationNameText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                GpsExpander.Visibility = Visibility.Collapsed;
            }

            // Camera Info Section
            bool hasCamera = !string.IsNullOrEmpty(metadata.CameraMake) || !string.IsNullOrEmpty(metadata.CameraModel);
            bool hasLens = !string.IsNullOrEmpty(metadata.LensModel);

            if (hasCamera || hasLens)
            {
                CameraExpander.Visibility = Visibility.Visible;

                if (hasCamera)
                {
                    CameraLabel.Visibility = Visibility.Visible;
                    CameraText.Visibility = Visibility.Visible;
                    CameraText.Text = metadata.CameraInfo;
                }
                else
                {
                    CameraLabel.Visibility = Visibility.Collapsed;
                    CameraText.Visibility = Visibility.Collapsed;
                }

                if (hasLens)
                {
                    LensLabel.Visibility = Visibility.Visible;
                    LensText.Visibility = Visibility.Visible;
                    LensText.Text = metadata.LensModel;
                }
                else
                {
                    LensLabel.Visibility = Visibility.Collapsed;
                    LensText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                CameraExpander.Visibility = Visibility.Collapsed;
            }

            // Capture Settings Section
            bool hasIso = metadata.Iso.HasValue && metadata.Iso.Value > 0;
            bool hasAperture = metadata.FNumber.HasValue;
            bool hasShutter = metadata.ExposureTime.HasValue;
            bool hasFocalLength = metadata.FocalLength.HasValue;
            bool hasFlash = metadata.Flash.HasValue;
            bool hasWhiteBalance = !string.IsNullOrEmpty(metadata.WhiteBalance);
            bool hasExposureMode = !string.IsNullOrEmpty(metadata.ExposureMode);

            if (hasIso || hasAperture || hasShutter || hasFocalLength || hasFlash || hasWhiteBalance || hasExposureMode)
            {
                SettingsExpander.Visibility = Visibility.Visible;

                // ISO
                if (hasIso)
                {
                    IsoLabel.Visibility = Visibility.Visible;
                    IsoText.Visibility = Visibility.Visible;
                    IsoText.Text = $"ISO {metadata.Iso}";
                }
                else
                {
                    IsoLabel.Visibility = Visibility.Collapsed;
                    IsoText.Visibility = Visibility.Collapsed;
                }

                // Aperture
                if (hasAperture)
                {
                    ApertureLabel.Visibility = Visibility.Visible;
                    ApertureText.Visibility = Visibility.Visible;
                    ApertureText.Text = $"f/{metadata.FNumber:F1}";
                }
                else
                {
                    ApertureLabel.Visibility = Visibility.Collapsed;
                    ApertureText.Visibility = Visibility.Collapsed;
                }

                // Shutter Speed
                if (hasShutter)
                {
                    ShutterLabel.Visibility = Visibility.Visible;
                    ShutterText.Visibility = Visibility.Visible;
                    ShutterText.Text = metadata.ShutterSpeedFormatted;
                }
                else
                {
                    ShutterLabel.Visibility = Visibility.Collapsed;
                    ShutterText.Visibility = Visibility.Collapsed;
                }

                // Focal Length
                if (hasFocalLength)
                {
                    FocalLengthLabel.Visibility = Visibility.Visible;
                    FocalLengthText.Visibility = Visibility.Visible;
                    FocalLengthText.Text = $"{metadata.FocalLength:F0}mm";
                }
                else
                {
                    FocalLengthLabel.Visibility = Visibility.Collapsed;
                    FocalLengthText.Visibility = Visibility.Collapsed;
                }

                // Flash
                if (hasFlash)
                {
                    FlashLabel.Visibility = Visibility.Visible;
                    FlashText.Visibility = Visibility.Visible;
                    bool flashFired = (metadata.Flash.Value & 0x01) != 0;
                    FlashText.Text = flashFired ? "Fired" : "Not fired";
                }
                else
                {
                    FlashLabel.Visibility = Visibility.Collapsed;
                    FlashText.Visibility = Visibility.Collapsed;
                }

                // White Balance
                if (hasWhiteBalance)
                {
                    WhiteBalanceLabel.Visibility = Visibility.Visible;
                    WhiteBalanceText.Visibility = Visibility.Visible;
                    WhiteBalanceText.Text = metadata.WhiteBalance;
                }
                else
                {
                    WhiteBalanceLabel.Visibility = Visibility.Collapsed;
                    WhiteBalanceText.Visibility = Visibility.Collapsed;
                }

                // Exposure Mode
                if (hasExposureMode)
                {
                    ExposureModeLabel.Visibility = Visibility.Visible;
                    ExposureModeText.Visibility = Visibility.Visible;
                    ExposureModeText.Text = metadata.ExposureMode;
                }
                else
                {
                    ExposureModeLabel.Visibility = Visibility.Collapsed;
                    ExposureModeText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                SettingsExpander.Visibility = Visibility.Collapsed;
            }

            // Image Details Section (always show)
            DetailsExpander.Visibility = Visibility.Visible;

            // Dimensions (always available)
            DimensionsText.Text = $"{metadata.ResolutionFormatted} ({metadata.MegapixelsFormatted})";

            // File Size (always available)
            FileSizeText.Text = metadata.FileSizeFormatted;

            // Date Taken
            if (metadata.DateTaken.HasValue)
            {
                DateTakenLabel.Visibility = Visibility.Visible;
                DateTakenText.Visibility = Visibility.Visible;
                DateTakenText.Text = metadata.DateTaken.Value.ToString("g");
            }
            else
            {
                DateTakenLabel.Visibility = Visibility.Collapsed;
                DateTakenText.Visibility = Visibility.Collapsed;
            }

            // Orientation
            if (metadata.Orientation.HasValue)
            {
                OrientationLabel.Visibility = Visibility.Visible;
                OrientationText.Visibility = Visibility.Visible;
                OrientationText.Text = FormatOrientation(metadata.Orientation.Value);
            }
            else
            {
                OrientationLabel.Visibility = Visibility.Collapsed;
                OrientationText.Visibility = Visibility.Collapsed;
            }

            // Color Space
            if (!string.IsNullOrEmpty(metadata.ColorSpace))
            {
                ColorSpaceLabel.Visibility = Visibility.Visible;
                ColorSpaceText.Visibility = Visibility.Visible;
                ColorSpaceText.Text = metadata.ColorSpace;
            }
            else
            {
                ColorSpaceLabel.Visibility = Visibility.Collapsed;
                ColorSpaceText.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowNoExifMessage()
        {
            GpsExpander.Visibility = Visibility.Collapsed;
            CameraExpander.Visibility = Visibility.Collapsed;
            SettingsExpander.Visibility = Visibility.Collapsed;
            DetailsExpander.Visibility = Visibility.Collapsed;
            NoExifInfoBar.IsOpen = true;
        }

        private string FormatLatitude(double lat)
        {
            var dir = lat >= 0 ? "N" : "S";
            return $"{Math.Abs(lat):F6}° {dir}";
        }

        private string FormatLongitude(double lon)
        {
            var dir = lon >= 0 ? "E" : "W";
            return $"{Math.Abs(lon):F6}° {dir}";
        }

        private string FormatOrientation(int orientation)
        {
            return orientation switch
            {
                1 => "Normal",
                2 => "Flip horizontal",
                3 => "Rotate 180°",
                4 => "Flip vertical",
                5 => "Transpose",
                6 => "Rotate 90° CW",
                7 => "Transverse",
                8 => "Rotate 270° CW",
                _ => "Unknown"
            };
        }
    }
}
