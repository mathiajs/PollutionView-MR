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
    private bool isInitialized = false;

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

            yield return null; // wait one frame for VFX Graph

            // Set up compute shader to process test data (same as real data path)
            if (preprocessShader == null)
            {
                Debug.LogError("Missing compute shader reference! Assign PreProcessParticles.compute in Inspector.");
                enabled = false;
                yield break;
            }

            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 4);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            // Dispatch for timestep 0
            DispatchForStep(0);

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

            visualBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 4);

            kernel = preprocessShader.FindKernel("CSMain");
            preprocessShader.SetBuffer(kernel, "InBuffer", rawBuffer);
            preprocessShader.SetBuffer(kernel, "OutBuffer", visualBuffer);
            preprocessShader.SetInt("PointCount", pointCount);

            // Dispatch for timestep 0
            DispatchForStep(0);
            isInitialized = true;
        }
    }

    void Update()
    {
        // Nothing to do after initialization
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

        LogFirstPoints(visualBuffer, Mathf.Min(20, pointCount));

        vfx.SetUInt("PointCount", (uint)pointCount);
        vfx.SetInt("CurrentTimestep", step);
        vfx.SetGraphicsBuffer("DataBuffer", visualBuffer);

        if (!vfx.HasGraphicsBuffer("DataBuffer"))
            Debug.LogError("VFX Graph is missing DataBuffer!");
        else
            Debug.Log($"Successfully bound DataBuffer with {pointCount} points.");

        vfx.Play();
    }

    void OnDestroy()
    {
        rawBuffer?.Release();
        visualBuffer?.Release();
    }

    // 🧱 Generate test cube with single timestep
    void CreateTestBuffer()
    {
        int width = 50, height = 15, depth = 50;
        pointCount = width * height * depth;

        HDF5ParticleData[] points = new HDF5ParticleData[pointCount];
        int index = 0;

        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    points[index] = new HDF5ParticleData
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

    // Match the HDF5 ParticleData struct from HDF5_InspectAndRead
    struct HDF5ParticleData
    {
        public int t;
        public int z;
        public int y;
        public int x;
        public float q;
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
