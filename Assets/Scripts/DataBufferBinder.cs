using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class DataBufferBinder : MonoBehaviour
{
    public HDF5_InspectAndRead reader; // referanse til din reader
    private VisualEffect vfx;

    void Start()
    {
        vfx = GetComponent<VisualEffect>();

        if (reader == null || reader.buffer == null)
        {
            Debug.LogError("Reader or buffer not set!");
            return;
        }

        vfx.SetUInt("PointCount", (uint)reader.buffer.count);
        vfx.SetGraphicsBuffer("DataBuffer", reader.buffer);
    }

    void Update()
    {
        // Kan senere oppdateres hvis du legger til play/pause og timestep interpolasjon
    }

    void OnDestroy()
    {
        reader.buffer?.Release();
    }
}
