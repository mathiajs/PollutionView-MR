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
    public Toggle pollutant1Toggle;  // Blue-Red scale
    public Toggle pollutant2Toggle;  // Different scale
    public Toggle pollutant3Toggle;  // Different scale

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

        // Set all toggles to false initially
        if (pollutant1Toggle != null) pollutant1Toggle.isOn = false;
        if (pollutant2Toggle != null) pollutant2Toggle.isOn = false;
        if (pollutant3Toggle != null) pollutant3Toggle.isOn = false;

        // Add listeners for exclusive toggle behavior
        if (pollutant1Toggle != null) pollutant1Toggle.onValueChanged.AddListener((isOn) => OnPollutantToggled(1, isOn));
        if (pollutant2Toggle != null) pollutant2Toggle.onValueChanged.AddListener((isOn) => OnPollutantToggled(2, isOn));
        if (pollutant3Toggle != null) pollutant3Toggle.onValueChanged.AddListener((isOn) => OnPollutantToggled(3, isOn));
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
            pollutant1Toggle.isOn = true; // Default to pollutant 1
        }
    }

    private IEnumerator SwitchCanvasNextFrame()
    {
        yield return null; // wait one frame
        canvasInit.SetActive(false);
        canvasEditor.SetActive(true);
        pollutant1Toggle.isOn = true; // Default to pollutant 1
    }

    /// <summary>
    /// Handle pollutant toggle - ensures only one is active at a time (radio button behavior)
    /// </summary>
    private void OnPollutantToggled(int pollutantIndex, bool isOn)
    {
        if (isOn)
        {
            // Already active? Skip to avoid recursion
            if (activePollutant == pollutantIndex)
                return;

            // Temporarily remove listeners to avoid triggering during manual toggle changes
            if (pollutant1Toggle != null) pollutant1Toggle.onValueChanged.RemoveAllListeners();
            if (pollutant2Toggle != null) pollutant2Toggle.onValueChanged.RemoveAllListeners();
            if (pollutant3Toggle != null) pollutant3Toggle.onValueChanged.RemoveAllListeners();

            // Turn off other toggles (radio button behavior)
            if (pollutantIndex != 1 && pollutant1Toggle != null) pollutant1Toggle.isOn = false;
            if (pollutantIndex != 2 && pollutant2Toggle != null) pollutant2Toggle.isOn = false;
            if (pollutantIndex != 3 && pollutant3Toggle != null) pollutant3Toggle.isOn = false;

            // Re-add listeners
            if (pollutant1Toggle != null) pollutant1Toggle.onValueChanged.AddListener((value) => OnPollutantToggled(1, value));
            if (pollutant2Toggle != null) pollutant2Toggle.onValueChanged.AddListener((value) => OnPollutantToggled(2, value));
            if (pollutant3Toggle != null) pollutant3Toggle.onValueChanged.AddListener((value) => OnPollutantToggled(3, value));

            // Update active pollutant and VFX
            activePollutant = pollutantIndex;
            UpdateVFXColorScheme(pollutantIndex);

            Debug.Log($"🎨 Switched to Pollutant {pollutantIndex} color scheme");
        }
        else
        {
            // Don't allow turning off the active pollutant (must always have one selected)
            if (activePollutant == pollutantIndex)
            {
                // Force it back on
                if (pollutantIndex == 1 && pollutant1Toggle != null) pollutant1Toggle.isOn = true;
                else if (pollutantIndex == 2 && pollutant2Toggle != null) pollutant2Toggle.isOn = true;
                else if (pollutantIndex == 3 && pollutant3Toggle != null) pollutant3Toggle.isOn = true;
            }
        }
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

        // Update UI toggles if they exist
        if (pollutant1Toggle != null) pollutant1Toggle.isOn = (pollutantIndex == 1);
        if (pollutant2Toggle != null) pollutant2Toggle.isOn = (pollutantIndex == 2);
        if (pollutant3Toggle != null) pollutant3Toggle.isOn = (pollutantIndex == 3);

        // Update state
        activePollutant = pollutantIndex;
        UpdateVFXColorScheme(pollutantIndex);

        Debug.Log($"🎨 Manually switched to Pollutant {pollutantIndex}");
    }

    public void BackToInit()
    {
        canvasEditor.SetActive(false);
        canvasInit.SetActive(true);

        // Reset to default pollutant
        pollutant1Toggle.isOn = false;
        pollutant2Toggle.isOn = false;
        pollutant3Toggle.isOn = false;
        activePollutant = 1;
    }
}
