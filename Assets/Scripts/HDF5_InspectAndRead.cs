using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PureHDF;

public class HDF5_InspectAndRead : MonoBehaviour
{
    [Tooltip("Name of the .nc file placed inside Assets/StreamingAssets/")]
    public string fileName = "dp_3d_clean.001.nc";

    void Start()
    {
        StartCoroutine(LoadAndInspect());
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
                Debug.LogError("Failed to load file: " + www.error);
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
            Debug.LogError($"File not found: {path}");
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
            Debug.Log($"Opening file: {path}");

            using var file = H5File.OpenRead(path);

            Debug.Log("Listing top-level objects in the file:");

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

                Debug.Log($" - {type}: {link.Name}");
            }

            var qLink = file.Children().FirstOrDefault(l => l.Name == "q" && l is IH5Dataset);
            if (qLink != null)
            {
                var dataset = file.Dataset("/q");
                float[] flat = dataset.Read<float[]>();
                int stepSize = 4; // Increase/decrease for more/less aggressive downsampling
                int time = 11, z = 41, y = 412, x = 412;

                int stepZ = z / stepSize;
                int stepY = y / stepSize;
                int stepX = x / stepSize;
                float[,,,] reshaped = new float[time, stepZ, stepY, stepX];

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
                                reshaped[t, zz, yy, xx] = flat[idxFlat];
                            }
                        }
                    }
                }

                Debug.Log($"Successfully read downsampled 'q' dataset with shape [{reshaped.GetLength(0)}, {reshaped.GetLength(1)}, {reshaped.GetLength(2)}, {reshaped.GetLength(3)}]");

                for (int zz = 0; zz < reshaped.GetLength(1); zz++)
                {
                    for (int yy = 0; yy < reshaped.GetLength(2); yy++)
                    {
                        for (int xx = 0; xx < reshaped.GetLength(3); xx++)
                        {
                            if (reshaped[0, zz, yy, xx] > 0)
                            {
                                Debug.Log($"q[0,{zz},{yy},{xx}] = {reshaped[0, zz, yy, xx]}");
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("No dataset named '/q' found in file.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading HDF5 file: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
