using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the HUD Slider's fill from the player's health. The slider itself carries no handle and
/// isn't interactive -- it's a display only, built as a Slider purely to reuse its fill-resizing
/// logic instead of hand-rolling a rect-resize on every health change.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealthBarUI : MonoBehaviour
{
    [SerializeField] private EntityHealth health;
    [SerializeField] private Slider slider;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();
    }

    private void OnEnable()
    {
        if (health == null) return;

        health.OnTakeDamage += HandleChanged;
        health.OnHealed += HandleChanged;
        health.OnDeath += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (health == null) return;

        health.OnTakeDamage -= HandleChanged;
        health.OnHealed -= HandleChanged;
        health.OnDeath -= Refresh;
    }

    private void HandleChanged(float _) => Refresh();

    private void Refresh()
    {
        if (slider == null || health == null) return;

        float max = health.GetMaxHealth();
        slider.value = max > 0f ? health.GetHealth() / max : 0f;
    }
}
