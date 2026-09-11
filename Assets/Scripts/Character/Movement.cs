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

    void Update()
    {
        // Ввод
        moveInput = controls.Player.Move.ReadValue<Vector2>().normalized;
        sprintHeld = controls.Player.Sprint.IsPressed();

        // Поворот
        if (flipSprite && spriteTransform != null && Mathf.Abs(moveInput.x) > 0.01f)
        {
            Vector3 scale = spriteTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (moveInput.x < 0 ? -1f : 1f);
            spriteTransform.localScale = scale;
        }
    }

    void FixedUpdate()
    {
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

        rb.linearVelocity = Vector2.LerpUnclamped(
            rb.linearVelocity,
            moveInput * currentSpeed,
            smoothing
        );
    }
}