using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using PureHDF;
using System;

public class DatasetPreprocessor : EditorWindow
{
    private string sourceFileName = "dp_3d_clean.001.nc";
    private string outputFileName = "preprocessed_particles.bytes";
    private int stepSize = 8;
    private float minThreshold = 0.006f;  // Just below your min value
    private float maxThreshold = 0.00975f; // Original threshold - values above this may be junk
    private bool useOriginalThreshold = true; // Toggle to test with/without filtering
    private float missingValueMarker = -999999f; // Common NetCDF missing data marker
    private bool excludeOrigin = true; // Exclude points at (0,0,0)
    private int maxParticles = 500000; // Limit for Quest memory (500k particles ≈ 10MB)

    // Debug info
    private int totalChecked = 0;
    private int passedThreshold = 0;

    [MenuItem("Tools/Dataset Preprocessor")]
    public static void ShowWindow()
    {
        GetWindow<DatasetPreprocessor>("Dataset Preprocessor");
    }

    void OnGUI()
    {
        GUILayout.Label("HDF5 Dataset Preprocessor", EditorStyles.boldLabel);
        GUILayout.Space(10);

        sourceFileName = EditorGUILayout.TextField("Source File (.nc):", sourceFileName);
        outputFileName = EditorGUILayout.TextField("Output File (.bytes):", outputFileName);

        GUILayout.Space(10);
        stepSize = EditorGUILayout.IntSlider("Downsampling Step:", stepSize, 1, 16);

        GUILayout.Space(10);
        minThreshold = EditorGUILayout.FloatField("Min Threshold:", minThreshold);
        maxThreshold = EditorGUILayout.FloatField("Max Threshold:", maxThreshold);
        missingValueMarker = EditorGUILayout.FloatField("Missing Value Marker:", missingValueMarker);

        GUILayout.Space(10);
        excludeOrigin = EditorGUILayout.Toggle("Exclude Origin (0,0,0):", excludeOrigin);

        GUILayout.Space(10);
        maxParticles = EditorGUILayout.IntField("Max Particles (Quest Limit):", maxParticles);

        GUILayout.Space(20);

        if (GUILayout.Button("Preprocess Dataset", GUILayout.Height(40)))
        {
            PreprocessDataset();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "This will process the HDF5 file and save it as a binary asset. " +
            "The processed file will load instantly at runtime, eliminating lag on Quest.",
            MessageType.Info
        );
    }

    void PreprocessDataset()
    {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, sourceFileName);
        string outputPath = Path.Combine(Application.streamingAssetsPath, outputFileName);

        if (!File.Exists(sourcePath))
        {
            EditorUtility.DisplayDialog("Error", $"Source file not found:\n{sourcePath}", "OK");
            return;
        }

        EditorUtility.DisplayProgressBar("Preprocessing Dataset", "Reading HDF5 file...", 0.1f);

        try
        {
            using var file = H5File.OpenRead(sourcePath);

            var qLink = file.Children().FirstOrDefault(l => l.Name == "q" && l is IH5Dataset);
            if (qLink == null)
            {
                EditorUtility.DisplayDialog("Error", "No dataset named '/q' found in file.", "OK");
                EditorUtility.ClearProgressBar();
                return;
            }

            EditorUtility.DisplayProgressBar("Preprocessing Dataset", "Loading dataset...", 0.2f);

            var dataset = file.Dataset("/q");
            float[] flat = dataset.Read<float[]>();

            // Dataset dimensions
            int time = 11, z = 41, y = 412, x = 412;
            int stepZ = z / stepSize;
            int stepY = y / stepSize;
            int stepX = x / stepSize;

            EditorUtility.DisplayProgressBar("Preprocessing Dataset", "Filtering and downsampling...", 0.4f);

            // Filter and downsample
            var particles = new System.Collections.Generic.List<ParticleData>();
            totalChecked = 0;
            passedThreshold = 0;
            int missingCount = 0;
            int validCount = 0;
            int originExcludedCount = 0;
            float minValidValue = float.MaxValue;
            float maxValidValue = float.MinValue;

            UnityEngine.Debug.Log($"Dataset dimensions: time={time}, z={z}, y={y}, x={x}");
            UnityEngine.Debug.Log($"After downsampling: stepZ={stepZ}, stepY={stepY}, stepX={stepX}");
            UnityEngine.Debug.Log($"Threshold range: {minThreshold} < q <= {maxThreshold}");
            UnityEngine.Debug.Log($"Missing value marker: {missingValueMarker}");
            UnityEngine.Debug.Log($"Exclude origin (0,0,0): {excludeOrigin}");

            for (int t = 0; t < time; t++)
            {
                EditorUtility.DisplayProgressBar(
                    "Preprocessing Dataset",
                    $"Processing timestep {t + 1}/{time}...",
                    0.4f + (0.5f * t / time)
                );

                for (int zz = 0; zz < stepZ; zz++)
                {
                    int srcZ = zz * stepSize;
                    for (int yy = 0; yy < stepY; yy++)
                    {
                        int srcY = yy * stepSize;
                        for (int xx = 0; xx < stepX; xx++)
                        {
                            int srcX = xx * stepSize;
                            long idxFlat = ((long)t * z + srcZ) * y * x + srcY * x + srcX;

                            if (idxFlat < 0 || idxFlat >= flat.Length) continue;

                            totalChecked++;
                            float value = flat[idxFlat];

                            // Check for missing data marker
                            if (Mathf.Approximately(value, missingValueMarker) || value < -999000f)
                            {
                                missingCount++;
                                continue;
                            }

                            validCount++;

                            // Track min/max for statistics
                            if (value < minValidValue) minValidValue = value;
                            if (value > maxValidValue) maxValidValue = value;

                            // Log some sample VALID values for debugging
                            if (validCount <= 100)
                            {
                                UnityEngine.Debug.Log($"Valid sample [{validCount}]: value={value:F6} at t={t},z={zz},y={yy},x={xx}");
                            }

                            // Exclude origin if enabled
                            if (excludeOrigin && xx == 0 && yy == 0 && zz == 0)
                            {
                                originExcludedCount++;
                                continue;
                            }

                            // Apply threshold filter
                            if (value < minThreshold || value > maxThreshold) continue;

                            passedThreshold++;

                            // Check if we've hit the particle limit
                            if (particles.Count >= maxParticles)
                            {
                                UnityEngine.Debug.LogWarning($"⚠️ Reached particle limit ({maxParticles:N0}). Stopping early to save memory.");
                                goto LIMIT_REACHED; // Break out of all loops
                            }

                            particles.Add(new ParticleData
                            {
                                t = t,
                                z = zz,
                                y = yy,
                                x = xx,
                                q = value
                            });
                        }
                    }
                }
            }

            LIMIT_REACHED: // Jump here if we hit the particle limit

            UnityEngine.Debug.Log($"=== PREPROCESSING SUMMARY ===");
            UnityEngine.Debug.Log($"Total checked: {totalChecked:N0}");
            UnityEngine.Debug.Log($"Missing data markers: {missingCount:N0} ({(missingCount * 100.0 / totalChecked):F2}%)");
            UnityEngine.Debug.Log($"Valid values: {validCount:N0} ({(validCount * 100.0 / totalChecked):F2}%)");
            UnityEngine.Debug.Log($"VALUE RANGE: min={minValidValue:F6}, max={maxValidValue:F6}");
            UnityEngine.Debug.Log($"Origin points excluded: {originExcludedCount:N0}");
            UnityEngine.Debug.Log($"Threshold filter [{minThreshold:F6} to {maxThreshold:F6}]: {passedThreshold:N0} passed ({(validCount > 0 ? (passedThreshold * 100.0 / validCount) : 0):F2}% of valid)");
            UnityEngine.Debug.Log($"Final particles: {particles.Count:N0}");

            if (particles.Count >= maxParticles)
            {
                UnityEngine.Debug.LogWarning($"⚠️ LIMITED TO {maxParticles:N0} particles to prevent Quest memory issues!");
            }

            EditorUtility.DisplayProgressBar("Preprocessing Dataset", "Writing binary file...", 0.9f);

            // Write binary file
            using (var writer = new BinaryWriter(File.Open(outputPath, FileMode.Create)))
            {
                // Write header
                writer.Write(particles.Count); // Particle count
                writer.Write(stepSize);        // Downsampling factor
                writer.Write(time);            // Time dimension
                writer.Write(stepZ);           // Z dimension
                writer.Write(stepY);           // Y dimension
                writer.Write(stepX);           // X dimension

                // Write particle data
                foreach (var p in particles)
                {
                    writer.Write(p.t);
                    writer.Write(p.z);
                    writer.Write(p.y);
                    writer.Write(p.x);
                    writer.Write(p.q);
                }
            }

            EditorUtility.ClearProgressBar();

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Success!",
                $"Preprocessed {particles.Count:N0} particles\n\n" +
                $"Output: {outputFileName}\n" +
                $"Size: {new FileInfo(outputPath).Length / 1024:N0} KB\n\n" +
                $"This file will load instantly on Quest!",
                "OK"
            );

            Debug.Log($"✅ Dataset preprocessed successfully!\n" +
                      $"Particles: {particles.Count:N0}\n" +
                      $"Downsampling: {stepSize}x\n" +
                      $"Output: {outputPath}");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", $"Failed to preprocess dataset:\n\n{ex.Message}", "OK");
            Debug.LogError($"Preprocessing error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    [System.Serializable]
    struct ParticleData
    {
        public int t, z, y, x;
        public float q;
    }
}
