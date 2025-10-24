using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class ParticleAnimationController : MonoBehaviour
{
    public HDF5_InspectAndRead reader;
    public ComputeShader preprocessShader;
    public int timestepCount = 11;
    public float secondsPerStep = 1f;

    private GraphicsBuffer rawBuffer;
    private GraphicsBuffer visualBuffer;
    private GraphicsBuffer dummyBuffer;
    private VisualEffect vfx;

    private int pointCount;
    private int currentStep = 0;
    private float timer;
    private int kernel;

void Start()
{
    Debug.Log("ParticleAnimationController is running!!!!");
    if (vfx == null)
    vfx = GetComponent<VisualEffect>();
    StartCoroutine(WaitForBufferAndInit());
}

System.Collections.IEnumerator WaitForBufferAndInit()
{
    if (reader == null || preprocessShader == null)
    {
        Debug.LogError("Missing HDF5 reader or compute shader reference!");
        enabled = false;
        yield break;
    }

    vfx = GetComponent<VisualEffect>();
    vfx.Reinit();

    // Wait until the buffer is ready
    while (reader.buffer == null)
        yield return null;
        
    // Wait an extra frame to ensure VFX Graph is initialized
    yield return null;

    rawBuffer = reader.buffer;
    pointCount = rawBuffer.count;

    visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 4);

    kernel = preprocessShader.FindKernel("CSMain");
    preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
    preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
    preprocessShader.SetInt("PointCount", pointCount);
    
    DispatchForStep(0);
}

    void Update()
    {
        timer += Time.deltaTime;
        if (timer > secondsPerStep)
        {
            timer = 0f;
            currentStep = (currentStep + 1) % timestepCount;
            DispatchForStep(currentStep);
        }
    }

    void DispatchForStep(int step)
    {
        preprocessShader.SetInt("_CurrentTimeStep", step);

        int groups = Mathf.CeilToInt(pointCount / 256f);
        preprocessShader.Dispatch(kernel, groups, 1, 1);

        var exposed = new System.Collections.Generic.List<VFXExposedProperty>();
        vfx.visualEffectAsset.GetExposedProperties(exposed);
        foreach (var prop in exposed)
            Debug.Log($"Exposed: {prop.name} ({prop.type})");


        // Send the filtered buffer to the VFX Graph
        vfx.SetUInt("PointCount", (uint)pointCount);
        vfx.SetInt("TimeStep", step);
        vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);
        if (!vfx.HasGraphicsBuffer("DataBuffer"))
            Debug.LogError("VFX Graph is missing DataBuffer!");
        else
            Debug.Log("Successfully bound DataBuffer to VFX Graph.");
        vfx.Play();
    }

    void OnDestroy()
    {
        visualBuffer?.Release();
    }
}