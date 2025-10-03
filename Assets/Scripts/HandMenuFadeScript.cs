using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class HandMenuToggle : MonoBehaviour
{
    [Header("References")]
    public Transform handAnchor;  // LeftHandAnchor
    public Camera xrCamera;       // Main camera

    [Header("Settings")]
    [Range(-1f, 1f)] public float facingThreshold = 0.5f;

    private CanvasGroup canvasGroup;

    void Start()
    {
        if (handAnchor == null) Debug.LogWarning("Hand anchor not assigned!");
        if (xrCamera == null) xrCamera = Camera.main;

        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        if (handAnchor == null || xrCamera == null) return;

        // Vector from palm to camera
        Vector3 toCamera = (xrCamera.transform.position - handAnchor.position).normalized;

        // Use -up for palm facing direction
        Vector3 palmForward = -handAnchor.up;

        // Dot product: palm vs. camera
        float dot = Vector3.Dot(palmForward, toCamera);

        // Show or hide instantly
        if (dot > facingThreshold)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
