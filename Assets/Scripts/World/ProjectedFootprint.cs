using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectedFootprint : MonoBehaviour
{
    [SerializeField] bool applied;
    public static void Apply(GameObject root)
    {
        var component = root.GetComponent<ProjectedFootprint>() ?? Add<ProjectedFootprint>(root);
        if (component.applied) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(component, "Project footprint");
#endif
        component.applied = true;
        foreach (var source in root.GetComponentsInChildren<Collider2D>(true))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(source, "Project collider");
#endif
            if (source is EdgeCollider2D edge)
            {
                var points = edge.points;
                for (int i = 0; i < points.Length; i++) points[i] = Project(source, points[i] + source.offset, root.transform.position);
                edge.offset = Vector2.zero; edge.points = points; continue;
            }
            var paths = new List<Vector2[]>();
            if (source is PolygonCollider2D polygon)
                for (int i = 0; i < polygon.pathCount; i++) paths.Add(polygon.GetPath(i));
            else if (source is BoxCollider2D box)
            {
                Vector2 h = box.size * .5f;
                paths.Add(new[] { new Vector2(-h.x,-h.y), new Vector2(h.x,-h.y), new Vector2(h.x,h.y), new Vector2(-h.x,h.y) });
            }
            else if (source is CircleCollider2D circle) paths.Add(Round(Vector2.one * circle.radius * 2, false));
            else if (source is CapsuleCollider2D capsule) paths.Add(Round(capsule.size, true));
            else continue;
            var replacement = source as PolygonCollider2D ?? Add<PolygonCollider2D>(source.gameObject);
            replacement.isTrigger = source.isTrigger; replacement.sharedMaterial = source.sharedMaterial;
            replacement.enabled = source.enabled; replacement.density = source.density;
            replacement.includeLayers = source.includeLayers; replacement.excludeLayers = source.excludeLayers;
            replacement.layerOverridePriority = source.layerOverridePriority;
            Vector2 offset = source.offset;
            replacement.offset = Vector2.zero; replacement.pathCount = paths.Count;
            for (int p = 0; p < paths.Count; p++)
            {
                var points = paths[p];
                for (int i = 0; i < points.Length; i++) points[i] = Project(source, points[i] + offset, root.transform.position);
                replacement.SetPath(p, points);
            }
            if (replacement != source)
            {
                source.enabled = false;
                if (Application.isPlaying) Destroy(source);
                else
                {
#if UNITY_EDITOR
                    UnityEditor.Undo.DestroyObjectImmediate(source);
#else
                    DestroyImmediate(source);
#endif
                }
            }
        }
    }
    static T Add<T>(GameObject go) where T : Component
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return UnityEditor.Undo.AddComponent<T>(go);
#endif
        return go.AddComponent<T>();
    }
    static Vector2 Project(Collider2D source, Vector2 point, Vector3 anchor)
    {
        Vector3 world = source.transform.TransformPoint(point);
        world.y = anchor.y + (world.y - anchor.y) * WorldProjection.VerticalScale;
        return source.transform.InverseTransformPoint(world);
    }
    public static Vector2[] Round(Vector2 size, bool capsule)
    {
        var points = new Vector2[32];
        bool vertical = size.y >= size.x;
        float radius = Mathf.Min(size.x, size.y) * .5f;
        float straight = Mathf.Abs(size.y - size.x) * .5f;
        for (int i = 0; i < points.Length; i++)
        {
            float angle = (i + .5f) * Mathf.PI * 2 / points.Length;
            Vector2 unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            points[i] = capsule ? unit * radius + (vertical ? Vector2.up * Mathf.Sign(unit.y) : Vector2.right * Mathf.Sign(unit.x)) * straight : Vector2.Scale(unit, size * .5f);
        }
        return points;
    }
}
