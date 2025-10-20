using System;
using System.Linq;
using UnityEngine;

public class PureHDF_TestLoad : MonoBehaviour
{
    void Start()
    {
        try
        {
            Debug.Log("PureHDF load test: listing loaded assemblies for verification:");

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .OrderBy(n => n);

            foreach (var a in assemblies)
            {
                if (a.IndexOf("PureHDF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("Found assembly: " + a);
                }
            }

            // Try to get a type from PureHDF to confirm runtime link
            var t = Type.GetType("PureHDF.H5File, PureHDF", throwOnError: false);
            if (t != null)
            {
                Debug.Log("PureHDF type found: " + t.FullName);
            }
            else
            {
                Debug.LogWarning("PureHDF type NOT found via reflection. Check DLL placement and API Compatibility level.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in PureHDF load test: " + ex);
        }
    }
}
