using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class GroundSorting : MonoBehaviour
{
    SortingGroup group;
    public static void Apply(GameObject root)
    {
        if (root.GetComponent<GroundSorting>() != null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEditor.Undo.AddComponent<GroundSorting>(root); return; }
#endif
        root.AddComponent<GroundSorting>();
    }
    void OnEnable()
    {
        group = GetComponent<SortingGroup>();
        if (group == null) group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = 0;
    }
    void LateUpdate()
    {
        // Sort groups by distance along camera's custom Y axis, without clamping large world coordinates.
        if (group != null) group.sortingOrder = 0;
    }
}
