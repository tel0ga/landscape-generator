using UnityEngine;

namespace SurvivalCombat
{
    public class CombatProjectile : MonoBehaviour
    {
        CombatPool pool; DamageInfo damage; Vector2 direction; float speed, radius, remaining, expires;
        public static void Spawn(CombatPool pool, CombatActor actor, AttackContext context, WeaponDefinition def, DamageInfo info)
        {
            Vector2 origin = CombatPhysics.SafeMuzzle(actor, context, out var obstruction);
            if (obstruction.collider != null) { CombatPhysics.Hit(obstruction, info); return; }
            var go = pool.Acquire("projectile." + def.id, () =>
            {
                var created = def.projectile.prefab != null ? Instantiate(def.projectile.prefab) : CombatArt.Shape("Arrow", Vector2.zero, new Vector2(.45f, .08f), def.tint);
                if (created.GetComponent<CombatProjectile>() == null) created.AddComponent<CombatProjectile>(); return created;
            });
            var shot = go.GetComponent<CombatProjectile>(); shot.pool = pool; shot.damage = info;
            shot.direction = context.direction; shot.speed = Mathf.Max(.1f, def.projectile.speed); shot.radius = Mathf.Max(0, def.projectile.radius);
            shot.remaining = def.range; shot.expires = Time.time + Mathf.Max(.01f, def.projectile.lifetime);
            go.transform.position = origin; go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(context.direction.y, context.direction.x) * Mathf.Rad2Deg);
            foreach (var collider in go.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            foreach (var trail in go.GetComponentsInChildren<TrailRenderer>()) trail.Clear();
            go.SetActive(true);
        }
        void FixedUpdate()
        {
            if (Time.timeScale <= 0) return;
            float distance = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            if (CombatPhysics.Cast(transform.position, direction, distance, radius, damage.owner, out var hit))
            { CombatPhysics.Hit(hit, damage); pool.Release(gameObject); return; }
            transform.position += (Vector3)(direction * distance); remaining -= distance;
            if (remaining <= 0 || Time.time >= expires) pool.Release(gameObject);
        }
        void OnDisable()
        {
            damage = default; pool = null; direction = Vector2.zero; remaining = expires = 0;
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
        }
    }
}
