using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeoLens.Controls
{
    public sealed partial class SkeletonPredictionCard : UserControl
    {
        public SkeletonPredictionCard()
        {
            this.InitializeComponent();
            this.Loaded += SkeletonPredictionCard_Loaded;
            this.Unloaded += SkeletonPredictionCard_Unloaded;
        }

        private void SkeletonPredictionCard_Loaded(object sender, RoutedEventArgs e)
        {
            // Start shimmer animation
            ShimmerStoryboard?.Begin();
        }

        private void SkeletonPredictionCard_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop animation to free resources
            ShimmerStoryboard?.Stop();
        }
    }
}
