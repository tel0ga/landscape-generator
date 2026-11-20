using System.Collections.Generic;
using UnityEngine;

// A reusable trigger-only shape, enabled only during a synchronous query.
// It never participates in a physics simulation step.
public static class ProjectedCast
{
    static PolygonCollider2D probe;
    static readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { if (probe != null) Object.Destroy(probe.gameObject); probe = null; hits.Clear(); }
    public static bool Cast(Vector2 origin, Vector2 mapDirection, float mapDistance, float radius,
        SurvivalCombat.Health owner, out RaycastHit2D closest)
    {
        Vector2 delta = WorldProjection.ToWorld(mapDirection.normalized * mapDistance);
        closest = default;
        if (radius <= 0) return SurvivalCombat.CombatPhysics.Cast(origin, delta.normalized, delta.magnitude, 0, owner, out closest);
        if (probe == null)
        {
            var go = new GameObject("Projected cast probe"); go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            probe = go.AddComponent<PolygonCollider2D>(); probe.isTrigger = true;
        }
        var points = ProjectedFootprint.Round(Vector2.one * radius * 2, false);
        for (int i = 0; i < points.Length; i++) points[i] = WorldProjection.ToWorld(points[i]);
        probe.points = points; probe.transform.position = origin;
        probe.gameObject.SetActive(true);
        try
        {
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D(); filter.SetLayerMask(Physics2D.DefaultRaycastLayers); filter.useTriggers = true;
            hits.Clear(); probe.Cast(delta.normalized, filter, hits, delta.magnitude, true);
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider == probe) continue;
                var health = hit.collider.GetComponentInParent<SurvivalCombat.Health>();
                if ((owner != null && health == owner) || (hit.collider.isTrigger && health == null)) continue;
                if (hit.distance < best) { best = hit.distance; closest = hit; }
            }
            return closest.collider != null;
        }
        finally { probe.gameObject.SetActive(false); }
    }
}
