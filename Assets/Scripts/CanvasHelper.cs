using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasHelper : MonoBehaviour
{
    public GameObject canvasInit;    // Canvas med Initialize-knappen
    public GameObject canvasEditor;  // Canvas med editor-knapper
    private bool firstTime = true;

    // Fargehåndtering
    private HashSet<Color> activeColors = new HashSet<Color>();

    private Color redColor = Color.red;
    private Color blueColor = Color.blue;
    private Color yellowColor = Color.yellow;

    // Toggles for farger
    public Toggle redToggle;
    public Toggle blueToggle;
    public Toggle yellowToggle;

    void Start()
    {
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
    public void Initialize()
    {
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
            ResetColors();
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

    // Få den blandede fargen
    public Color GetMixedColor()
    {
        if (activeColors.Count == 0)
            return Color.white;

        return MixColorsHSV();
    }

    // Sjekk om det er aktive farger
    public bool HasActiveColors()
    {
        return activeColors.Count > 0;
    }

    // Tilbakestill farger
    private void ResetColors()
    {
        // Tilbakestill toggles
        redToggle.isOn = false;
        blueToggle.isOn = false;
        yellowToggle.isOn = false;
        activeColors.Clear();
    }

    public void BackToInit()
    {
        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);

        redToggle.isOn = false;
        blueToggle.isOn = false;
        yellowToggle.isOn = false;
        activeColors.Clear();
    }
}
