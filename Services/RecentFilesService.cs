using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GeoLens.Services;

public class RecentFilesService
{
    private const int MaxRecentFiles = 10;
    private readonly string _recentFilesPath;
    private List<RecentFileEntry> _recentFiles = new();

    public RecentFilesService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var geoLensPath = Path.Combine(appData, "GeoLens");
        Directory.CreateDirectory(geoLensPath);
        _recentFilesPath = Path.Combine(geoLensPath, "recent_files.json");
        LoadRecentFiles();
    }

    public IReadOnlyList<RecentFileEntry> GetRecentFiles() => _recentFiles.AsReadOnly();

    public void AddRecentFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var fileInfo = new FileInfo(filePath);
        var entry = new RecentFileEntry
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            LastAccessed = DateTime.Now
        };

        // Remove if already exists
        _recentFiles.RemoveAll(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        // Add to top
        _recentFiles.Insert(0, entry);

        // Keep only max items
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();

        SaveRecentFiles();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (File.Exists(_recentFilesPath))
            {
                var json = File.ReadAllText(_recentFilesPath);
                _recentFiles = JsonSerializer.Deserialize<List<RecentFileEntry>>(json) ?? new();

                // Remove files that no longer exist
                _recentFiles = _recentFiles.Where(f => File.Exists(f.FilePath)).ToList();
            }
        }
        catch
        {
            _recentFiles = new();
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_recentFilesPath, json);
        }
        catch { }
    }
}

public class RecentFileEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime LastAccessed { get; set; }
}
