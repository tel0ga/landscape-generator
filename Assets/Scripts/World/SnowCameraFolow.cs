using UnityEngine;

public class SnowFollowCamera : MonoBehaviour
{
    [SerializeField] public Transform target;

    [SerializeField] public float offsetY = 6f;
    [SerializeField] public float predictionTime = 2f;
    [SerializeField] public float smoothSpeed = 5f;

    private Vector3 lastPosition;
    private Vector3 velocity;

    private void Start()
    {
        lastPosition = target.position;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (dt <= 0f || target == null)
            return;

        // Вычисляем скорость камеры
        velocity = (target.position - lastPosition) / dt;

        // Предсказываем будущую позицию
        Vector3 predictedPosition =
            target.position + velocity * predictionTime;

        // Смещаем область появления снега вверх
        predictedPosition.y += offsetY;

        // Сохраняем Z системы частиц
        predictedPosition.z = transform.position.z;

        // Плавно перемещаем систему частиц
        transform.position = Vector3.Lerp(
            transform.position,
            predictedPosition,
            1f - Mathf.Exp(-smoothSpeed * dt)
        );

        lastPosition = target.position;
    }
}