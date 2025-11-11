# 🌍 GeoLens

`GeoLens` is a lightweight WinUI shell that lays the groundwork for an AI-driven place-detection experience. Drop one or more images, then queue them for a background predictor that will eventually surface five location guesses (with latitude/longitude) and render them on a globe.

## Getting started

```bash
dotnet restore GeoLens.sln
dotnet build GeoLens.sln
```

## What’s here

- `App.xaml` / `App.xaml.cs`: boots the app, hosts the main frame, and opens a standalone settings window.
- `Views/MainPage`: laid out with an image queue view, prediction list, and globe placeholder for future pins.
- `Views/SettingsPage`: stub page you can expand with configuration controls later.

This repo is intentionally minimal so you can layer your AI/visualization logic on top of the skeleton UI.
