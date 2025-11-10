using UnityEngine;

/// <summary>
/// ScriptableObject that holds pre-processed particle data.
/// Created at build time, loads instantly at runtime.
/// </summary>
[CreateAssetMenu(fileName = "DatasetAsset", menuName = "Dataset/Prebaked Dataset Asset")]
public class PrebakedDatasetAsset : ScriptableObject
{
    [System.Serializable]
    public struct ParticleData
    {
        public int t, z, y, x;
        public float q;
    }

    [Header("Metadata")]
    public int particleCount;
    public int downsamplingFactor;
    public int timeDim, zDim, yDim, xDim;

    [Header("Value Ranges")]
    public int minX, maxX;
    public int minY, maxY;
    public int minZ, maxZ;
    public float minQ, maxQ;

    [Header("Particle Data")]
    [HideInInspector]
    public byte[] serializedData; // Stores particle data as byte array

    /// <summary>
    /// Deserialize the byte array back to ParticleData array.
    /// This is FAST because it's just reading binary data.
    /// </summary>
    public ParticleData[] GetParticles()
    {
        if (serializedData == null || serializedData.Length == 0)
        {
            Debug.LogError("No serialized data in asset!");
            return new ParticleData[0];
        }

        int structSize = sizeof(int) * 4 + sizeof(float);
        int count = serializedData.Length / structSize;
        ParticleData[] particles = new ParticleData[count];

        using (System.IO.MemoryStream stream = new System.IO.MemoryStream(serializedData))
        using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream))
        {
            for (int i = 0; i < count; i++)
            {
                particles[i].t = reader.ReadInt32();
                particles[i].z = reader.ReadInt32();
                particles[i].y = reader.ReadInt32();
                particles[i].x = reader.ReadInt32();
                particles[i].q = reader.ReadSingle();
            }
        }

        return particles;
    }

    /// <summary>
    /// Create a GraphicsBuffer from this asset's data.
    /// Called at runtime - very fast!
    /// </summary>
    public GraphicsBuffer CreateGraphicsBuffer()
    {
        ParticleData[] particles = GetParticles();

        if (particles.Length == 0)
        {
            Debug.LogError("No particle data to create buffer from!");
            return null;
        }

        int structSize = sizeof(int) * 4 + sizeof(float);
        GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particles.Length, structSize);
        buffer.SetData(particles);

        Debug.Log($"✅ Created GraphicsBuffer from prebaked asset: {particles.Length:N0} particles");

        return buffer;
    }

    /// <summary>
    /// Get estimated memory size in MB
    /// </summary>
    public float GetMemorySizeMB()
    {
        return (serializedData != null ? serializedData.Length : 0) / (1024f * 1024f);
    }
}
