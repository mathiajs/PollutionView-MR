using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX; // <-- needed for Visual Effect component

public class VFXManager : MonoBehaviour
{
    public VisualEffect targetVFX;   // 🆕 assign your VFX GameObject here
    public GameObject canvasInit;    // Canvas with Initialize button
    public GameObject canvasEditor;  // Canvas with color toggles or other controls
    private bool firstTime = true;

    // // Color handling (if you want to pass color into the VFX)
    // private HashSet<Color> activeColors = new HashSet<Color>();

    // private Color redColor = Color.red;
    // private Color blueColor = Color.blue;
    // private Color yellowColor = Color.yellow;

    // public Toggle redToggle;
    // public Toggle blueToggle;
    // public Toggle yellowToggle;

    void Start()
    {
        canvasInit.SetActive(true);
        canvasEditor.SetActive(false);

        // redToggle.isOn = false;
        // blueToggle.isOn = false;
        // yellowToggle.isOn = false;

        // redToggle.onValueChanged.AddListener((value) => OnToggleChanged(redColor, value));
        // blueToggle.onValueChanged.AddListener((value) => OnToggleChanged(blueColor, value));
        // yellowToggle.onValueChanged.AddListener((value) => OnToggleChanged(yellowColor, value));

        // Make sure VFX starts inactive
        if (targetVFX != null)
            targetVFX.Stop();
    }

    // Called by Initialize button
    public void InitializeVFX()
    {
        if (targetVFX == null) return;

        targetVFX.Play();

        if (firstTime)
        {
            firstTime = false;
            StartCoroutine(SwitchCanvasNextFrame());
        }
        else
        {
            canvasInit.SetActive(false);
            canvasEditor.SetActive(true);
            // redToggle.isOn = true;
        }

        // blueToggle.isOn = false;
        // yellowToggle.isOn = false;
    }

    private IEnumerator SwitchCanvasNextFrame()
    {
        yield return null;
        canvasInit.SetActive(false);
        canvasEditor.SetActive(true);
        // redToggle.isOn = true;
    }

    // private void OnToggleChanged(Color color, bool isOn)
    // {
    //     if (isOn)
    //         activeColors.Add(color);
    //     else
    //         activeColors.Remove(color);

    //     UpdateColor();
    // }

    // private void UpdateColor()
    // {
    //     if (activeColors.Count == 0)
    //     {
    //         RemoveVFX();
    //     }
    //     else
    //     {
    //         // 🧠 Optional: If your VFX has a color property exposed (e.g., "MainColor")
    //         // you can dynamically change it like this:
    //         targetVFX.SetVector4("MainColor", MixColorsHSV());
    //         targetVFX.Play();
    //     }
    // }

    // private Color MixColorsHSV()
    // {
    //     float h = 0f, s = 0f, v = 0f;
    //     foreach (var c in activeColors)
    //     {
    //         Color.RGBToHSV(c, out float ch, out float cs, out float cv);
    //         h += ch;
    //         s += cs;
    //         v += cv;
    //     }

    //     int count = activeColors.Count;
    //     h /= count;
    //     s /= count;
    //     v /= count;

    //     return Color.HSVToRGB(h, s, v);
    // }

    // private void RemoveVFX()
    // {
    //     if (targetVFX != null)
    //         targetVFX.Stop();

    //     redToggle.isOn = false;
    //     blueToggle.isOn = false;
    //     yellowToggle.isOn = false;
    //     activeColors.Clear();
    // }

    public void BackToSpawnVFX()
    {
        if (targetVFX != null)
            targetVFX.Stop();

        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);

        // redToggle.isOn = false;
        // blueToggle.isOn = false;
        // yellowToggle.isOn = false;
        // activeColors.Clear();
    }
}

