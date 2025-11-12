using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class ParticleAnimationController : MonoBehaviour
{
    [Header("Data Source")]
    public PrebakedDatasetLoader prebakedLoader;

    public ComputeShader preprocessShader;
    public bool useTestData = true;
    public bool initializeOnStart = false;

    [Header("Debug Settings")]
    [Tooltip("Enable expensive GPU readback logging (causes LAG! Disable for Quest builds)")]
    public bool enableDebugLogging = false;

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
    private int currentColorScheme = 1;

    public bool IsInitialized => isInitialized;

    void Start()
    {
        Debug.Log("ParticleAnimationController is running!!!!");
        if (vfx == null)
            vfx = GetComponent<VisualEffect>();

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

        if (useTestData)
        {
            CreateTestBuffer();

            yield return null;

            if (preprocessShader == null)
            {
                Debug.LogError("Missing compute shader reference! Assign PreProcessParticles.compute in Inspector.");
                enabled = false;
                yield break;
            }

            int structSize = sizeof(int) * 4 + sizeof(float);
            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            DispatchForStep(currentTimestep);
            lastDispatchedTimestep = currentTimestep;

            Debug.Log($"Processed and bound test cube buffer ({pointCount} points) to VFX Graph.");

            isInitialized = true;
            yield break;
        }

        else
        {
            if (prebakedLoader == null)
            {
                Debug.LogError("No data source assigned! Assign PrebakedDatasetLoader in the Inspector.");
                enabled = false;
                yield break;
            }

            Debug.Log("Using PrebakedDatasetLoader");

            float startTime = Time.realtimeSinceStartup;
            while (prebakedLoader.buffer == null && !prebakedLoader.IsLoaded)
            {
                if (Time.realtimeSinceStartup - startTime > 5f)
                {
                    Debug.LogError("PrebakedDatasetLoader timed out after 5 seconds!");
                    enabled = false;
                    yield break;
                }
                yield return null;
            }

            GraphicsBuffer sourceBuffer = prebakedLoader.buffer;
            Debug.Log($"Prebaked loader ready! Load time: {Time.realtimeSinceStartup - startTime:F3}s");

            vfx.SetInt("MinX", prebakedLoader.MinX);
            vfx.SetInt("MaxX", prebakedLoader.MaxX);
            vfx.SetInt("MinY", prebakedLoader.MinY);
            vfx.SetInt("MaxY", prebakedLoader.MaxY);
            vfx.SetInt("MinZ", prebakedLoader.MinZ);
            vfx.SetInt("MaxZ", prebakedLoader.MaxZ);
            vfx.SetFloat("MinQ", prebakedLoader.MinQ);
            vfx.SetFloat("MaxQ", prebakedLoader.MaxQ);

            Debug.Log($"Value ranges passed to VFX:\n" +
                     $"X: {prebakedLoader.MinX} to {prebakedLoader.MaxX}\n" +
                     $"Y: {prebakedLoader.MinY} to {prebakedLoader.MaxY}\n" +
                     $"Z: {prebakedLoader.MinZ} to {prebakedLoader.MaxZ}\n" +
                     $"Q: {prebakedLoader.MinQ:F6} to {prebakedLoader.MaxQ:F6}");

            vfx.SetInt("ColorScheme", 1);
            Debug.Log("Initial color scheme set to Pollutant 1");

            if (preprocessShader == null)
            {
                Debug.LogError("Missing compute shader reference!");
                enabled = false;
                yield break;
            }

            yield return null;

            rawBuffer = sourceBuffer;
            pointCount = rawBuffer.count;

            // Debug: Analyze timestep distribution in real data (EXPENSIVE - only if debug enabled)
            if (enableDebugLogging)
            {
                ParticleData[] sampleData = new ParticleData[Mathf.Min(100, pointCount)];
                rawBuffer.GetData(sampleData, 0, 0, sampleData.Length);

                int[] timestepCounts = new int[20];
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
            }

            int structSize = sizeof(int) * 4 + sizeof(float);
            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            DispatchForStep(currentTimestep);
            lastDispatchedTimestep = currentTimestep;
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized)
            return;

        if (autoPlay)
        {
            timeSinceLastStep += Time.deltaTime;

            if (timeSinceLastStep >= timestepInterval)
            {
                timeSinceLastStep = 0f;
                currentTimestep = (currentTimestep + 1) % (maxTimestep + 1);
                Debug.Log($"🎬 Auto-play: advancing to timestep {currentTimestep}");
            }
        }

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

        if (enableDebugLogging)
        {
            ParticleData[] allData = new ParticleData[pointCount];
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

            Debug.Log($"Timestep {step}: Found {matchCount} particles out of {pointCount} total ({(matchCount * 100f / pointCount):F2}%)");
        }
        else
        {
            Debug.Log($"Switched to timestep {step}");
        }

        vfx.SetUInt("PointCount", (uint)pointCount);
        vfx.SetInt("CurrentTimestep", step);
        vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);

        if (enableDebugLogging)
        {
            if (!vfx.HasGraphicsBuffer("DataBuffer"))
                Debug.LogError("VFX Graph is missing DataBuffer!");
            else
                Debug.Log($"Bound DataBuffer with {pointCount} points to VFX.");

            Debug.Log($"VFX Parameters: PointCount={vfx.GetUInt("PointCount")}, CurrentTimestep={vfx.GetInt("CurrentTimestep")}");
        }

        vfx.Stop();
        vfx.Reinit();
        vfx.Play();

        vfx.SetInt("ColorScheme", currentColorScheme);
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

        Debug.Log("Initializing dataset...");
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

        autoPlay = false;

        vfx.Stop();
        vfx.Reinit();

        visualBuffer?.Release();
        visualBuffer = null;


        // Reset state
        isInitialized = false;
        lastDispatchedTimestep = -1;
        currentTimestep = 0;
        timeSinceLastStep = 0f;

        Debug.Log("Dataset uninitialized and hidden.");
    }

    /// <summary>
    /// Toggle auto-play on/off. Call this from a Play/Pause button.
    /// </summary>
    public void ToggleAutoPlay()
    {
        autoPlay = !autoPlay;

        if (autoPlay)
        {
            Debug.Log("Auto-play STARTED");
            timeSinceLastStep = 0f;
        }
        else
        {
            Debug.Log("Auto-play PAUSED");
        }
    }

    /// <summary>
    /// Start auto-play. Call this from a Play button.
    /// </summary>
    public void Play()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Dataset not initialized! Call InitializeDataset() first.");
            return;
        }

        autoPlay = true;
        timeSinceLastStep = 0f;
        Debug.Log("Auto-play STARTED");
    }

    /// <summary>
    /// Pause auto-play. Call this from a Pause button.
    /// </summary>
    public void Pause()
    {
        autoPlay = false;
        Debug.Log("Auto-play PAUSED");
    }

    /// <summary>
    /// Go to next timestep manually. Call this from a Next button.
    /// </summary>
    public void NextTimestep()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Dataset not initialized!");
            return;
        }

        currentTimestep = (currentTimestep + 1) % (maxTimestep + 1);
        Debug.Log($"Advanced to timestep {currentTimestep}");
    }

    /// <summary>
    /// Go to previous timestep manually. Call this from a Previous button.
    /// </summary>
    public void PreviousTimestep()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Dataset not initialized!");
            return;
        }

        currentTimestep = (currentTimestep - 1 + maxTimestep + 1) % (maxTimestep + 1);
        Debug.Log($"Went back to timestep {currentTimestep}");
    }

    /// <summary>
    /// Reset to timestep 0. Call this from a Reset button.
    /// </summary>
    public void ResetToStart()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Dataset not initialized!");
            return;
        }

        currentTimestep = 0;
        autoPlay = false;
        Debug.Log("Reset to timestep 0");
    }

    /// <summary>
    /// Set the color scheme for particle visualization.
    /// Called by CanvasHelper when user toggles pollutant selection.
    /// </summary>
    /// <param name="colorSchemeIndex">1 = Pollutant 1 (Red), 2 = Pollutant 2 (Yellow), 3 = Pollutant 3 (Purple)</param>
    public void SetColorScheme(int colorSchemeIndex)
    {
        currentColorScheme = colorSchemeIndex;

        if (vfx != null)
        {
            vfx.SetInt("ColorScheme", colorSchemeIndex);

            if (isInitialized)
            {
                vfx.SetInt("CurrentTimestep", currentTimestep);

                vfx.Stop();
                vfx.Reinit();
                vfx.Play();

                Debug.Log($"VFX color scheme changed to Pollutant {colorSchemeIndex} - visual update applied");
            }
            else
            {
                Debug.Log($"VFX color scheme set to Pollutant {colorSchemeIndex} (will apply when dataset initializes)");
            }
        }
        else
        {
            Debug.LogError("VFX reference is null!");
        }
    }

    //Mainly a Debug method, use this to check if the reader or the visualization is the problem
    void CreateTestBuffer()
    {
        int width = 50, height = 15, depth = 50;
        pointCount = width * height * depth;

        ParticleData[] points = new ParticleData[pointCount];
        int index = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    points[index] = new ParticleData
                    {
                        t = 0,
                        z = z,
                        y = y,
                        x = x,
                        q = (float)index / pointCount  
                    };
                    index++;
                }
            }
        }

        // Create rawBuffer in ParticleData format
        int structSize = sizeof(int) * 4 + sizeof(float); // t,z,y,x (ints) + q (float)
        rawBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, structSize);
        rawBuffer.SetData(points);

        Debug.Log($"Created test cube buffer with {pointCount} points in ParticleData format.");
    }

}
