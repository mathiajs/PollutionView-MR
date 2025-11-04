using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

/// <summary>
/// Loads a pre-baked dataset asset at runtime.
/// INSTANT loading - no processing needed!
/// </summary>
public class PrebakedDatasetLoader : MonoBehaviour
{
    [Header("Prebaked Asset")]
    public PrebakedDatasetAsset datasetAsset;

    [Header("Settings")]
    public bool loadOnStart = true;

    [HideInInspector]
    public GraphicsBuffer buffer;
    private bool isLoaded = false;

    public bool IsLoaded => isLoaded;
    public int ParticleCount => datasetAsset != null ? datasetAsset.particleCount : 0;

    // Public accessors for value ranges
    public int MinX => datasetAsset != null ? datasetAsset.minX : 0;
    public int MaxX => datasetAsset != null ? datasetAsset.maxX : 0;
    public int MinY => datasetAsset != null ? datasetAsset.minY : 0;
    public int MaxY => datasetAsset != null ? datasetAsset.maxY : 0;
    public int MinZ => datasetAsset != null ? datasetAsset.minZ : 0;
    public int MaxZ => datasetAsset != null ? datasetAsset.maxZ : 0;
    public float MinQ => datasetAsset != null ? datasetAsset.minQ : 0f;
    public float MaxQ => datasetAsset != null ? datasetAsset.maxQ : 0f;

    void Start()
    {
        if (loadOnStart && datasetAsset != null)
        {
            StartCoroutine(LoadFromAsset());
        }
    }

    IEnumerator LoadFromAsset()
    {
        if (datasetAsset == null)
        {
            Debug.LogError("❌ No dataset asset assigned!");
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;

        Debug.Log($"⚡ Loading prebaked dataset asset: {datasetAsset.name}");
        Debug.Log($"📊 Particles: {datasetAsset.particleCount:N0}, Size: {datasetAsset.GetMemorySizeMB():F2} MB");

        // This is INSTANT - just memory copy!
        buffer = datasetAsset.CreateGraphicsBuffer();

        yield return null;

        if (buffer == null)
        {
            Debug.LogError("❌ Failed to create GraphicsBuffer from asset!");
            yield break;
        }

        isLoaded = true;

        float loadTime = Time.realtimeSinceStartup - startTime;
        Debug.Log($"✅ Prebaked dataset loaded in {loadTime:F3}s - INSTANT!");
    }

    [ContextMenu("Load Now")]
    public void LoadNow()
    {
        if (Application.isPlaying && !isLoaded)
        {
            StartCoroutine(LoadFromAsset());
        }
    }

    void OnDestroy()
    {
        buffer?.Release();
    }
}
