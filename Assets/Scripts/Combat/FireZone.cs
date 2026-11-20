using System;
using UnityEngine;

namespace SurvivalCombat
{
    public class FireZone : MonoBehaviour
    {
        CombatPool pool; Health owner; float expires, nextTick, interval, radius, damage;
        public static void Spawn(CombatPool pool, Vector2 position, ThrowProfile profile, Health owner)
        {
            var go = pool.Acquire("fire", () => { var created = CombatArt.Shape("Fire", Vector2.zero, Vector2.one, new Color(1, .25f, 0, .45f), true, 8); created.AddComponent<FireZone>(); return created; });
            var zone = go.GetComponent<FireZone>(); zone.pool = pool; zone.owner = owner;
            zone.expires = Time.time + profile.fireDuration; zone.nextTick = Time.time;
            zone.interval = Mathf.Max(.1f, profile.fireInterval); zone.radius = profile.radius; zone.damage = profile.damage * .12f;
            go.transform.position = position; go.transform.localScale = new Vector3(profile.radius * 2, profile.radius * 2 * WorldProjection.VerticalScale, 1); go.SetActive(true);
        }
        void Update()
        {
            if (Time.timeScale <= 0) return;
            if (Time.time >= expires) { pool.Release(gameObject); return; }
            if (Time.time < nextTick) return;
            nextTick = Time.time + interval;
            CombatPhysics.Radial(transform.position, radius, new DamageInfo { attackId = Guid.NewGuid().ToString("N"), owner = owner,
                allowSelf = true, damage = damage, type = DamageType.Fire,
                effect = new EffectSpec { kind = EffectKind.Burning, damage = 2, duration = 2, interval = 1 } }, false);
        }
        void OnDisable() { pool = null; owner = null; expires = nextTick = 0; }
    }
}
