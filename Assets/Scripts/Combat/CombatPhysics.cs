using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    public static class CombatPhysics
    {
        static bool Ignored(Collider2D collider, Health owner) => collider == null || (owner != null && collider.GetComponentInParent<Health>() == owner) ||
            (collider.isTrigger && collider.GetComponentInParent<Health>() == null);
        public static bool Cast(Vector2 from, Vector2 direction, float distance, float radius, Health owner, out RaycastHit2D closest)
        {
            var hits = radius > 0 ? Physics2D.CircleCastAll(from, radius, direction, distance) : Physics2D.RaycastAll(from, direction, distance);
            closest = default; float best = float.MaxValue;
            foreach (var hit in hits) if (!Ignored(hit.collider, owner) && hit.distance < best) { closest = hit; best = hit.distance; }
            return closest.collider != null;
        }
        public static bool WallBetween(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            foreach (var hit in Physics2D.RaycastAll(from, delta.normalized, delta.magnitude))
                if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<Health>() == null) return true;
            return false;
        }
        public static Vector2 SafeMuzzle(CombatActor actor, AttackContext context, out RaycastHit2D obstruction)
        {
            Vector2 delta = context.origin - (Vector2)actor.transform.position;
            if (Cast(actor.transform.position, delta.normalized, delta.magnitude, 0, actor.Health, out obstruction)) return obstruction.point;
            return context.origin;
        }
        public static void Hit(RaycastHit2D hit, DamageInfo damage)
        {
            damage.point = hit.point;
            var health = hit.collider.GetComponentInParent<Health>();
            if (health != null) health.ReceiveDamage(damage);
        }
        public static void Hitscan(CombatActor actor, AttackContext context, WeaponDefinition definition, WeaponStats stats, DamageInfo damage)
        {
            Vector2 origin = SafeMuzzle(actor, context, out var obstruction);
            for (int i = 0; i < Mathf.Max(1, definition.fire.pellets); i++)
            {
                float angle = UnityEngine.Random.Range(-stats.spread * .5f, stats.spread * .5f);
                Vector2 direction = Quaternion.Euler(0, 0, angle) * context.direction;
                Vector2 end = origin + direction * definition.range;
                if (obstruction.collider != null) { damage.direction = direction; Hit(obstruction, damage); end = origin; }
                else if (Cast(origin, direction, definition.range, 0, actor.Health, out var hit)) { damage.direction = direction; Hit(hit, damage); end = hit.point; }
                CombatEffect.Line(actor.Pool, origin, end, new Color(1, .85f, .35f), .07f);
            }
        }
        public static void Melee(CombatActor actor, AttackContext context, WeaponDefinition definition, DamageInfo damage, HashSet<Health> hitSet)
        {
            Vector2 origin = actor.transform.position;
            foreach (var collider in Physics2D.OverlapCircleAll(origin, definition.range))
            {
                var target = collider.GetComponentInParent<Health>();
                if (target == null || target == actor.Health || target.Dead || hitSet.Contains(target)) continue;
                Vector2 point = collider.ClosestPoint(origin); Vector2 delta = point - origin;
                if (delta.sqrMagnitude > .001f && Vector2.Angle(context.direction, delta) > definition.melee.angle * .5f) continue;
                if (WallBetween(origin, point)) continue;
                hitSet.Add(target); damage.point = point; damage.direction = delta.normalized; target.ReceiveDamage(damage);
            }
        }
        public static void Radial(Vector2 origin, float radius, DamageInfo damage, bool falloff)
        {
            var targets = new HashSet<Health>();
            foreach (var collider in Physics2D.OverlapCircleAll(origin, radius))
            {
                var target = collider.GetComponentInParent<Health>();
                if (target == null || targets.Contains(target)) continue;
                Vector2 point = collider.ClosestPoint(origin);
                if (WallBetween(origin, point)) continue;
                targets.Add(target); var hit = damage; hit.point = point;
                hit.direction = ((Vector2)target.transform.position - origin).normalized;
                if (falloff) hit.damage *= 1 - Mathf.Clamp01(Vector2.Distance(origin, point) / Mathf.Max(.01f, radius));
                target.ReceiveDamage(hit);
            }
        }
    }
}
