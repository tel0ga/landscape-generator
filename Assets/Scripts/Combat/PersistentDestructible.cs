using UnityEngine;
namespace SurvivalCombat
{
    public class PersistentDestructible : MonoBehaviour
    {
        public string stableId;
        public string resourceId = "resource.scrap"; public int resourceCount = 3;
        Health health; bool subscribed;
        public void Bind(string id)
        {
            stableId = id;
            health = GetComponent<Health>() ?? gameObject.AddComponent<Health>();
            if (!subscribed) { health.Died += OnDeath; subscribed = true; }
            RefreshFromWorld();
        }
        public void RefreshFromWorld()
        {
            if (health == null) return;
            if (CombatWorld.Current != null && CombatWorld.Current.IsDestroyed(stableId))
            { health.Restore(0); gameObject.SetActive(false); }
            else
            {
                health.ResetHealth();
                var pooled = GetComponent<MarchingBytes.PoolObject>();
                if (pooled == null || !pooled.isPooled) gameObject.SetActive(true);
            }
        }
        void OnDeath()
        {
            var world = CombatWorld.Current;
            if (world == null || world.IsDestroyed(stableId)) return;
            world.MarkDestroyed(stableId);
            world.AddItem(new ItemEntry { definitionId = resourceId, count = resourceCount }, transform.position, stableId + ":loot");
            gameObject.SetActive(false); // Pool return/unload is not death and never awards loot.
        }
        void OnDestroy() { if (subscribed && health != null) health.Died -= OnDeath; }
    }
}
