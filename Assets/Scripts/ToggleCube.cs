using UnityEngine;

public class CubeToggle : MonoBehaviour
{
    public GameObject targetCube; // drag your cube here
    private bool isVisible = false;

    // This is the function you put in the button's OnClick()
    public void ToggleCube()
    {
        if (targetCube == null) return;

        isVisible = !isVisible;
        targetCube.SetActive(isVisible);
        Debug.Log("ToggleCube called! Visible: " + isVisible);
    }
}

