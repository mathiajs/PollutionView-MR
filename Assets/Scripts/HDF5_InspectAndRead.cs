using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PureHDF;
using UnityEngine.VFX;
using System.Collections.Generic;
public class HDF5_InspectAndRead : MonoBehaviour
{
    [Tooltip("Name of the .nc file placed inside Assets/StreamingAssets/")]
    public string fileName = "dp_3d_clean.001.nc";
    public GraphicsBuffer buffer;
    public bool useTestData = true;

    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct ParticleData
    {
        public int t, z, y, x;
        public float q;
    }
    void Start()
    {
        UnityEngine.Debug.Log("Jada");

        if(!useTestData)
        {
            StartCoroutine(LoadAndInspect());
        }
    }
    System.Collections.IEnumerator LoadAndInspect()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var www = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Failed to load file: " + www.error);
                yield break;
            }
            byte[] data = www.downloadHandler.data;
            string tempPath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(tempPath, data);
            InspectFile(tempPath);
        }
#else
        if (!File.Exists(path))
        {
            UnityEngine.Debug.LogError($"File not found: {path}");
            yield break;
        }
        InspectFile(path);
        yield return null;
#endif
    }
    void InspectFile(string path)
    {
        try
        {
            UnityEngine.Debug.Log($"Opening file: {path}");
            using var file = H5File.OpenRead(path);
            UnityEngine.Debug.Log("Listing top-level objects in the file:");
            foreach (var link in file.Children())
            {
                string type = link switch
                {
                    IH5Group => "Group",
                    IH5Dataset => "Dataset",
                    IH5CommitedDatatype => "Datatype",
                    IH5UnresolvedLink => "Broken Link",
                    _ => "Unknown"
                };
                UnityEngine.Debug.Log($" - {type}: {link.Name}");
            }
            var qLink = file.Children().FirstOrDefault(l => l.Name == "q" && l is IH5Dataset);
            if (qLink != null)
            {
                var dataset = file.Dataset("/q");
                float[] flat = dataset.Read<float[]>();
                int stepSize = 8; // Increase/decrease for more/less aggressive downsampling
                int time = 11, z = 41, y = 412, x = 412;
                int pointSize = sizeof(int) * 4 + sizeof(float);
                int stepZ = z / stepSize;
                int stepY = y / stepSize;
                int stepX = x / stepSize;
                List<object[]> thresholdValues = new List<object[]>(); // Where the points we want to visualize are stored
                for (int t = 0; t < time; t++)
                {
                    for (int zz = 0; zz < stepZ; zz++)
                    {
                        int srcZ = zz * stepSize;
                        for (int yy = 0; yy < stepY; yy++)
                        {
                            int srcY = yy * stepSize;
                            for (int xx = 0; xx < stepX; xx++)
                            {
                                int srcX = xx * stepSize;
                                long idxFlat = ((long)t * z + srcZ) * y * x + srcY * x + srcX;
                                if (idxFlat < 0 || idxFlat >= flat.Length) continue;
                                if (flat[idxFlat] <= 0 || flat[idxFlat] > 0.00975) continue;
                                thresholdValues.Add(new object[] { t, zz, yy, xx, flat[idxFlat] });
                            }
                        }
                    }
                }
                ParticleData[] particles = new ParticleData[thresholdValues.Count];
                for (int i = 0; i < thresholdValues.Count; i++)
                {
                    particles[i].t = (int)thresholdValues[i][0];
                    particles[i].z = (int)thresholdValues[i][1];
                    particles[i].y = (int)thresholdValues[i][2];
                    particles[i].x = (int)thresholdValues[i][3];
                    particles[i].q = (float)thresholdValues[i][4];
                }
                UnityEngine.Debug.Log($"Successfully read 'q' dataset.\nDownsampling factor: {stepSize}\nNumber of data points to visualize: {thresholdValues.Count}");
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particles.Length, pointSize);
                buffer.SetData(particles);
                Shader.SetGlobalBuffer("DataBuffer", buffer);
            }
            else
            {
                UnityEngine.Debug.LogWarning("No dataset named '/q' found in file.");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error reading HDF5 file: {ex.Message}\n{ex.StackTrace}");
        }
    }
}