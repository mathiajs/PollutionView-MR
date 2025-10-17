using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PureHDF;

public class HDF5_InspectAndRead : MonoBehaviour
{
    [Tooltip("Name of the .nc file placed inside Assets/StreamingAssets/")]
    public string fileName = "q_subset.nc";

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
                var data = dataset.Read<float[]>();

                int time = 1, z = 11, y = 11, x = 11;
                float[] flat = dataset.Read<float[]>();
                float[,,,] reshaped = new float[time, z, y, x];

                int idx = 0;
                for (int t = 0; t < time; t++)
                    for (int zz = 0; zz < z; zz++)
                        for (int yy = 0; yy < y; yy++)
                            for (int xx = 0; xx < x; xx++)
                                reshaped[t, zz, yy, xx] = flat[idx++];


                Debug.Log($"Successfully read 'q' dataset with shape [{reshaped.GetLength(0)}, {reshaped.GetLength(1)}, {reshaped.GetLength(2)}, {reshaped.GetLength(3)}]");

                // Iterating through reshaped array, using y = i and x = j
                for (int i = 0; i < Math.Min(3, reshaped.GetLength(2)); i++)
                {
                    for (int j = 0; j < Math.Min(3, reshaped.GetLength(3)); j++)
                    {
                        Debug.Log($"q[0,0,{i},{j}] = {reshaped[0, 0, i, j]}");
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
