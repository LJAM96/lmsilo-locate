/**
 * GeoLens Web Demo - Map Library Comparison
 * Supports: Globe.GL, Leaflet, MapLibre (2D/3D), and Cesium
 */

(function() {
    'use strict';

    // App State
    const state = {
        currentTab: 'globe-gl',
        predictions: [],
        selectedFile: null,
        maps: {
            globegl: null,
            leaflet: null,
            maplibre2d: null,
            maplibre3d: null,
            cesium: null
        },
        markersData: []
    };

    // Initialize app
    function init() {
        console.log('App initializing...');
        showDebugInfo();
        setupEventListeners();
        initAllMaps();
        checkBackendStatus();
    }

    // Show debug info
    function showDebugInfo() {
        const debugPanel = document.getElementById('debugPanel');
        const debugContent = document.getElementById('debugContent');

        const checks = [
            { name: 'Leaflet', check: typeof L !== 'undefined' },
            { name: 'Globe.GL', check: typeof Globe !== 'undefined' },
            { name: 'MapLibre', check: typeof maplibregl !== 'undefined' },
            { name: 'Cesium', check: typeof Cesium !== 'undefined' },
            { name: 'Three.js', check: typeof THREE !== 'undefined' }
        ];

        const html = checks.map(item => {
            const status = item.check ? '✅' : '❌';
            return `<div>${status} ${item.name}</div>`;
        }).join('');

        debugContent.innerHTML = html;
        debugPanel.style.display = 'block';

        console.log('Library status:', checks);
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

        // Tab buttons
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.addEventListener('click', () => switchTab(btn.dataset.tab));
        });
    }

    // Initialize all maps
    function initAllMaps() {
        console.log('Starting map initialization...');

        // Wait for all libraries to load, then initialize only the first visible map
        setTimeout(() => {
            // Only initialize the first tab (globe-gl) which is visible
            initGlobeGL();
            // Others will be initialized when their tabs are clicked
        }, 500);
    }

    // Initialize Globe.GL
    function initGlobeGL() {
        const container = document.getElementById('map-globe-gl');

        try {
            if (typeof Globe === 'undefined') {
                console.error('Globe.gl library not loaded');
                showMapError(container, 'Globe.GL library not loaded. Check internet connection.');
                return;
            }

            console.log('Initializing Globe.GL...');

            state.maps.globegl = Globe()
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

            state.maps.globegl.controls().autoRotate = true;
            state.maps.globegl.controls().autoRotateSpeed = 0.5;

            // Force explicit sizing
            setTimeout(() => {
                const width = container.offsetWidth;
                const height = container.offsetHeight;
                console.log('Globe.GL container size:', width, 'x', height);
                if (width > 0 && height > 0) {
                    state.maps.globegl.width(width);
                    state.maps.globegl.height(height);
                } else {
                    console.error('Globe.GL container has zero dimensions!');
                }
            }, 100);

            console.log('Globe.GL initialized successfully');
        } catch (error) {
            console.error('Failed to initialize Globe.GL:', error);
            showMapError(container, 'Failed to initialize Globe.GL: ' + error.message);
        }
    }

    // Show error message in map container
    function showMapError(container, message) {
        if (!container) return;
        container.innerHTML = `
            <div style="display: flex; align-items: center; justify-content: center; height: 100%; color: #ff4444; flex-direction: column; gap: 1rem; padding: 2rem; text-align: center;">
                <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
                <div style="font-size: 1.1rem; font-weight: bold;">Map Failed to Load</div>
                <div style="color: #a0a0a0; max-width: 400px;">${message}</div>
            </div>
        `;
    }

    // Initialize Leaflet
    function initLeaflet() {
        const container = document.getElementById('map-leaflet');

        try {
            if (!container) {
                console.error('Leaflet container not found');
                return;
            }

            if (typeof L === 'undefined') {
                console.error('Leaflet library not loaded');
                showMapError(container, 'Leaflet library not loaded. Check internet connection.');
                return;
            }

            console.log('Initializing Leaflet on container:', container);
            console.log('Leaflet container dimensions:', container.offsetWidth, 'x', container.offsetHeight);

            state.maps.leaflet = L.map(container, {
                center: [20, 0],
                zoom: 2,
                minZoom: 2,
                maxZoom: 18
            });

            // CartoDB Dark Matter tiles
            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                subdomains: 'abcd',
                maxZoom: 20
            }).addTo(state.maps.leaflet);

            // Force invalidate size after a moment
            setTimeout(() => {
                state.maps.leaflet.invalidateSize();
                console.log('Leaflet size invalidated, new dimensions:', container.offsetWidth, 'x', container.offsetHeight);
            }, 200);

            console.log('Leaflet initialized successfully');
        } catch (error) {
            console.error('Failed to initialize Leaflet:', error);
            showMapError(container, 'Failed to initialize Leaflet: ' + error.message);
        }
    }

    // Initialize MapLibre 2D
    function initMapLibre2D() {
        const container = document.getElementById('map-maplibre-2d');

        try {
            if (!container) {
                console.error('MapLibre 2D container not found');
                return;
            }

            if (typeof maplibregl === 'undefined') {
                console.error('MapLibre GL JS not loaded');
                return;
            }

            console.log('Initializing MapLibre 2D...');

            state.maps.maplibre2d = new maplibregl.Map({
                container: container,
                style: 'https://demotiles.maplibre.org/style.json',
                center: [0, 20],
                zoom: 1.5
            });

            state.maps.maplibre2d.on('load', () => {
                console.log('MapLibre 2D initialized successfully');
            });
        } catch (error) {
            console.error('Failed to initialize MapLibre 2D:', error);
        }
    }

    // Initialize MapLibre 3D Globe
    function initMapLibre3D() {
        const container = document.getElementById('map-maplibre-3d');

        try {
            if (typeof maplibregl === 'undefined') {
                console.error('MapLibre GL JS not loaded');
                return;
            }

            state.maps.maplibre3d = new maplibregl.Map({
                container: container,
                style: 'https://demotiles.maplibre.org/style.json',
                center: [0, 20],
                zoom: 1.5,
                projection: 'globe'  // Enable globe projection
            });

            state.maps.maplibre3d.on('load', () => {
                console.log('MapLibre 3D Globe initialized');
            });
        } catch (error) {
            console.error('Failed to initialize MapLibre 3D:', error);
        }
    }

    // Initialize Cesium
    function initCesium() {
        const container = document.getElementById('map-cesium');

        try {
            if (typeof Cesium === 'undefined') {
                console.error('Cesium not loaded');
                return;
            }

            // Set Cesium ion token (using default for demo)
            Cesium.Ion.defaultAccessToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJlYWE1OWUxNy1mMWZiLTQzYjYtYTQ0OS1kMWFjYmFkNjc5YzciLCJpZCI6NTc3MzMsImlhdCI6MTYyNzg0NTE4Mn0.XcKpgANiY19MC4bdFUXMVEBToBmqS8kuYpUlxJHYZxk';

            state.maps.cesium = new Cesium.Viewer(container, {
                imageryProvider: new Cesium.IonImageryProvider({ assetId: 3845 }),
                baseLayerPicker: false,
                geocoder: false,
                homeButton: false,
                sceneModePicker: false,
                navigationHelpButton: false,
                animation: false,
                timeline: false,
                fullscreenButton: false
            });

            // Dark background
            state.maps.cesium.scene.backgroundColor = Cesium.Color.BLACK;

            console.log('Cesium initialized');
        } catch (error) {
            console.error('Failed to initialize Cesium:', error);
        }
    }

    // Switch between tabs
    function switchTab(tabName) {
        console.log('Switching to tab:', tabName);
        state.currentTab = tabName;

        // Update tab button states
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.tab === tabName);
        });

        // Update tab content visibility
        document.querySelectorAll('.tab-content').forEach(content => {
            content.classList.remove('active');
        });
        document.getElementById(`tab-${tabName}`).classList.add('active');

        // Lazy initialize maps when their tab is first opened
        let isLazyInit = false;
        if (tabName === 'leaflet' && !state.maps.leaflet) {
            console.log('Lazy initializing Leaflet...');
            isLazyInit = true;
            setTimeout(() => {
                initLeaflet();
                // Re-display predictions after initialization
                if (state.predictions.length > 0) {
                    setTimeout(() => displayPredictionsOnMap('leaflet', state.predictions), 500);
                }
            }, 100);
        } else if (tabName === 'maplibre-2d' && !state.maps.maplibre2d) {
            console.log('Lazy initializing MapLibre 2D...');
            isLazyInit = true;
            setTimeout(() => {
                initMapLibre2D();
                // Re-display predictions after initialization
                if (state.predictions.length > 0) {
                    setTimeout(() => displayPredictionsOnMap('maplibre-2d', state.predictions), 500);
                }
            }, 100);
        } else if (tabName === 'maplibre-3d' && !state.maps.maplibre3d) {
            console.log('Lazy initializing MapLibre 3D...');
            isLazyInit = true;
            setTimeout(() => {
                initMapLibre3D();
                // Re-display predictions after initialization
                if (state.predictions.length > 0) {
                    setTimeout(() => displayPredictionsOnMap('maplibre-3d', state.predictions), 500);
                }
            }, 100);
        } else if (tabName === 'cesium' && !state.maps.cesium) {
            console.log('Lazy initializing Cesium...');
            isLazyInit = true;
            setTimeout(() => {
                initCesium();
                // Re-display predictions after initialization
                if (state.predictions.length > 0) {
                    setTimeout(() => displayPredictionsOnMap('cesium', state.predictions), 500);
                }
            }, 100);
        }

        // Resize map on tab switch (only if already initialized)
        if (!isLazyInit) {
            setTimeout(() => {
                if (tabName === 'leaflet' && state.maps.leaflet) {
                    state.maps.leaflet.invalidateSize();
                }
                if (tabName === 'globe-gl' && state.maps.globegl) {
                    const container = document.getElementById('map-globe-gl');
                    state.maps.globegl.width(container.offsetWidth);
                    state.maps.globegl.height(container.offsetHeight);
                }
                if (tabName === 'maplibre-2d' && state.maps.maplibre2d) {
                    state.maps.maplibre2d.resize();
                }
                if (tabName === 'maplibre-3d' && state.maps.maplibre3d) {
                    state.maps.maplibre3d.resize();
                }
                if (tabName === 'cesium' && state.maps.cesium) {
                    state.maps.cesium.resize();
                }
            }, 100);

            // Re-display predictions on new map
            if (state.predictions.length > 0) {
                setTimeout(() => displayPredictionsOnMap(tabName, state.predictions), 200);
            }
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

        const reader = new FileReader();
        reader.onload = (e) => {
            const preview = document.getElementById('imagePreview');
            const previewImg = document.getElementById('previewImg');
            previewImg.src = e.target.result;
            preview.style.display = 'block';
        };
        reader.readAsDataURL(file);

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
            }
        ];

        displayPredictions(demoData, null);
    }

    // Display predictions on all maps
    function displayPredictions(predictions, imageUrl) {
        state.predictions = predictions;

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

        // Add click handlers
        document.querySelectorAll('.prediction-item').forEach(item => {
            item.addEventListener('click', () => {
                const index = parseInt(item.dataset.index);
                focusOnPrediction(index);
            });
        });

        // Display on currently active map only (others will load when tab is clicked)
        displayPredictionsOnMap(state.currentTab, predictions);
    }

    // Display predictions on specific map
    function displayPredictionsOnMap(mapType, predictions) {
        switch (mapType) {
            case 'globe-gl':
                displayOnGlobeGL(predictions);
                break;
            case 'leaflet':
                displayOnLeaflet(predictions);
                break;
            case 'maplibre-2d':
            case 'maplibre-3d':
                displayOnMapLibre(mapType, predictions);
                break;
            case 'cesium':
                displayOnCesium(predictions);
                break;
        }
    }

    // Display on Globe.GL
    function displayOnGlobeGL(predictions) {
        if (!state.maps.globegl) return;

        const points = predictions.map((pred, index) => ({
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

        state.maps.globegl.pointsData(points);

        // Focus on first prediction
        if (predictions.length > 0) {
            setTimeout(() => {
                state.maps.globegl.pointOfView({
                    lat: predictions[0].latitude,
                    lng: predictions[0].longitude,
                    altitude: 2
                }, 1000);
            }, 500);
        }
    }

    // Display on Leaflet
    function displayOnLeaflet(predictions) {
        if (!state.maps.leaflet) return;

        // Clear existing markers
        state.maps.leaflet.eachLayer(layer => {
            if (layer instanceof L.CircleMarker) {
                state.maps.leaflet.removeLayer(layer);
            }
        });

        // Add new markers
        predictions.forEach((pred, index) => {
            const marker = L.circleMarker([pred.latitude, pred.longitude], {
                radius: 8,
                fillColor: pred.confidence.color,
                color: pred.confidence.color,
                weight: 2,
                opacity: 1,
                fillOpacity: 0.6
            }).addTo(state.maps.leaflet);

            marker.bindPopup(`
                <div style="color: #e0e0e0;">
                    <strong>${pred.city}, ${pred.country}</strong><br>
                    Confidence: ${pred.confidence.label}<br>
                    Probability: ${(pred.probability * 100).toFixed(1)}%<br>
                    <small>${pred.latitude.toFixed(4)}°, ${pred.longitude.toFixed(4)}°</small>
                </div>
            `);
        });

        // Fit bounds
        if (predictions.length > 0) {
            const bounds = L.latLngBounds(predictions.map(p => [p.latitude, p.longitude]));
            state.maps.leaflet.fitBounds(bounds, { padding: [50, 50] });
        }
    }

    // Display on MapLibre (2D or 3D)
    function displayOnMapLibre(mapType, predictions) {
        const map = state.maps[mapType === 'maplibre-2d' ? 'maplibre2d' : 'maplibre3d'];
        if (!map) return;

        // Remove existing sources and layers
        if (map.getSource('predictions')) {
            map.removeLayer('predictions-layer');
            map.removeSource('predictions');
        }

        // Add predictions as GeoJSON
        map.addSource('predictions', {
            type: 'geojson',
            data: {
                type: 'FeatureCollection',
                features: predictions.map(pred => ({
                    type: 'Feature',
                    geometry: {
                        type: 'Point',
                        coordinates: [pred.longitude, pred.latitude]
                    },
                    properties: {
                        color: pred.confidence.color,
                        city: pred.city,
                        country: pred.country,
                        probability: pred.probability
                    }
                }))
            }
        });

        map.addLayer({
            id: 'predictions-layer',
            type: 'circle',
            source: 'predictions',
            paint: {
                'circle-radius': 10,
                'circle-color': ['get', 'color'],
                'circle-stroke-width': 2,
                'circle-stroke-color': ['get', 'color'],
                'circle-opacity': 0.8
            }
        });

        // Fit bounds
        if (predictions.length > 0) {
            const bounds = predictions.reduce((bounds, pred) => {
                return bounds.extend([pred.longitude, pred.latitude]);
            }, new maplibregl.LngLatBounds());
            map.fitBounds(bounds, { padding: 50 });
        }
    }

    // Display on Cesium
    function displayOnCesium(predictions) {
        if (!state.maps.cesium) return;

        // Clear existing entities
        state.maps.cesium.entities.removeAll();

        // Add new entities
        predictions.forEach(pred => {
            state.maps.cesium.entities.add({
                position: Cesium.Cartesian3.fromDegrees(pred.longitude, pred.latitude, 10000),
                point: {
                    pixelSize: 12,
                    color: Cesium.Color.fromCssColorString(pred.confidence.color),
                    outlineColor: Cesium.Color.WHITE,
                    outlineWidth: 2
                },
                label: {
                    text: pred.city,
                    font: '14px sans-serif',
                    fillColor: Cesium.Color.WHITE,
                    style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                    outlineWidth: 2,
                    verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                    pixelOffset: new Cesium.Cartesian2(0, -15)
                },
                description: `
                    <strong>${pred.city}, ${pred.country}</strong><br>
                    Confidence: ${pred.confidence.label}<br>
                    Probability: ${(pred.probability * 100).toFixed(1)}%
                `
            });
        });

        // Fly to first prediction
        if (predictions.length > 0) {
            state.maps.cesium.camera.flyTo({
                destination: Cesium.Cartesian3.fromDegrees(
                    predictions[0].longitude,
                    predictions[0].latitude,
                    10000000
                ),
                duration: 2
            });
        }
    }

    // Focus on a specific prediction
    function focusOnPrediction(index) {
        const pred = state.predictions[index];

        switch (state.currentTab) {
            case 'globe-gl':
                if (state.maps.globegl) {
                    state.maps.globegl.pointOfView({
                        lat: pred.latitude,
                        lng: pred.longitude,
                        altitude: 1.5
                    }, 1000);
                }
                break;
            case 'leaflet':
                if (state.maps.leaflet) {
                    state.maps.leaflet.flyTo([pred.latitude, pred.longitude], 8);
                }
                break;
            case 'maplibre-2d':
            case 'maplibre-3d':
                const map = state.maps[state.currentTab === 'maplibre-2d' ? 'maplibre2d' : 'maplibre3d'];
                if (map) {
                    map.flyTo({
                        center: [pred.longitude, pred.latitude],
                        zoom: 8,
                        duration: 1000
                    });
                }
                break;
            case 'cesium':
                if (state.maps.cesium) {
                    state.maps.cesium.camera.flyTo({
                        destination: Cesium.Cartesian3.fromDegrees(
                            pred.longitude,
                            pred.latitude,
                            5000000
                        ),
                        duration: 2
                    });
                }
                break;
        }

        // Highlight prediction in list
        document.querySelectorAll('.prediction-item').forEach((item, i) => {
            item.style.background = i === index ? '#3a3a3a' : '';
        });
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
        switchTab,
        focusOnPrediction
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
