using UnityEngine.VFX;

/// <summary>
/// Particle data structure used by VFX Graph and compute shaders.
/// Matches the binary format: t,z,y,x (ints) + q (float) = 20 bytes per particle
/// </summary>
[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct ParticleData
{
    public int t, z, y, x;
    public float q;
}
