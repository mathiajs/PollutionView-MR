using UnityEngine;

/// <summary>
/// Ensures a Canvas is properly configured for VR viewing.
/// Attach this to any Canvas you want to display in VR.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRCanvasSetup : MonoBehaviour
{
    [Header("Canvas Position")]
    [Tooltip("Distance from camera")]
    public float distanceFromCamera = 2f;

    [Tooltip("Height offset from camera")]
    public float heightOffset = 0f;

    [Header("Auto-Setup")]
    [Tooltip("Automatically configure canvas on Start")]
    public bool autoSetupOnStart = true;

    [Tooltip("Position canvas in front of camera on Start")]
    public bool positionInFrontOfCamera = false;

    private Canvas canvas;

    void Start()
    {
        canvas = GetComponent<Canvas>();

        if (autoSetupOnStart)
        {
            SetupForVR();
        }

        if (positionInFrontOfCamera)
        {
            PositionInFrontOfCamera();
        }
    }

    public void SetupForVR()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        // Set to World Space for VR
        canvas.renderMode = RenderMode.WorldSpace;

        // Set reasonable scale for VR
        if (transform.localScale.x > 0.01f) // If scale is too large
        {
            transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        }

        Debug.Log($"Canvas '{gameObject.name}' configured for VR (World Space, scale: {transform.localScale.x})");
    }

    public void PositionInFrontOfCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("No main camera found");
            return;
        }

        // Position in front of camera
        Vector3 cameraPosition = mainCam.transform.position;
        Vector3 cameraForward = mainCam.transform.forward;
        Vector3 cameraUp = mainCam.transform.up;

        transform.position = cameraPosition + cameraForward * distanceFromCamera + cameraUp * heightOffset;
        transform.rotation = Quaternion.LookRotation(transform.position - cameraPosition);

        Debug.Log($"Canvas positioned at {transform.position}");
    }

    [ContextMenu("Position In Front of Camera Now")]
    public void PositionNow()
    {
        PositionInFrontOfCamera();
    }
}
