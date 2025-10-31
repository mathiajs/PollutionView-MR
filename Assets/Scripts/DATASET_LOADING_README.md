# Fast Dataset Loading for Quest

## The Problem
Loading and processing HDF5 files at runtime causes severe lag on Quest headsets because:
- Reading large HDF5 files takes time
- Processing 76M+ data points is CPU-intensive
- Downsampling and filtering happens every launch

## The Solution: Preprocess at Build Time

Instead of processing data at runtime, we preprocess it once in the Unity Editor and save it as a binary file that loads instantly.

---

## Setup Instructions

### Step 1: Preprocess Your Dataset (Once, in Unity Editor)

1. Open Unity Editor (Windows/Mac - NOT on Quest)
2. Go to menu: **Tools > Dataset Preprocessor**
3. Configure settings:
   - **Source File**: `dp_3d_clean.001.nc` (your HDF5 file in StreamingAssets)
   - **Output File**: `preprocessed_particles.bytes`
   - **Downsampling Step**: 8 (adjust for performance vs quality)
   - **Thresholds**: Min=0.0, Max=0.00975
4. Click **"Preprocess Dataset"**
5. Wait for processing (may take a minute)
6. Done! The `.bytes` file is now in StreamingAssets

### Step 2: Update Your Scene

1. Select your GameObject with `ParticleAnimationController`
2. In the Inspector:
   - **Assign `FastDatasetLoader`** component (or add it if missing)
   - **Uncheck `useTestData`**
   - Leave old `HDF5_InspectAndRead` empty (or keep for fallback)
3. On the FastDatasetLoader component:
   - Set filename to: `preprocessed_particles.bytes`

### Step 3: Build and Deploy

Build to Quest as normal. The preprocessed file will be included automatically!

---

## Performance Comparison

| Method | Load Time | Processing | Total Startup Time |
|--------|-----------|------------|-------------------|
| **Old (HDF5_InspectAndRead)** | ~5-10s | ~10-20s | **15-30 seconds** |
| **New (FastDatasetLoader)** | <0.5s | None | **<1 second** |

---

## Technical Details

### What Gets Preprocessed?

The preprocessor:
1. Reads the HDF5 file
2. Applies downsampling (8x by default)
3. Filters by threshold (0 < q <= 0.00975)
4. Writes binary file with:
   - Header: particle count, dimensions, metadata
   - Data: all particle positions (t,z,y,x,q) as binary

### File Sizes

Example with downsampling=8:
- Original HDF5: ~76 MB
- Preprocessed binary: ~2-5 MB (depends on threshold filtering)

### Binary Format

```
Header (24 bytes):
  int32: particleCount
  int32: downsamplingFactor
  int32: timeDimension
  int32: zDimension
  int32: yDimension
  int32: xDimension

Particle Data (20 bytes each):
  int32: t
  int32: z
  int32: y
  int32: x
  float: q
```

---

## Troubleshooting

### "Preprocessed file not found"
- Make sure you ran the preprocessor in Unity Editor first
- Check that the `.bytes` file exists in `Assets/StreamingAssets/`
- Verify the filename matches in FastDatasetLoader component

### "Still laggy on Quest"
- Ensure `useTestData` is FALSE
- Check that FastDatasetLoader is assigned (not HDF5_InspectAndRead)
- Try increasing downsampling factor (16x instead of 8x)

### "Not enough particles visible"
- Decrease downsampling factor (4x or 2x)
- Adjust threshold values in preprocessor
- Re-run preprocessing after changes

---

## When to Re-preprocess

You need to re-run the preprocessor when:
- You change the source HDF5 file
- You want different downsampling settings
- You adjust threshold values
- You update filtering logic

---

## Files Overview

| File | Purpose |
|------|---------|
| `FastDatasetLoader.cs` | Runtime loader (instant, no processing) |
| `DatasetPreprocessor.cs` | Editor tool to preprocess HDF5 → binary |
| `HDF5_InspectAndRead.cs` | Legacy loader (slow, kept for reference) |
| `DataBufferBinder.cs` | VFX controller, supports both loaders |

---

## Advanced: Customizing Preprocessing

Edit `DatasetPreprocessor.cs` to change:
- Filtering logic (line 85-92)
- Threshold values
- Dimension handling
- Data transformations

After editing, re-run the preprocessor to regenerate the `.bytes` file.

---

## Questions?

- Check Unity Console for detailed logs (✅ = success, ⚡ = fast mode, ❌ = error)
- Use `FastDatasetLoader` for Quest builds
- Keep `HDF5_InspectAndRead` only for testing in Editor
