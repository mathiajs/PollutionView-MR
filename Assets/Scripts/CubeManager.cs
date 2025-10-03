using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CubeManager : MonoBehaviour
{
    public GameObject targetCube;
    public GameObject canvasInit;    // Canvas med Initialize-knappen
    public GameObject canvasEditor;  // Canvas med editor-knapper
    private bool firstTime = true;

    // Fargehåndtering
    private HashSet<Color> activeColors = new HashSet<Color>();
    private Renderer cubeRenderer;

    private Color redColor = Color.red;
    private Color blueColor = Color.blue;
    private Color yellowColor = Color.yellow;

    // Toggles for farger
    public Toggle redToggle;
    public Toggle blueToggle;
    public Toggle yellowToggle;

    void Start()
    {
        cubeRenderer = targetCube.GetComponent<Renderer>();
        canvasInit.SetActive(true);
        canvasEditor.SetActive(false);

        // Sett alle toggles til false i starten
        redToggle.isOn = false;
        blueToggle.isOn = false;
        yellowToggle.isOn = false;

        // Legg til lyttere på toggles
        redToggle.onValueChanged.AddListener((value) => OnToggleChanged(redColor, value));
        blueToggle.onValueChanged.AddListener((value) => OnToggleChanged(blueColor, value));
        yellowToggle.onValueChanged.AddListener((value) => OnToggleChanged(yellowColor, value));
    }

    // Initialize-knapp
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
            redToggle.isOn = true;
        }

        blueToggle.isOn = false;
        yellowToggle.isOn = false;
    }

    private IEnumerator SwitchCanvasNextFrame()
    {
        yield return null; // vent en frame
        canvasInit.SetActive(false);
        canvasEditor.SetActive(true);
        redToggle.isOn = true;

    }

    private void OnToggleChanged(Color color, bool isOn)
    {
        if (isOn)
            activeColors.Add(color);
        else
            activeColors.Remove(color);

        UpdateColor();
    }

    private void UpdateColor()
    {
        if (activeColors.Count == 0)
        {
            RemoveCube();
        }
        else
        {
            cubeRenderer.material.color = MixColorsHSV();
            targetCube.SetActive(true);
        }
    }

    private Color MixColorsHSV()
    {
        float h = 0f, s = 0f, v = 0f;
        foreach (var c in activeColors)
        {
            Color.RGBToHSV(c, out float ch, out float cs, out float cv);
            h += ch;
            s += cs;
            v += cv;
        }

        int count = activeColors.Count;
        h /= count;
        s /= count;
        v /= count;

        return Color.HSVToRGB(h, s, v);
    }


    // Fjern kuben
    private void RemoveCube()
    {
        targetCube.SetActive(false);

        // Tilbakestill toggles
        redToggle.isOn = false;
        blueToggle.isOn = false;
        yellowToggle.isOn = false;
        activeColors.Clear();
    }

    public void backToSpawnCube()
    {
        targetCube.SetActive(false);
        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);

        redToggle.isOn = false;
        blueToggle.isOn = false;
        yellowToggle.isOn = false;
        activeColors.Clear();
    }
}
