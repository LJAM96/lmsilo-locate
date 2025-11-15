# Test Data Directory

This directory contains test images for integration testing.

## Auto-Generation

Test images are automatically generated during test execution using the `TestImageGenerator` helper class. If no test images are found, the test infrastructure will create default test images:

- `test-red.jpg` - Red colored image (800x600)
- `test-green.jpg` - Green colored image (800x600)
- `test-blue.jpg` - Blue colored image (800x600)
- `test-landscape.jpg` - Landscape orientation (1920x1080)
- `test-portrait.jpg` - Portrait orientation (1080x1920)

## Manual Test Images

You can add your own test images to this directory:

1. Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.webp`
2. Recommended size: 800x600 to 1920x1080
3. For EXIF testing: Add images with GPS metadata

## Image Requirements for Tests

- **Small file size**: Keep under 5MB for fast test execution
- **Varied content**: Different colors/patterns help verify cache isolation
- **No sensitive data**: Test images are included in the repository

## Usage in Tests

```csharp
// Get all test images
var images = TestDataPaths.GetAllTestImages();

// Get first available test image
var image = TestDataPaths.GetFirstTestImage();

// Get N test images (auto-generates if needed)
var batch = TestDataPaths.GetTestImageBatch(10);
```

## Note

Test images in this directory are .gitignored to keep the repository size small. They will be generated automatically during test runs.
