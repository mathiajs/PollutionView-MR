using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoController : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public Button playButton;
    public Button pauseButton;
    public Slider videoSlider; 

    private bool isDragging = false;

    void Start()
    {
        playButton.onClick.AddListener(PlayVideo);
        pauseButton.onClick.AddListener(PauseVideo);

        if (videoSlider != null)
        {
            videoSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        // Prepare event ensures slider knows the video length
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.Prepare();
        }
    }

    void Update()
    {
        // Continuously update slider position if not dragging
        if (videoPlayer != null && videoPlayer.isPlaying && !isDragging && videoPlayer.length > 0)
        {
            videoSlider.value = (float)(videoPlayer.time / videoPlayer.length);
        }
    }

    void PlayVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }
    }

    void PauseVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (videoSlider != null)
        {
            videoSlider.minValue = 0;
            videoSlider.maxValue = 1; // normalized 0-1 value
            videoSlider.value = 0;
        }
    }

    void OnSliderValueChanged(float value)
    {
        if (isDragging && videoPlayer != null && videoPlayer.length > 0)
        {
            double targetTime = value * videoPlayer.length;
            videoPlayer.time = targetTime;
        }
    }

    // These are called when user starts/stops dragging the slider
    public void OnPointerDown()
    {
        isDragging = true;
    }

    public void OnPointerUp()
    {
        isDragging = false;
        if (videoPlayer != null && videoPlayer.length > 0)
        {
            videoPlayer.time = videoSlider.value * videoPlayer.length;
        }
    }
}
