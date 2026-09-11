using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FlyingMode : MonoBehaviour
{
    // ===== Настройки режимов =====
    [SerializeField]
    private Dictionary<bool, float> speedModes = new Dictionary<bool, float>
    {
        [true] = 200f,
        [false] = 40f
    };

    private Dictionary<bool, float> cameraModes = new Dictionary<bool, float>
    {
        [true] = 70f,
        [false] = 40f
    };

    private float currentSpeed = 200f;
    private float currentCameraSize = 70f;

    // ===== Ссылки на компоненты =====
    private Rigidbody2D rb;
    private Camera mainCamera;
    private GameObject character;

    // ===== Input System =====
    // Имя класса берем из скриншота: InputSystem_Actions
    private InputSystem_Actions controls;

    // ===== Состояние =====
    private bool isflying = true;

    public bool isFlying
    {
        get => isflying;
        set
        {
            isflying = value;
            character.SetActive(!value);
            currentCameraSize = cameraModes[value];
            currentSpeed = speedModes[value];
        }
    }

    // ===== Жизненный цикл =====
    void Awake()
    {
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        // Включаем карту действий.
        // ВАЖНО: Проверь в ассете, как называется твоя Action Map (папка слева).
        // Если она называется "Player", оставь так. Если "Gameplay" - поменяй на controls.Gameplay.Enable();
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
    }

    void Start()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        character = transform.GetChild(0).gameObject;

        isFlying = true;
    }

    void Update()
    {
        // ==== Чтение ввода ====
        // Если твоя карта называется не "Player", поменяй здесь тоже
        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>().normalized;

        // ==== Движение ====
        rb.linearVelocity = Vector2.LerpUnclamped(
            rb.linearVelocity,
            moveInput * currentSpeed,
            0.03f
        );

        // ==== Переключение полета ====
        // Если действие называется не "FlyToggle", поменяй здесь
        if (controls.Player.FlyToggle.WasPressedThisFrame())
        {
            isFlying = !isFlying;
        }

        // ==== Камера ====
        mainCamera.orthographicSize = Mathf.Lerp(
            mainCamera.orthographicSize,
            currentCameraSize,
            0.02f
        );
    }
}