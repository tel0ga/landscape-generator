using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalCombat
{
    [Serializable] public class CombatSaveData
    {
        public int version = 1; public string worldKey;
        public InventoryData inventory; public string[] slots; public int activeSlot; public string throwable;
        public float health, stamina; public Vector2 playerPosition; public WorldSnapshot world;
    }
    public sealed class CombatSaveService
    {
        readonly CombatWorld world;
        public string Path { get; }
        public event Action<string> Completed;
        public CombatSaveService(CombatWorld value, string overridePath = null)
        {
            world = value;
            Path = overridePath ?? System.IO.Path.Combine(Application.persistentDataPath, "survival-" + SceneManager.GetActiveScene().name + ".json");
        }
        public CombatSaveData Capture()
        {
            var actor = world.Player;
            return new CombatSaveData { worldKey = world.WorldKey, inventory = Inventory.Copy(actor.Inventory.Data),
                slots = (string[])actor.Equipment.Slots.Clone(), activeSlot = actor.Equipment.ActiveSlot, throwable = actor.Equipment.ThrowableId,
                health = actor.Health.Current, stamina = actor.Stamina.CurrentStamina, playerPosition = actor.transform.position, world = Inventory.Copy(world.Data) };
        }
        public ActionResult TrySave()
        {
            try
            {
                var data = Capture(); if (!Validate(data)) return new ActionResult(Failure.SaveError);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                string temporary = Path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream)) { writer.Write(JsonUtility.ToJson(data, true)); writer.Flush(); stream.Flush(true); }
                if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak"); else File.Move(temporary, Path);
                Completed?.Invoke("Сохранено"); return ActionResult.Ok;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { Debug.LogWarning("Combat save failed: " + e.Message); return new ActionResult(Failure.SaveError); }
        }
        public bool Validate(CombatSaveData data)
        {
            if (data == null || data.version != 1 || data.worldKey != world.WorldKey || !world.Player.Inventory.Validate(data.inventory) || data.world == null || data.world.items == null || data.world.destroyedIds == null || data.world.consumedGeneratedIds == null) return false;
            if (!Finite(data.health) || data.health < 0 || data.health > world.Player.Health.maxHealth || !Finite(data.stamina) || data.stamina < 0 || data.stamina > world.Player.Stamina.MaxStamina || !Finite(data.playerPosition.x) || !Finite(data.playerPosition.y)) return false;
            if (data.slots == null || data.slots.Length != 2 || data.activeSlot < 0 || data.activeSlot > 1) return false;
            foreach (string slot in data.slots) if (!string.IsNullOrEmpty(slot) && data.inventory.entries.Find(e => e.instanceId == slot)?.weapon == null) return false;
            if (!string.IsNullOrEmpty(data.slots[0]) && data.slots[0] == data.slots[1]) return false;
            if (!string.IsNullOrEmpty(data.throwable))
            {
                var entry = data.inventory.entries.Find(e => e.instanceId == data.throwable);
                if (entry == null || world.Catalog.Find(entry.definitionId).kind != ItemKind.Throwable) return false;
            }
            var ids = new HashSet<string>(); var records = new HashSet<string>(); var generated = new HashSet<string>(data.world.consumedGeneratedIds);
            foreach (var item in data.inventory.entries) ids.Add(item.instanceId);
            foreach (var record in data.world.items)
            {
                if (record == null || record.item == null || string.IsNullOrEmpty(record.recordId) || !records.Add(record.recordId) || !ids.Add(record.item.instanceId) || !Finite(record.position.x) || !Finite(record.position.y)) return false;
                if (!string.IsNullOrEmpty(record.generatedId) && !generated.Add(record.generatedId)) return false;
                if (!world.Player.Inventory.Validate(new InventoryData { entries = new List<ItemEntry> { record.item } })) return false;
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public ActionResult TryLoad()
        {
            CombatSaveData data = ReadValid(Path) ?? ReadValid(Path + ".bak");
            if (data == null) return new ActionResult(Failure.SaveError);
            var actor = world.Player;
            actor.SetInventoryOpen(false); actor.Weapons.ResetTransient();
            if (actor.Flight != null) actor.Flight.isFlying = false;
            actor.Inventory.Restore(data.inventory); actor.Equipment.Restore(data.slots, data.activeSlot, data.throwable);
            actor.Health.Restore(data.health); actor.Stamina.RestoreStamina(data.stamina);
            actor.GetComponent<StatusEffectController>().Clear();
            actor.transform.position = data.playerPosition;
            if (actor.TryGetComponent<Rigidbody2D>(out var body)) { body.position = data.playerPosition; body.linearVelocity = Vector2.zero; }
            actor.GetComponent<PlayerMovement>()?.ClearKnockback();
            world.Restore(data.world); Physics2D.SyncTransforms();
            Completed?.Invoke("Загружено"); return ActionResult.Ok;
        }
        CombatSaveData ReadValid(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<CombatSaveData>(File.ReadAllText(path));
                return Validate(data) ? data : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NullReferenceException) { return null; }
        }
    }
}
