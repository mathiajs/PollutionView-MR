using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasHelper : MonoBehaviour
{
    public GameObject canvasInit;    // Canvas med Initialize-knappen
    public GameObject canvasEditor;  // Canvas med editor-knapper
    private bool firstTime = true;

    // Toggles for pollutants (only one active at a time)
    public Toggle pollutant1Toggle;
    public Toggle pollutant2Toggle;
    public Toggle pollutant3Toggle;

    [Header("Optional: Use ToggleGroup for proper radio button behavior")]
    [Tooltip("Assign the same ToggleGroup to all pollutant toggles in the Inspector")]
    public ToggleGroup pollutantToggleGroup;

    // Reference to particle controller to update VFX
    public ParticleAnimationController particleController;

    // Current active pollutant (0 = none, 1 = pollutant1, 2 = pollutant2, 3 = pollutant3)
    private int activePollutant = 1;

    [Header("Testing (Play Mode Only)")]
    [Tooltip("Toggle these in Inspector during Play Mode to test color schemes")]
    public bool testPollutant1 = false;
    public bool testPollutant2 = false;
    public bool testPollutant3 = false;

    private bool lastTestPollutant1 = false;
    private bool lastTestPollutant2 = false;
    private bool lastTestPollutant3 = false;

    void Start()
    {
        canvasInit.SetActive(true);
        canvasEditor.SetActive(false);

        // Set up ToggleGroup for radio button behavior
        if (pollutantToggleGroup != null)
        {
            pollutant1Toggle.group = pollutantToggleGroup;
            pollutant2Toggle.group = pollutantToggleGroup;
            pollutant3Toggle.group = pollutantToggleGroup;
            Debug.Log("✅ ToggleGroup assigned to pollutant toggles");
        }
        else
        {
            Debug.LogWarning("⚠️ PollutantToggleGroup is not assigned! Assign it in the Inspector for proper radio button behavior.");
        }

        // Set all toggles to OFF initially
        pollutant1Toggle.isOn = false;
        pollutant2Toggle.isOn = false;
        pollutant3Toggle.isOn = false;
    }

    void Update()
    {
        // Test mode: Check for changes in test bools during play mode
        if (Application.isPlaying)
        {
            if (testPollutant1 && !lastTestPollutant1)
            {
                SwitchToPollutant(1);
                testPollutant2 = false;
                testPollutant3 = false;
            }
            else if (testPollutant2 && !lastTestPollutant2)
            {
                SwitchToPollutant(2);
                testPollutant1 = false;
                testPollutant3 = false;
            }
            else if (testPollutant3 && !lastTestPollutant3)
            {
                SwitchToPollutant(3);
                testPollutant1 = false;
                testPollutant2 = false;
            }

            lastTestPollutant1 = testPollutant1;
            lastTestPollutant2 = testPollutant2;
            lastTestPollutant3 = testPollutant3;
        }
    }

    // Initialize button
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
            // Always start with pollutant 1 selected
            SelectPollutant(1);
        }
    }

    private IEnumerator SwitchCanvasNextFrame()
    {
        yield return null; // wait one frame
        canvasInit.SetActive(false);
        canvasEditor.SetActive(true);
        // Always start with pollutant 1 selected
        SelectPollutant(1);
    }

    // Public methods to be called from Toggle onClick events in Inspector
    public void OnPollutant1Clicked()
    {
        SelectPollutant(1);
    }

    public void OnPollutant2Clicked()
    {
        SelectPollutant(2);
    }

    public void OnPollutant3Clicked()
    {
        SelectPollutant(3);
    }

    private void SelectPollutant(int pollutantIndex)
    {
        Debug.Log($"SelectPollutant called: {pollutantIndex}");

        // Skip if already active to prevent unnecessary updates
        if (activePollutant == pollutantIndex)
        {
            Debug.Log($"Pollutant {pollutantIndex} already active, skipping");
            return;
        }

        // ToggleGroup handles the exclusive toggle behavior automatically!
        // We only need to update the color scheme
        activePollutant = pollutantIndex;
        UpdateVFXColorScheme(pollutantIndex);

        Debug.Log($"✅ Selected Pollutant {pollutantIndex}");
    }

    /// <summary>
    /// Update VFX Graph with the selected color scheme
    /// </summary>
    private void UpdateVFXColorScheme(int pollutantIndex)
    {
        if (particleController != null)
        {
            particleController.SetColorScheme(pollutantIndex);
        }
        else
        {
            Debug.LogWarning("⚠️ ParticleAnimationController reference is missing!");
        }
    }

    /// <summary>
    /// Get the currently active pollutant index
    /// </summary>
    public int GetActivePollutant()
    {
        return activePollutant;
    }

    /// <summary>
    /// Public method to switch to a specific pollutant (for testing or programmatic control)
    /// </summary>
    public void SwitchToPollutant(int pollutantIndex)
    {
        if (pollutantIndex < 1 || pollutantIndex > 3)
        {
            Debug.LogWarning($"⚠️ Invalid pollutant index: {pollutantIndex}. Must be 1, 2, or 3.");
            return;
        }

        if (pollutant1Toggle != null) pollutant1Toggle.isOn = (pollutantIndex == 1);
        if (pollutant2Toggle != null) pollutant2Toggle.isOn = (pollutantIndex == 2);
        if (pollutant3Toggle != null) pollutant3Toggle.isOn = (pollutantIndex == 3);

        // Skip color update if already active (but toggles are still updated above)
        if (activePollutant == pollutantIndex)
        {
            Debug.Log($"Pollutant {pollutantIndex} already active, toggles synced");
            return;
        }

        // Update state and color scheme
        activePollutant = pollutantIndex;
        UpdateVFXColorScheme(pollutantIndex);

        Debug.Log($"🎨 Switched to Pollutant {pollutantIndex}");
    }

    public void BackToInit()
    {
        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);

        // Reset all toggles to OFF when going back to init (dataset will be uninitialized)
        pollutant1Toggle.isOn = false;
        pollutant2Toggle.isOn = false;
        pollutant3Toggle.isOn = false;

        // Reset to default (will be applied when re-initializing)
        activePollutant = 1;
    }
}
