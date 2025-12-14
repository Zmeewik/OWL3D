using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DebugHPBar : MonoBehaviour
{
    [Header("References")]
    public EntityHealth entity;

    [Header("UI Settings")]
    public Vector3 offset = new Vector3(0, 2.5f, 0);
    public Vector2 barSize = new Vector2(100, 10);

    private Canvas canvas;
    private Image fillImage;
    private Text healthText;

    private void Awake()
    {
        if (entity == null) entity = GetComponent<EntityHealth>();
        if (entity == null)
        {
            Debug.LogWarning("DebugHPBar: EntityHealth not assigned.");
            enabled = false;
            return;
        }

        // Создаём Canvas
        GameObject canvasObj = new GameObject("HPBarCanvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = offset;
        canvasObj.transform.localRotation = Quaternion.identity;

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.scaleFactor = 0.01f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // Создаём Background
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(canvas.transform);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = Color.black;
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.sizeDelta = barSize;
        bgRect.localPosition = Vector3.zero;

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(bg.transform);
        fillImage = fill.AddComponent<Image>();
        fillImage.color = Color.green;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = barSize;
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.localPosition = new Vector3(-barSize.x / 2, 0, 0);

        // Text
        GameObject txt = new GameObject("Text");
        txt.transform.SetParent(canvas.transform);
        healthText = txt.AddComponent<Text>();
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.font = (Font)Resources.GetBuiltinResource (typeof(Font), "LegacyRuntime.ttf");
        healthText.fontSize = 40;
        healthText.color = Color.white;

        RectTransform txtRect = healthText.GetComponent<RectTransform>();
        txtRect.sizeDelta = barSize * 1.2f;
        txtRect.localScale = Vector3.one * 0.01f;
        txtRect.localPosition = new Vector3(0, 15, -0.001f);

        // subscribe to events
        entity.OnTakeDamage += _ => UpdateBar();
        entity.OnDeath += () => UpdateBar();
        UpdateBar();
    }

    private void LateUpdate()
    {
        // Rotate toward camera
        if (Camera.main != null)
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - Camera.main.transform.position);
    }

    private void UpdateBar()
    {
        float healthPercent = entity.GetHealth() / entity.GetMaxHealth();
        fillImage.rectTransform.sizeDelta = new Vector2(barSize.x * healthPercent, barSize.y);
        fillImage.color = Color.Lerp(Color.red, Color.green, healthPercent);
        healthText.text = $"{Mathf.Ceil(entity.GetHealth())}/{entity.GetMaxHealth()}";
        canvas.enabled = healthPercent > 0f;
    }
}