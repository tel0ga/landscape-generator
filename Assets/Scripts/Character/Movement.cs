using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Скорость")]
    [SerializeField] private float walkSpeed = 50f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float smoothing = 0.03f;

    [Header("Поворот")]
    [SerializeField] private bool flipSprite = true;
    [SerializeField] private Transform spriteTransform;

    [Header("Проверка движения (для стамины)")]
    [Tooltip("Минимальная реальная скорость, чтобы считать, что игрок двигается")]
    [SerializeField] private float movementThreshold = 0.5f;

    [Tooltip("Минимальная длина ввода, чтобы считать, что игрок жмёт клавиши")]
    [SerializeField] private float inputThreshold = 0.1f;

    // ===== Ссылки =====
    private Rigidbody2D rb;
    private StaminaSystem stamina;

    // ===== Input =====
    private InputSystem_Actions controls;
    private Vector2 moveInput;
    private bool sprintHeld;
    private Vector2 knockback, appliedKnockback;
    public void AddKnockback(Vector2 velocity) { knockback = Vector2.ClampMagnitude(knockback + velocity, 25); }
    public void ClearKnockback() { knockback = appliedKnockback = Vector2.zero; }

    void Awake()
    {
        controls = new InputSystem_Actions();
        rb = GetComponent<Rigidbody2D>();
        stamina = GetComponent<StaminaSystem>();

        if (spriteTransform == null && transform.childCount > 0)
            spriteTransform = transform.GetChild(0);
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();
    void OnDestroy() => controls.Dispose();

    void Update()
    {
        // Ввод
        moveInput = controls.Player.Move.ReadValue<Vector2>().normalized;
        sprintHeld = controls.Player.Sprint.IsPressed();

        // Поворот
        var combat = GetComponent<SurvivalCombat.CombatActor>();
        Vector2 facing = combat != null && combat.Aim != null ? combat.Aim.Direction : moveInput;
        if (flipSprite && spriteTransform != null && Mathf.Abs(facing.x) > 0.01f)
        {
            Vector3 scale = spriteTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facing.x < 0 ? -1f : 1f);
            spriteTransform.localScale = scale;
        }
    }

    void FixedUpdate()
    {
        var combat = GetComponent<SurvivalCombat.CombatActor>();
        if (combat != null && combat.Blocked)
        { rb.linearVelocity = Vector2.zero; ClearKnockback(); return; }
        // ==== 1. Проверяем, есть ли вообще ввод с клавиатуры ====
        bool hasInput = moveInput.sqrMagnitude > inputThreshold * inputThreshold;

        // ==== 2. Проверяем, реально ли игрок двигается (не уперся в стену) ====
        // Смотрим на фактическую скорость Rigidbody2D
        float actualSpeed = rb.linearVelocity.magnitude;
        bool isActuallyMoving = actualSpeed > movementThreshold;

        // ==== 3. Проверяем, разрешен ли бег (по стамине) ====
        bool sprintAllowed = stamina == null || stamina.CanSprint;

        // ==== 4. Итоговое условие: бежим ли мы по-настоящему ====
        bool actuallySprinting = sprintHeld && hasInput && isActuallyMoving && sprintAllowed;

        // ==== 5. Тратим стамину ТОЛЬКО если реально бежим ====
        if (stamina != null && actuallySprinting)
        {
            stamina.DrainStamina();
        }

        // ==== 6. Скорость и движение ====
        float currentSpeed = walkSpeed * (actuallySprinting ? sprintMultiplier : 1f);
        var effects = GetComponent<SurvivalCombat.StatusEffectController>();
        if (effects != null) currentSpeed *= effects.MovementMultiplier;

        rb.linearVelocity = Vector2.LerpUnclamped(
            rb.linearVelocity - appliedKnockback,
            moveInput * currentSpeed,
            smoothing
        ) + knockback;
        appliedKnockback = knockback;
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, 15f * Time.fixedDeltaTime);
    }
}
