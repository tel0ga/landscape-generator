using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    [Header("Настройки выносливости")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainRate = 25f;
    [SerializeField] private float regenRate = 15f;
    [SerializeField] private float regenDelay = 1f;

    [Header("Блокировка бега")]
    [SerializeField] private bool blockSprintOnEmpty = true;
    [SerializeField] private float unblockThreshold = 20f;

    [Header("UI — Fill")]
    [SerializeField] private Image staminaFill;
    [SerializeField] private CanvasGroup staminaCanvasGroup;

    [Header("Прозрачность по уровням")]
    [Tooltip("Alpha, когда стамина на максимуме (полупрозрачная)")]
    [SerializeField] private float highAlpha = 0.3f;

    [Tooltip("Alpha, когда стамина в середине и ниже (полностью видимая)")]
    [SerializeField] private float fullAlpha = 1f;

    [Tooltip("Порог, ниже которого полоска становится полностью непрозрачной (0..1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fullVisibilityThreshold = 0.5f;

    [Tooltip("Скорость плавного перехода alpha")]
    [SerializeField] private float fadeSpeed = 5f;

    [Header("Пульсация при низкой стамине")]
    [Tooltip("Порог, ниже которого начинается пульсация (0..1)")]
    [Range(0f, 1f)]
    [SerializeField] private float lowStaminaThreshold = 0.25f;

    [Tooltip("Скорость пульсации (чем больше — тем чаще мигает)")]
    [SerializeField] private float pulseSpeed = 8f;

    [Tooltip("Минимальная alpha во время пульсации")]
    [Range(0f, 1f)]
    [SerializeField] private float pulseMinAlpha = 0.2f;

    [Header("Цвета")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color lowColor = new Color(1f, 0.5f, 0f); // оранжевый
    [SerializeField] private Color emptyColor = Color.red;

    // ===== Состояние =====
    private float currentStamina;
    private float regenTimer;
    private bool isExhausted;
    private bool wasDrainingThisFrame;
    private float pulseTimer;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => currentStamina / maxStamina;
    public bool CanSprint => !isExhausted && currentStamina > 0f;

    void Start()
    {
        currentStamina = maxStamina;
        UpdateFill();
        ApplyAlpha(instant: true);
        ApplyColor();
    }

    void Update()
    {
        // Восстановление
        if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += regenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            UpdateFill();
            ApplyColor();
        }

        // Разблокировка бега
        if (isExhausted && currentStamina >= unblockThreshold)
            isExhausted = false;

        // Прозрачность + пульсация
        ApplyAlpha(instant: false);

        // Сброс флага траты
        wasDrainingThisFrame = false;
    }

    /// <summary>
    /// Вызывается из PlayerMovement при беге.
    /// </summary>
    public void DrainStamina()
    {
        currentStamina -= drainRate * Time.deltaTime;
        currentStamina = Mathf.Max(currentStamina, 0f);
        regenTimer = regenDelay;

        UpdateFill();
        ApplyColor();

        if (blockSprintOnEmpty && currentStamina <= 0f)
            isExhausted = true;

        wasDrainingThisFrame = true;
    }

    // ===== Внутренние методы =====

    private void UpdateFill()
    {
        if (staminaFill != null)
            staminaFill.fillAmount = StaminaPercent;
    }

    /// <summary>
    /// Управляет прозрачностью с учётом:
    /// — высокого уровня (полупрозрачная)
    /// — среднего (полная видимость)
    /// — низкого (пульсация)
    /// — полного нуля (аварийная пульсация)
    /// </summary>
    private void ApplyAlpha(bool instant)
    {
        if (staminaCanvasGroup == null) return;

        float percent = StaminaPercent;
        float targetAlpha;

        // Если игрок не бежит и стамина полная — скрываем полностью
        if (!wasDrainingThisFrame && currentStamina >= maxStamina)
        {
            targetAlpha = 0f;
        }
        // Низкий уровень — пульсация
        else if (percent <= lowStaminaThreshold && percent > 0f)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f; // 0..1
            targetAlpha = Mathf.Lerp(pulseMinAlpha, fullAlpha, pulse);
        }
        // Полный ноль — быстрая пульсация
        else if (percent <= 0f)
        {
            pulseTimer += Time.deltaTime * pulseSpeed * 2f; // в 2 раза быстрее
            targetAlpha = (Mathf.Sin(pulseTimer) > 0f) ? fullAlpha : pulseMinAlpha;
        }
        // Высокий уровень — полупрозрачная
        else if (percent >= fullVisibilityThreshold)
        {
            // Плавно от fullAlpha (на пороге) до highAlpha (на максимуме)
            float t = Mathf.InverseLerp(fullVisibilityThreshold, 1f, percent);
            targetAlpha = Mathf.Lerp(fullAlpha, highAlpha, t);
        }
        // Средний уровень — полная видимость
        else
        {
            targetAlpha = fullAlpha;
        }

        // Применяем с плавностью
        if (instant)
            staminaCanvasGroup.alpha = targetAlpha;
        else
            staminaCanvasGroup.alpha = Mathf.Lerp(
                staminaCanvasGroup.alpha,
                targetAlpha,
                Time.deltaTime * fadeSpeed
            );
    }

    /// <summary>
    /// Меняет цвет полоски: зелёный → оранжевый → красный.
    /// </summary>
    private void ApplyColor()
    {
        if (staminaFill == null) return;

        float percent = StaminaPercent;

        if (percent <= lowStaminaThreshold)
        {
            // От оранжевого к красному
            float t = Mathf.InverseLerp(0f, lowStaminaThreshold, percent);
            staminaFill.color = Color.Lerp(emptyColor, lowColor, t);
        }
        else
        {
            // От оранжевого к зелёному
            float t = Mathf.InverseLerp(lowStaminaThreshold, 1f, percent);
            staminaFill.color = Color.Lerp(lowColor, normalColor, t);
        }
    }
}