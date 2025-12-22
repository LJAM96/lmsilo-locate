"""
Generates an HTML report of GeoCLIP predictions for a set of images.
"""
import base64
import io
import sys
import time
from pathlib import Path
from typing import List

from jinja2 import Template
from PIL import Image

# This block allows the script to be run as a module with relative imports
# or handles path setup if run directly (though running as module is preferred).
try:
    from .llocale import (
        GeoClipPredictor,
        InputRecord,
        PredictionOutcome,
        load_records_from_path,
        normalise_extensions,
    )
except ImportError:
    # If running directly from backend/ without -m
    sys.path.append(str(Path(__file__).parent.parent))
    from backend.llocale import (
        GeoClipPredictor,
        InputRecord,
        PredictionOutcome,
        load_records_from_path,
        normalise_extensions,
    )

HTML_TEMPLATE = """
<!DOCTYPE html>
<html>
<head>
    <title>GeoLens Prediction Report</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 2rem; color: #333; background-color: #f4f4f9; }
        .container { max-width: 900px; margin: 0 auto; background: white; padding: 2rem; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
        h1 { border-bottom: 2px solid #eee; padding-bottom: 0.5rem; color: #2c3e50; }
        .timestamp { color: #888; font-style: italic; margin-bottom: 2rem; }
        .entry { display: flex; margin-bottom: 2rem; border: 1px solid #e0e0e0; padding: 1.5rem; border-radius: 8px; background: #fff; gap: 1.5rem; }
        .thumbnail { flex: 0 0 250px; display: flex; align-items: flex-start; justify-content: center; background: #fafafa; border-radius: 4px; overflow: hidden; border: 1px solid #eee; }
        .thumbnail img { max-width: 100%; height: auto; display: block; }
        .details { flex: 1; }
        h2 { margin-top: 0; font-size: 1.1rem; color: #0056b3; margin-bottom: 0.5rem; word-break: break-all; }
        table { width: 100%; border-collapse: collapse; margin-top: 1rem; font-size: 0.9rem; }
        th, td { text-align: left; padding: 10px; border-bottom: 1px solid #eee; }
        th { background-color: #f8f9fa; font-weight: 600; color: #555; }
        tr:last-child td { border-bottom: none; }
        .rank-col { width: 50px; text-align: center; }
        .prob-col { width: 80px; text-align: right; }
        td.prob-val { text-align: right; font-family: monospace; }
        td.coords { font-family: monospace; color: #555; }
        .meta { color: #666; font-size: 0.85rem; margin-bottom: 0.5rem; }
    </style>
</head>
<body>
    <div class="container">
        <h1>GeoLens Prediction Report</h1>
        <div class="timestamp">Generated on {{ date }}</div>

        {% for item in items %}
        <div class="entry">
            <div class="thumbnail">
                <img src="data:image/jpeg;base64,{{ item.image_b64 }}" alt="Thumbnail">
            </div>
            <div class="details">
                <h2>{{ item.filename }}</h2>
                <div class="meta">Path: {{ item.path }}</div>
                <table>
                    <thead>
                        <tr>
                            <th class="rank-col">#</th>
                            <th>Location</th>
                            <th>Coordinates</th>
                            <th class="prob-col">Prob.</th>
                        </tr>
                    </thead>
                    <tbody>
                        {% for pred in item.predictions %}
                        <tr>
                            <td class="rank-col">{{ pred.rank }}</td>
                            <td>{{ pred.location_summary }}</td>
                            <td class="coords">{{ "%.5f"|format(pred.latitude) }}, {{ "%.5f"|format(pred.longitude) }}</td>
                            <td class="prob-val">{{ "%.2f"|format(pred.probability * 100) }}%</td>
                        </tr>
                        {% endfor %}
                    </tbody>
                </table>
            </div>
        </div>
        {% endfor %}
    </div>
</body>
</html>
"""

def create_thumbnail_b64(image_path: Path, size=(400, 400)) -> str:
    try:
        with Image.open(image_path) as img:
            img.thumbnail(size)
            if img.mode != "RGB":
                img = img.convert("RGB")
            
            buffer = io.BytesIO()
            img.save(buffer, format="JPEG", quality=85)
            return base64.b64encode(buffer.getvalue()).decode("utf-8")
    except Exception as e:
        print(f"Error processing image {image_path}: {e}", file=sys.stderr)
        return ""

def main():
    # Determine directories relative to this script
    base_dir = Path(__file__).parent
    input_dir = base_dir / "Test"
    output_file = base_dir / "prediction_report.html"
    
    print(f"Input Directory: {input_dir}")
    print(f"Output File: {output_file}")

    print("Loading GeoCLIP model...")
    predictor = GeoClipPredictor(device="auto")
    
    # Load records
    records = load_records_from_path(
        input_dir,
        delimiter=",",
        path_column="path",
        md5_column="md5",
        recursive=False,
        allowed_extensions=normalise_extensions(".jpg,.jpeg,.png,.webp")
    )
    
    # Filter out specific file
    filtered_records = [
        r for r in records 
        if r.path.name != "1-formby-1-1200px.png"
    ]
    
    items = []
    total = len(filtered_records)
    print(f"Found {len(records)} images. Processing {total} (filtered)...")
    
    # Run predictions
    outcomes = predictor.predict_records(filtered_records, top_k=5, skip_missing=True)
    
    for i, outcome in enumerate(outcomes):
        if not outcome.success:
            print(f"Skipping failed or missing: {outcome.record.path}")
            continue
            
        print(f"[{i+1}/{total}] Predicted: {outcome.record.path.name}")
        
        b64_img = create_thumbnail_b64(outcome.record.path)
        
        items.append({
            "filename": outcome.record.path.name,
            "path": str(outcome.record.path),
            "image_b64": b64_img,
            "predictions": outcome.predictions
        })

    print("Rendering HTML report...")
    template = Template(HTML_TEMPLATE)
    html_content = template.render(
        date=time.strftime("%A, %d %B %Y at %H:%M"),
        items=items
    )
    
    with open(output_file, "w", encoding="utf-8") as f:
        f.write(html_content)
        
    print("Done!")

if __name__ == "__main__":
    main()
