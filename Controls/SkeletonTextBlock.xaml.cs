using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GeoLens.Controls
{
    public sealed partial class SkeletonTextBlock : UserControl
    {
        public static readonly DependencyProperty TextHeightProperty =
            DependencyProperty.Register(
                nameof(TextHeight),
                typeof(double),
                typeof(SkeletonTextBlock),
                new PropertyMetadata(14.0));

        public static readonly DependencyProperty TextWidthProperty =
            DependencyProperty.Register(
                nameof(TextWidth),
                typeof(double),
                typeof(SkeletonTextBlock),
                new PropertyMetadata(100.0));

        public double TextHeight
        {
            get => (double)GetValue(TextHeightProperty);
            set => SetValue(TextHeightProperty, value);
        }

        public double TextWidth
        {
            get => (double)GetValue(TextWidthProperty);
            set => SetValue(TextWidthProperty, value);
        }

        public SkeletonTextBlock()
        {
            this.InitializeComponent();
            this.Loaded += SkeletonTextBlock_Loaded;
            this.Unloaded += SkeletonTextBlock_Unloaded;
        }

        private void SkeletonTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            // Start shimmer animation
            ShimmerStoryboard?.Begin();
        }

        private void SkeletonTextBlock_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop animation to free resources
            ShimmerStoryboard?.Stop();
        }
    }
}
