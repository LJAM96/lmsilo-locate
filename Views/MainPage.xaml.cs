using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly ObservableCollection<string> _imageQueue = new();
        private readonly ObservableCollection<Prediction> _predictions = new()
        {
            new Prediction("Vienna, Austria", "48.2082° N", "16.3738° E", "Confidence: 74%", 80, 60),
            new Prediction("Kyoto, Japan", "35.0116° N", "135.7681° E", "Confidence: 68%", 140, 70),
            new Prediction("Cusco, Peru", "13.5320° S", "71.9675° W", "Confidence: 61%", 40, 130),
            new Prediction("Tasman Sea", "31.0000° S", "159.0000° E", "Confidence: 58%", 150, 150),
            new Prediction("Lofoten, Norway", "68.2070° N", "13.5641° E", "Confidence: 52%", 100, 20)
        };

        public MainPage()
        {
            this.InitializeComponent();
            QueueList.ItemsSource = _imageQueue;
            PredictionsList.ItemsSource = _predictions;
            GlobePins.ItemsSource = _predictions;
            UpdateStatus();
        }

        private void AddImages_Click(object sender, RoutedEventArgs e)
        {
            _imageQueue.Add($"Image_{_imageQueue.Count + 1}.jpg");
            UpdateStatus();
        }

        private void StartPredictions_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Prediction engine is not yet wired in. When available, it will analyze the queue and refresh these locations.";
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            App.ShowSettingsWindow();
        }

        private void UpdateStatus()
        {
            StatusLabel.Text = _imageQueue.Count == 0
                ? "No images queued."
                : $"Queued {_imageQueue.Count} image(s)";
        }

        private sealed class Prediction
        {
            public string LocationName { get; }
            public string Latitude { get; }
            public string Longitude { get; }
            public string ConfidenceLabel { get; }
            public double MapX { get; }
            public double MapY { get; }

            public Prediction(string locationName, string latitude, string longitude, string confidenceLabel, double mapX, double mapY)
            {
                LocationName = locationName;
                Latitude = latitude;
                Longitude = longitude;
                ConfidenceLabel = confidenceLabel;
                MapX = mapX;
                MapY = mapY;
            }

            public string Coordinates => $"{Latitude}, {Longitude}";
        }
    }
}
