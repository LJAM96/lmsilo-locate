# Video Frame Extraction for Geolocation

**Status**: Planned for Phase 2 (Post-MVP)
**Priority**: Medium
**Dependencies**: Core GeoCLIP pipeline (✅ Complete), FFmpeg integration
**Estimated Effort**: 2-3 weeks

---

## Overview

Enable users to upload video files and extract multiple frames for GeoCLIP geolocation analysis. This feature allows investigation teams, journalists, and OSINT analysts to geolocate video footage by processing key frames.

### Use Cases

1. **OSINT Investigation**: Geolocate surveillance footage or social media videos
2. **Travel Documentation**: Extract frames from GoPro/drone footage for location tagging
3. **Journalism**: Verify location claims in video evidence
4. **Academic Research**: Analyze video datasets for geographic patterns

---

## Supported Video Formats

| Format | Extension | Notes |
|--------|-----------|-------|
| MP4 | `.mp4` | H.264/H.265 (most common) |
| MOV | `.mov` | QuickTime (iPhone default) |
| AVI | `.avi` | Legacy format |
| MKV | `.mkv` | Matroska container |
| WebM | `.webm` | Web-optimized format |
| FLV | `.flv` | Flash video (less common) |
| WMV | `.wmv` | Windows Media Video |

---

## Feature Specifications

### 1. Video Upload and Preview

**File Picker Integration**:
```csharp
// Add to MainPage.xaml.cs AddImages_Click
picker.FileTypeFilter.Add(".mp4");
picker.FileTypeFilter.Add(".mov");
picker.FileTypeFilter.Add(".avi");
picker.FileTypeFilter.Add(".mkv");
picker.FileTypeFilter.Add(".webm");
picker.FileTypeFilter.Add(".flv");
picker.FileTypeFilter.Add(".wmv");
```

**Video Queue Item Model**:
```csharp
public class VideoQueueItem : ImageQueueItem
{
    public TimeSpan Duration { get; set; }
    public int FrameCount { get; set; }
    public string VideoCodec { get; set; } = "";
    public string Resolution { get; set; } = "";
    public double FrameRate { get; set; }
    public List<ExtractedFrame> ExtractedFrames { get; set; } = new();
    public bool IsVideo => true;
}

public class ExtractedFrame
{
    public TimeSpan Timestamp { get; set; }
    public string TempFilePath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
    public bool IsSelected { get; set; }
    public EnhancedPredictionResult? Prediction { get; set; }
}
```

**Video Metadata Extraction**:
- Use FFmpeg to extract duration, codec, resolution, frame rate
- Extract embedded GPS metadata (GoPro, DJI drones often include this)
- Display video info in queue item tooltip

---

### 2. Frame Extraction Modes

#### Mode 1: Manual Selection

**UI Components**:
- Timeline scrubber with thumbnail preview
- Click to mark frame for extraction
- Visual indicators (blue dots) on timeline
- Max 50 frames per video (configurable)

**Implementation**:
```csharp
public class VideoFrameSelector
{
    private readonly string _videoPath;
    private readonly TimeSpan _duration;

    public async Task<BitmapImage> GetThumbnailAtTimeAsync(TimeSpan timestamp)
    {
        // Use FFmpeg to extract single frame at timestamp
        var tempPath = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.jpg");
        await FFMpegService.ExtractFrameAsync(_videoPath, timestamp, tempPath);
        return await LoadImageAsync(tempPath);
    }

    public async Task<List<ExtractedFrame>> ExtractSelectedFramesAsync(
        List<TimeSpan> timestamps,
        IProgress<int> progress)
    {
        var frames = new List<ExtractedFrame>();
        for (int i = 0; i < timestamps.Count; i++)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"video_frame_{i}.jpg");
            await FFMpegService.ExtractFrameAsync(_videoPath, timestamps[i], tempPath);

            frames.Add(new ExtractedFrame
            {
                Timestamp = timestamps[i],
                TempFilePath = tempPath,
                IsSelected = true
            });

            progress?.Report((i + 1) * 100 / timestamps.Count);
        }
        return frames;
    }
}
```

#### Mode 2: Interval Extraction

**UI Settings**:
- Slider: Extract every N seconds (1-60s, default: 10s)
- Preview: "Will extract ~X frames from this video"
- Option: "Start at 0:00" vs "Smart start" (skip first 3 seconds)

**Implementation**:
```csharp
public class IntervalFrameExtractor
{
    public static List<TimeSpan> CalculateIntervals(
        TimeSpan duration,
        int intervalSeconds,
        int maxFrames = 50)
    {
        var timestamps = new List<TimeSpan>();
        var current = TimeSpan.Zero;

        while (current < duration && timestamps.Count < maxFrames)
        {
            timestamps.Add(current);
            current = current.Add(TimeSpan.FromSeconds(intervalSeconds));
        }

        return timestamps;
    }
}
```

#### Mode 3: Smart Extraction (Scene Change Detection)

**Algorithm**:
1. Use FFmpeg `scenedetect` filter to identify scene changes
2. Extract one frame per scene (first frame after scene change)
3. Limit to top N most significant scene changes (by delta)

**FFmpeg Command**:
```bash
ffmpeg -i input.mp4 -vf "select='gt(scene,0.3)',showinfo" -vsync vfr frames_%04d.jpg
```

**Implementation**:
```csharp
public class SmartFrameExtractor
{
    public async Task<List<TimeSpan>> DetectSceneChangesAsync(
        string videoPath,
        double threshold = 0.3,
        int maxScenes = 20)
    {
        // Run FFmpeg with scene detection
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var args = $"-i \"{videoPath}\" -vf \"select='gt(scene,{threshold})',showinfo\" " +
                   $"-vsync vfr \"{tempDir}/frame_%04d.jpg\"";

        var output = await FFMpegService.RunAsync(args);

        // Parse timestamps from FFmpeg output
        var timestamps = ParseSceneTimestamps(output);

        // Return top N most significant changes
        return timestamps.Take(maxScenes).ToList();
    }
}
```

---

### 3. FFmpeg Integration

**NuGet Package**: `FFMpegCore` (recommended) or `Xabe.FFmpeg`

**Service Implementation**:
```csharp
public class FFMpegService
{
    private static readonly string _ffmpegPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    public static async Task<VideoMetadata> GetVideoMetadataAsync(string videoPath)
    {
        var mediaInfo = await FFProbe.AnalyseAsync(videoPath);

        return new VideoMetadata
        {
            Duration = mediaInfo.Duration,
            Width = mediaInfo.PrimaryVideoStream.Width,
            Height = mediaInfo.PrimaryVideoStream.Height,
            FrameRate = mediaInfo.PrimaryVideoStream.FrameRate,
            Codec = mediaInfo.PrimaryVideoStream.CodecName,
            BitRate = mediaInfo.PrimaryVideoStream.BitRate
        };
    }

    public static async Task ExtractFrameAsync(
        string videoPath,
        TimeSpan timestamp,
        string outputPath)
    {
        await FFMpeg.SnapshotAsync(
            videoPath,
            outputPath,
            new Size(1920, 1080),
            timestamp
        );
    }

    public static async Task ExtractGpsMetadataAsync(string videoPath)
    {
        // Extract GPS track from video metadata (GoPro/DJI format)
        var args = $"-i \"{videoPath}\" -c copy -map 0:m:handler_name:gpmd -f rawvideo -";
        var output = await RunAsync(args);

        // Parse GPMF (GoPro Metadata Format) or similar
        return ParseGpsTrack(output);
    }
}
```

**Distribution**:
- Bundle `ffmpeg.exe` and `ffprobe.exe` in installer (~100MB)
- Place in `ffmpeg/` subdirectory
- Add to `.gitignore` (download in build script)

---

### 4. Batch Processing Workflow

**Process Flow**:
1. User selects video file → Video added to queue
2. User opens video frame selector dialog
3. User chooses extraction mode and selects frames
4. Frames extracted to temp directory (with progress bar)
5. Frames added to image queue as separate items (with video icon badge)
6. User clicks "Process Images" → All frames processed through GeoCLIP pipeline
7. Results displayed with video timestamp in location summary

**UI Flow**:
```
[Video Queue Item] → [Frame Selector Button] → [Modal Dialog]
                                                    ├─ Manual Tab
                                                    ├─ Interval Tab
                                                    └─ Smart Tab
                                                    [Extract Frames] → [Processing...]

[Image Queue] ← [Extracted Frame Items (with 🎬 badge)]
    ↓
[Process Images] → [GeoCLIP Pipeline] → [Results with Timestamp]
```

**Export Integration**:
```csharp
// Extend EnhancedPredictionResult
public class VideoFramePredictionResult : EnhancedPredictionResult
{
    public string? SourceVideoPath { get; set; }
    public TimeSpan? VideoTimestamp { get; set; }
    public string? VideoCodec { get; set; }
    public string? VideoResolution { get; set; }
}

// CSV Export includes video source
// Image Path, Video Source, Timestamp, Latitude, Longitude, ...
```

---

### 5. UI Components

#### Video Frame Selector Dialog

**XAML Structure**:
```xml
<ContentDialog x:Name="VideoFrameSelectorDialog"
               Title="Extract Frames"
               PrimaryButtonText="Extract"
               SecondaryButtonText="Cancel">
    <Grid>
        <TabView>
            <!-- Manual Selection Tab -->
            <TabViewItem Header="Manual">
                <StackPanel>
                    <!-- Video preview -->
                    <MediaPlayerElement x:Name="VideoPlayer" Height="400"/>

                    <!-- Timeline with markers -->
                    <Canvas x:Name="Timeline" Height="100">
                        <!-- Frame markers rendered here -->
                    </Canvas>

                    <!-- Controls -->
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Mark Frame" Click="MarkFrame_Click"/>
                        <TextBlock Text="{Binding SelectedFrameCount}"/>
                    </StackPanel>
                </StackPanel>
            </TabViewItem>

            <!-- Interval Extraction Tab -->
            <TabViewItem Header="Interval">
                <StackPanel Spacing="12">
                    <Slider x:Name="IntervalSlider"
                            Minimum="1" Maximum="60" Value="10"
                            Header="Extract every N seconds"/>
                    <TextBlock Text="{Binding EstimatedFrameCount}"/>
                </StackPanel>
            </TabViewItem>

            <!-- Smart Extraction Tab -->
            <TabViewItem Header="Smart">
                <StackPanel Spacing="12">
                    <TextBlock Text="Detect scene changes automatically"/>
                    <Slider x:Name="ThresholdSlider"
                            Minimum="0.1" Maximum="0.5" Value="0.3"
                            Header="Sensitivity"/>
                    <NumberBox x:Name="MaxScenesBox"
                               Value="20"
                               Header="Max scenes"/>
                </StackPanel>
            </TabViewItem>
        </TabView>
    </Grid>
</ContentDialog>
```

#### Video Queue Item Badge

**Visual Indicators**:
- 🎬 Video icon badge on thumbnail
- Duration displayed: "00:02:45"
- Frame count: "📹 145 frames @ 30fps"
- "Extract Frames" button overlay on hover

---

### 6. Performance Considerations

**Frame Extraction Speed**:
- FFmpeg can extract ~20 frames/second on modern CPUs
- Use `-qscale:v 2` for high-quality JPEG output
- Consider parallel extraction for multiple frames (limit to 4 threads)

**Temporary Storage**:
- Store extracted frames in `%TEMP%/GeoLens/video_frames/`
- Auto-cleanup on app exit or after 24 hours
- Estimate: 1MB per frame (1920x1080 JPEG)
- 50 frames = ~50MB temporary storage

**Memory Management**:
- Don't load all frame thumbnails into memory at once
- Use virtualization for frame list
- Release MediaPlayer resources when dialog closes

---

### 7. Video GPS Metadata Extraction

**Supported Formats**:
- **GoPro**: GPMF (GoPro Metadata Format) in MP4
- **DJI Drones**: SRT subtitle track with GPS telemetry
- **Smartphones**: GPS coordinates in MP4 metadata (rare)

**Extraction Strategy**:
1. Try extracting embedded GPS track with FFmpeg
2. Parse GPS coordinates at frame timestamps
3. If available, use GPS as "EXIF GPS" equivalent (VeryHigh confidence)
4. Display GPS track on map as polyline

**Implementation**:
```csharp
public class VideoGpsExtractor
{
    public async Task<List<GpsPoint>> ExtractGpsTrackAsync(string videoPath)
    {
        // Check for GoPro GPMF
        var goproTrack = await TryExtractGoproGpsAsync(videoPath);
        if (goproTrack != null) return goproTrack;

        // Check for DJI SRT
        var djiTrack = await TryExtractDjiSrtAsync(videoPath);
        if (djiTrack != null) return djiTrack;

        // Check generic MP4 location metadata
        return await TryExtractMp4LocationAsync(videoPath);
    }

    public GpsPoint? GetGpsAtTimestamp(List<GpsPoint> track, TimeSpan timestamp)
    {
        // Interpolate between nearest GPS points
        return Interpolate(track, timestamp);
    }
}

public class GpsPoint
{
    public TimeSpan Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? Speed { get; set; }
}
```

---

### 8. Testing Strategy

**Test Videos**:
1. Short MP4 (10 seconds, 1920x1080, H.264)
2. Long video (5 minutes, 4K)
3. GoPro footage with GPS metadata
4. DJI drone footage with SRT GPS
5. iPhone MOV file
6. Low-res AVI (640x480)
7. Corrupted/truncated video file

**Test Cases**:
- [ ] Manual frame selection (5 frames)
- [ ] Interval extraction (every 10 seconds)
- [ ] Smart scene detection
- [ ] Video with GPS track extraction
- [ ] Video without GPS
- [ ] Process all extracted frames through GeoCLIP
- [ ] Export results with video timestamps
- [ ] Handle corrupted video gracefully

---

### 9. User Experience Flow

**Example: OSINT Analyst Workflow**

1. **Upload Video**: Drag & drop surveillance footage (`evidence.mp4`)
2. **Extract Frames**: Click "Extract Frames" → Choose "Smart" mode → 8 scenes detected
3. **Review Frames**: Preview extracted frames in queue (thumbnails with timestamps)
4. **Process**: Click "Process Images" → GeoCLIP analyzes all 8 frames
5. **View Results**: Map shows 8 pins (or heatmap if clustered)
6. **Verify**: Click pin → See frame thumbnail + timestamp + location prediction
7. **Export**: Export to KML → Open in Google Earth → Verify with satellite imagery

**Time Savings**:
- Manual screenshot extraction: ~10 minutes
- GeoLens smart extraction: ~30 seconds
- GeoCLIP processing: ~2-3 seconds per frame (with GPU)

---

### 10. Implementation Checklist

**Phase 2.1: Basic Video Support (Week 1)**
- [ ] Integrate FFMpegCore NuGet package
- [ ] Implement FFMpegService (metadata extraction)
- [ ] Add video file type filters to file picker
- [ ] Create VideoQueueItem model
- [ ] Display video metadata in queue item

**Phase 2.2: Manual Frame Extraction (Week 2)**
- [ ] Build VideoFrameSelectorDialog UI
- [ ] Implement timeline scrubber with MediaPlayerElement
- [ ] Add frame marker visualization
- [ ] Implement manual frame extraction
- [ ] Handle temp file storage and cleanup

**Phase 2.3: Advanced Extraction Modes (Week 3)**
- [ ] Implement interval extraction
- [ ] Implement smart scene detection
- [ ] Add progress reporting for batch extraction
- [ ] Integrate with existing GeoCLIP pipeline
- [ ] Update export services with video metadata

**Phase 2.4: GPS Metadata (Week 4)**
- [ ] Implement GoPro GPMF parser
- [ ] Implement DJI SRT parser
- [ ] Add GPS track visualization on map
- [ ] Interpolate GPS coordinates for frames

**Phase 2.5: Testing & Polish (Week 5)**
- [ ] Test with various video formats
- [ ] Performance optimization (parallel extraction)
- [ ] Error handling for corrupted videos
- [ ] UI polish (loading states, animations)
- [ ] Documentation and user guide

---

## Open Questions

1. **Frame Quality**: Extract at original resolution or downscale to 1920x1080?
   - **Recommendation**: Offer quality setting (High/Medium/Low)

2. **Batch Video Processing**: Allow processing multiple videos at once?
   - **Recommendation**: Phase 3 feature (after single video works well)

3. **Live Video Stream**: Support processing from webcam/screen capture?
   - **Recommendation**: Phase 4 (requires different architecture)

4. **GPU Acceleration**: Use hardware video decoding?
   - **Recommendation**: Yes, FFmpeg supports NVDEC/DXVA2 automatically

---

## Related Documentation

- `02_Service_Implementations.md`: FFMpegService integration patterns
- `09_Testing_Strategy.md`: Video processing test cases
- `11_Advanced_Features.md`: Batch video processing (Phase 3)

---

## Dependencies

**NuGet Packages**:
- `FFMpegCore` (5.1.0) - FFmpeg wrapper for .NET
- `VideoLAN.LibVLC.Windows` (optional, for advanced playback)

**External Tools**:
- FFmpeg.exe (~60MB) - Video processing
- FFprobe.exe (~40MB) - Metadata extraction

**Python Packages** (none required - reuses existing GeoCLIP pipeline)

---

**Last Updated**: 2025-01-12
**Status**: Specification Complete, Implementation Pending
