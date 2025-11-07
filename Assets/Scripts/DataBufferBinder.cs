using UnityEngine;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(VisualEffect))]
public class ParticleAnimationController : MonoBehaviour
{
    public HDF5_InspectAndRead reader;
    public ComputeShader preprocessShader;
    public bool useTestData = true; // toggle in Inspector
    public bool initializeOnStart = false; // if false, wait for manual initialization

	// Placeholder VFX replacement
	[Header("Placeholder Visualization (replaces dataset when assigned)")]
	public VisualEffectAsset placeholderVfxAsset;
	public Vector3 placeholderSpawnPosition = Vector3.zero;
	public Transform placeholderParent;
	public float placeholderSpawnDistance = 3.0f;
	private VisualEffect placeholderVfxInstance;

	// Placeholder control bindings
	private bool usingPlaceholder = false;
	public string placeholderStepProperty = "CurrentTimestep";
	public string placeholderMainColorProperty = "MainColor";

	[Header("Pollutant Variants")]
	public VisualEffectAsset pollutant1Vfx; // sphere
	public VisualEffectAsset pollutant2Vfx; // cube
	public VisualEffectAsset pollutant3Vfx; // plume (cylinder)

	private VisualEffectAsset currentVfxAsset;
	private int currentPollutant = 1;

	// Script-driven motion (no VFX Graph edits required)
	public bool placeholderUseScriptMotion = true;
	public Vector3 placeholderStepOffset = new Vector3(0f, 0f, 0.25f);
	public float placeholderStepYawDegrees = 15f;
	private Vector3 placeholderBasePosition;
	private Quaternion placeholderBaseRotation;

	// Smooth motion between timesteps
	public bool placeholderSmoothMotion = true;
	public float placeholderMoveSmoothTime = 0.35f;
	public float placeholderRotateLerpSpeed = 8f;
	private Vector3 placeholderMoveVelocity;
	private Vector3 placeholderTargetPosition;
	private Quaternion placeholderTargetRotation;

	[Header("Multi-instance layout")]
	public float pollutantHorizontalSeparation = 1.25f; // meters left/right from center

	// Multi-instance support for showing multiple pollutants at once
	private class PollutantRuntime
	{
		public VisualEffect vfx;
		public Vector3 basePos;
		public Quaternion baseRot;
		public Vector3 targetPos;
		public Quaternion targetRot;
		public Vector3 moveVel;
		public Vector3 stepOffset;
		public float stepYawDeg;
	}

	private readonly System.Collections.Generic.Dictionary<int, PollutantRuntime> activePollutants = new System.Collections.Generic.Dictionary<int, PollutantRuntime>();

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


            if (reader == null || preprocessShader == null)
            {
                Debug.LogError("Missing HDF5 reader or compute shader reference!");
                enabled = false;
                yield break;
            }

            while (reader.buffer == null)
                yield return null;

            yield return null; // wait one frame for VFX Graph

            rawBuffer = reader.buffer;
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

        // === Placeholder VFX control path ===
        if (usingPlaceholder)
        {
            if (autoPlay)
            {
                timeSinceLastStep += Time.deltaTime;
                if (timeSinceLastStep >= timestepInterval)
                {
                    timeSinceLastStep = 0f;
                    currentTimestep = (currentTimestep + 1) % (maxTimestep + 1);
                }
            }

			if (currentTimestep != lastDispatchedTimestep)
            {
                ApplyPlaceholderStep();
                lastDispatchedTimestep = currentTimestep;
            }

			// Smoothly ease transforms for all active instances
			if (placeholderSmoothMotion)
			{
				foreach (var kv in activePollutants)
				{
					var rt = kv.Value;
					if (rt == null || rt.vfx == null) continue;
					rt.vfx.transform.position = Vector3.SmoothDamp(
						rt.vfx.transform.position,
						rt.targetPos,
						ref rt.moveVel,
						placeholderMoveSmoothTime);
					float rT = 1f - Mathf.Exp(-placeholderRotateLerpSpeed * Time.deltaTime);
					rt.vfx.transform.rotation = Quaternion.Slerp(
						rt.vfx.transform.rotation,
						rt.targetRot,
						rT);
				}
			}

            return; // Skip dataset path entirely
        }

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

	void ApplyPlaceholderStep()
	{
		// Drive all active pollutant instances
		foreach (var kv in activePollutants)
		{
			var rt = kv.Value;
			if (rt == null || rt.vfx == null) continue;
			if (!string.IsNullOrEmpty(placeholderStepProperty))
			{
				rt.vfx.SetInt(placeholderStepProperty, currentTimestep);
			}

			if (placeholderUseScriptMotion)
			{
				var stepPos = rt.basePos + (rt.stepOffset * currentTimestep);
				var stepYaw = Quaternion.Euler(0f, rt.stepYawDeg * currentTimestep, 0f);
				var stepRot = stepYaw * rt.baseRot;
				rt.targetPos = stepPos;
				rt.targetRot = stepRot;
				if (!placeholderSmoothMotion)
					rt.vfx.transform.SetPositionAndRotation(stepPos, stepRot);
			}
		}
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

        // Disable/stop the dataset-driven VisualEffect and switch into placeholder mode
        if (vfx == null) vfx = GetComponent<VisualEffect>();
        vfx.Stop();
        vfx.Reinit();
        vfx.enabled = false;

        // Clear any previous single-instance placeholder
        if (placeholderVfxInstance != null)
        {
            placeholderVfxInstance.Stop();
            Destroy(placeholderVfxInstance.gameObject);
            placeholderVfxInstance = null;
        }

        // Clear multi-instance registry (safety)
        foreach (var kv in activePollutants)
        {
            if (kv.Value != null && kv.Value.vfx != null)
                Destroy(kv.Value.vfx.gameObject);
        }
        activePollutants.Clear();

        // Enter placeholder control path; no auto-spawn. Use toggles to add pollutants
        isInitialized = true;
        usingPlaceholder = true;
        currentTimestep = 0;
        timeSinceLastStep = 0f;
        lastDispatchedTimestep = -1;
        Debug.Log("🚀 Placeholder controller initialized. Use pollutant toggles to spawn instances.");
        return;
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

        // Stop and clear dataset-driven VFX
        vfx.Stop();
        vfx.Reinit();

        // Stop and remove any legacy single placeholder instance
        if (placeholderVfxInstance != null)
        {
            placeholderVfxInstance.Stop();
            Destroy(placeholderVfxInstance.gameObject);
            placeholderVfxInstance = null;
        }

        // Stop and remove all active pollutant instances
        foreach (var kv in activePollutants)
        {
            if (kv.Value != null && kv.Value.vfx != null)
            {
                kv.Value.vfx.Stop();
                Destroy(kv.Value.vfx.gameObject);
            }
        }
        activePollutants.Clear();

		usingPlaceholder = false;
		placeholderMoveVelocity = Vector3.zero;

        // Re-enable the dataset VFX component for next time
        if (vfx != null) vfx.enabled = true;

        // Release buffers
        rawBuffer?.Release();
        visualBuffer?.Release();
        rawBuffer = null;
        visualBuffer = null;

        // Reset state
        isInitialized = false;
        lastDispatchedTimestep = -1;
        currentTimestep = 0;
        timeSinceLastStep = 0f;

        Debug.Log("💤 Dataset uninitialized and hidden.");
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

		if (usingPlaceholder)
		{
			autoPlay = true;
			timeSinceLastStep = 0f;
			Debug.Log("▶️ Auto-play STARTED (placeholder)");
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

		if (usingPlaceholder)
		{
			currentTimestep = (currentTimestep + 1) % (maxTimestep + 1);
			ApplyPlaceholderStep();
			Debug.Log($"⏭️ Advanced to timestep {currentTimestep} (placeholder)");
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

		if (usingPlaceholder)
		{
			currentTimestep = (currentTimestep - 1 + maxTimestep + 1) % (maxTimestep + 1);
			ApplyPlaceholderStep();
			Debug.Log($"⏮️ Went back to timestep {currentTimestep} (placeholder)");
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

		if (usingPlaceholder)
		{
			currentTimestep = 0;
			autoPlay = false;
			ApplyPlaceholderStep();
			Debug.Log("🔄 Reset to timestep 0 (placeholder)");
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

    // === Placeholder color controls (for EditorCanvas buttons) ===
    public void SetMainColor(float r, float g, float b, float a = 1f)
    {
        if (!string.IsNullOrEmpty(placeholderMainColorProperty))
        {
            // Apply to all active pollutant instances
            foreach (var kv in activePollutants)
            {
                var rt = kv.Value;
                if (rt != null && rt.vfx != null)
                {
                    rt.vfx.SetVector4(placeholderMainColorProperty, new Vector4(r, g, b, a));
                }
            }
        }
    }

    public void SetMainColorRed()    { SetMainColor(1f, 0f, 0f, 1f); }
    public void SetMainColorBlue()   { SetMainColor(0f, 0f, 1f, 1f); }
    public void SetMainColorYellow() { SetMainColor(1f, 0.92f, 0.016f, 1f); }

    // Toggle helpers (use with Toggle.onValueChanged)
    public void SetMainColorRedToggle(bool isOn)    { if (isOn) SetMainColorRed(); }
    public void SetMainColorBlueToggle(bool isOn)   { if (isOn) SetMainColorBlue(); }
    public void SetMainColorYellowToggle(bool isOn) { if (isOn) SetMainColorYellow(); }

	public void SelectPollutant1Toggle(bool isOn) { if (isOn) SelectPollutant(1); }
	public void SelectPollutant2Toggle(bool isOn) { if (isOn) SelectPollutant(2); }
	public void SelectPollutant3Toggle(bool isOn) { if (isOn) SelectPollutant(3); }

	public void SelectPollutant(int id)
	{
		// Legacy single-instance selector is no longer used in multi-instance mode.
		// Keep method to avoid broken scene references; redirect to Toggle-style behavior: ensure only this pollutant is active.
		foreach (var key in new System.Collections.Generic.List<int>(activePollutants.Keys))
		{
			if (key != id) DespawnPollutant(key);
		}
		TogglePollutant(id, true);
	}

	// === Multi-instance API for UI toggles ===
	public void TogglePollutant1(bool on) { TogglePollutant(1, on); }
	public void TogglePollutant2(bool on) { TogglePollutant(2, on); }
	public void TogglePollutant3(bool on) { TogglePollutant(3, on); }

	void TogglePollutant(int id, bool on)
	{
		if (on) SpawnPollutant(id); else DespawnPollutant(id);
	}

	VisualEffectAsset GetAssetForPollutant(int id)
	{
		switch (id)
		{
			case 1: return pollutant1Vfx ?? currentVfxAsset ?? placeholderVfxAsset; 
			case 2: return pollutant2Vfx ?? currentVfxAsset ?? placeholderVfxAsset; 
			case 3: return pollutant3Vfx ?? currentVfxAsset ?? placeholderVfxAsset; 
			default: return placeholderVfxAsset;
		}
	}

	Vector3 GetSpawnPositionWithOffset(Vector3 localOffset)
	{
		var cam = Camera.main;
		var spawn = placeholderSpawnPosition;
		if (placeholderParent == null && cam != null && spawn == Vector3.zero)
			spawn = cam.transform.position + cam.transform.forward * placeholderSpawnDistance;
		// make offset relative to camera orientation so multiple instances spread around view
		if (cam != null) spawn += cam.transform.TransformDirection(localOffset); else spawn += localOffset;
		return spawn;
	}

	void SpawnPollutant(int id)
	{
		if (activePollutants.ContainsKey(id)) return; // already active
		var asset = GetAssetForPollutant(id);
		if (asset == null)
		{
			Debug.LogWarning($"Pollutant {id} VFX not assigned.");
			return;
		}
		var go = new GameObject($"Pollutant{id}_VFX");
		if (placeholderParent != null) go.transform.SetParent(placeholderParent, false);
		// Per-pollutant horizontal spawn offsets in camera space (left/center/right)
		Vector3 offset =
			id == 1 ? Vector3.zero :
			id == 2 ? new Vector3(+pollutantHorizontalSeparation, 0f, 0f) :
			new Vector3(-pollutantHorizontalSeparation, 0f, 0f);
		go.transform.position = GetSpawnPositionWithOffset(offset);
		var v = go.AddComponent<VisualEffect>();
		v.visualEffectAsset = asset;
		v.Reinit();
		v.Play();
		var rt = new PollutantRuntime
		{
			vfx = v,
			basePos = go.transform.position,
			baseRot = go.transform.rotation,
			targetPos = go.transform.position,
			targetRot = go.transform.rotation,
			moveVel = Vector3.zero,
			stepOffset = (id == 1) ? new Vector3(0.02f, 0.02f, 0.02f)
				: (id == 2) ? new Vector3(0.025f, 0.015f, 0.025f)
				: new Vector3(0.015f, 0.03f, 0.015f),
			stepYawDeg = (id == 1) ? 5f : (id == 2) ? 6f : 4f
		};
		activePollutants[id] = rt;
		// Apply current timestep only; keep asset's default color
		if (!string.IsNullOrEmpty(placeholderStepProperty))
			v.SetInt(placeholderStepProperty, currentTimestep);
	}

	void DespawnPollutant(int id)
	{
		if (!activePollutants.TryGetValue(id, out var rt) || rt == null) return;
		if (rt.vfx != null) { rt.vfx.Stop(); Destroy(rt.vfx.gameObject); }
		activePollutants.Remove(id);
	}
}
