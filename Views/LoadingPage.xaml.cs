using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Views
{
    public sealed partial class LoadingPage : Page
    {
        private readonly Random _random = new();
        private readonly List<string> _loadingTips = new()
        {
            "GeoLens uses GeoCLIP AI to predict locations from photos without GPS data.",
            "The AI model was trained on millions of geotagged images worldwide.",
            "All processing happens locally on your device - no cloud required!",
            "GeoLens works offline once the models are downloaded.",
            "You can export predictions to CSV, PDF, or KML for Google Earth.",
            "Photos with GPS metadata get 'Very High' confidence instantly.",
            "The 3D globe view helps visualize prediction confidence geographically.",
            "Multiple predictions in the same region boost overall confidence.",
            "GeoLens automatically selects CPU, CUDA, or ROCm based on your hardware."
        };

        private CancellationTokenSource? _tipRotationCts;

        public event EventHandler? RetryRequested;
        public event EventHandler? ExitRequested;

        public LoadingPage()
        {
            InitializeComponent();
            ShowRandomTip();
            this.Unloaded += LoadingPage_Unloaded;
        }

        /// <summary>
        /// Update the main status message
        /// </summary>
        public void UpdateStatus(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = message;
            });
        }

        /// <summary>
        /// Update the sub-status detail message
        /// </summary>
        public void UpdateSubStatus(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SubStatusText.Text = message;
                SubStatusText.Visibility = string.IsNullOrEmpty(message)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            });
        }

        /// <summary>
        /// Update detailed progress (0-100)
        /// </summary>
        public void UpdateProgress(double value, double maximum = 100)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (value >= 0 && maximum > 0)
                {
                    DetailedProgress.IsIndeterminate = false;
                    DetailedProgress.Value = value;
                    DetailedProgress.Maximum = maximum;
                    DetailedProgress.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailedProgress.IsIndeterminate = true;
                    DetailedProgress.Visibility = Visibility.Collapsed;
                }
            });
        }

        /// <summary>
        /// Show error panel with message
        /// </summary>
        public void ShowError(string message, bool showRetry = true)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ErrorText.Text = message;
                ErrorPanel.Visibility = Visibility.Visible;
                RetryButton.Visibility = showRetry ? Visibility.Visible : Visibility.Collapsed;
                LoadingRing.IsActive = false;
                TipsPanel.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Hide error panel and resume loading
        /// </summary>
        public void HideError()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
                LoadingRing.IsActive = true;
                ShowRandomTip();
            });
        }

        /// <summary>
        /// Show a random loading tip
        /// </summary>
        private void ShowRandomTip()
        {
            if (_loadingTips.Any())
            {
                var tip = _loadingTips[_random.Next(_loadingTips.Count)];
                TipText.Text = tip;
                TipsPanel.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Animate tip rotation every 5 seconds
        /// </summary>
        public async Task RotateTipsAsync()
        {
            _tipRotationCts = new CancellationTokenSource();
            try
            {
                while (TipsPanel.Visibility == Visibility.Visible && !_tipRotationCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, _tipRotationCts.Token);
                    DispatcherQueue.TryEnqueue(ShowRandomTip);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when page is unloaded
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cancel tip rotation task
            _tipRotationCts?.Cancel();
            _tipRotationCts?.Dispose();

            // Unsubscribe events
            this.Unloaded -= LoadingPage_Unloaded;
        }
    }

    /// <summary>
    /// Initialization stages for progress tracking
    /// </summary>
    public enum InitializationStage
    {
        Starting,
        DetectingHardware,
        FindingRuntime,
        StartingPythonService,
        WaitingForService,
        InitializingApiClient,
        Complete,
        Failed
    }

    /// <summary>
    /// Progress data for initialization
    /// </summary>
    public class InitializationProgress
    {
        public InitializationStage Stage { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public double Percentage { get; set; }
    }
}
