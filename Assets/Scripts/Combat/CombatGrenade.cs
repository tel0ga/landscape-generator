using System;
using UnityEngine;

namespace SurvivalCombat
{
    public class CombatGrenade : MonoBehaviour
    {
        CombatPool pool; Health owner; ThrowProfile profile; Vector2 start, end; float began; Transform visual;
        public static void Spawn(CombatPool pool, CombatActor actor, AttackContext context, ThrowProfile profile)
        {
            var go = pool.Acquire("grenade", () =>
            {
                var root = CombatArt.Shape("Grenade shadow", Vector2.zero, Vector2.one, new Color(0, 0, 0, .4f), true, 12);
                root.GetComponent<SpriteRenderer>().size = Vector2.one;
                var picture = CombatArt.Shape("Grenade", Vector2.zero, new Vector2(.22f, .22f), Color.green, true, 30); picture.transform.SetParent(root.transform);
                root.AddComponent<CombatGrenade>(); return root;
            });
            var grenade = go.GetComponent<CombatGrenade>(); grenade.pool = pool; grenade.owner = actor.Health;
            grenade.profile = Inventory.Copy(profile); grenade.start = actor.transform.position;
            Vector2 delta = WorldProjection.ToMap(context.target - grenade.start);
            grenade.end = grenade.start + WorldProjection.ToWorld(Vector2.ClampMagnitude(delta, Mathf.Max(.1f, profile.range)));
            if (ProjectedCast.Cast(grenade.start, delta.normalized, WorldProjection.Distance(grenade.start, grenade.end), .1f, actor.Health, out var hit))
                grenade.end = hit.centroid - (grenade.end - grenade.start).normalized * .03f;
            grenade.began = Time.time; grenade.visual = go.transform.GetChild(0);
            go.transform.position = grenade.start; go.transform.localScale = Vector3.one;
            go.GetComponent<SpriteRenderer>().transform.localScale = Vector3.one;
            go.GetComponent<SpriteRenderer>().sprite = CombatArt.Circle;
            go.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Sliced;
            go.GetComponent<SpriteRenderer>().size = new Vector2(.3f, .15f);
            grenade.visual.GetComponent<SpriteRenderer>().color = profile.incendiary ? new Color(1, .4f, .1f) : Color.green;
            go.SetActive(true);
        }
        void Update()
        {
            float elapsed = Time.time - began; float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, profile.travelTime));
            transform.position = Vector2.Lerp(start, end, t); visual.localPosition = new Vector3(0, Mathf.Sin(t * Mathf.PI) * 1.2f, 0);
            if (elapsed < Mathf.Max(profile.travelTime, profile.fuse)) return;
            var damage = new DamageInfo { owner = owner, damage = profile.damage, type = profile.incendiary ? DamageType.Fire : DamageType.Physical,
                attackId = Guid.NewGuid().ToString("N"), allowSelf = true, knockback = 5 };
            CombatPhysics.Radial(transform.position, profile.radius, damage, true);
            if (profile.incendiary) FireZone.Spawn(pool, transform.position, profile, owner);
            else CombatEffect.Line(pool, (Vector2)transform.position - Vector2.right * profile.radius, (Vector2)transform.position + Vector2.right * profile.radius, Color.yellow, .2f);
            pool.Release(gameObject);
        }
        void OnDisable() { pool = null; owner = null; profile = null; began = 0; }
    }
}
