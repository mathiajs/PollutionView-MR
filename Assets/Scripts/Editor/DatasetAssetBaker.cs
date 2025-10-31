using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using PureHDF;
using System;

/// <summary>
/// Editor tool to "bake" HDF5 data into a Unity ScriptableObject asset.
/// This asset loads INSTANTLY at runtime - no processing needed!
/// </summary>
public class DatasetAssetBaker : EditorWindow
{
    private string sourceFileName = "dp_3d_clean.001.nc";
    private string assetName = "BakedDatasetAsset";
    private int stepSize = 8;
    private float minThreshold = 0.006f;
    private float maxThreshold = 0.00975f;
    private float missingValueMarker = -999999f;
    private bool excludeOrigin = true;
    private int maxParticles = 500000;

    [MenuItem("Tools/Dataset Asset Baker")]
    public static void ShowWindow()
    {
        GetWindow<DatasetAssetBaker>("Dataset Asset Baker");
    }

    void OnGUI()
    {
        GUILayout.Label("Bake Dataset to Unity Asset", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "This creates a Unity ScriptableObject asset with pre-processed data.\n" +
            "At runtime, it loads INSTANTLY with zero processing!",
            MessageType.Info
        );

        GUILayout.Space(10);

        sourceFileName = EditorGUILayout.TextField("Source File (.nc):", sourceFileName);
        assetName = EditorGUILayout.TextField("Asset Name:", assetName);

        GUILayout.Space(10);
        stepSize = EditorGUILayout.IntSlider("Downsampling Step:", stepSize, 1, 16);

        GUILayout.Space(10);
        minThreshold = EditorGUILayout.FloatField("Min Threshold:", minThreshold);
        maxThreshold = EditorGUILayout.FloatField("Max Threshold:", maxThreshold);
        missingValueMarker = EditorGUILayout.FloatField("Missing Value Marker:", missingValueMarker);

        GUILayout.Space(10);
        excludeOrigin = EditorGUILayout.Toggle("Exclude Origin (0,0,0):", excludeOrigin);
        maxParticles = EditorGUILayout.IntField("Max Particles:", maxParticles);

        GUILayout.Space(20);

        if (GUILayout.Button("Bake Dataset to Asset", GUILayout.Height(40)))
        {
            BakeDataset();
        }
    }

    void BakeDataset()
    {
        string sourcePath = Path.Combine(Application.streamingAssetsPath, sourceFileName);

        if (!File.Exists(sourcePath))
        {
            EditorUtility.DisplayDialog("Error", $"Source file not found:\n{sourcePath}", "OK");
            return;
        }

        EditorUtility.DisplayProgressBar("Baking Dataset", "Reading HDF5 file...", 0.1f);

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

            EditorUtility.DisplayProgressBar("Baking Dataset", "Loading dataset...", 0.2f);

            var dataset = file.Dataset("/q");
            float[] flat = dataset.Read<float[]>();

            // Dataset dimensions
            int time = 11, z = 41, y = 412, x = 412;
            int stepZ = z / stepSize;
            int stepY = y / stepSize;
            int stepX = x / stepSize;

            EditorUtility.DisplayProgressBar("Baking Dataset", "Processing particles...", 0.4f);

            var particles = new System.Collections.Generic.List<PrebakedDatasetAsset.ParticleData>();
            int totalChecked = 0;
            int missingCount = 0;
            int validCount = 0;

            for (int t = 0; t < time; t++)
            {
                EditorUtility.DisplayProgressBar(
                    "Baking Dataset",
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

                            // Skip missing data
                            if (Mathf.Approximately(value, missingValueMarker) || value < -999000f)
                            {
                                missingCount++;
                                continue;
                            }

                            validCount++;

                            // Exclude origin if enabled
                            if (excludeOrigin && xx == 0 && yy == 0 && zz == 0)
                                continue;

                            // Apply threshold filter
                            if (value < minThreshold || value > maxThreshold) continue;

                            // Check particle limit
                            if (particles.Count >= maxParticles)
                            {
                                Debug.LogWarning($"⚠️ Reached particle limit ({maxParticles:N0})");
                                goto LIMIT_REACHED;
                            }

                            particles.Add(new PrebakedDatasetAsset.ParticleData
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

            LIMIT_REACHED:

            EditorUtility.DisplayProgressBar("Baking Dataset", "Creating asset...", 0.9f);

            // Create the ScriptableObject asset
            PrebakedDatasetAsset asset = ScriptableObject.CreateInstance<PrebakedDatasetAsset>();
            asset.particleCount = particles.Count;
            asset.downsamplingFactor = stepSize;
            asset.timeDim = time;
            asset.zDim = stepZ;
            asset.yDim = stepY;
            asset.xDim = stepX;

            // Serialize particle data to byte array
            int structSize = sizeof(int) * 4 + sizeof(float);
            asset.serializedData = new byte[particles.Count * structSize];

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(asset.serializedData))
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
            {
                foreach (var p in particles)
                {
                    writer.Write(p.t);
                    writer.Write(p.z);
                    writer.Write(p.y);
                    writer.Write(p.x);
                    writer.Write(p.q);
                }
            }

            // Save as asset
            string assetPath = $"Assets/{assetName}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.ClearProgressBar();

            float sizeMB = asset.GetMemorySizeMB();

            EditorUtility.DisplayDialog(
                "Success!",
                $"Baked {particles.Count:N0} particles into Unity asset!\n\n" +
                $"Asset: {assetName}.asset\n" +
                $"Size: {sizeMB:F2} MB\n\n" +
                $"This will load INSTANTLY at runtime!",
                "OK"
            );

            Debug.Log($"✅ Dataset baked successfully!\n" +
                      $"Particles: {particles.Count:N0}\n" +
                      $"Asset: {assetPath}\n" +
                      $"Size: {sizeMB:F2} MB");

            // Select the asset
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", $"Failed to bake dataset:\n\n{ex.Message}", "OK");
            Debug.LogError($"Baking error: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
