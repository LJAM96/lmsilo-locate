# GeoLens - Fluent Design System Implementation

## Overview

This document details the implementation of Windows 11 Fluent Design System in GeoLens, including Mica/Acrylic materials, modern native UI patterns, and Segoe Fluent Icons glyphs.

---

## Fluent Design Principles

### Five Key Elements

1. **Light**: Visual affordances that guide the eye
2. **Depth**: Layers create hierarchy (Mica, Acrylic)
3. **Motion**: Fluid animations reinforce actions
4. **Material**: Translucent surfaces that reveal context
5. **Scale**: Responsive design that adapts

---

## Material System

### Mica (Base Layer)

**Purpose**: Desktop-integrated background that shows wallpaper through the app

**Usage**:
- Main window background
- Creates sense of depth and connection to desktop
- Automatically adapts to dark/light theme

```csharp
// App.xaml.cs
public App()
{
    InitializeComponent();

    // Enable Mica for all windows
    if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
    {
        RequestedTheme = ApplicationTheme.Dark;
    }
}

// MainWindow.xaml.cs
public MainWindow()
{
    InitializeComponent();

    // Apply Mica backdrop
    SystemBackdrop = new MicaBackdrop()
    {
        Kind = MicaKind.Base
    };

    // Enable custom title bar
    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
}
```

```xml
<!-- MainWindow.xaml -->
<Window
    x:Class="GeoLens.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <!-- Custom Title Bar -->
        <Grid x:Name="AppTitleBar"
              Height="48"
              VerticalAlignment="Top"
              Canvas.ZIndex="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- App Icon & Title -->
            <StackPanel Grid.Column="0"
                       Orientation="Horizontal"
                       Spacing="12"
                       Margin="16,0,0,0"
                       VerticalAlignment="Center">
                <Image Source="Assets/icon_white.png"
                       Width="16" Height="16"/>
                <TextBlock Text="GeoLens"
                          Style="{StaticResource CaptionTextBlockStyle}"/>
            </StackPanel>

            <!-- Tab Navigation (Optional) -->
            <StackPanel Grid.Column="1"
                       Orientation="Horizontal"
                       HorizontalAlignment="Center">
                <!-- Navigation items -->
            </StackPanel>
        </Grid>

        <!-- Main Content -->
        <Grid Margin="0,48,0,0">
            <!-- Your 3-panel layout -->
        </Grid>
    </Grid>
</Window>
```

---

### Acrylic (Overlay Layer)

**Purpose**: Translucent surfaces for panels, sidebars, and overlays

**Usage**:
- Left panel (image queue)
- Right panel (results)
- Flyouts and popups
- Command bars

```xml
<!-- Left Panel with Acrylic -->
<Grid Grid.Column="0" Width="320">
    <Grid.Background>
        <AcrylicBrush TintColor="#1A1A1A"
                      TintOpacity="0.8"
                      TintLuminosityOpacity="0.8"/>
    </Grid.Background>

    <!-- Content -->
</Grid>

<!-- Right Panel with Acrylic -->
<Grid Grid.Column="2" Width="400">
    <Grid.Background>
        <AcrylicBrush TintColor="#1A1A1A"
                      TintOpacity="0.7"
                      TintLuminosityOpacity="0.9"/>
    </Grid.Background>

    <!-- Content -->
</Grid>
```

**Acrylic Types**:

```csharp
// In-app Acrylic (default)
new AcrylicBrush
{
    TintColor = Color.FromArgb(255, 26, 26, 26),
    TintOpacity = 0.8,
    TintLuminosityOpacity = 0.8,
    FallbackColor = Color.FromArgb(255, 30, 30, 30)
};

// Background Acrylic (desktop integration)
new DesktopAcrylicBackdrop
{
    Kind = DesktopAcrylicKind.Base
};
```

---

### Layering System

```
┌─────────────────────────────────────┐
│  Mica (Base)                        │  ← Desktop wallpaper shows through
│  ┌───────────────────────────────┐  │
│  │ Acrylic Panel (Left)          │  │  ← Translucent overlay
│  │ ┌───────────────────────────┐ │  │
│  │ │ Card (Elevated)           │ │  │  ← Raised surface
│  │ │ ┌─────────────────────┐   │ │  │
│  │ │ │ Button (Interactive) │   │ │  │  ← Pressable element
│  │ │ └─────────────────────┘   │ │  │
│  │ └───────────────────────────┘ │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘

Z-Index Layers:
0: Mica backdrop
1: Main content area
2: Acrylic panels
3: Cards and surfaces
4: Interactive controls
5: Flyouts and dialogs
```

---

## Segoe Fluent Icons Glyphs

### Complete Icon Reference

```csharp
// Services/FluentIcons.cs
public static class FluentIcons
{
    // Navigation
    public const string Home = "\uE80F";
    public const string Back = "\uE72B";
    public const string Forward = "\uE72A";
    public const string Menu = "\uE700";

    // Actions
    public const string Add = "\uE710";
    public const string Delete = "\uE74D";
    public const string Edit = "\uE70F";
    public const string Save = "\uE74E";
    public const string Cancel = "\uE711";
    public const string Refresh = "\uE72C";
    public const string Search = "\uE721";

    // Media
    public const string Photo = "\uE91B";
    public const string Camera = "\uE722";
    public const string Play = "\uE768";
    public const string Pause = "\uE769";

    // Map/Location
    public const string MapPin = "\uE707";
    public const string Globe = "\uE774";
    public const string Location = "\uE81D";
    public const string Compass = "\uE753";

    // Data
    public const string Document = "\uE8A5";
    public const string Folder = "\uE8B7";
    public const string Download = "\uE896";
    public const string Upload = "\uE898";

    // Status
    public const string Accept = "\uE8FB";
    public const string StatusCircleCheckmark = "\uF13E";
    public const string StatusCircleError = "\uF13D";
    public const string Info = "\uE946";
    public const string Warning = "\uE7BA";

    // UI Elements
    public const string ChevronDown = "\uE70D";
    public const string ChevronUp = "\uE70E";
    public const string ChevronLeft = "\uE76B";
    public const string ChevronRight = "\uE76C";
    public const string More = "\uE712";

    // Settings
    public const string Settings = "\uE713";
    public const string Filter = "\uE71C";
    public const string Sort = "\uE8CB";

    // Tools
    public const string Calculator = "\uE8EF";
    public const string Calendar = "\uE787";
    public const string Clock = "\uE917";

    // Special (GeoLens specific)
    public const string Heatmap = "\uE81C"; // Heat/Fire
    public const string Layers = "\uE80A"; // Map layers
    public const string Target = "\uE611"; // Crosshair/target
    public const string World = "\uE909"; // World map
}
```

### Usage in XAML

```xml
<!-- Button with icon -->
<Button>
    <StackPanel Orientation="Horizontal" Spacing="8">
        <FontIcon Glyph="&#xE710;" FontFamily="Segoe Fluent Icons"/>
        <TextBlock Text="Add Images"/>
    </StackPanel>
</Button>

<!-- Icon-only button -->
<Button Width="40" Height="40" ToolTipService.ToolTip="Settings">
    <FontIcon Glyph="&#xE713;" FontSize="16"/>
</Button>

<!-- NavigationViewItem with icon -->
<NavigationViewItem Content="Map View" Icon="{ui:FontIcon Glyph=&#xE774;}"/>
```

---

## Updated UI Structure

### Main Window Layout

```xml
<!-- MainWindow.xaml -->
<Window
    x:Class="GeoLens.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="using:Microsoft.UI.Xaml.Controls">

    <Grid>
        <!-- Title Bar -->
        <Grid x:Name="AppTitleBar"
              Height="48"
              VerticalAlignment="Top"
              Canvas.ZIndex="2">

            <Grid.Background>
                <AcrylicBrush TintColor="#0F0F0F"
                             TintOpacity="0.5"/>
            </Grid.Background>

            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Left: Icon & Title -->
            <StackPanel Grid.Column="0"
                       Orientation="Horizontal"
                       Spacing="12"
                       Margin="16,0">
                <FontIcon Glyph="&#xE774;"
                         FontSize="16"
                         Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                <TextBlock Text="GeoLens"
                          Style="{StaticResource CaptionTextBlockStyle}"
                          VerticalAlignment="Center"/>
            </StackPanel>

            <!-- Center: Quick Actions -->
            <CommandBar Grid.Column="1"
                       DefaultLabelPosition="Right"
                       Background="Transparent"
                       HorizontalAlignment="Center">

                <AppBarButton Label="Add Images"
                             Icon="{ui:FontIcon Glyph=&#xE710;}"/>

                <AppBarButton Label="Process"
                             Icon="{ui:FontIcon Glyph=&#xE768;}"/>

                <AppBarSeparator/>

                <AppBarToggleButton Label="Heatmap"
                                   Icon="{ui:FontIcon Glyph=&#xE81C;}"/>

                <AppBarButton Label="Export"
                             Icon="{ui:FontIcon Glyph=&#xE74E;}">
                    <AppBarButton.Flyout>
                        <MenuFlyout>
                            <MenuFlyoutItem Text="Export CSV"
                                          Icon="{ui:FontIcon Glyph=&#xE8A5;}"/>
                            <MenuFlyoutItem Text="Export PDF"
                                          Icon="{ui:FontIcon Glyph=&#xE8A5;}"/>
                            <MenuFlyoutItem Text="Export KML"
                                          Icon="{ui:FontIcon Glyph=&#xE909;}"/>
                        </MenuFlyout>
                    </AppBarButton.Flyout>
                </AppBarButton>
            </CommandBar>

            <!-- Right: Settings -->
            <StackPanel Grid.Column="2"
                       Orientation="Horizontal"
                       Spacing="8"
                       Margin="0,0,16,0">

                <Button Style="{StaticResource SubtleButtonStyle}"
                       Width="40" Height="40"
                       ToolTipService.ToolTip="Settings">
                    <FontIcon Glyph="&#xE713;" FontSize="16"/>
                </Button>
            </StackPanel>
        </Grid>

        <!-- Main Content with Navigation -->
        <NavigationView x:Name="NavView"
                       PaneDisplayMode="Left"
                       IsBackButtonVisible="Collapsed"
                       IsSettingsVisible="False"
                       IsPaneToggleButtonVisible="False"
                       OpenPaneLength="320"
                       Margin="0,48,0,0">

            <NavigationView.MenuItems>
                <!-- Will contain image queue -->
            </NavigationView.MenuItems>

            <NavigationView.Content>
                <!-- Main 2-column layout (map + results) -->
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/> <!-- Map -->
                        <ColumnDefinition Width="400"/> <!-- Results -->
                    </Grid.ColumnDefinitions>

                    <!-- Content panels -->
                </Grid>
            </NavigationView.Content>
        </NavigationView>
    </Grid>
</Window>
```

---

## Left Panel: Image Queue (Fluent Style)

```xml
<!-- Left Panel as NavigationView Pane -->
<Grid Padding="8">
    <!-- Selection Toolbar -->
    <StackPanel Spacing="8" Margin="0,0,0,8">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <CheckBox Content="Select All"
                     IsChecked="{x:Bind IsAllSelected, Mode=TwoWay}"/>

            <TextBlock Grid.Column="1"
                      Text="{x:Bind SelectedCount, Mode=OneWay}"
                      Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"
                      VerticalAlignment="Center"/>
        </Grid>

        <!-- Action Bar with Icons -->
        <Grid ColumnSpacing="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <Button Grid.Column="0"
                   HorizontalAlignment="Stretch"
                   ToolTipService.ToolTip="Overlay All">
                <FontIcon Glyph="&#xE80A;" FontSize="16"/>
            </Button>

            <Button Grid.Column="1"
                   HorizontalAlignment="Stretch"
                   ToolTipService.ToolTip="Export Selection">
                <FontIcon Glyph="&#xE74E;" FontSize="16"/>
            </Button>

            <Button Grid.Column="2"
                   HorizontalAlignment="Stretch"
                   ToolTipService.ToolTip="Clear Selection">
                <FontIcon Glyph="&#xE711;" FontSize="16"/>
            </Button>

            <Button Grid.Column="3"
                   Style="{StaticResource AccentButtonStyle}"
                   HorizontalAlignment="Stretch"
                   ToolTipService.ToolTip="Remove Selected">
                <FontIcon Glyph="&#xE74D;" FontSize="16"/>
            </Button>
        </Grid>
    </StackPanel>

    <!-- Image Grid -->
    <GridView SelectionMode="Multiple"
             ItemsSource="{x:Bind ImageQueueItems}"
             Padding="0,60,0,60">

        <GridView.ItemTemplate>
            <DataTemplate x:DataType="local:ImageQueueItem">
                <!-- Card with elevation -->
                <Grid Width="140" Height="200"
                     CornerRadius="8"
                     Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                     BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                     BorderThickness="1">

                    <!-- Reveal border on hover -->
                    <Grid.Resources>
                        <RevealBorderBrush x:Key="RevealBorderBrush"
                                          TargetTheme="Dark"
                                          Color="{ThemeResource SystemAccentColor}"/>
                    </Grid.Resources>

                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <!-- Thumbnail -->
                    <Border Grid.Row="0" Margin="8,8,8,4" CornerRadius="4">
                        <Image Source="{x:Bind ThumbnailSource}"
                              Stretch="UniformToFill"/>
                    </Border>

                    <!-- Status Overlay -->
                    <Grid Grid.Row="0"
                         Background="{ThemeResource AcrylicBackgroundFillColorDefaultBrush}"
                         Visibility="{x:Bind IsProcessing, Mode=OneWay}"
                         CornerRadius="8,8,0,0">
                        <ProgressRing IsActive="True" Width="40" Height="40"/>
                    </Grid>

                    <!-- Cache Badge -->
                    <Border Grid.Row="0"
                           HorizontalAlignment="Right"
                           VerticalAlignment="Top"
                           Margin="12"
                           Visibility="{x:Bind IsCached, Mode=OneWay}"
                           Background="{ThemeResource AccentFillColorDefaultBrush}"
                           CornerRadius="12"
                           Padding="8,4">
                        <FontIcon Glyph="&#xE8FB;"
                                 FontSize="12"
                                 Foreground="White"/>
                    </Border>

                    <!-- Info Panel -->
                    <StackPanel Grid.Row="1"
                               Padding="8"
                               Spacing="4">
                        <TextBlock Text="{x:Bind FileName}"
                                  FontSize="11"
                                  TextTrimming="CharacterEllipsis"
                                  TextAlignment="Center"/>

                        <!-- Status with Icon -->
                        <Border HorizontalAlignment="Center"
                               Background="{x:Bind StatusColor, Mode=OneWay}"
                               CornerRadius="4"
                               Padding="8,2">
                            <StackPanel Orientation="Horizontal" Spacing="4">
                                <FontIcon Glyph="{x:Bind StatusGlyph, Mode=OneWay}"
                                         FontSize="10"
                                         Foreground="White"/>
                                <TextBlock Text="{x:Bind StatusText, Mode=OneWay}"
                                          FontSize="10"
                                          Foreground="White"/>
                            </StackPanel>
                        </Border>
                    </StackPanel>

                    <!-- Selection Checkbox -->
                    <CheckBox Grid.Row="0"
                            IsChecked="{x:Bind IsSelected, Mode=TwoWay}"
                            HorizontalAlignment="Left"
                            VerticalAlignment="Top"
                            Margin="8"/>
                </Grid>
            </DataTemplate>
        </GridView.ItemTemplate>

        <GridView.ItemContainerStyle>
            <Style TargetType="GridViewItem">
                <Setter Property="Margin" Value="4"/>
            </Style>
        </GridView.ItemContainerStyle>
    </GridView>

    <!-- Add Images Card (Bottom) -->
    <Border VerticalAlignment="Bottom"
           Margin="8"
           Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
           BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
           BorderThickness="1"
           CornerRadius="8"
           Padding="16">

        <Button HorizontalAlignment="Stretch"
               Style="{StaticResource AccentButtonStyle}"
               Click="AddImages_Click">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <FontIcon Glyph="&#xE710;"/>
                <TextBlock Text="Add Images"/>
            </StackPanel>
        </Button>
    </Border>
</Grid>
```

---

## Right Panel: Results (Fluent Style)

```xml
<!-- Right Panel with Acrylic -->
<Grid Grid.Column="1">
    <Grid.Background>
        <AcrylicBrush TintColor="#1A1A1A"
                     TintOpacity="0.7"
                     TintLuminosityOpacity="0.9"/>
    </Grid.Background>

    <ScrollViewer>
        <StackPanel Spacing="16" Padding="16">

            <!-- EXIF GPS Card -->
            <Expander Header="GPS Metadata"
                     IsExpanded="True"
                     Visibility="{x:Bind HasExifGps, Mode=OneWay}">
                <Expander.HeaderTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal" Spacing="12">
                            <FontIcon Glyph="&#xE81D;"
                                     Foreground="{ThemeResource SystemFillColorSuccessBrush}"/>
                            <TextBlock Text="GPS Metadata" FontWeight="SemiBold"/>
                            <Border Background="{ThemeResource SystemFillColorSuccessBrush}"
                                   CornerRadius="4" Padding="8,2">
                                <TextBlock Text="VERY HIGH"
                                          FontSize="10"
                                          FontWeight="Bold"
                                          Foreground="White"/>
                            </Border>
                        </StackPanel>
                    </DataTemplate>
                </Expander.HeaderTemplate>

                <StackPanel Spacing="8" Margin="0,12,0,0">
                    <TextBlock Text="{x:Bind ExifLocation}"
                              FontSize="16"
                              FontWeight="SemiBold"/>

                    <StackPanel Orientation="Horizontal" Spacing="16">
                        <StackPanel Spacing="4">
                            <TextBlock Text="Latitude"
                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                      FontSize="11"/>
                            <TextBlock Text="{x:Bind ExifLat}"
                                      FontFamily="Consolas"/>
                        </StackPanel>

                        <StackPanel Spacing="4">
                            <TextBlock Text="Longitude"
                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                      FontSize="11"/>
                            <TextBlock Text="{x:Bind ExifLon}"
                                      FontFamily="Consolas"/>
                        </StackPanel>
                    </StackPanel>
                </StackPanel>
            </Expander>

            <!-- Reliability Info Bar -->
            <InfoBar IsOpen="True"
                    Severity="{x:Bind ReliabilitySeverity, Mode=OneWay}"
                    Message="{x:Bind ReliabilityMessage, Mode=OneWay}"
                    IsClosable="False"/>

            <!-- AI Predictions Section -->
            <StackPanel Spacing="8">
                <TextBlock Text="AI Predictions"
                          Style="{StaticResource SubtitleTextBlockStyle}"/>

                <ItemsRepeater ItemsSource="{x:Bind Predictions, Mode=OneWay}">
                    <ItemsRepeater.ItemTemplate>
                        <DataTemplate x:DataType="local:EnhancedLocationPrediction">
                            <!-- Prediction Card -->
                            <Expander HorizontalAlignment="Stretch"
                                     HorizontalContentAlignment="Stretch"
                                     Margin="0,0,0,8">

                                <Expander.Header>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>

                                        <!-- Rank Badge -->
                                        <Border Grid.Column="0"
                                               Width="32" Height="32"
                                               Background="{ThemeResource AccentFillColorDefaultBrush}"
                                               CornerRadius="16"
                                               VerticalAlignment="Center">
                                            <TextBlock Text="{x:Bind Rank}"
                                                      HorizontalAlignment="Center"
                                                      VerticalAlignment="Center"
                                                      FontWeight="Bold"/>
                                        </Border>

                                        <!-- Location Info -->
                                        <StackPanel Grid.Column="1"
                                                   Margin="12,0"
                                                   VerticalAlignment="Center">
                                            <TextBlock Text="{x:Bind LocationSummary}"
                                                      FontWeight="SemiBold"/>

                                            <StackPanel Orientation="Horizontal" Spacing="4"
                                                       Visibility="{x:Bind IsPartOfCluster}">
                                                <FontIcon Glyph="&#xE81E;"
                                                         FontSize="11"
                                                         Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                                                <TextBlock Text="Clustered prediction"
                                                          FontSize="11"
                                                          Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                                            </StackPanel>
                                        </StackPanel>

                                        <!-- Confidence Badge -->
                                        <Border Grid.Column="2"
                                               Background="{x:Bind ConfidenceColor, Mode=OneWay}"
                                               CornerRadius="4"
                                               Padding="8,4"
                                               VerticalAlignment="Center">
                                            <StackPanel Orientation="Horizontal" Spacing="6">
                                                <FontIcon Glyph="{x:Bind ConfidenceGlyph}"
                                                         FontSize="12"
                                                         Foreground="White"/>
                                                <TextBlock Text="{x:Bind ConfidenceText}"
                                                          FontSize="11"
                                                          FontWeight="Bold"
                                                          Foreground="White"/>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </Expander.Header>

                                <StackPanel Spacing="12" Margin="44,8,0,0">
                                    <!-- Coordinates -->
                                    <Grid ColumnSpacing="16">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="*"/>
                                        </Grid.ColumnDefinitions>

                                        <StackPanel Grid.Column="0" Spacing="4">
                                            <TextBlock Text="Latitude"
                                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                                      FontSize="11"/>
                                            <TextBlock Text="{x:Bind LatitudeFormatted}"
                                                      FontFamily="Consolas"/>
                                        </StackPanel>

                                        <StackPanel Grid.Column="1" Spacing="4">
                                            <TextBlock Text="Longitude"
                                                      Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                                      FontSize="11"/>
                                            <TextBlock Text="{x:Bind LongitudeFormatted}"
                                                      FontFamily="Consolas"/>
                                        </StackPanel>
                                    </Grid>

                                    <!-- Actions -->
                                    <Grid ColumnSpacing="8">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="*"/>
                                        </Grid.ColumnDefinitions>

                                        <Button Grid.Column="0"
                                               HorizontalAlignment="Stretch">
                                            <StackPanel Orientation="Horizontal" Spacing="8">
                                                <FontIcon Glyph="&#xE707;" FontSize="14"/>
                                                <TextBlock Text="View"/>
                                            </StackPanel>
                                        </Button>

                                        <Button Grid.Column="1"
                                               HorizontalAlignment="Stretch">
                                            <StackPanel Orientation="Horizontal" Spacing="8">
                                                <FontIcon Glyph="&#xE8C8;" FontSize="14"/>
                                                <TextBlock Text="Copy"/>
                                            </StackPanel>
                                        </Button>
                                    </Grid>
                                </StackPanel>
                            </Expander>
                        </DataTemplate>
                    </ItemsRepeater.ItemTemplate>
                </ItemsRepeater>
            </StackPanel>

            <!-- Export Actions -->
            <StackPanel Spacing="8">
                <TextBlock Text="Export"
                          Style="{StaticResource SubtitleTextBlockStyle}"/>

                <Grid ColumnSpacing="8" RowSpacing="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>

                    <Button Grid.Row="0" Grid.Column="0"
                           HorizontalAlignment="Stretch"
                           Style="{StaticResource AccentButtonStyle}">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <FontIcon Glyph="&#xE8A5;"/>
                            <TextBlock Text="CSV"/>
                        </StackPanel>
                    </Button>

                    <Button Grid.Row="0" Grid.Column="1"
                           HorizontalAlignment="Stretch">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <FontIcon Glyph="&#xE8A5;"/>
                            <TextBlock Text="PDF"/>
                        </StackPanel>
                    </Button>

                    <Button Grid.Row="1" Grid.Column="0"
                           HorizontalAlignment="Stretch">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <FontIcon Glyph="&#xE909;"/>
                            <TextBlock Text="KML"/>
                        </StackPanel>
                    </Button>

                    <Button Grid.Row="1" Grid.Column="1"
                           HorizontalAlignment="Stretch">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <FontIcon Glyph="&#xE8C8;"/>
                            <TextBlock Text="Copy All"/>
                        </StackPanel>
                    </Button>
                </Grid>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</Grid>
```

---

## App.xaml - Fluent Theme Resources

```xml
<Application
    x:Class="GeoLens.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls"/>
            </ResourceDictionary.MergedDictionaries>

            <!-- Custom Theme Overrides -->
            <ResourceDictionary.ThemeDictionaries>
                <ResourceDictionary x:Key="Dark">
                    <!-- Accent Color -->
                    <Color x:Key="SystemAccentColor">#0078D4</Color>

                    <!-- Background Colors with Mica -->
                    <SolidColorBrush x:Key="ApplicationPageBackgroundThemeBrush" Color="Transparent"/>

                    <!-- Card Colors -->
                    <Color x:Key="CardBackgroundFillColorDefault">#0F000000</Color>
                    <Color x:Key="CardStrokeColorDefault">#19FFFFFF</Color>

                    <!-- Subtle Button (for icon buttons) -->
                    <StaticResource x:Key="SubtleButtonBackgroundPointerOver" ResourceKey="SubtleFillColorSecondaryBrush"/>

                    <!-- Custom Acrylic -->
                    <AcrylicBrush x:Key="NavigationViewDefaultPaneBackground"
                                 TintColor="#1A1A1A"
                                 TintOpacity="0.8"
                                 TintLuminosityOpacity="0.8"
                                 Fallback="#1E1E1E"/>
                </ResourceDictionary>
            </ResourceDictionary.ThemeDictionaries>

            <!-- Fluent Animations -->
            <Duration x:Key="ControlFastAnimationDuration">0:0:0.167</Duration>
            <Duration x:Key="ControlNormalAnimationDuration">0:0:0.250</Duration>
            <Duration x:Key="ControlSlowAnimationDuration">0:0:0.500</Duration>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## Status Glyph Mapping

```csharp
// Models/ImageQueueItem.cs
public string StatusGlyph => Status switch
{
    QueueStatus.Queued => FluentIcons.Clock,      // ⏱️
    QueueStatus.Processing => FluentIcons.Sync,   // 🔄
    QueueStatus.Done => FluentIcons.Accept,       // ✓
    QueueStatus.Error => FluentIcons.StatusCircleError, // ✕
    QueueStatus.Cached => FluentIcons.StatusCircleCheckmark, // ✓ with circle
    _ => FluentIcons.Info
};
```

---

## Fluent Motion

### Implicit Animations

```csharp
// Enable connected animations for navigation
protected override void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);

    // Animate cards in
    var itemsRepeater = FindName("PredictionsList") as ItemsRepeater;
    if (itemsRepeater != null)
    {
        foreach (var item in itemsRepeater.ItemsSourceView)
        {
            // Apply entrance animation
            var container = itemsRepeater.GetOrCreateElementFor(item);
            var animation = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("ItemAnimation", container);
        }
    }
}
```

### Page Transitions

```xml
<Page.Transitions>
    <TransitionCollection>
        <NavigationThemeTransition>
            <NavigationThemeTransition.DefaultNavigationTransitionInfo>
                <EntranceNavigationTransitionInfo/>
            </NavigationThemeTransition.DefaultNavigationTransitionInfo>
        </NavigationThemeTransition>
    </TransitionCollection>
</Page.Transitions>
```

---

## Complete Glyph Reference Table

| Element | Glyph | Unicode | Icon |
|---------|-------|---------|------|
| Add Images | E710 | \uE710 | ➕ |
| Process/Play | E768 | \uE768 | ▶️ |
| Pause | E769 | \uE769 | ⏸️ |
| Stop | E71A | \uE71A | ⏹️ |
| Delete | E74D | \uE74D | 🗑️ |
| Settings | E713 | \uE713 | ⚙️ |
| Refresh | E72C | \uE72C | 🔄 |
| Search | E721 | \uE721 | 🔍 |
| Filter | E71C | \uE71C | 🔽 |
| Map Pin | E707 | \uE707 | 📍 |
| Globe | E774 | \uE774 | 🌍 |
| Location | E81D | \uE81D | 📍 |
| Heatmap | E81C | \uE81C | 🔥 |
| Layers | E80A | \uE80A | 📚 |
| Document | E8A5 | \uE8A5 | 📄 |
| Save | E74E | \uE74E | 💾 |
| Export | E898 | \uE898 | 📤 |
| Copy | E8C8 | \uE8C8 | 📋 |
| Info | E946 | \uE946 | ℹ️ |
| Warning | E7BA | \uE7BA | ⚠️ |
| Error | F13D | \uF13D | ❌ |
| Success | F13E | \uF13E | ✅ |
| Clock | E917 | \uE917 | 🕐 |
| Calendar | E787 | \uE787 | 📅 |
| Camera | E722 | \uE722 | 📷 |
| Photo | E91B | \uE91B | 🖼️ |

---

This completes the Fluent Design System implementation guide. All UI elements now use modern Windows 11 materials, Segoe Fluent Icons glyphs, and proper Fluent Design patterns.
