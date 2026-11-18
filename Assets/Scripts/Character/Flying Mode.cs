using UnityEngine;

public class FlyingMode : MonoBehaviour
{
    [SerializeField] float flyingSpeed = 40;
    [SerializeField] float flightCameraSize = 40;
    [SerializeField] float walkingCameraSize = 11.25f;
    Rigidbody2D body;
    Camera mainCamera;
    GameObject observerVisual;
    PlayerMovement player;
    InputSystem_Actions controls;
    bool flying;
    public bool isFlying
    {
        get => flying;
        set
        {
            if (flying == value) return;
            flying = value;
            if (player != null)
            {
                if (value) transform.position = player.transform.position;
                else player.transform.position = transform.position;
                if (player.TryGetComponent<Rigidbody2D>(out var playerBody)) playerBody.linearVelocity = Vector2.zero;
                foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = !value;
                var combat = player.GetComponent<SurvivalCombat.CombatActor>();
                if (value && combat != null) combat.Weapons.DisableCombat();
            }
            if (observerVisual != null) observerVisual.SetActive(value);
        }
    }
    void Awake() { controls = new InputSystem_Actions(); body = GetComponent<Rigidbody2D>(); }
    void OnEnable() { controls.Player.Enable(); }
    void OnDisable() { controls.Player.Disable(); }
    void OnDestroy() { controls.Dispose(); }
    void Start()
    {
        mainCamera = Camera.main; player = FindAnyObjectByType<PlayerMovement>();
        if (transform.childCount > 0) observerVisual = transform.GetChild(0).gameObject;
        if (observerVisual != null) observerVisual.SetActive(false);
    }
    void Update()
    {
        var combat = player != null ? player.GetComponent<SurvivalCombat.CombatActor>() : null;
        if (Time.timeScale <= 0 || (combat != null && combat.Health.Dead)) return;
        if (controls.Player.FlyToggle.WasPressedThisFrame()) isFlying = !isFlying;
        if (mainCamera != null) mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, flying ? flightCameraSize : walkingCameraSize, 1 - Mathf.Exp(-5 * Time.deltaTime));
    }
    void FixedUpdate()
    {
        if (body == null) return;
        body.linearVelocity = flying ? controls.Player.Move.ReadValue<Vector2>().normalized * flyingSpeed : Vector2.zero;
        if (flying && player != null)
        {
            player.transform.position = transform.position;
            if (player.TryGetComponent<Rigidbody2D>(out var playerBody)) playerBody.position = body.position;
        }
    }
}
