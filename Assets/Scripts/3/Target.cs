using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Target : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("LovePercentSetting")]
    public float currentlovePercent = 0f;
    public float maxlovePercent = 100f;
    public float holdTime = 3.0f;

    [Header("UI Slider")]
    public Slider loveSlider;

    private bool held = false;
    private float holdTimer;

    void Start()
    {
        if (loveSlider != null)
        {
            loveSlider.maxValue = 0f;
            loveSlider.maxValue = maxlovePercent;
            loveSlider.value = currentlovePercent;
        }
    }
    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        holdTimer = 0f;
    }
    public void OnPointerUp(PointerEventData e)
    {
        held = false;
        holdTimer = 0f;
    }
    void Update()
    {
        if (!held)
            return;

        holdTimer += Time.deltaTime;

        if(holdTimer >= holdTime)
        {
            currentlovePercent += 1f;
            currentlovePercent = Mathf.Clamp(currentlovePercent, 0f, maxlovePercent);

            if (loveSlider != null)
            {
                loveSlider.value = currentlovePercent;
            }

            holdTimer = 0f;
        }
    }
}
