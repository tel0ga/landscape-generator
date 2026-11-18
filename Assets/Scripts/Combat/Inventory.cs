using System;
using System.Linq;
using UnityEngine;

namespace SurvivalCombat
{
    // All transfers are validated before committing. Equipment references IDs, never list indices.
    public sealed class Inventory
    {
        public const int Capacity = 24;
        public CombatCatalog Catalog { get; }
        public InventoryData Data { get; private set; } = new InventoryData();
        public event Action Changed;
        public Inventory(CombatCatalog catalog) { Catalog = catalog; }
        public ItemEntry Find(string id) => Data.entries.Find(entry => entry.instanceId == id);
        public int Count(string definitionId) => Data.entries.Where(e => e.definitionId == definitionId).Sum(e => e.count);
        public void NotifyChanged() => Changed?.Invoke();
        public static T Copy<T>(T data) => JsonUtility.FromJson<T>(JsonUtility.ToJson(data));
        public void Restore(InventoryData data)
        {
            if (!Validate(data)) throw new ArgumentException("Invalid inventory snapshot");
            Data = Copy(data); NotifyChanged();
        }
        public bool Validate(InventoryData data)
        {
            if (data == null || data.entries == null || data.entries.Count > Capacity) return false;
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.instanceId) || !ids.Add(entry.instanceId)) return false;
                var def = Catalog.Find(entry.definitionId);
                if (def == null || entry.count < 1 || entry.count > Mathf.Max(1, def.stackLimit)) return false;
                if (def.kind == ItemKind.Weapon)
                {
                    var weapon = entry.weapon;
                    if (weapon == null || entry.count != 1 || weapon.instanceId != entry.instanceId || weapon.definitionId != entry.definitionId || weapon.rolledModifiers == null || weapon.installedAttachments == null) return false;
                    if (!Enum.IsDefined(typeof(Rarity), weapon.rarity) || weapon.rolledModifiers.Any(m => m == null || !Finite(m.flat) || !Finite(m.percent))) return false;
                    var slots = new System.Collections.Generic.HashSet<string>();
                    var wd = (WeaponDefinition)def;
                    foreach (string attachmentId in weapon.installedAttachments)
                    {
                        var attachment = Catalog.Find(attachmentId);
                        if (!Compatible(wd, attachment) || !slots.Add(attachment.attachmentSlot)) return false;
                    }
                    var stats = WeaponStats.Calculate(wd, weapon, Catalog);
                    if (!Finite(weapon.durability) || weapon.durability < 0 || weapon.durability > stats.maxDurability || weapon.ammoInMagazine < 0 || weapon.ammoInMagazine > stats.magazineSize) return false;
                }
                else if (entry.weapon != null) return false;
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public ActionResult TryAdd(ItemEntry item)
        {
            if (item == null || item.count <= 0 || Catalog.Find(item.definitionId) == null) return new ActionResult(Failure.Invalid);
            if (Find(item.instanceId) != null) return new ActionResult(Failure.Invalid);
            var next = Copy(Data);
            if (!Add(next, item)) return new ActionResult(Failure.Full);
            if (!Validate(next)) return new ActionResult(Failure.Invalid);
            Data = next; NotifyChanged(); return ActionResult.Ok;
        }
        bool Add(InventoryData next, ItemEntry incoming)
        {
            var def = Catalog.Find(incoming.definitionId);
            if (def == null) return false;
            if (incoming.weapon != null)
            {
                if (next.entries.Count >= Capacity) return false;
                next.entries.Add(Copy(incoming)); return true;
            }
            int remaining = incoming.count;
            foreach (var entry in next.entries.Where(e => e.weapon == null && e.definitionId == incoming.definitionId))
            {
                int amount = Mathf.Min(remaining, Mathf.Max(0, def.stackLimit - entry.count));
                entry.count += amount; remaining -= amount;
            }
            bool first = true;
            while (remaining > 0)
            {
                if (next.entries.Count >= Capacity) return false;
                int amount = Mathf.Min(remaining, Mathf.Max(1, def.stackLimit));
                next.entries.Add(new ItemEntry { instanceId = first ? incoming.instanceId : Guid.NewGuid().ToString("N"), definitionId = incoming.definitionId, count = amount });
                first = false; remaining -= amount;
            }
            return true;
        }
        public bool TryConsume(string id, int amount)
        {
            if (amount < 0 || Count(id) < amount) return false;
            Consume(Data, id, amount); NotifyChanged(); return true;
        }
        static void Consume(InventoryData data, string id, int amount)
        {
            for (int i = data.entries.Count - 1; i >= 0 && amount > 0; i--)
            {
                var entry = data.entries[i]; if (entry.definitionId != id) continue;
                int take = Mathf.Min(entry.count, amount); entry.count -= take; amount -= take;
                if (entry.count == 0) data.entries.RemoveAt(i);
            }
        }
        public ItemEntry Remove(string instanceId)
        {
            var entry = Find(instanceId); if (entry == null) return null;
            Data.entries.Remove(entry); NotifyChanged(); return entry;
        }
        public int TransferAmmo(WeaponInstance weapon, int maxTransfer)
        {
            var def = Catalog.Weapon(weapon.definitionId);
            var stats = WeaponStats.Calculate(def, weapon, Catalog);
            int amount = Mathf.Max(0, Mathf.Min(maxTransfer, Mathf.Min(stats.magazineSize - weapon.ammoInMagazine, Count(def.fire.ammoId))));
            // Change both sides before publishing a single inventory event.
            Consume(Data, def.fire.ammoId, amount); weapon.ammoInMagazine += amount;
            NotifyChanged(); return amount;
        }
        static bool Compatible(WeaponDefinition weapon, ItemDefinition attachment) => attachment != null && attachment.kind == ItemKind.Attachment &&
            Array.IndexOf(weapon.attachmentSlots, attachment.attachmentSlot) >= 0 && Array.IndexOf(attachment.compatibleFamilies, weapon.family) >= 0;
        public ActionResult TryInstall(string weaponId, string attachmentEntryId)
        {
            var next = Copy(Data);
            var target = next.entries.Find(e => e.instanceId == weaponId);
            var source = next.entries.Find(e => e.instanceId == attachmentEntryId);
            if (target?.weapon == null || source == null) return new ActionResult(Failure.Invalid);
            var def = Catalog.Weapon(target.definitionId); var attachment = Catalog.Find(source.definitionId);
            if (!Compatible(def, attachment)) return new ActionResult(Failure.Incompatible);
            string previous = target.weapon.installedAttachments.Find(id => Catalog.Find(id).attachmentSlot == attachment.attachmentSlot);
            source.count--; if (source.count == 0) next.entries.Remove(source);
            if (previous != null)
            {
                target.weapon.installedAttachments.Remove(previous);
                if (!Add(next, new ItemEntry { definitionId = previous })) return new ActionResult(Failure.Full);
            }
            target.weapon.installedAttachments.Add(attachment.id);
            return CommitModification(next, target);
        }
        public ActionResult TryUninstall(string weaponId, string attachmentId)
        {
            var next = Copy(Data); var target = next.entries.Find(e => e.instanceId == weaponId);
            if (target?.weapon == null || !target.weapon.installedAttachments.Remove(attachmentId)) return new ActionResult(Failure.Invalid);
            if (!Add(next, new ItemEntry { definitionId = attachmentId })) return new ActionResult(Failure.Full);
            return CommitModification(next, target);
        }
        ActionResult CommitModification(InventoryData next, ItemEntry target)
        {
            var def = Catalog.Weapon(target.definitionId); var stats = WeaponStats.Calculate(def, target.weapon, Catalog);
            int surplus = Mathf.Max(0, target.weapon.ammoInMagazine - stats.magazineSize);
            if (surplus > 0 && !Add(next, new ItemEntry { definitionId = def.fire.ammoId, count = surplus })) return new ActionResult(Failure.Full);
            target.weapon.ammoInMagazine -= surplus;
            target.weapon.durability = Mathf.Min(target.weapon.durability, stats.maxDurability);
            Data = next; NotifyChanged(); return ActionResult.Ok;
        }
        public ActionResult TryRepair(string weaponId, bool nearWorkbench)
        {
            if (!nearWorkbench) return new ActionResult(Failure.TooFar);
            var target = Find(weaponId)?.weapon; if (target == null) return new ActionResult(Failure.Invalid);
            var def = Catalog.Weapon(target.definitionId); var stats = WeaponStats.Calculate(def, target, Catalog);
            int cost = Mathf.CeilToInt(Mathf.Max(0, stats.maxDurability - target.durability) * def.repairCostPerPoint);
            if (Count(def.repairResourceId) < cost) return new ActionResult(Failure.NoResources);
            Consume(Data, def.repairResourceId, cost); target.durability = stats.maxDurability;
            NotifyChanged(); return ActionResult.Ok;
        }
    }
}
