using UnityEngine;
using System.Collections;

public class CubeManager : MonoBehaviour
{
    public GameObject targetCube;       // eksisterende kube i scenen
    public GameObject canvasInit;       // Canvas med Initialize-knappen
    public GameObject canvasEditor;     // Canvas med editor-knapper
    private bool firstTime = true;

    void Start()
    {
        canvasInit.SetActive(true);
        canvasEditor.SetActive(false);
    }

    public void InitializeCube()
    {
        if (targetCube == null) return;
        targetCube.SetActive(true);

        if (firstTime)
        {
            firstTime = false;
            StartCoroutine(SwitchCanvasNextFrame());
        }
        else
        {
            canvasInit.SetActive(false);
            canvasEditor.SetActive(true);
        }
    }

    private IEnumerator SwitchCanvasNextFrame()
    {
        yield return null; // wait one frame
        canvasInit.SetActive(false);
        canvasEditor.SetActive(true);
    }

    public void RemoveCube()
    {
        if (targetCube == null) return;

        targetCube.SetActive(false);
        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);
    }
}
