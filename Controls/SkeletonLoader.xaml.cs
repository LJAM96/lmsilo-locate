using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeoLens.Controls
{
    public sealed partial class SkeletonLoader : UserControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(SkeletonLoader),
                new PropertyMetadata(new CornerRadius(4)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public SkeletonLoader()
        {
            this.InitializeComponent();
            this.Loaded += SkeletonLoader_Loaded;
            this.Unloaded += SkeletonLoader_Unloaded;
        }

        private void SkeletonLoader_Loaded(object sender, RoutedEventArgs e)
        {
            // Start shimmer animation
            ShimmerStoryboard?.Begin();
        }

        private void SkeletonLoader_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop animation to free resources
            ShimmerStoryboard?.Stop();
        }
    }
}
