using UnityEngine;

public class ColorToggler : MonoBehaviour
{
    public Renderer targetRenderer;
    public Color newColor = Color.blue;

    private Color originalColor;
    private bool isToggled = false;

    void Start()
    {
        if (targetRenderer != null)
            originalColor = targetRenderer.material.color;
    }

    // ✅ This is what you put in OnClick()
    public void ToggleColor()
    {
        if (targetRenderer == null) return;

        targetRenderer.material.color = isToggled ? originalColor : newColor;
        isToggled = !isToggled;
        Debug.Log("ToggleColor called!");
    }
}
