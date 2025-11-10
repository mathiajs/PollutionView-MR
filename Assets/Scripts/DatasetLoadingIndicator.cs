using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading indicator that monitors PrebakedDatasetLoader progress.
/// Shows loading status and automatically hides when complete.
/// Works with both legacy Text and TextMeshPro.
/// </summary>
public class DatasetLoadingIndicator : MonoBehaviour
{
    [Header("References")]
    public PrebakedDatasetLoader prebakedLoader;
    public GameObject loadingPanel; // The panel to show/hide

    [Header("Text (use one)")]
    public Text legacyText; // Legacy UI Text
    public TMPro.TextMeshProUGUI tmpText; // TextMeshPro

    [Header("Settings")]
    public bool hideWhenLoaded = true;
    public float hideDelay = 0.5f;
    public bool showPercentage = true;

    [Header("Additional Message Settings")]
    [Tooltip("Additional message to show after 'Dataset ready!' and before fade out")]
    public string additionalMessage = "Initializing visualization...";
    [Tooltip("How long to show the 'Dataset ready!' message before showing additional message")]
    public float readyMessageDuration = 1.0f;

    private bool wasLoading = false;
    private bool hasHidden = false;
    private bool showingAdditionalMessage = false;

    void Start()
    {
        Debug.Log("🔍 DatasetLoadingIndicator started");

        bool isLoaded = GetIsLoaded();

        // Show loading panel initially if data hasn't loaded yet
        if (loadingPanel != null && !isLoaded)
        {
            loadingPanel.SetActive(true);
            Debug.Log("✅ Loading panel shown on start");
        }
        else
        {
            if (loadingPanel == null)
                Debug.LogWarning("⚠️ LoadingPanel reference is missing!");
            if (prebakedLoader == null)
                Debug.LogWarning("⚠️ No loader reference assigned!");
            if (isLoaded)
                Debug.Log("ℹ️ Dataset already loaded, hiding panel");
        }
    }

    bool GetIsLoaded()
    {
        if (prebakedLoader != null)
            return prebakedLoader.IsLoaded;
        return false;
    }

    bool GetIsLoading()
    {
        // Prebaked loader is instant, so check if it's not loaded yet
        if (prebakedLoader != null)
            return !prebakedLoader.IsLoaded;
        return false;
    }

    void Update()
    {
        if (prebakedLoader == null) return;

        bool isCurrentlyLoading = GetIsLoading();
        bool isLoaded = GetIsLoaded();

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
        else if (wasLoading && !hasHidden && isLoaded)
        {
            // Just finished loading - show "Dataset ready!" first
            Debug.Log("✅ Dataset finished loading! Showing ready message...");
            SetText("Dataset ready!");

            if (hideWhenLoaded && loadingPanel != null)
            {
                // Show additional message after readyMessageDuration
                Debug.Log($"⏳ Will show additional message in {readyMessageDuration}s...");
                Invoke(nameof(ShowAdditionalMessage), readyMessageDuration);
            }
            else if (!hideWhenLoaded)
            {
                Debug.Log("⚠️ hideWhenLoaded is false - panel will stay visible");
            }

            wasLoading = false;
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

    void ShowAdditionalMessage()
    {
        if (!showingAdditionalMessage)
        {
            Debug.Log($"📝 Showing additional message: '{additionalMessage}'");
            SetText(additionalMessage);
            showingAdditionalMessage = true;

            // Now schedule the fade out after hideDelay
            Debug.Log($"⏳ Hiding panel in {hideDelay}s...");
            Invoke(nameof(HideLoadingPanel), hideDelay);
        }
    }

    void HideLoadingPanel()
    {
        if (loadingPanel != null)
        {
            Debug.Log("👻 Hiding loading panel now!");
            loadingPanel.SetActive(false);
            hasHidden = true;
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
            showingAdditionalMessage = false;
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
