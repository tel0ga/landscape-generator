using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    [Serializable] public class WorldItemRecord
    {
        public string recordId = Guid.NewGuid().ToString("N"); public string generatedId;
        public ItemEntry item; public Vector2 position;
    }
    [Serializable] public class WorldSnapshot
    {
        public List<WorldItemRecord> items = new List<WorldItemRecord>();
        public List<string> consumedGeneratedIds = new List<string>();
        public List<string> destroyedIds = new List<string>();
    }
    public class CombatWorld : MonoBehaviour
    {
        public static CombatWorld Current { get; private set; }
        public WorldSnapshot Data { get; private set; } = new WorldSnapshot();
        public CombatActor Player { get; set; }
        public CombatSaveService Save { get; private set; }
        public CombatCatalog Catalog { get; private set; }
        CombatPool pool; readonly Dictionary<string, WorldItem> views = new Dictionary<string, WorldItem>();
        float nextRefresh; public string WorldKey { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() { Current = null; }
        public void Initialize(CombatCatalog catalog, CombatPool value, string key)
        { Current = this; Catalog = catalog; pool = value; WorldKey = key; Save = new CombatSaveService(this); }
        void OnDestroy() { if (Current == this) Current = null; }
        public bool AddItem(ItemEntry item, Vector2 position, string generatedId = null)
        {
            if (item == null || Data.items.Exists(record => record.item.instanceId == item.instanceId)) return false;
            if (generatedId != null && (Data.consumedGeneratedIds.Contains(generatedId) || Data.items.Exists(record => record.generatedId == generatedId))) return false;
            Data.items.Add(new WorldItemRecord { item = Inventory.Copy(item), position = WorldProjection.ToMap(position), generatedId = generatedId });
            nextRefresh = 0; return true;
        }
        public bool IsDestroyed(string id) => !string.IsNullOrEmpty(id) && Data.destroyedIds.Contains(id);
        public void MarkDestroyed(string id) { if (!IsDestroyed(id)) Data.destroyedIds.Add(id); }
        public ActionResult TryPickup(CombatActor actor, string recordId)
        {
            var record = Data.items.Find(item => item.recordId == recordId);
            if (record == null) return new ActionResult(Failure.Invalid);
            var result = actor.Inventory.TryAdd(record.item); if (!result) return result;
            if (!string.IsNullOrEmpty(record.generatedId)) Data.consumedGeneratedIds.Add(record.generatedId);
            Data.items.Remove(record);
            if (views.TryGetValue(recordId, out var view)) { pool.Release(view.gameObject); views.Remove(recordId); }
            return ActionResult.Ok;
        }
        public WorldItem Nearest(Vector2 position, float range)
        {
            WorldItem best = null; float distance = range * range;
            foreach (var view in views.Values)
            {
                float sqr = WorldProjection.SqrDistance(view.transform.position, position);
                if (sqr < distance && !CombatPhysics.WallBetween(position, view.transform.position)) { best = view; distance = sqr; }
            }
            return best;
        }
        public void Restore(WorldSnapshot snapshot)
        {
            foreach (var view in views.Values) if (view != null) pool.Release(view.gameObject);
            views.Clear(); pool.ClearActive(); Data = Inventory.Copy(snapshot); nextRefresh = 0;
            foreach (var target in FindObjectsByType<PersistentDestructible>(FindObjectsInactive.Include)) target.RefreshFromWorld();
        }
        void Update()
        {
            if (Player == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .4f; RefreshViews(Player.transform.position);
        }
        public void RefreshViews(Vector2 centre)
        {
            var keep = new HashSet<string>();
            foreach (var record in Data.items)
            {
                if ((WorldProjection.ToWorld(record.position) - centre).sqrMagnitude > 60 * 60) continue;
                keep.Add(record.recordId); if (views.ContainsKey(record.recordId)) continue;
                var definition = Catalog.Find(record.item.definitionId); if (definition == null) continue;
                var go = pool.Acquire("world." + definition.id, () =>
                {
                    var created = definition.worldPrefab != null ? Instantiate(definition.worldPrefab) : CombatArt.Shape("Loot: " + definition.displayName, Vector2.zero, new Vector2(.45f, .45f), definition.tint, true, 25);
                    if (created.GetComponent<WorldItem>() == null) created.AddComponent<WorldItem>();
                    return created;
                });
                foreach (var collider in go.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                go.transform.position = WorldProjection.ToWorld(record.position);
                GroundSorting.Apply(go);
                var view = go.GetComponent<WorldItem>(); view.Bind(record.recordId, definition.displayName); go.SetActive(true); views.Add(record.recordId, view);
            }
            foreach (string id in new List<string>(views.Keys)) if (!keep.Contains(id)) { pool.Release(views[id].gameObject); views.Remove(id); }
        }
    }
}
