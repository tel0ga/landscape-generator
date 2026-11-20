using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A marker on the scene, not on a scaled parent. Runtime changes are discarded on leaving Play Mode.
public sealed class SceneProjectionState : MonoBehaviour
{
    public int version;
    public static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var state in root.GetComponentsInChildren<SceneProjectionState>(true))
                if (state.version >= 1) return;
        var candidates = new HashSet<Transform>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.GetComponentInParent<Canvas>() != null || transform.GetComponentInParent<MapPreview>() != null) continue;
                if (transform.GetComponent<Camera>() != null) continue;
                if (transform.GetComponent<PlayerMovement>() != null || transform.GetComponent<FlyingMode>() != null ||
                    transform.GetComponent<SurvivalCombat.Health>() != null || transform.GetComponent<Collider2D>() != null ||
                    transform.GetComponent<SpriteRenderer>() != null || transform.GetComponent<SurvivalCombat.Workbench>() != null)
                    candidates.Add(transform);
            }
        }
        var anchors = new List<Transform>();
        foreach (var candidate in candidates)
        {
            bool child = false;
            for (var parent = candidate.parent; parent != null; parent = parent.parent)
                if (candidates.Contains(parent)) { child = true; break; }
            if (!child) anchors.Add(candidate);
        }
        foreach (var anchor in anchors)
        {
            anchor.position = WorldProjection.ProjectPosition(anchor.position);
            if (anchor.TryGetComponent<Rigidbody2D>(out var body)) body.position = anchor.position;
            bool ground = anchor.name == "Wall" || anchor.name == "Arena floor";
            ProjectedFootprint.Apply(anchor.gameObject);
            if (ground)
            {
                // Change visual size only; the collider has already been projected.
                var sprite = anchor.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    var visual = new GameObject("Ground visual"); visual.transform.SetParent(anchor, false);
                    var renderer = visual.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite.sprite; renderer.color = sprite.color; renderer.sharedMaterial = sprite.sharedMaterial;
                    renderer.sortingOrder = sprite.sortingOrder; renderer.sortingLayerID = sprite.sortingLayerID;
                    visual.transform.localScale = new Vector3(1, WorldProjection.VerticalScale, 1);
                    sprite.enabled = false;
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(visual, "Project ground visual");
#endif
                }
            }
            else GroundSorting.Apply(anchor.gameObject);
        }
        var marker = new GameObject("World projection (2:1)");
        SceneManager.MoveGameObjectToScene(marker, scene);
        marker.AddComponent<SceneProjectionState>().version = 1;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(marker, "Mark projected scene");
#endif
        Physics2D.SyncTransforms();
    }
}
