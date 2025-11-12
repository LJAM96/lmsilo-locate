# GeoLens Web Demo

A Flask-based web demo to visualize GeoLens predictions on interactive maps before building the full WinUI3 desktop application.

## Features

- **Dual Visualization Modes**
  - 2D map with dark theme (Leaflet.js + CartoDB Dark tiles)
  - 3D interactive globe (globe.gl)

- **Image Upload & Analysis**
  - Drag & drop or click to upload
  - Integrates with FastAPI backend for real predictions
  - Demo mode with sample data (no backend required)

- **Confidence Color Coding**
  - 🟢 High (>10% probability) - Green
  - 🟡 Medium (5-10% probability) - Yellow
  - 🔴 Low (<5% probability) - Red

- **Interactive Features**
  - Click predictions to focus on map
  - Hover over markers for details
  - Toggle between 2D/3D views
  - Auto-rotating globe
  - Dark theme throughout

## Quick Start

### Option 1: Demo Mode (No Backend Required)

1. Install Flask dependencies:
```bash
cd web_demo
pip install -r requirements.txt
```

2. Run the Flask server:
```bash
python app.py
```

3. Open your browser to: http://localhost:5000

4. Click "Load Demo Data" to see sample predictions on the map

### Option 2: Full Mode (With FastAPI Backend)

1. Start the FastAPI backend (in a separate terminal):
```bash
cd /home/user/geolens
uvicorn Core.api_service:app --reload --port 8899
```

2. Install Flask dependencies:
```bash
cd web_demo
pip install -r requirements.txt
```

3. Run the Flask server:
```bash
python app.py
```

4. Open your browser to: http://localhost:5000

5. Upload an image to get real predictions from GeoCLIP!

## Usage

### Demo Mode
- Click **"Load Demo Data"** to see sample predictions across major cities
- Toggle between **2D Map** and **3D Globe** views
- Click on prediction items in the sidebar to focus the map

### Upload Mode (Backend Required)
1. Upload an image using drag & drop or file picker
2. Adjust "Predictions to show" (1-50, default 10)
3. Click **"Analyze Image"**
4. View predictions on the map with confidence levels
5. Click predictions to zoom and see details

## Architecture

```
┌─────────────────────────────────────┐
│   Flask Web Server (port 5000)     │
│   - Image upload handling           │
│   - HTML template rendering         │
│   - Static file serving             │
└──────────────┬──────────────────────┘
               │ HTTP requests
┌──────────────▼──────────────────────┐
│  FastAPI Backend (port 8899)        │
│  - GeoCLIP inference                │
│  - Reverse geocoding                │
│  - /health and /infer endpoints     │
└─────────────────────────────────────┘
```

## File Structure

```
web_demo/
├── app.py                 # Flask application
├── requirements.txt       # Python dependencies
├── templates/
│   └── index.html        # Main page template
├── static/
│   ├── css/
│   │   └── style.css     # Dark theme styles
│   ├── js/
│   │   └── app.js        # Map interactions & API calls
│   └── uploads/          # Uploaded images (auto-created)
└── README.md             # This file
```

## Technologies

- **Backend**: Flask 3.0+
- **2D Maps**: Leaflet.js with CartoDB Dark Matter tiles
- **3D Globe**: globe.gl + Three.js
- **Styling**: Custom dark theme CSS
- **Image Processing**: Pillow

## Troubleshooting

### Backend not connecting
- Make sure FastAPI is running on port 8899
- Check the status indicator in the top-right (should be green when connected)
- Demo mode works without backend - click "Load Demo Data"

### Maps not loading
- Check browser console for errors
- Ensure you have internet connection (for map tiles and CDN libraries)
- Try refreshing the page

### Upload not working
- Supported formats: PNG, JPG, JPEG, GIF, BMP, WEBP
- Max file size: 16MB
- Backend must be running for analysis

## Next Steps

Once you're happy with the visualizations:
1. These map styles can be integrated into the WinUI3 app using WebView2
2. The confidence color coding will be used throughout the UI
3. The 3D globe can be embedded in the main desktop application
4. The prediction layout can inform the final UI design

## Development

To modify the demo:
- Edit `templates/index.html` for structure changes
- Edit `static/css/style.css` for styling
- Edit `static/js/app.js` for map behavior
- Edit `app.py` for backend routes

All changes auto-reload with Flask's debug mode.
