using UnityEngine;
using UnityEngine.UI;
using System.Collections;

//Handles screen fade transitions for door teleportation -EM//
//Manages a full-Screen black panel that fades in/out -EM//

public class DoorTransitionManager : MonoBehaviour
{
    [Header("UI Refernces")]
    [SerializeField] private Canvas fadeCanvas;
    [SerializeField] private Image fadePanel;

    [Header("Settings")]
    [Tooltip("Create fade panel automatically if missing")]
    public bool autoCreatePanel = true;

    private static DoorTransitionManager instance;

    private void Awake()
    {
        //Singelton pattern//
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        //Auto-Create fade panel if needed//
        if(fadePanel != null)
        {
            Color c = fadePanel.color;
            c.a = 0;
            fadePanel.color = c;
            fadePanel.gameObject.SetActive(false);
        }
    }

    //Create the fade panel UI programmatically -EM//
    private void CreateFadePanel()
    {
        //Create canvas if needed//
        if(fadeCanvas == null)
        {
            GameObject canvasObj = new GameObject("FadeCanvas");
            canvasObj.transform.SetParent(transform);

            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            //Render on top of everything//
            fadeCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        //Create black panel//
        GameObject panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(fadeCanvas.transform);

        fadePanel = panelObj.AddComponent<Image>();
        fadePanel.color = Color.black;

        //Make the panel fill the entire screen//
        RectTransform rect = fadePanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Debug.Log("[DungeonTransitionManager] Created fade panel");
    }

    //Fade to black over specified duration -EM//
    public IEnumerator FadeToBlack(float duration)
    {
        if(fadePanel == null)
        {
            Debug.LogError("[DoorTransitionManager] No fade panel found!");
            yield break;
        }

        fadePanel.gameObject.SetActive(true);

        float elapsed = 0f;
        Color color = fadePanel.color;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            color.a = alpha;
            fadePanel.color = color;
            yield return null;
        }

        //Ensure fully black -EM//
        color.a = 1f;
        fadePanel.color = color;
    }

    //Fade from black to transparent over specified duration -EM//
    public IEnumerator FadeFromBlack(float duration)
    {
        if(fadePanel == null)
        {
            Debug.LogError("[DoorTransitionManager] No fade panel found!");
            yield break;
        }

        float elapsed = 0f;
        Color color = fadePanel.color;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            color.a = alpha;
            fadePanel.color = color;
            yield return null;
        }

        //Enusre fully transparent -EM//
        color.a = 0f;
        fadePanel.color = color;
        fadePanel.gameObject.SetActive(false);
    }

    //instant fade to black -EM//
    public void SetBlack()
    {
        if(fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            Color c = fadePanel.color;
            c.a = 1f;
            fadePanel.color = c;
        }
    }

    //Instant fade to transparent -EM//
    public void SetClear()
    {
        if(fadePanel != null)
        {
            Color c = fadePanel.color;
            c.a = 0f;
            fadePanel.color = c;
            fadePanel.gameObject.SetActive(false);
        }
    }

    //Get singelton instance -EM//
    public static DoorTransitionManager Instance => instance;
}
