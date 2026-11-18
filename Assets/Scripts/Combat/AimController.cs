using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalCombat
{
    public class AimController : MonoBehaviour
    {
        CombatActor actor;
        public Vector2 Direction { get; private set; } = Vector2.right;
        public Vector2 Target { get; private set; }
        public void Initialize(CombatActor value) { actor = value; }
        public void SetAim(Vector2 target)
        {
            Target = target; Vector2 delta = target - (Vector2)transform.position;
            if (delta.sqrMagnitude > .0025f) Direction = delta.normalized;
        }
        public void AimAtScreen(Vector2 position)
        {
            var camera = Camera.main; if (camera == null) return;
            var ray = camera.ScreenPointToRay(position);
            if (new Plane(Vector3.forward, transform.position).Raycast(ray, out float distance)) SetAim(ray.GetPoint(distance));
        }
        public AttackContext Context => new AttackContext(actor.View != null ? (Vector2)actor.View.Muzzle.position : (Vector2)transform.position, Direction, Target);
    }
}
