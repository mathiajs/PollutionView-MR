using System;
using System.IO;
using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

/// <summary>
/// Fast dataset loader that reads preprocessed binary data.
/// Loads asynchronously to avoid freezing the app.
/// </summary>
public class FastDatasetLoader : MonoBehaviour
{
    [Tooltip("Name of the preprocessed .bytes file in StreamingAssets")]
    public string preprocessedFileName = "preprocessed_particles.bytes";

    [Tooltip("Load immediately on Start (disable if you want manual control)")]
    public bool loadOnStart = true;

    [Tooltip("Auto-load on Quest but manual in Editor (for faster testing)")]
    public bool autoLoadOnlyOnDevice = true;

    [Tooltip("Particles to process per frame (higher = faster, lower = smoother)")]
    public int particlesPerFrame = 50000; // Increased for faster loading

    public GraphicsBuffer buffer;
    private bool isLoaded = false;
    public bool isLoading = false;

    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct ParticleData
    {
        public int t, z, y, x;
        public float q;
    }

    // Metadata from file
    private int particleCount;
    private int downsamplingFactor;
    private int timeDim, zDim, yDim, xDim;

    public bool IsLoaded => isLoaded;
    public bool IsLoading => isLoading;

    void Start()
    {
        bool shouldAutoLoad = loadOnStart;

        // Skip auto-load in Editor if autoLoadOnlyOnDevice is enabled
        if (autoLoadOnlyOnDevice && Application.isEditor)
        {
            shouldAutoLoad = false;
            Debug.Log("📝 Editor mode: Auto-load disabled. Right-click component → 'Load Now' to load manually.");
        }

        if (shouldAutoLoad)
        {
            Debug.Log("🚀 Auto-loading dataset on start...");
            StartCoroutine(LoadPreprocessedData());
        }
    }

    public void StartLoading()
    {
        if (!isLoaded && buffer == null && !isLoading)
        {
            StartCoroutine(LoadPreprocessedData());
        }
    }

    // Editor convenience: Right-click component → "Load Now" in play mode
    [ContextMenu("Load Now")]
    public void LoadNow()
    {
        if (Application.isPlaying)
        {
            Debug.Log("📥 Manual load triggered from context menu");
            StartLoading();
        }
        else
        {
            Debug.LogWarning("⚠️ Can only load in Play mode!");
        }
    }

    IEnumerator LoadPreprocessedData()
    {
        isLoading = true;
        string path = Path.Combine(Application.streamingAssetsPath, preprocessedFileName);

        Debug.Log($"⚡ Fast loading preprocessed data from: {path}");

        byte[] fileData = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android requires UnityWebRequest to access StreamingAssets
        using (var www = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ Failed to load preprocessed file: {www.error}");
                isLoading = false;
                yield break;
            }

            fileData = www.downloadHandler.data;
        }
#else
        if (!File.Exists(path))
        {
            Debug.LogError($"❌ Preprocessed file not found: {path}\n" +
                          "Run Tools > Dataset Preprocessor in the Unity Editor to generate it.");
            isLoading = false;
            yield break;
        }

        fileData = File.ReadAllBytes(path);
        yield return null;
#endif

        Debug.Log($"📦 Loaded {fileData.Length / 1024:N0} KB in {Time.realtimeSinceStartup:F2}s");
        yield return null; // Let one frame pass after file load

        // Parse binary data
        using (var stream = new MemoryStream(fileData))
        using (var reader = new BinaryReader(stream))
            {
                // Read header
                particleCount = reader.ReadInt32();
                downsamplingFactor = reader.ReadInt32();
                timeDim = reader.ReadInt32();
                zDim = reader.ReadInt32();
                yDim = reader.ReadInt32();
                xDim = reader.ReadInt32();

                Debug.Log($"📊 Dataset info:\n" +
                          $"  Particles: {particleCount:N0}\n" +
                          $"  Downsampling: {downsamplingFactor}x\n" +
                          $"  Dimensions: t={timeDim}, z={zDim}, y={yDim}, x={xDim}");

                yield return null; // Let one frame pass after header

                // Read particle data in chunks to avoid freezing
                ParticleData[] particles = new ParticleData[particleCount];
                int particlesRead = 0;
                float lastYieldTime = Time.realtimeSinceStartup;

                Debug.Log($"🔄 Reading {particleCount:N0} particles in chunks of {particlesPerFrame:N0}...");

                while (particlesRead < particleCount)
                {
                    int chunkSize = Mathf.Min(particlesPerFrame, particleCount - particlesRead);

                    for (int i = 0; i < chunkSize; i++)
                    {
                        int idx = particlesRead + i;
                        particles[idx].t = reader.ReadInt32();
                        particles[idx].z = reader.ReadInt32();
                        particles[idx].y = reader.ReadInt32();
                        particles[idx].x = reader.ReadInt32();
                        particles[idx].q = reader.ReadSingle();
                    }

                    particlesRead += chunkSize;

                    // Yield every chunk or every 33ms (30fps target for loading - faster)
                    if (Time.realtimeSinceStartup - lastYieldTime > 0.033f)
                    {
                        Debug.Log($"  Progress: {particlesRead:N0}/{particleCount:N0} ({(particlesRead * 100f / particleCount):F1}%)");
                        lastYieldTime = Time.realtimeSinceStartup;
                        yield return null;
                    }
                }

                Debug.Log($"✅ All particles read in {Time.realtimeSinceStartup:F2}s");
                yield return null;

                // Create GraphicsBuffer
                Debug.Log($"🎨 Creating GraphicsBuffer...");
                int structSize = sizeof(int) * 4 + sizeof(float); // t,z,y,x,q
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, structSize);

                yield return null;

                Debug.Log($"📤 Uploading data to GPU in chunks...");

                // Upload in chunks to avoid freezing
                int uploadChunkSize = 1000; // Upload 1k particles at a time
                int uploadedCount = 0;

                while (uploadedCount < particleCount)
                {
                    int chunkSize = Mathf.Min(uploadChunkSize, particleCount - uploadedCount);

                    // Upload chunk
                    buffer.SetData(particles, uploadedCount, uploadedCount, chunkSize);
                    uploadedCount += chunkSize;

                    if (uploadedCount % 200000 == 0 || uploadedCount == particleCount)
                    {
                        Debug.Log($"  GPU Upload: {uploadedCount:N0}/{particleCount:N0} ({(uploadedCount * 100f / particleCount):F1}%)");
                        yield return null; // Let a frame pass
                    }
                }

                Debug.Log($"✅ GraphicsBuffer ready with {particleCount:N0} particles!");
                Debug.Log($"⚡ Total load time: {Time.realtimeSinceStartup:F2}s");

                isLoaded = true;
                isLoading = false;
            }
    }

    void OnDestroy()
    {
        buffer?.Release();
    }

    // Public accessors
    public int ParticleCount => particleCount;
    public int DownsamplingFactor => downsamplingFactor;
    public (int time, int z, int y, int x) Dimensions => (timeDim, zDim, yDim, xDim);
}
