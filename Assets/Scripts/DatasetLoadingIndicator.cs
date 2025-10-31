using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading indicator that monitors FastDatasetLoader progress.
/// Shows loading status and automatically hides when complete.
/// Works with both legacy Text and TextMeshPro.
/// </summary>
public class DatasetLoadingIndicator : MonoBehaviour
{
    [Header("References")]
    public FastDatasetLoader loader;
    public GameObject loadingPanel; // The panel to show/hide

    [Header("Text (use one)")]
    public Text legacyText; // Legacy UI Text
    public TMPro.TextMeshProUGUI tmpText; // TextMeshPro

    [Header("Settings")]
    public bool hideWhenLoaded = true;
    public float hideDelay = 0.5f;
    public bool showPercentage = true;

    private bool wasLoading = false;
    private bool hasHidden = false;

    void Start()
    {
        Debug.Log("🔍 DatasetLoadingIndicator started");

        // Show loading panel initially if loader hasn't loaded yet
        if (loadingPanel != null && loader != null && !loader.IsLoaded)
        {
            loadingPanel.SetActive(true);
            Debug.Log("✅ Loading panel shown on start");
        }
        else
        {
            if (loadingPanel == null)
                Debug.LogWarning("⚠️ LoadingPanel reference is missing!");
            if (loader == null)
                Debug.LogWarning("⚠️ FastDatasetLoader reference is missing!");
            if (loader != null && loader.IsLoaded)
                Debug.Log("ℹ️ Dataset already loaded, hiding panel");
        }
    }

    void Update()
    {
        if (loader == null) return;

        // Check if actively loading OR buffer not ready yet
        bool isCurrentlyLoading = loader.IsLoading || (!loader.IsLoaded && loader.buffer == null);

        if (isCurrentlyLoading)
        {
            // Still loading
            string message = "Loading dataset...\nPlease wait";

            SetText(message);

            if (loadingPanel != null && !loadingPanel.activeSelf)
            {
                loadingPanel.SetActive(true);
            }

            wasLoading = true;
            hasHidden = false;
        }
        else if (wasLoading && !hasHidden && loader.IsLoaded)
        {
            // Just finished loading
            Debug.Log("✅ Dataset finished loading! Hiding panel...");
            SetText("Dataset ready!");

            if (hideWhenLoaded && loadingPanel != null)
            {
                Debug.Log($"⏳ Hiding panel in {hideDelay}s...");
                Invoke(nameof(HideLoadingPanel), hideDelay);
            }
            else if (!hideWhenLoaded)
            {
                Debug.Log("⚠️ hideWhenLoaded is false - panel will stay visible");
            }

            wasLoading = false;
            hasHidden = true;
        }
    }

    void SetText(string text)
    {
        if (legacyText != null)
        {
            legacyText.text = text;
        }
        if (tmpText != null)
        {
            tmpText.text = text;
        }
    }

    void HideLoadingPanel()
    {
        if (loadingPanel != null)
        {
            Debug.Log("👻 Hiding loading panel now!");
            loadingPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("⚠️ Cannot hide panel - loadingPanel reference is null!");
        }
    }

    // Public method to manually show loading screen
    public void ShowLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            hasHidden = false;
        }
    }

    // Public method to manually hide loading screen
    public void HideLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            hasHidden = true;
        }
    }
}
