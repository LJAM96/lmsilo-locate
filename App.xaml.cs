using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GeoLens
{
    public partial class App : Application
    {
        private Window? _mainWindow;
        private static Window? _settingsWindow;
        public static Window? MainWindow { get; private set; }
        public new static App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _mainWindow ??= new Window
            {
                Title = "GeoLens"
            };

            var frame = _mainWindow.Content as Frame ?? new Frame();
            frame.RequestedTheme = ElementTheme.Dark;
            frame.NavigationFailed += OnNavigationFailed;
            _mainWindow.Content = frame;
            frame.Navigate(typeof(Views.MainPage), args.Arguments);
            MainWindow = _mainWindow;
            _mainWindow.Activate();
        }

        public static void ShowSettingsWindow()
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            var frame = new Frame
            {
                RequestedTheme = ElementTheme.Dark
            };
            frame.NavigationFailed += (s, e) => Current.OnNavigationFailed(s, e);
            frame.Navigate(typeof(Views.SettingsPage));

            var settingsWindow = new Window
            {
                Title = "GeoLens Settings",
                Content = frame
            };

            settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow = settingsWindow;
            settingsWindow.Activate();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationFailed: {e.SourcePageType.FullName} - {e.Exception}");
            e.Handled = true;
        }
    }
}
