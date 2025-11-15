using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeoLens.Controls
{
    public sealed partial class SkeletonImageCard : UserControl
    {
        public SkeletonImageCard()
        {
            this.InitializeComponent();
            this.Loaded += SkeletonImageCard_Loaded;
            this.Unloaded += SkeletonImageCard_Unloaded;
        }

        private void SkeletonImageCard_Loaded(object sender, RoutedEventArgs e)
        {
            // Start shimmer animation
            ShimmerStoryboard?.Begin();
        }

        private void SkeletonImageCard_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop animation to free resources
            ShimmerStoryboard?.Stop();
        }
    }
}
