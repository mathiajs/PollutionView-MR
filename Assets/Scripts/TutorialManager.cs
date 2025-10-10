using UnityEngine;
using System.Collections;


public class TutorialManager : MonoBehaviour
{
    public GameObject tutorialCanvas;
    public GameObject canvasInit;
    public GameObject canvasTutorialControl;

    void Start()
    {
        tutorialCanvas.SetActive(false);
    }

    public void InitializeTutorialPanel()
    {
        if (tutorialCanvas == null) return;
        tutorialCanvas.SetActive(true);
        canvasInit.SetActive(false);
        canvasTutorialControl.SetActive(true);
    }

    public void RemoveTutorialPanel()
    {
        if (tutorialCanvas == null) return;
        tutorialCanvas.SetActive(false);
        canvasTutorialControl.SetActive(false);
        canvasInit.SetActive(true);
    }
}
