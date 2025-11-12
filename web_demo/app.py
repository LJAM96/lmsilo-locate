"""
Flask Web Demo for GeoLens Map Visualizations
Allows users to upload images and see predictions on interactive maps
"""
import os
from pathlib import Path
from flask import Flask, render_template, request, jsonify, url_for
import requests
from werkzeug.utils import secure_filename
import base64
from io import BytesIO
from PIL import Image

app = Flask(__name__)
app.config['UPLOAD_FOLDER'] = Path(__file__).parent / 'static' / 'uploads'
app.config['MAX_CONTENT_LENGTH'] = 16 * 1024 * 1024  # 16MB max file size
app.config['ALLOWED_EXTENSIONS'] = {'png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'}

# Ensure upload folder exists
app.config['UPLOAD_FOLDER'].mkdir(parents=True, exist_ok=True)

# FastAPI backend URL (assumes it's running)
BACKEND_URL = 'http://localhost:8899'


def allowed_file(filename):
    """Check if file extension is allowed"""
    return '.' in filename and filename.rsplit('.', 1)[1].lower() in app.config['ALLOWED_EXTENSIONS']


def get_confidence_level(probability):
    """Classify prediction confidence into levels"""
    if probability > 0.1:
        return {'level': 'high', 'color': '#00ff88', 'label': 'High'}
    elif probability >= 0.05:
        return {'level': 'medium', 'color': '#ffcc00', 'label': 'Medium'}
    else:
        return {'level': 'low', 'color': '#ff4444', 'label': 'Low'}


@app.route('/')
def index():
    """Main page with map visualization"""
    return render_template('index.html')


@app.route('/health')
def health():
    """Check if backend is running"""
    try:
        response = requests.get(f'{BACKEND_URL}/health', timeout=2)
        return jsonify({
            'status': 'ok',
            'backend': response.json()
        })
    except Exception as e:
        return jsonify({
            'status': 'error',
            'message': f'Backend not available: {str(e)}'
        }), 503


@app.route('/upload', methods=['POST'])
def upload_image():
    """Handle image upload and get predictions"""
    if 'image' not in request.files:
        return jsonify({'error': 'No image file provided'}), 400

    file = request.files['image']

    if file.filename == '':
        return jsonify({'error': 'No file selected'}), 400

    if not allowed_file(file.filename):
        return jsonify({'error': 'Invalid file type'}), 400

    try:
        # Save uploaded file
        filename = secure_filename(file.filename)
        filepath = app.config['UPLOAD_FOLDER'] / filename
        file.save(filepath)

        # Read image and convert to base64 for API
        with open(filepath, 'rb') as f:
            image_data = base64.b64encode(f.read()).decode('utf-8')

        # Get top_k from request (default 10)
        top_k = int(request.form.get('top_k', 10))

        # Call FastAPI backend
        response = requests.post(
            f'{BACKEND_URL}/infer',
            json={
                'image_base64': image_data,
                'top_k': top_k
            },
            timeout=30
        )

        if response.status_code != 200:
            return jsonify({'error': f'Backend error: {response.text}'}), 500

        result = response.json()

        # Enhance predictions with confidence levels and URLs
        predictions = []
        for pred in result['predictions']:
            confidence = get_confidence_level(pred['probability'])
            predictions.append({
                'latitude': pred['latitude'],
                'longitude': pred['longitude'],
                'probability': pred['probability'],
                'city': pred.get('city', 'Unknown'),
                'state': pred.get('state', ''),
                'country': pred.get('country', 'Unknown'),
                'confidence': confidence
            })

        return jsonify({
            'success': True,
            'image_url': url_for('static', filename=f'uploads/{filename}'),
            'predictions': predictions,
            'metadata': result.get('metadata', {})
        })

    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/demo')
def demo():
    """Demo page with sample predictions (no backend needed)"""
    sample_predictions = [
        {
            'latitude': 48.8566,
            'longitude': 2.3522,
            'probability': 0.25,
            'city': 'Paris',
            'state': 'Île-de-France',
            'country': 'France',
            'confidence': get_confidence_level(0.25)
        },
        {
            'latitude': 51.5074,
            'longitude': -0.1278,
            'probability': 0.18,
            'city': 'London',
            'state': 'England',
            'country': 'United Kingdom',
            'confidence': get_confidence_level(0.18)
        },
        {
            'latitude': 40.7128,
            'longitude': -74.0060,
            'probability': 0.12,
            'city': 'New York',
            'state': 'New York',
            'country': 'United States',
            'confidence': get_confidence_level(0.12)
        },
        {
            'latitude': 35.6762,
            'longitude': 139.6503,
            'probability': 0.08,
            'city': 'Tokyo',
            'state': 'Tokyo',
            'country': 'Japan',
            'confidence': get_confidence_level(0.08)
        },
        {
            'latitude': -33.8688,
            'longitude': 151.2093,
            'probability': 0.04,
            'city': 'Sydney',
            'state': 'New South Wales',
            'country': 'Australia',
            'confidence': get_confidence_level(0.04)
        }
    ]

    return render_template('index.html', demo_predictions=sample_predictions)


if __name__ == '__main__':
    print("=" * 60)
    print("GeoLens Web Demo")
    print("=" * 60)
    print(f"Starting Flask server on http://localhost:5000")
    print(f"Make sure FastAPI backend is running on {BACKEND_URL}")
    print("=" * 60)
    app.run(debug=True, port=5000)
