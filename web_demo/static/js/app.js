/**
 * GeoLens Web Demo - Main Application
 */

(function() {
    'use strict';

    // App State
    const state = {
        map2d: null,
        globe3d: null,
        currentView: '2d',
        predictions: [],
        markers: [],
        selectedFile: null
    };

    // Initialize app
    function init() {
        setupEventListeners();
        initMap2D();
        initGlobe3D();
        checkBackendStatus();
    }

    // Setup all event listeners
    function setupEventListeners() {
        const uploadZone = document.getElementById('uploadZone');
        const imageInput = document.getElementById('imageInput');
        const analyzeBtn = document.getElementById('analyzeBtn');
        const demoBtn = document.getElementById('demoBtn');

        // Upload zone click
        uploadZone.addEventListener('click', () => imageInput.click());

        // File input change
        imageInput.addEventListener('change', handleFileSelect);

        // Drag and drop
        uploadZone.addEventListener('dragover', handleDragOver);
        uploadZone.addEventListener('dragleave', handleDragLeave);
        uploadZone.addEventListener('drop', handleDrop);

        // Analyze button
        analyzeBtn.addEventListener('click', analyzeImage);

        // Demo button
        demoBtn.addEventListener('click', loadDemoData);

        // View toggle buttons
        document.querySelectorAll('.toggle-btn').forEach(btn => {
            btn.addEventListener('click', () => switchView(btn.dataset.view));
        });
    }

    // Initialize 2D map with Leaflet
    function initMap2D() {
        state.map2d = L.map('map2d', {
            center: [20, 0],
            zoom: 2,
            minZoom: 2,
            maxZoom: 18
        });

        // Use dark tile layer (CartoDB Dark Matter)
        L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
            subdomains: 'abcd',
            maxZoom: 20
        }).addTo(state.map2d);
    }

    // Initialize 3D globe
    function initGlobe3D() {
        const container = document.getElementById('globe3d');

        state.globe3d = Globe()
            (container)
            .globeImageUrl('https://unpkg.com/three-globe@2.30.0/example/img/earth-blue-marble.jpg')
            .bumpImageUrl('https://unpkg.com/three-globe@2.30.0/example/img/earth-topology.png')
            .backgroundImageUrl('https://unpkg.com/three-globe@2.30.0/example/img/night-sky.png')
            .backgroundColor('rgba(0,0,0,0)')
            .pointsData([])
            .pointAltitude(0.01)
            .pointRadius(0.5)
            .pointColor(d => d.color)
            .pointLabel(d => d.label)
            .onPointClick(d => {
                if (d.onClick) d.onClick();
            })
            .atmosphereColor('#00d4ff')
            .atmosphereAltitude(0.15);

        // Rotate globe slowly
        state.globe3d.controls().autoRotate = true;
        state.globe3d.controls().autoRotateSpeed = 0.5;
    }

    // Switch between 2D and 3D views
    function switchView(view) {
        state.currentView = view;

        // Update button states
        document.querySelectorAll('.toggle-btn').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.view === view);
        });

        // Update view visibility
        document.querySelectorAll('.map-view').forEach(mapView => {
            mapView.classList.remove('active');
        });

        if (view === '2d') {
            document.getElementById('map2d').classList.add('active');
            // Invalidate size to fix rendering issues
            setTimeout(() => state.map2d.invalidateSize(), 100);
        } else {
            document.getElementById('globe3d').classList.add('active');
        }
    }

    // File handling
    function handleFileSelect(e) {
        const file = e.target.files[0];
        if (file) {
            processFile(file);
        }
    }

    function handleDragOver(e) {
        e.preventDefault();
        e.currentTarget.classList.add('drag-over');
    }

    function handleDragLeave(e) {
        e.currentTarget.classList.remove('drag-over');
    }

    function handleDrop(e) {
        e.preventDefault();
        e.currentTarget.classList.remove('drag-over');

        const file = e.dataTransfer.files[0];
        if (file && file.type.startsWith('image/')) {
            processFile(file);
        }
    }

    function processFile(file) {
        state.selectedFile = file;

        // Show preview
        const reader = new FileReader();
        reader.onload = (e) => {
            const preview = document.getElementById('imagePreview');
            const previewImg = document.getElementById('previewImg');
            previewImg.src = e.target.result;
            preview.style.display = 'block';
        };
        reader.readAsDataURL(file);

        // Enable analyze button
        document.getElementById('analyzeBtn').disabled = false;
    }

    // Analyze image via backend
    async function analyzeImage() {
        if (!state.selectedFile) return;

        const formData = new FormData();
        formData.append('image', state.selectedFile);
        formData.append('top_k', document.getElementById('topK').value);

        showLoading(true);

        try {
            const response = await fetch('/upload', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                throw new Error(`Server error: ${response.statusText}`);
            }

            const data = await response.json();

            if (data.error) {
                throw new Error(data.error);
            }

            displayPredictions(data.predictions, data.image_url);

        } catch (error) {
            console.error('Error analyzing image:', error);
            alert(`Failed to analyze image: ${error.message}\n\nMake sure the FastAPI backend is running on port 8899.`);
        } finally {
            showLoading(false);
        }
    }

    // Load demo data
    async function loadDemoData() {
        const demoData = [
            {
                latitude: 48.8566,
                longitude: 2.3522,
                probability: 0.25,
                city: 'Paris',
                state: 'Île-de-France',
                country: 'France',
                confidence: {level: 'high', color: '#00ff88', label: 'High'}
            },
            {
                latitude: 51.5074,
                longitude: -0.1278,
                probability: 0.18,
                city: 'London',
                state: 'England',
                country: 'United Kingdom',
                confidence: {level: 'high', color: '#00ff88', label: 'High'}
            },
            {
                latitude: 40.7128,
                longitude: -74.0060,
                probability: 0.12,
                city: 'New York',
                state: 'New York',
                country: 'United States',
                confidence: {level: 'high', color: '#00ff88', label: 'High'}
            },
            {
                latitude: 35.6762,
                longitude: 139.6503,
                probability: 0.08,
                city: 'Tokyo',
                state: 'Tokyo',
                country: 'Japan',
                confidence: {level: 'medium', color: '#ffcc00', label: 'Medium'}
            },
            {
                latitude: -33.8688,
                longitude: 151.2093,
                probability: 0.04,
                city: 'Sydney',
                state: 'New South Wales',
                country: 'Australia',
                confidence: {level: 'low', color: '#ff4444', label: 'Low'}
            },
            {
                latitude: 55.7558,
                longitude: 37.6173,
                probability: 0.03,
                city: 'Moscow',
                state: 'Moscow',
                country: 'Russia',
                confidence: {level: 'low', color: '#ff4444', label: 'Low'}
            },
            {
                latitude: -22.9068,
                longitude: -43.1729,
                probability: 0.02,
                city: 'Rio de Janeiro',
                state: 'Rio de Janeiro',
                country: 'Brazil',
                confidence: {level: 'low', color: '#ff4444', label: 'Low'}
            }
        ];

        displayPredictions(demoData, null);
    }

    // Display predictions on maps
    function displayPredictions(predictions, imageUrl) {
        state.predictions = predictions;

        // Clear existing markers
        clearMarkers();

        // Show predictions list
        const listContainer = document.getElementById('predictionsList');
        listContainer.style.display = 'block';

        const predictionsHtml = predictions.map((pred, index) => `
            <div class="prediction-item ${pred.confidence.level}" data-index="${index}">
                <div class="prediction-header">
                    <span class="prediction-location">${pred.city}, ${pred.country}</span>
                    <span class="prediction-probability ${pred.confidence.level}">
                        ${(pred.probability * 100).toFixed(1)}%
                    </span>
                </div>
                <div class="prediction-details">
                    ${pred.state ? pred.state + ', ' : ''}${pred.country}
                </div>
                <div class="prediction-details">
                    ${pred.latitude.toFixed(4)}°, ${pred.longitude.toFixed(4)}°
                </div>
            </div>
        `).join('');

        document.getElementById('predictionsContainer').innerHTML = predictionsHtml;

        // Add click handlers to prediction items
        document.querySelectorAll('.prediction-item').forEach(item => {
            item.addEventListener('click', () => {
                const index = parseInt(item.dataset.index);
                focusOnPrediction(index);
            });
        });

        // Add markers to 2D map
        predictions.forEach((pred, index) => {
            const marker = L.circleMarker([pred.latitude, pred.longitude], {
                radius: 8,
                fillColor: pred.confidence.color,
                color: pred.confidence.color,
                weight: 2,
                opacity: 1,
                fillOpacity: 0.6
            }).addTo(state.map2d);

            marker.bindPopup(`
                <div style="color: #e0e0e0;">
                    <strong>${pred.city}, ${pred.country}</strong><br>
                    Confidence: ${pred.confidence.label}<br>
                    Probability: ${(pred.probability * 100).toFixed(1)}%<br>
                    <small>${pred.latitude.toFixed(4)}°, ${pred.longitude.toFixed(4)}°</small>
                </div>
            `);

            marker.on('click', () => focusOnPrediction(index));

            state.markers.push(marker);
        });

        // Fit map to show all markers
        if (predictions.length > 0) {
            const bounds = L.latLngBounds(predictions.map(p => [p.latitude, p.longitude]));
            state.map2d.fitBounds(bounds, { padding: [50, 50] });
        }

        // Add points to 3D globe
        const globePoints = predictions.map((pred, index) => ({
            lat: pred.latitude,
            lng: pred.longitude,
            color: pred.confidence.color,
            label: `
                <div style="background: #242424; padding: 8px; border-radius: 4px; color: #e0e0e0;">
                    <strong>${pred.city}, ${pred.country}</strong><br>
                    ${(pred.probability * 100).toFixed(1)}% confidence
                </div>
            `,
            onClick: () => focusOnPrediction(index)
        }));

        state.globe3d.pointsData(globePoints);

        // Focus on first prediction
        if (predictions.length > 0) {
            setTimeout(() => focusOnPrediction(0, false), 500);
        }
    }

    // Focus on a specific prediction
    function focusOnPrediction(index, animate = true) {
        const pred = state.predictions[index];

        if (state.currentView === '2d') {
            if (animate) {
                state.map2d.flyTo([pred.latitude, pred.longitude], 8, {
                    duration: 1
                });
            } else {
                state.map2d.setView([pred.latitude, pred.longitude], 8);
            }

            // Open popup
            if (state.markers[index]) {
                state.markers[index].openPopup();
            }
        } else {
            // Focus globe on location
            state.globe3d.pointOfView({
                lat: pred.latitude,
                lng: pred.longitude,
                altitude: 1.5
            }, animate ? 1000 : 0);
        }

        // Highlight prediction in list
        document.querySelectorAll('.prediction-item').forEach((item, i) => {
            item.style.background = i === index ? '#3a3a3a' : '';
        });
    }

    // Clear all markers
    function clearMarkers() {
        state.markers.forEach(marker => marker.remove());
        state.markers = [];
        state.globe3d.pointsData([]);
    }

    // Show/hide loading overlay
    function showLoading(show) {
        document.getElementById('loadingOverlay').style.display = show ? 'flex' : 'none';
    }

    // Check backend status
    async function checkBackendStatus() {
        try {
            const response = await fetch('/health');
            const data = await response.json();

            const statusDot = document.getElementById('statusDot');
            const statusText = document.getElementById('statusText');

            if (data.status === 'ok') {
                statusDot.classList.add('online');
                statusText.textContent = 'Backend online';
            } else {
                statusText.textContent = 'Backend offline';
            }
        } catch (error) {
            document.getElementById('statusText').textContent = 'Backend offline (demo mode)';
        }
    }

    // Expose public API
    window.GeoLensApp = {
        displayPredictions,
        switchView,
        focusOnPrediction
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
