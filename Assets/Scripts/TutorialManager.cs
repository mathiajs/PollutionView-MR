using UnityEngine;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public GameObject tutorialCanvas;

    void Start()
    {
        tutorialCanvas.SetActive(false);
    }

    public void InitializeTutorialPanel()
    {
        if (tutorialCanvas == null) return;
        tutorialCanvas.SetActive(true);
    }

    public void RemoveTutorialPanel()
    {
        if (tutorialCanvas == null) return;
        tutorialCanvas.SetActive(false);
    }
}
