# Whisper AI Transcription Module

**Status**: Planned for Phase 3 (Future Major Feature)
**Priority**: Low (Post-MVP)
**Dependencies**: Core app infrastructure (✅), FFmpeg (✅), Whisper model integration
**Estimated Effort**: 6-8 weeks (full feature set)

---

## Executive Summary

Add a dedicated AI transcription screen to GeoLens using OpenAI's Whisper model for audio/video transcription, translation, and speaker diarization. All processing runs locally (no cloud dependency), supporting 99 languages with GPU acceleration.

### Strategic Rationale

1. **OSINT Workflow Integration**: Analysts often need to transcribe foreign language audio/video alongside geolocation
2. **Privacy-First**: Sensitive audio/video can be transcribed locally without cloud upload
3. **Leverage Existing Infrastructure**: Reuse Python service architecture, GPU detection, and UI patterns
4. **Competitive Advantage**: Few desktop tools combine geolocation + transcription in one app

---

## Overview

### Core Capabilities

- **Transcription**: Audio/video → text with timestamps (99 languages)
- **Translation**: Auto-translate to English from any source language
- **Speaker Diarization**: Identify and label different speakers ("Speaker 1", "Speaker 2", etc.)
- **Export**: TXT, SRT (subtitles), VTT (WebVTT), JSON with full metadata

### Supported Formats

**Audio**:
- `.mp3` - MP3 (most common)
- `.wav` - WAV (lossless)
- `.flac` - FLAC (lossless compression)
- `.m4a` - AAC/ALAC (Apple)
- `.ogg` - Ogg Vorbis
- `.wma` - Windows Media Audio
- `.aac` - AAC (raw)

**Video** (extract audio):
- `.mp4`, `.mov`, `.avi`, `.mkv`, `.webm` - Audio track extracted via FFmpeg

---

## Architecture

### System Design

```
┌─────────────────────────────────────────────────┐
│  WinUI3 Frontend (TranscriptionPage.xaml)      │
│  - Audio/video queue management                 │
│  - Waveform visualization (NAudio)              │
│  - Transcript editor (RichEditBox)              │
│  - Speaker diarization UI                       │
│  - Export options                               │
└─────────────────┬───────────────────────────────┘
                  │ HTTP/REST (localhost:8900)
┌─────────────────▼───────────────────────────────┐
│  C# Service Layer (Services/Transcription/)     │
│  - WhisperRuntimeManager.cs                     │
│  - WhisperApiClient.cs                          │
│  - AudioExtractionService.cs (FFmpeg)           │
│  - TranscriptionCacheService.cs (SQLite)        │
│  - SpeakerDiarizationService.cs                 │
│  - TranscriptExportService.cs                   │
│  - WaveformGeneratorService.cs                  │
└─────────────────┬───────────────────────────────┘
                  │ Process Management
┌─────────────────▼───────────────────────────────┐
│  Python Whisper Service (Core/whisper_service/) │
│  - whisper_api.py (FastAPI)                     │
│  - /transcribe endpoint                         │
│  - /translate endpoint                          │
│  - /identify-language endpoint                  │
│  - /diarize endpoint (pyannote.audio)           │
│  - faster-whisper or whisper.cpp backend        │
└─────────────────────────────────────────────────┘
```

### Service Endpoints

**Whisper API** (Port 8900):
- `GET /health`: Health check
- `POST /transcribe`: Transcribe audio with timestamps
- `POST /translate`: Translate to English
- `POST /identify-language`: Detect language with confidence
- `POST /diarize`: Speaker diarization (segments with speaker IDs)

---

## Implementation Phases

### Phase 3.1: Basic Transcription (Weeks 1-2)

**Deliverables**:
- Whisper model integration (faster-whisper)
- Basic UI (upload audio, display transcript)
- Export to TXT format
- GPU acceleration with CPU fallback

**Implementation**:
1. Create `Core/whisper_service/whisper_api.py`
2. Implement `WhisperRuntimeManager.cs` (similar to PythonRuntimeManager)
3. Create `TranscriptionPage.xaml` with NavigationView tab
4. Basic transcription workflow (upload → process → display → export)

---

### Phase 3.2: Translation & Language Detection (Week 3)

**Deliverables**:
- Automatic language detection
- Translate-to-English mode
- Side-by-side original/translation view
- Language confidence display

**UI Mockup**:
```
┌─────────────────────────────────────────────┐
│ Detected Language: Spanish (98% confidence) │
│ [Auto-Translate to English] ☑              │
└─────────────────────────────────────────────┘
┌────────────────────┬────────────────────────┐
│ Original (Spanish) │ Translation (English)  │
├────────────────────┼────────────────────────┤
│ Hola, ¿cómo estás? │ Hello, how are you?    │
│ [00:00:01]         │ [00:00:01]             │
└────────────────────┴────────────────────────┘
```

---

### Phase 3.3: Speaker Diarization (Weeks 4-5)

**Deliverables**:
- Speaker segmentation (who spoke when)
- Speaker label assignment UI
- Color-coded transcript by speaker
- Export with speaker attribution

**Model**: pyannote.audio (open-source, MIT license)

**UI Features**:
- Auto-detect number of speakers (2-10)
- Manual speaker count override
- Assign names to speakers ("Speaker 1" → "John Doe")
- Waveform shows speaker segments with colors

---

### Phase 3.4: Advanced UI & Editing (Weeks 6-8)

**Deliverables**:
- Audio waveform visualization (NAudio)
- Synchronized playback (click word → jump to timestamp)
- Edit transcript inline (save corrected version)
- Confidence heatmap (highlight low-confidence words)
- SRT/VTT subtitle export

**Advanced Features**:
- Word-level timestamps (highlight current word during playback)
- Search transcript (jump to keyword)
- Adjust timestamp offsets (sync correction)
- Export video with burned-in subtitles

---

## Python Whisper Service

### Whisper Model Options

| Model | Size | Speed | Accuracy | Use Case |
|-------|------|-------|----------|----------|
| tiny | 75 MB | 32x realtime | Good | Quick drafts, low-end CPUs |
| base | 142 MB | 16x realtime | Better | Balanced performance |
| small | 466 MB | 6x realtime | Very Good | Recommended default |
| medium | 1.5 GB | 2x realtime | Excellent | High accuracy needed |
| large-v3 | 2.9 GB | 1x realtime | Best | Professional use, GPU required |

**Recommendation**: Bundle `small` by default, offer optional download for `medium`/`large`

### Backend Implementation

**Option 1: faster-whisper** (Recommended)

```python
# Core/whisper_service/whisper_api.py
from fastapi import FastAPI, File, UploadFile, Form
from faster_whisper import WhisperModel
from pydantic import BaseModel
from typing import List, Optional
import torch

app = FastAPI(title="GeoLens Whisper Service", version="0.1.0")

# Global model (loaded once, kept in memory)
_model: Optional[WhisperModel] = None

def get_model(model_size: str = "small", device: str = "auto"):
    global _model
    if _model is None:
        device_type = "cuda" if torch.cuda.is_available() else "cpu"
        compute_type = "float16" if device_type == "cuda" else "int8"
        _model = WhisperModel(model_size, device=device_type, compute_type=compute_type)
    return _model

class TranscriptionSegment(BaseModel):
    start: float
    end: float
    text: str
    confidence: float
    speaker: Optional[int] = None

class TranscriptionResponse(BaseModel):
    language: str
    language_confidence: float
    duration: float
    segments: List[TranscriptionSegment]
    full_text: str

@app.post("/transcribe", response_model=TranscriptionResponse)
async def transcribe(
    audio: UploadFile = File(...),
    model_size: str = Form("small"),
    language: Optional[str] = Form(None),
    translate: bool = Form(False)
):
    # Save uploaded file to temp
    temp_path = f"/tmp/{audio.filename}"
    with open(temp_path, "wb") as f:
        f.write(await audio.read())

    # Load model
    model = get_model(model_size)

    # Transcribe
    segments, info = model.transcribe(
        temp_path,
        language=language,
        task="translate" if translate else "transcribe",
        beam_size=5,
        word_timestamps=True
    )

    # Convert to response format
    result_segments = []
    full_text = []

    for segment in segments:
        result_segments.append(TranscriptionSegment(
            start=segment.start,
            end=segment.end,
            text=segment.text.strip(),
            confidence=segment.avg_logprob
        ))
        full_text.append(segment.text.strip())

    return TranscriptionResponse(
        language=info.language,
        language_confidence=info.language_probability,
        duration=info.duration,
        segments=result_segments,
        full_text=" ".join(full_text)
    )

@app.post("/identify-language")
async def identify_language(audio: UploadFile = File(...)):
    temp_path = f"/tmp/{audio.filename}"
    with open(temp_path, "wb") as f:
        f.write(await audio.read())

    model = get_model()
    _, info = model.transcribe(temp_path, language=None, max_initial_timestamp=30.0)

    return {
        "language": info.language,
        "confidence": info.language_probability,
        "all_probabilities": info.all_language_probs if hasattr(info, 'all_language_probs') else {}
    }
```

**Option 2: whisper.cpp** (Best for CPU inference)

- C++ implementation, 4x faster than Python
- Lower memory footprint
- Harder to integrate (requires C# P/Invoke or subprocess)
- **Use if**: Targeting low-end systems without GPU

---

### Speaker Diarization Service

```python
# Core/whisper_service/diarization.py
from pyannote.audio import Pipeline
from pyannote.audio.pipelines.utils.hook import ProgressHook
import torch

class SpeakerDiarizer:
    def __init__(self, device: str = "auto"):
        device_type = "cuda" if torch.cuda.is_available() else "cpu"
        self.pipeline = Pipeline.from_pretrained(
            "pyannote/speaker-diarization-3.1",
            use_auth_token="YOUR_HUGGINGFACE_TOKEN"  # Required for model download
        )
        self.pipeline.to(torch.device(device_type))

    def diarize(self, audio_path: str, num_speakers: int = None):
        # Run speaker diarization
        with ProgressHook() as hook:
            diarization = self.pipeline(
                audio_path,
                num_speakers=num_speakers,
                hook=hook
            )

        # Convert to segments
        segments = []
        for turn, _, speaker in diarization.itertracks(yield_label=True):
            segments.append({
                "start": turn.start,
                "end": turn.end,
                "speaker": speaker
            })

        return segments

# Add endpoint to whisper_api.py
@app.post("/diarize")
async def diarize(
    audio: UploadFile = File(...),
    num_speakers: Optional[int] = Form(None)
):
    temp_path = f"/tmp/{audio.filename}"
    with open(temp_path, "wb") as f:
        f.write(await audio.read())

    diarizer = SpeakerDiarizer()
    segments = diarizer.diarize(temp_path, num_speakers)

    return {"segments": segments}
```

**Note**: pyannote.audio requires a HuggingFace account and model agreement acceptance.

---

## C# Service Implementations

### WhisperRuntimeManager.cs

```csharp
public class WhisperRuntimeManager : IDisposable
{
    private Process? _whisperProcess;
    private readonly string _pythonExePath;
    private readonly int _port = 8900;

    public WhisperRuntimeManager(string pythonExePath)
    {
        _pythonExePath = pythonExePath;
    }

    public async Task<bool> StartServiceAsync(
        string modelSize = "small",
        IProgress<string>? progress = null)
    {
        progress?.Report("Starting Whisper service...");

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            Arguments = $"-m uvicorn Core.whisper_service.whisper_api:app --host 127.0.0.1 --port {_port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _whisperProcess = Process.Start(startInfo);

        // Wait for service to be ready
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    progress?.Report("Whisper service ready");
                    return true;
                }
            }
            catch { }

            await Task.Delay(1000);
            progress?.Report($"Waiting for service... ({i + 1}/30)");
        }

        return false;
    }

    public void Dispose()
    {
        if (_whisperProcess != null && !_whisperProcess.HasExited)
        {
            _whisperProcess.Kill();
            _whisperProcess.Dispose();
        }
    }
}
```

---

### WhisperApiClient.cs

```csharp
public class WhisperApiClient
{
    private readonly HttpClient _client;

    public WhisperApiClient(string baseUrl = "http://localhost:8900")
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(30) // Transcription can be slow
        };
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string audioPath,
        string modelSize = "small",
        string? language = null,
        bool translate = false,
        IProgress<int>? progress = null)
    {
        using var content = new MultipartFormDataContent();

        // Read audio file
        var audioBytes = await File.ReadAllBytesAsync(audioPath);
        content.Add(new ByteArrayContent(audioBytes), "audio", Path.GetFileName(audioPath));
        content.Add(new StringContent(modelSize), "model_size");

        if (language != null)
            content.Add(new StringContent(language), "language");

        content.Add(new StringContent(translate.ToString().ToLower()), "translate");

        // Post to /transcribe endpoint
        var response = await _client.PostAsync("/transcribe", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TranscriptionResult>(json)!;
    }

    public async Task<LanguageDetectionResult> DetectLanguageAsync(string audioPath)
    {
        using var content = new MultipartFormDataContent();
        var audioBytes = await File.ReadAllBytesAsync(audioPath);
        content.Add(new ByteArrayContent(audioBytes), "audio", Path.GetFileName(audioPath));

        var response = await _client.PostAsync("/identify-language", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LanguageDetectionResult>(json)!;
    }

    public async Task<DiarizationResult> DiarizeAsync(
        string audioPath,
        int? numSpeakers = null)
    {
        using var content = new MultipartFormDataContent();
        var audioBytes = await File.ReadAllBytesAsync(audioPath);
        content.Add(new ByteArrayContent(audioBytes), "audio", Path.GetFileName(audioPath));

        if (numSpeakers.HasValue)
            content.Add(new StringContent(numSpeakers.Value.ToString()), "num_speakers");

        var response = await _client.PostAsync("/diarize", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DiarizationResult>(json)!;
    }
}
```

---

### AudioExtractionService.cs

```csharp
public class AudioExtractionService
{
    // Reuse FFMpegService from video feature
    public static async Task<string> ExtractAudioFromVideoAsync(
        string videoPath,
        string? outputPath = null,
        IProgress<int>? progress = null)
    {
        outputPath ??= Path.Combine(
            Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(videoPath) + ".wav"
        );

        // Extract audio as 16kHz mono WAV (Whisper's expected format)
        await FFMpeg.Arguments
            .FromFileInput(videoPath)
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithAudioCodec("pcm_s16le")
                .WithAudioSamplingRate(16000)
                .WithCustomArgument("-ac 1")) // Mono
            .ProcessAsynchronously();

        return outputPath;
    }

    public static async Task<AudioMetadata> GetAudioMetadataAsync(string audioPath)
    {
        var info = await FFProbe.AnalyseAsync(audioPath);

        return new AudioMetadata
        {
            Duration = info.Duration,
            SampleRate = info.PrimaryAudioStream?.SampleRateHz ?? 0,
            Channels = info.PrimaryAudioStream?.Channels ?? 0,
            Codec = info.PrimaryAudioStream?.CodecName ?? "unknown",
            BitRate = info.PrimaryAudioStream?.BitRate ?? 0
        };
    }
}
```

---

### TranscriptionCacheService.cs

```csharp
public class TranscriptionCacheService
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public TranscriptionCacheService(string? cachePath = null)
    {
        cachePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeoLens",
            "transcription_cache.db"
        );

        _dbPath = cachePath;
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS transcriptions (
                audio_hash TEXT PRIMARY KEY,
                audio_path TEXT NOT NULL,
                model_size TEXT NOT NULL,
                language TEXT,
                was_translated INTEGER,
                full_text TEXT,
                segments_json TEXT,
                created_at TEXT,
                access_count INTEGER DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_audio_path ON transcriptions(audio_path);
            CREATE INDEX IF NOT EXISTS idx_created_at ON transcriptions(created_at);
        ";

        using var cmd = new SqliteCommand(createTableSql, _connection);
        cmd.ExecuteNonQuery();
    }

    public async Task<TranscriptionResult?> GetCachedTranscriptionAsync(
        string audioPath,
        string modelSize = "small")
    {
        var hash = await ComputeAudioHashAsync(audioPath);
        var cacheKey = $"{hash}_{modelSize}";

        var sql = "SELECT * FROM transcriptions WHERE audio_hash = @hash";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@hash", cacheKey);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            // Increment access count
            await IncrementAccessCountAsync(cacheKey);

            // Deserialize segments
            var segmentsJson = reader.GetString(6);
            var segments = JsonSerializer.Deserialize<List<TranscriptionSegment>>(segmentsJson);

            return new TranscriptionResult
            {
                Language = reader.GetString(3),
                FullText = reader.GetString(5),
                Segments = segments!
            };
        }

        return null;
    }

    public async Task StoreTranscriptionAsync(
        string audioPath,
        string modelSize,
        TranscriptionResult result)
    {
        var hash = await ComputeAudioHashAsync(audioPath);
        var cacheKey = $"{hash}_{modelSize}";

        var sql = @"
            INSERT OR REPLACE INTO transcriptions
            (audio_hash, audio_path, model_size, language, full_text, segments_json, created_at)
            VALUES (@hash, @path, @model, @lang, @text, @segments, @created)
        ";

        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@hash", cacheKey);
        cmd.Parameters.AddWithValue("@path", audioPath);
        cmd.Parameters.AddWithValue("@model", modelSize);
        cmd.Parameters.AddWithValue("@lang", result.Language);
        cmd.Parameters.AddWithValue("@text", result.FullText);
        cmd.Parameters.AddWithValue("@segments", JsonSerializer.Serialize(result.Segments));
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> ComputeAudioHashAsync(string audioPath)
    {
        using var stream = File.OpenRead(audioPath);
        var hash = await XXHash64.HashAsync(stream);
        return hash.ToString("X");
    }
}
```

---

### TranscriptExportService.cs

```csharp
public class TranscriptExportService
{
    public async Task ExportToTxtAsync(TranscriptionResult result, string outputPath)
    {
        var lines = result.Segments.Select(s => s.Text);
        await File.WriteAllLinesAsync(outputPath, lines);
    }

    public async Task ExportToSrtAsync(TranscriptionResult result, string outputPath)
    {
        var srt = new StringBuilder();
        int index = 1;

        foreach (var segment in result.Segments)
        {
            srt.AppendLine(index.ToString());
            srt.AppendLine($"{FormatSrtTimestamp(segment.Start)} --> {FormatSrtTimestamp(segment.End)}");
            srt.AppendLine(segment.Text);
            srt.AppendLine();
            index++;
        }

        await File.WriteAllTextAsync(outputPath, srt.ToString());
    }

    public async Task ExportToVttAsync(TranscriptionResult result, string outputPath)
    {
        var vtt = new StringBuilder("WEBVTT\n\n");

        foreach (var segment in result.Segments)
        {
            vtt.AppendLine($"{FormatVttTimestamp(segment.Start)} --> {FormatVttTimestamp(segment.End)}");
            vtt.AppendLine(segment.Text);
            vtt.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, vtt.ToString());
    }

    public async Task ExportToJsonAsync(TranscriptionResult result, string outputPath)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json);
    }

    private string FormatSrtTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }

    private string FormatVttTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }
}
```

---

## UI Implementation

### TranscriptionPage.xaml

```xml
<Page x:Class="GeoLens.Views.TranscriptionPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="12" Padding="16">
            <Button Content="Add Audio/Video" Click="AddAudio_Click">
                <Button.Icon>
                    <FontIcon Glyph="&#xE710;"/>
                </Button.Icon>
            </Button>

            <Button Content="Transcribe" Click="Transcribe_Click" Style="{StaticResource AccentButtonStyle}">
                <Button.Icon>
                    <FontIcon Glyph="&#xE8B1;"/>
                </Button.Icon>
            </Button>

            <AppBarSeparator/>

            <ComboBox x:Name="ModelSizeComboBox" Header="Model" SelectedIndex="1" Width="150">
                <ComboBoxItem Content="Tiny (Fast)"/>
                <ComboBoxItem Content="Small (Balanced)"/>
                <ComboBoxItem Content="Medium (Accurate)"/>
            </ComboBox>

            <ToggleSwitch x:Name="TranslateToggle" Header="Auto-translate to English" OffContent="Off" OnContent="On"/>

            <AppBarSeparator/>

            <Button Content="Export" Click="Export_Click">
                <Button.Icon>
                    <FontIcon Glyph="&#xE74E;"/>
                </Button.Icon>
            </Button>
        </StackPanel>

        <!-- Main Content -->
        <Grid Grid.Row="1" Padding="16">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="300"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Audio Queue (Left Panel) -->
            <Grid Grid.Column="0">
                <ListView x:Name="AudioQueueListView"
                          ItemsSource="{x:Bind AudioQueue}"
                          SelectionMode="Single"
                          SelectionChanged="AudioQueue_SelectionChanged">
                    <ListView.ItemTemplate>
                        <DataTemplate x:DataType="local:AudioQueueItem">
                            <Grid Padding="8" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                                  CornerRadius="8" Margin="0,4">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>

                                <!-- Icon -->
                                <FontIcon Grid.Column="0" Glyph="&#xE8D6;" FontSize="32" Margin="0,0,12,0"/>

                                <!-- Info -->
                                <StackPanel Grid.Column="1">
                                    <TextBlock Text="{x:Bind FileName}" FontWeight="SemiBold"/>
                                    <TextBlock Text="{x:Bind DurationFormatted}" Opacity="0.7" FontSize="12"/>
                                    <TextBlock Text="{x:Bind StatusText}" Opacity="0.7" FontSize="12"/>
                                </StackPanel>
                            </Grid>
                        </DataTemplate>
                    </ListView.ItemTemplate>
                </ListView>
            </Grid>

            <!-- Transcript View (Right Panel) -->
            <Grid Grid.Column="1" Margin="16,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="120"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- Language Detection Info -->
                <Grid Grid.Row="0" Padding="12" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                      CornerRadius="8" Margin="0,0,0,16" Visibility="{x:Bind HasTranscription}">
                    <StackPanel Orientation="Horizontal" Spacing="16">
                        <TextBlock>
                            <Run Text="Detected Language:"/>
                            <Run Text="{x:Bind DetectedLanguage}" FontWeight="Bold"/>
                            <Run Text=" "/>
                            <Run Text="{x:Bind LanguageConfidence}" Opacity="0.7"/>
                        </TextBlock>
                    </StackPanel>
                </Grid>

                <!-- Waveform Visualization -->
                <Border Grid.Row="1" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                        CornerRadius="8" Margin="0,0,0,16">
                    <Canvas x:Name="WaveformCanvas" Background="Transparent"/>
                </Border>

                <!-- Transcript Editor -->
                <Grid Grid.Row="2">
                    <RichEditBox x:Name="TranscriptEditor"
                                 PlaceholderText="Transcript will appear here after processing..."
                                 AcceptsReturn="True"
                                 TextWrapping="Wrap"
                                 BorderThickness="1"
                                 CornerRadius="8"
                                 Padding="12"/>
                </Grid>
            </Grid>
        </Grid>
    </Grid>
</Page>
```

---

## Export Formats

### SRT (SubRip Subtitle) Example

```
1
00:00:00,000 --> 00:00:02,500
Hello, welcome to GeoLens.

2
00:00:02,500 --> 00:00:05,800
This is a demonstration of the Whisper transcription feature.

3
00:00:05,800 --> 00:00:09,200
It supports 99 languages and runs completely offline.
```

### VTT (WebVTT) Example

```
WEBVTT

00:00:00.000 --> 00:00:02.500
Hello, welcome to GeoLens.

00:00:02.500 --> 00:00:05.800
This is a demonstration of the Whisper transcription feature.

00:00:05.800 --> 00:00:09.200
It supports 99 languages and runs completely offline.
```

### JSON Example

```json
{
  "language": "en",
  "language_confidence": 0.98,
  "duration": 120.5,
  "full_text": "Hello, welcome to GeoLens. This is a demonstration...",
  "segments": [
    {
      "start": 0.0,
      "end": 2.5,
      "text": "Hello, welcome to GeoLens.",
      "confidence": 0.95,
      "speaker": 1
    },
    {
      "start": 2.5,
      "end": 5.8,
      "text": "This is a demonstration of the Whisper transcription feature.",
      "confidence": 0.92,
      "speaker": 1
    }
  ]
}
```

---

## Distribution Impact

### Model Sizes

**Whisper Models** (bundled in installer):
- tiny.pt: 75 MB
- base.pt: 142 MB
- small.pt: 466 MB
- **Recommended**: Bundle `small`, offer download for `medium`/`large`

**Pyannote Models** (for speaker diarization):
- speaker-diarization-3.1: ~500 MB
- Requires HuggingFace token for download

**Total Impact**:
- Base feature (small model): +466 MB
- With diarization: +966 MB
- With all models: +5 GB (not recommended)

### Python Dependencies

Add to `requirements-whisper.txt`:
```
faster-whisper==1.0.0
pyannote.audio==3.1.0
onnxruntime==1.16.0  # For faster inference
noisereduce==3.0.0   # Optional: noise reduction preprocessing
```

---

## Testing Strategy

### Test Audio Files

1. **Short English (10s)**: Clear single speaker
2. **Long Podcast (30min)**: Multiple speakers
3. **Non-English (Spanish)**: Translation test
4. **Noisy Audio**: Background music/traffic
5. **Multiple Speakers**: Meeting recording (3-5 people)
6. **Low Quality**: Phone call recording (8kHz)
7. **Video File**: Extract audio from MP4

### Test Cases

- [ ] Transcribe short English audio (small model)
- [ ] Transcribe 30-minute podcast
- [ ] Auto-detect language (Spanish → "Spanish, 98%")
- [ ] Translate Spanish to English
- [ ] Speaker diarization (2 speakers)
- [ ] Export SRT subtitles
- [ ] Export VTT subtitles
- [ ] Export JSON with full metadata
- [ ] Cache hit (re-transcribe same file → instant result)
- [ ] Video audio extraction
- [ ] Handle corrupted audio file gracefully

---

## Performance Benchmarks

**Hardware**: RTX 3060 (12GB VRAM), i7-12700K

| Model | Audio Length | Processing Time | Realtime Factor |
|-------|--------------|-----------------|-----------------|
| tiny (GPU) | 10 min | 30 sec | 20x |
| small (GPU) | 10 min | 90 sec | 6.7x |
| medium (GPU) | 10 min | 4 min | 2.5x |
| small (CPU) | 10 min | 15 min | 0.67x |

**Recommendation**: Default to `small` model, suggest GPU for `medium`/`large`

---

## User Workflows

### Workflow 1: OSINT Interview Analysis

1. **Upload**: Drag interview recording (Russian language)
2. **Detect**: GeoLens detects "Russian (96% confidence)"
3. **Translate**: Enable "Auto-translate to English"
4. **Transcribe**: Process with `small` model
5. **Review**: Read side-by-side Russian/English transcript
6. **Export**: Export SRT for video overlay
7. **Geolocation**: Extract mentioned locations and search in main GeoLens screen

### Workflow 2: Podcast Speaker Attribution

1. **Upload**: 45-minute podcast with 3 hosts
2. **Diarize**: Auto-detect 3 speakers
3. **Label**: Assign names ("Host 1" → "John", "Host 2" → "Sarah", "Host 3" → "Mike")
4. **Transcribe**: Color-coded transcript by speaker
5. **Export**: JSON with speaker attribution
6. **Search**: Find all segments where "Sarah" spoke

### Workflow 3: Subtitle Generation

1. **Upload**: Travel vlog MP4 (1920x1080, 10 minutes)
2. **Extract Audio**: GeoLens extracts audio track
3. **Transcribe**: Generate word-level timestamps
4. **Edit**: Fix misheard words in editor
5. **Export**: SRT subtitles
6. **Burn-In**: Use FFmpeg to overlay subtitles on video

---

## Implementation Checklist

### Phase 3.1: Basic Transcription (Weeks 1-2)

- [ ] Install faster-whisper Python package
- [ ] Implement `whisper_api.py` with /transcribe endpoint
- [ ] Create `WhisperRuntimeManager.cs`
- [ ] Create `WhisperApiClient.cs`
- [ ] Build `TranscriptionPage.xaml` UI
- [ ] Add "Transcription" tab to NavigationView
- [ ] Implement audio file picker
- [ ] Display transcript in RichEditBox
- [ ] Export to TXT format

### Phase 3.2: Translation & Language Detection (Week 3)

- [ ] Add /identify-language endpoint
- [ ] Implement language detection UI
- [ ] Add auto-translate toggle
- [ ] Side-by-side translation view
- [ ] Cache translated transcripts separately

### Phase 3.3: Speaker Diarization (Weeks 4-5)

- [ ] Install pyannote.audio Python package
- [ ] Implement /diarize endpoint
- [ ] Merge diarization segments with transcript
- [ ] Color-code transcript by speaker
- [ ] Speaker label assignment UI
- [ ] Export with speaker attribution

### Phase 3.4: Advanced UI (Weeks 6-8)

- [ ] Integrate NAudio for waveform visualization
- [ ] Implement audio playback with sync
- [ ] Word-level timestamp highlighting
- [ ] Transcript search functionality
- [ ] Export SRT/VTT subtitles
- [ ] Burn-in subtitles to video (FFmpeg)
- [ ] Noise reduction preprocessing (noisereduce)

---

## Open Questions

1. **Model Selection**: Bundle multiple model sizes or download on demand?
   - **Recommendation**: Bundle `small` (466MB), offer download for `medium`

2. **HuggingFace Token**: How to handle pyannote.audio model download?
   - **Recommendation**: Prompt user for token on first use, store encrypted

3. **Real-time Transcription**: Support live audio input (microphone)?
   - **Recommendation**: Phase 4 feature (requires streaming implementation)

4. **GPU Memory**: How to handle low-VRAM GPUs (4GB)?
   - **Recommendation**: Auto-fallback to `tiny` or `base` model on low VRAM

5. **Subtitle Editing**: Allow word-level timestamp adjustment?
   - **Recommendation**: Phase 3.4 (advanced feature)

---

## Related Documentation

- `15_Video_Frame_Extraction.md`: Video processing integration
- `02_Service_Implementations.md`: Service architecture patterns
- `09_Testing_Strategy.md`: Audio processing test cases

---

## Dependencies

**NuGet Packages**:
- `FFMpegCore` (5.1.0) - Audio extraction from video
- `NAudio` (2.2.1) - Waveform visualization and audio playback
- `System.Data.SQLite.Core` (1.0.118) - Transcription caching

**Python Packages** (`requirements-whisper.txt`):
- `faster-whisper` (1.0.0) - Whisper inference
- `pyannote.audio` (3.1.0) - Speaker diarization
- `onnxruntime` (1.16.0) - Optimized inference
- `noisereduce` (3.0.0) - Audio preprocessing

**Models**:
- Whisper `small`: 466 MB (bundled)
- pyannote.audio: 500 MB (download on first use)

---

**Last Updated**: 2025-01-12
**Status**: Specification Complete, Implementation Pending (Phase 3)
