using UnityEngine;

public static class WorldProjection
{
    public const float VerticalScale = .5f;
    public static Vector2 ToWorld(Vector2 map) => new Vector2(map.x, map.y * VerticalScale);
    public static Vector2 ToMap(Vector2 world) => new Vector2(world.x, world.y / VerticalScale);
    public static Vector3 ProjectPosition(Vector3 map) => new Vector3(map.x, map.y * VerticalScale, map.z);
    public static float Distance(Vector2 a, Vector2 b) => ToMap(a - b).magnitude;
    public static float SqrDistance(Vector2 a, Vector2 b) => ToMap(a - b).sqrMagnitude;
    public static Bounds ProjectBounds(Vector2 centre, Vector2 size) => new Bounds(ToWorld(centre), ToWorld(size));
}
