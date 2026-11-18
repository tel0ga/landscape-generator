using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    Transform player;
    FlyingMode flight;
    public void SetTarget(Transform target, FlyingMode flyingMode) { player = target; flight = flyingMode; }
    void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }
    void LateUpdate()
    {
        if (player == null) return;
        var target = flight != null && flight.isFlying ? flight.transform : player;
        transform.position = Vector3.Lerp(transform.position, target.position - Vector3.forward * 10, 1 - Mathf.Exp(-12 * Time.deltaTime));
    }
}
