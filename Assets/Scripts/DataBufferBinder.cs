using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class ParticleAnimationController : MonoBehaviour
{
    [Header("Data Source (choose one)")]
    public HDF5_InspectAndRead reader; // Legacy: slow runtime processing
    public FastDatasetLoader fastLoader; // Recommended: instant loading from preprocessed file

    public ComputeShader preprocessShader;
    public bool useTestData = true; // toggle in Inspector
    public bool initializeOnStart = false; // if false, wait for manual initialization (RECOMMENDED: keep false to avoid startup freeze)

    [Header("Timestep Control")]
    public int currentTimestep = 0;
    public bool autoPlay = false;
    public float timestepInterval = 1.0f; // seconds between timesteps
    public int maxTimestep = 10; // max timestep to cycle to (set based on your data)

    private GraphicsBuffer rawBuffer;
    private GraphicsBuffer visualBuffer;
    private VisualEffect vfx;

    private int pointCount;
    private int kernel;
    private bool isInitialized = false;
    private int lastDispatchedTimestep = -1;
    private float timeSinceLastStep = 0f;

    // Public accessor
    public bool IsInitialized => isInitialized;

    void Start()
    {
        Debug.Log("ParticleAnimationController is running!!!!");
        if (vfx == null)
            vfx = GetComponent<VisualEffect>();

        // Hide VFX initially if not auto-initializing
        if (!initializeOnStart)
        {
            vfx.Stop();
            Debug.Log("💤 Dataset not initialized. Call InitializeDataset() to start.");
        }
        else
        {
            StartCoroutine(WaitForBufferAndInit());
        }
    }

    System.Collections.IEnumerator WaitForBufferAndInit()
    {
        vfx = GetComponent<VisualEffect>();
        vfx.Reinit();

        // 🧱 If using test data, skip waiting for reader
        if (useTestData)
        {
            CreateTestBuffer();

            yield return null; // wait one frame for VFX Graph

            // Set up compute shader to process test data (same as real data path)
            if (preprocessShader == null)
            {
                Debug.LogError("Missing compute shader reference! Assign PreProcessParticles.compute in Inspector.");
                enabled = false;
                yield break;
            }

            // Output buffer needs to match ParticleData struct size
            int structSize = sizeof(int) * 4 + sizeof(float); // t,z,y,x,q
            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            // Dispatch initial timestep
            DispatchForStep(currentTimestep);
            lastDispatchedTimestep = currentTimestep;

            Debug.Log($"✅ Processed and bound test cube buffer ({pointCount} points) to VFX Graph.");

            isInitialized = true;
            yield break;
        }

        // --- Normal data path ---
        else
        {
            // Try fast loader first, fallback to legacy reader
            GraphicsBuffer sourceBuffer = null;

            if (fastLoader != null)
            {
                Debug.Log("⚡ Using FastDatasetLoader (preprocessed data)...");

                // Start loading if not already started
                if (!fastLoader.IsLoaded && fastLoader.buffer == null)
                {
                    fastLoader.StartLoading();
                }

                // Wait for loader to finish (with timeout)
                float startTime = Time.realtimeSinceStartup;
                while (fastLoader.buffer == null)
                {
                    if (Time.realtimeSinceStartup - startTime > 30f)
                    {
                        Debug.LogError("❌ FastDatasetLoader timed out after 30 seconds!");
                        enabled = false;
                        yield break;
                    }
                    yield return null;
                }

                sourceBuffer = fastLoader.buffer;
                Debug.Log("✅ Fast loader ready!");
            }
            else if (reader != null)
            {
                Debug.Log("⚠️ Using HDF5_InspectAndRead (slow legacy mode)...");
                while (reader.buffer == null)
                    yield return null;

                sourceBuffer = reader.buffer;
                Debug.Log("✅ Legacy reader ready!");
            }
            else
            {
                Debug.LogError("❌ No data source assigned! Assign either FastDatasetLoader or HDF5_InspectAndRead.");
                enabled = false;
                yield break;
            }

            if (preprocessShader == null)
            {
                Debug.LogError("Missing compute shader reference!");
                enabled = false;
                yield break;
            }

            yield return null; // wait one frame for VFX Graph

            rawBuffer = sourceBuffer;
            pointCount = rawBuffer.count;

            // Debug: Analyze timestep distribution in real data
            HDF5_InspectAndRead.ParticleData[] sampleData = new HDF5_InspectAndRead.ParticleData[Mathf.Min(100, pointCount)];
            rawBuffer.GetData(sampleData, 0, 0, sampleData.Length);

            int[] timestepCounts = new int[20]; // Count particles per timestep
            for (int i = 0; i < sampleData.Length; i++)
            {
                if (sampleData[i].t >= 0 && sampleData[i].t < timestepCounts.Length)
                    timestepCounts[sampleData[i].t]++;
            }

            Debug.Log("=== TIMESTEP DISTRIBUTION (first 100 particles) ===");
            for (int t = 0; t < timestepCounts.Length; t++)
            {
                if (timestepCounts[t] > 0)
                    Debug.Log($"Timestep {t}: {timestepCounts[t]} particles");
            }

            // Output buffer needs to match ParticleData struct size
            int structSize = sizeof(int) * 4 + sizeof(float); // t,z,y,x,q
            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            // Dispatch initial timestep
            DispatchForStep(currentTimestep);
            lastDispatchedTimestep = currentTimestep;
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized)
            return;

        // Auto-play: cycle through timesteps automatically
        if (autoPlay)
        {
            timeSinceLastStep += Time.deltaTime;

            if (timeSinceLastStep >= timestepInterval)
            {
                timeSinceLastStep = 0f;
                currentTimestep = (currentTimestep + 1) % (maxTimestep + 1); // Loop back to 0 after maxTimestep
                Debug.Log($"🎬 Auto-play: advancing to timestep {currentTimestep}");
            }
        }

        // Re-dispatch if timestep changed (manual or auto)
        if (currentTimestep != lastDispatchedTimestep)
        {
            Debug.Log($"Timestep changed from {lastDispatchedTimestep} to {currentTimestep}");
            DispatchForStep(currentTimestep);
            lastDispatchedTimestep = currentTimestep;
        }
    }

    void DispatchForStep(int step)
    {
        if (preprocessShader != null)
        {
            preprocessShader.SetInt("CurrentTimeStep", step);
            int groups = Mathf.CeilToInt(pointCount / 256f);
            preprocessShader.Dispatch(kernel, groups, 1, 1);

            Debug.Log($"Dispatched compute shader for timestep {step} with {groups} thread groups");
        }
        else
        {
            Debug.LogError("Compute shader is null! Cannot process particle data.");
        }

        // Find and log first 20 particles matching current timestep
        HDF5_InspectAndRead.ParticleData[] allData = new HDF5_InspectAndRead.ParticleData[pointCount];
        visualBuffer.GetData(allData);

        int matchCount = 0;
        int loggedCount = 0;
        Debug.Log($"=== PARTICLES MATCHING TIMESTEP {step} (first 20) ===");

        for (int i = 0; i < allData.Length && loggedCount < 20; i++)
        {
            if (allData[i].t == step)
            {
                if (loggedCount < 20)
                {
                    Debug.Log($"[index {i}] t={allData[i].t} x={allData[i].x} y={allData[i].y} z={allData[i].z} q={allData[i].q:F5}");
                    loggedCount++;
                }
                matchCount++;
            }
        }

        Debug.Log($"📊 Timestep {step}: Found {matchCount} particles out of {pointCount} total ({(matchCount * 100f / pointCount):F2}%)");

        // Check if we need more capacity
        if (matchCount > 40000)
        {
            Debug.LogWarning($"⚠️ WARNING: {matchCount} particles for timestep {step}, but VFX capacity is only 40,000!");
            Debug.LogWarning($"   Open Dataset_Visual.vfx and increase capacity to at least {Mathf.CeilToInt(matchCount * 1.2f)}");
        }

        vfx.SetUInt("PointCount", (uint)pointCount);
        vfx.SetInt("CurrentTimestep", step);
        vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);

        if (!vfx.HasGraphicsBuffer("DataBuffer"))
            Debug.LogError("VFX Graph is missing DataBuffer!");
        else
            Debug.Log($"✅ Bound DataBuffer with {pointCount} points to VFX.");

        // Verify VFX parameters
        Debug.Log($"VFX Parameters: PointCount={vfx.GetUInt("PointCount")}, CurrentTimestep={vfx.GetInt("CurrentTimestep")}");

        // Force VFX to respawn particles by stopping and restarting
        vfx.Stop();
        vfx.Reinit();
        vfx.Play();
    }

    void OnDestroy()
    {
        rawBuffer?.Release();
        visualBuffer?.Release();
    }

    // === PUBLIC METHODS FOR UI BUTTONS ===

    /// <summary>
    /// Initialize and display the dataset. Call this from a UI button.
    /// </summary>
    public void InitializeDataset()
    {
        if (isInitialized)
        {
            Debug.LogWarning("Dataset already initialized!");
            return;
        }

        Debug.Log("🚀 Initializing dataset...");
        StartCoroutine(WaitForBufferAndInit());
    }

    /// <summary>
    /// Uninitialize and hide the dataset. Call this from a UI button.
    /// </summary>
    public void UninitializeDataset()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Dataset not initialized!");
            return;
        }

        Debug.Log("🛑 Uninitializing dataset...");

        // Stop auto-play
        autoPlay = false;

        // Stop and clear VFX
        vfx.Stop();
        vfx.Reinit();

        // DON'T release rawBuffer - we need to keep the source data!
        // Only release the visual buffer
        visualBuffer?.Release();
        visualBuffer = null;

        // Note: rawBuffer is kept because it's managed by FastDatasetLoader/HDF5Reader
        // We just clear our reference to the visual buffer

        // Reset state
        isInitialized = false;
        lastDispatchedTimestep = -1;
        currentTimestep = 0;
        timeSinceLastStep = 0f;

        Debug.Log("💤 Dataset uninitialized and hidden. Ready to re-initialize.");
    }

    /// <summary>
    /// Toggle auto-play on/off. Call this from a Play/Pause button.
    /// </summary>
    public void ToggleAutoPlay()
    {
        autoPlay = !autoPlay;

        if (autoPlay)
        {
            Debug.Log("▶️ Auto-play STARTED");
            timeSinceLastStep = 0f; // Reset timer
        }
        else
        {
            Debug.Log("⏸️ Auto-play PAUSED");
        }
    }

    /// <summary>
    /// Start auto-play. Call this from a Play button.
    /// </summary>
    public void Play()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ Dataset not initialized! Call InitializeDataset() first.");
            return;
        }

        autoPlay = true;
        timeSinceLastStep = 0f;
        Debug.Log("▶️ Auto-play STARTED");
    }

    /// <summary>
    /// Pause auto-play. Call this from a Pause button.
    /// </summary>
    public void Pause()
    {
        autoPlay = false;
        Debug.Log("⏸️ Auto-play PAUSED");
    }

    /// <summary>
    /// Go to next timestep manually. Call this from a Next button.
    /// </summary>
    public void NextTimestep()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ Dataset not initialized!");
            return;
        }

        currentTimestep = (currentTimestep + 1) % (maxTimestep + 1);
        Debug.Log($"⏭️ Advanced to timestep {currentTimestep}");
    }

    /// <summary>
    /// Go to previous timestep manually. Call this from a Previous button.
    /// </summary>
    public void PreviousTimestep()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ Dataset not initialized!");
            return;
        }

        currentTimestep = (currentTimestep - 1 + maxTimestep + 1) % (maxTimestep + 1);
        Debug.Log($"⏮️ Went back to timestep {currentTimestep}");
    }

    /// <summary>
    /// Reset to timestep 0. Call this from a Reset button.
    /// </summary>
    public void ResetToStart()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠️ Dataset not initialized!");
            return;
        }

        currentTimestep = 0;
        autoPlay = false;
        Debug.Log("🔄 Reset to timestep 0");
    }

    // 🧱 Generate test cube with single timestep
    void CreateTestBuffer()
    {
        int width = 50, height = 15, depth = 50;
        pointCount = width * height * depth;

        HDF5_InspectAndRead.ParticleData[] points = new HDF5_InspectAndRead.ParticleData[pointCount];
        int index = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    points[index] = new HDF5_InspectAndRead.ParticleData
                    {
                        t = 0,          // All particles at timestep 0
                        z = z,
                        y = y,
                        x = x,
                        q = (float)index / pointCount      // Gradient from 0 to 1
                    };
                    index++;
                }
            }
        }

        // Create rawBuffer in ParticleData format
        int structSize = sizeof(int) * 4 + sizeof(float); // t,z,y,x (ints) + q (float)
        rawBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);
        rawBuffer.SetData(points);

        Debug.Log($"🧱 Created test cube buffer with {pointCount} points in ParticleData format.");
    }

}
