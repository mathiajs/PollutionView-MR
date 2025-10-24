using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class ParticleAnimationController : MonoBehaviour
{
    public HDF5_InspectAndRead reader;
    public ComputeShader preprocessShader;
    public bool useTestData = true; // toggle in Inspector

    private GraphicsBuffer rawBuffer;
    private GraphicsBuffer visualBuffer;
    private VisualEffect vfx;

    private int pointCount;
    private int kernel;
    private bool hasDispatchedFirstStep = false;

    void Start()
    {
        Debug.Log("ParticleAnimationController is running!!!!");
        if (vfx == null)
            vfx = GetComponent<VisualEffect>();

        StartCoroutine(WaitForBufferAndInit());
    }

    System.Collections.IEnumerator WaitForBufferAndInit()
    {
        vfx = GetComponent<VisualEffect>();
        vfx.Reinit();

        // 🧱 If using test data, skip waiting for reader
        if (useTestData)
        {
            CreateTestBuffer();

            // Bind immediately to VFX
            vfx.SetUInt("PointCount", (uint)pointCount);
            vfx.SetInt("TimeStep", 0);
            vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);

            Debug.Log($"✅ Bound test cube buffer ({pointCount} points) to VFX Graph.");
            vfx.Play();

            hasDispatchedFirstStep = true;
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

            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 4);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            // Dispatch only once
            DispatchForStep(0);
            hasDispatchedFirstStep = true;
        }
    }

    void Update()
    {
        if (hasDispatchedFirstStep)
            return;
    }

    void DispatchForStep(int step)
    {
        if (!useTestData && preprocessShader != null)
        {
            preprocessShader.SetInt("_CurrentTimeStep", step);
            int groups = Mathf.CeilToInt(pointCount / 256f);
            preprocessShader.Dispatch(kernel, groups, 1, 1);
        }

        LogFirstPoints(visualBuffer, Mathf.Min(20, pointCount));

        vfx.SetUInt("PointCount", (uint)pointCount);
        vfx.SetInt("TimeStep", step);
        vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);

        if (!vfx.HasGraphicsBuffer("DataBuffer"))
            Debug.LogError("VFX Graph is missing DataBuffer!");
        else
            Debug.Log($"Successfully bound DataBuffer with {pointCount} points.");

        vfx.Play();
    }

    void OnDestroy()
    {
        visualBuffer?.Release();
    }

    // 🧱 Generate 10x10x3 cube (one point per meter)
    void CreateTestBuffer()
    {
        int width = 10, height = 10, depth = 3;
        pointCount = width * height * depth;

        Vector4[] points = new Vector4[pointCount];
        int index = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 2; x < width; x++)
                {
                    // Position: 1 unit spacing
                    // Color (w): simple gradient 0 → 1
                    //float w = (float)index / (pointCount - 1);
                    float w = 1;
                    points[index] = new Vector4(x, y, z, w);
                    index++;
                }
            }
        }

        visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 4);
        visualBuffer.SetData(points);

        Debug.Log($"🧱 Created test cube buffer with {pointCount} points.");
        LogFirstPoints(visualBuffer, 20);
    }

    void LogFirstPoints(GraphicsBuffer buffer, int count = 10)
    {
        if (buffer == null)
        {
            Debug.LogError("Buffer is null, cannot log data!");
            return;
        }

        Vector4[] data = new Vector4[Mathf.Min(count, buffer.count)];
        buffer.GetData(data, 0, 0, data.Length);

        Debug.Log($"--- First {data.Length} points in buffer ---");
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"[{i}] x:{data[i].x:F2}, y:{data[i].y:F2}, z:{data[i].z:F2}, w:{data[i].w:F2}");
        }
    }
}
