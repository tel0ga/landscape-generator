// Minimal managed substitutes for data-only verification without a licensed Unity editor.
// This runner does NOT verify Unity physics, lifecycle, rendering, or input.
using System;
using System.Reflection;
using System.Text.Json;
namespace UnityEngine
{
    public interface ISerializationCallbackReceiver { void OnBeforeSerialize(); void OnAfterDeserialize(); }
    public class Object { public string name; }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        { var value = new T(); typeof(T).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(value, null); return value; }
    }
    public class Sprite : Object { } public class GameObject : Object { } public class AudioClip : Object { }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public class TextAreaAttribute : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x*x + y*y;
        public Vector2 normalized => sqrMagnitude > 0 ? new Vector2(x / MathF.Sqrt(sqrMagnitude), y / MathF.Sqrt(sqrMagnitude)) : new Vector2();
        public static Vector2 right => new Vector2(1, 0);
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r, float g, float b, float a = 1) { this.r=r; this.g=g; this.b=b; this.a=a; }
        public static Color white => new Color(1,1,1); public static Color yellow => new Color(1,1,0);
        public static Color green => new Color(0,1,0); public static Color gray => new Color(.5f,.5f,.5f); public static Color cyan => new Color(0,1,1);
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a,b); public static int Max(int a,int b) => Math.Max(a,b);
        public static float Min(float a, float b) => MathF.Min(a,b); public static int Min(int a,int b) => Math.Min(a,b);
        public static float Clamp(float v,float a,float b) => Math.Clamp(v,a,b); public static int Clamp(int v,int a,int b) => Math.Clamp(v,a,b);
        public static float Clamp01(float v) => Math.Clamp(v,0,1);
        public static int RoundToInt(float v) => (int)MathF.Round(v); public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
    }
    public static class JsonUtility
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
        public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
    }
}
namespace SurvivalCombat { public class Health { } }
