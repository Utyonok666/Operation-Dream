using UnityEngine;

public class DeathMenuController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;

    private float targetAlpha;
    private float currentAlpha;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        currentAlpha = 0f;
        targetAlpha = 0f;
    }

    private void Update()
    {
        if (Mathf.Abs(currentAlpha - targetAlpha) < 0.001f)
            return;

        float step = Time.deltaTime / fadeDuration;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, step);

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;
    }

    public void ShowDeathMenu()
    {
        targetAlpha = 1f;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    public void HideDeathMenu()
    {
        targetAlpha = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
