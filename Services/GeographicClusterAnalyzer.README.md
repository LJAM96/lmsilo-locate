# GeographicClusterAnalyzer

## Overview

The `GeographicClusterAnalyzer` service detects geographic clustering in GeoCLIP location predictions and boosts confidence for clustered results. When multiple predictions fall within 100km of each other, they are considered a cluster, suggesting higher reliability.

## Clustering Algorithm

### Step-by-Step Process

1. **Input Validation**
   - Requires at least 2 predictions to form a cluster
   - Returns non-clustered result if insufficient data

2. **Cluster Detection**
   - For each prediction, find all other predictions within 100km radius
   - Uses Haversine formula for accurate spherical distance calculation
   - Selects the largest cluster found (most predictions within radius)

3. **Cluster Analysis**
   - Calculate geographic center (centroid) using spherical averaging
   - Calculate average distance from center
   - Calculate maximum distance (cluster radius)
   - Determine confidence boost based on cluster size

4. **Confidence Boosting**
   - Apply formula: `boost = 0.15 * (clusterSize / totalPredictions)`
   - Add boost to `AdjustedProbability` (capped at 1.0)
   - Mark predictions with `IsPartOfCluster = true`
   - Reclassify confidence level based on new adjusted probability

5. **Output**
   - Return `ClusterAnalysisResult` with all cluster metrics
   - Modified predictions now have boosted confidence and cluster flags

## Haversine Formula

The service uses the Haversine formula for accurate great-circle distance calculation between two points on Earth's surface:

```
a = sin²(Δlat/2) + cos(lat1) × cos(lat2) × sin²(Δlon/2)
c = 2 × atan2(√a, √(1-a))
distance = R × c
```

Where:
- R = Earth's radius (6,371 km)
- Δlat = lat2 - lat1
- Δlon = lon2 - lon1

This provides accuracy within 0.5% for distances up to several hundred kilometers.

## Geographic Centroid Calculation

Unlike simple arithmetic averaging, the service uses **spherical averaging** for accurate centroid calculation:

1. Convert each lat/lon to Cartesian coordinates (x, y, z)
2. Average the Cartesian coordinates
3. Convert back to latitude/longitude

This prevents distortion near poles and date line wrapping issues.

## Usage Example

```csharp
using GeoLens.Services;
using GeoLens.Models;

var analyzer = new GeographicClusterAnalyzer();

// Get predictions from GeoCLIP API
List<EnhancedLocationPrediction> predictions = GetPredictionsFromApi();

// Analyze for clustering
ClusterAnalysisResult clusterInfo = analyzer.AnalyzeClusters(predictions);

if (clusterInfo.IsClustered)
{
    Console.WriteLine($"Cluster detected! {predictions.Count(p => p.IsPartOfCluster)} predictions within {clusterInfo.ClusterRadius:F1}km");
    Console.WriteLine($"Confidence boosted by {clusterInfo.ConfidenceBoost * 100:F1}%");
    Console.WriteLine($"Cluster center: {clusterInfo.ClusterCenterLat:F4}°, {clusterInfo.ClusterCenterLon:F4}°");
}

// Display predictions (now with boosted confidence)
foreach (var pred in predictions.Where(p => p.IsPartOfCluster))
{
    Console.WriteLine($"{pred.City}: {pred.AdjustedProbability:P1} ({pred.ConfidenceLevel})");
}
```

## Integration with PredictionProcessor

The analyzer should be called after getting predictions from the API but before displaying to the user:

```
1. Check cache → miss
2. Check EXIF GPS → not found
3. Call GeoCLIP API → get predictions
4. Run GeographicClusterAnalyzer → boost clustered predictions ← **HERE**
5. Store in cache
6. Display to user
```

## Key Methods

### `AnalyzeClusters(predictions)`
Main entry point. Analyzes all predictions and returns cluster information.

**Parameters:**
- `predictions`: List of `EnhancedLocationPrediction` objects

**Returns:**
- `ClusterAnalysisResult` with clustering metrics

### `CalculateDistance(lat1, lon1, lat2, lon2)`
Calculate great-circle distance between two points using Haversine formula.

**Parameters:**
- `lat1`, `lon1`: First point coordinates (degrees)
- `lat2`, `lon2`: Second point coordinates (degrees)

**Returns:**
- Distance in kilometers

### `FindClusterCenter(predictions)`
Calculate geographic centroid of clustered predictions using spherical averaging.

**Parameters:**
- `predictions`: List of predictions in the cluster

**Returns:**
- Tuple of `(latitude, longitude)` for cluster center

### `BoostClusteredPredictions(predictions, boostAmount)`
Apply confidence boost to clustered predictions and reclassify confidence levels.

**Parameters:**
- `predictions`: Predictions to boost
- `boostAmount`: Boost value to add (0-1 scale)

**Returns:**
- None (modifies predictions in-place)

## Configuration Constants

```csharp
private const double ClusterRadiusKm = 100.0;        // 100km clustering threshold
private const double ConfidenceBoostFactor = 0.15;   // 15% max boost factor
private const int MinimumClusterSize = 2;            // Minimum 2 predictions for cluster
```

## Test Examples

Run the test examples to verify correct behavior:

```csharp
using GeoLens.Services.Tests;

// Run all test examples
GeographicClusterAnalyzerTestExamples.RunAllTests();

// Or run individual tests
GeographicClusterAnalyzerTestExamples.TestClusteredPredictions();
GeographicClusterAnalyzerTestExamples.TestDistanceCalculations();
```

## Expected Behavior

### Scenario 1: Clustered Predictions (Paris Area)
- Input: 4 predictions (3 near Paris, 1 in Berlin)
- Output: Cluster of 3 predictions, ~50km radius, +11.25% confidence boost
- Berlin prediction remains unclustered

### Scenario 2: Dispersed Predictions
- Input: 3 predictions across different continents
- Output: No cluster detected, no confidence boost applied

### Scenario 3: All Predictions Cluster
- Input: 5 predictions all within 50km
- Output: Full cluster, +15% confidence boost (maximum)

## Performance Considerations

- **Time Complexity**: O(n²) where n is number of predictions
  - Acceptable for typical use (5-10 predictions per image)
  - For n=10: ~45 distance calculations

- **Memory**: Minimal overhead, works on existing prediction objects

- **Precision**: Double-precision floating point (15-17 significant digits)

## Error Handling

The service includes try-catch blocks to gracefully handle:
- Null or empty prediction lists
- Invalid coordinates (NaN, infinity)
- Mathematical edge cases (poles, date line)

On error, returns a non-clustered result rather than throwing exceptions.

## Future Enhancements

Potential improvements for post-MVP:

1. **DBSCAN Clustering**: Replace simple radius-based clustering with density-based algorithm
2. **Weighted Centroid**: Weight center calculation by prediction probability
3. **Multi-Cluster Detection**: Identify multiple separate clusters in predictions
4. **Adaptive Radius**: Adjust 100km threshold based on prediction confidence
5. **Cluster Visualization**: Highlight cluster boundaries on map/globe

## References

- Haversine formula: https://en.wikipedia.org/wiki/Haversine_formula
- Geographic coordinate centroid: https://en.wikipedia.org/wiki/Centroid#Of_a_finite_set_of_points
- DBSCAN clustering: https://en.wikipedia.org/wiki/DBSCAN
