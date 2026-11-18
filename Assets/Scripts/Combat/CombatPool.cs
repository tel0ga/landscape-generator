using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    // Adapter over the existing MarchingBytes pool. Combat only tracks active leases;
    // allocation, available-object storage and reuse remain in EasyObjectPool.
    public class CombatPool : MonoBehaviour
    {
        MarchingBytes.EasyObjectPool backend;
        readonly Dictionary<string, string> registered = new Dictionary<string, string>();
        readonly HashSet<GameObject> leased = new HashSet<GameObject>();
        readonly string prefix = "combat." + Guid.NewGuid().ToString("N") + ".";
        public GameObject Acquire(string key, Func<GameObject> factory)
        {
            if (backend == null)
            {
                backend = MarchingBytes.EasyObjectPool.instance;
                if (backend == null) backend = FindAnyObjectByType<MarchingBytes.EasyObjectPool>();
                if (backend == null) backend = gameObject.AddComponent<MarchingBytes.EasyObjectPool>();
                backend.EnsureInitialized();
            }
            if (!registered.TryGetValue(key, out string poolName))
            {
                var template = factory(); template.name += " (pool template)"; template.SetActive(false);
                template.transform.SetParent(transform); poolName = prefix + key;
                backend.RegisterPool(poolName, template); registered.Add(key, poolName);
            }
            var go = backend.GetInactiveObjectFromPool(poolName, Vector3.zero, Quaternion.identity);
            if (go == null) throw new InvalidOperationException("Unable to acquire combat pool object " + key);
            leased.Add(go); return go;
        }
        public void Release(GameObject go)
        {
            if (go == null || !leased.Remove(go)) return;
            if (backend != null) backend.ReturnObjectToPool(go);
            else go.SetActive(false);
        }
        public void ClearActive()
        {
            foreach (var go in new List<GameObject>(leased)) Release(go);
        }
        void OnDestroy() { ClearActive(); }
    }
    public static class CombatArt
    {
        static Sprite square, circle;
        public static Sprite Square => square != null ? square : square = Make(false);
        public static Sprite Circle => circle != null ? circle : circle = Make(true);
        static Sprite Make(bool round)
        {
            var texture = new Texture2D(16, 16) { filterMode = FilterMode.Point, name = "Combat placeholder" };
            var colours = new Color[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                colours[y * 16 + x] = !round || Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) <= 7.5f ? Color.white : Color.clear;
            texture.SetPixels(colours); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16);
        }
        public static GameObject Shape(string name, Vector2 position, Vector2 size, Color colour, bool round = false, int order = 10)
        {
            var go = new GameObject(name); go.transform.position = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = round ? Circle : Square; renderer.color = colour; renderer.sortingOrder = order;
            return go;
        }
    }
}
