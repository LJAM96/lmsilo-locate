namespace GeoLens.Models
{
    /// <summary>
    /// EXIF GPS data extracted from image metadata
    /// </summary>
    public class ExifGpsData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasGps { get; set; }
        public string? LocationName { get; set; }
        public double? Altitude { get; set; }

        public string LatitudeFormatted => $"{System.Math.Abs(Latitude):F6}° {(Latitude >= 0 ? "N" : "S")}";
        public string LongitudeFormatted => $"{System.Math.Abs(Longitude):F6}° {(Longitude >= 0 ? "E" : "W")}";
        public string Coordinates => $"{LatitudeFormatted}, {LongitudeFormatted}";
    }
}
