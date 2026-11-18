using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SurvivalCombat;
using UnityEngine;

static class DataChecks
{
    static int passed;
    static void Check(bool value, string label) { if (!value) throw new Exception("FAILED: " + label); passed++; Console.WriteLine("PASS " + label); }
    static void Main(string[] args)
    {
        var catalog = CombatDefaults.CreateCatalog();
        Check(catalog.items.Count == 17 && catalog.items.Select(i => i.id).Distinct().Count() == 17, "Catalog IDs unique; all 8 weapons and 9 supplies present");
        var a = catalog.CreateWeapon("weapon.pistol", Rarity.Rare, 42); var b = catalog.CreateWeapon("weapon.pistol", Rarity.Rare, 42);
        a.ammoInMagazine--; a.durability--;
        Check(a.instanceId != b.instanceId && b.ammoInMagazine == 12, "Independent weapon state");
        Check(a.rolledModifiers.Count == 2 && a.rolledModifiers[0].stat != a.rolledModifiers[1].stat && a.rolledModifiers[0].percent == b.rolledModifiers[0].percent, "Stable nonduplicate rarity modifiers");
        var inv = new Inventory(catalog); inv.TryAdd(new ItemEntry { definitionId = "ammo.light", count = 98 });
        while(inv.Data.entries.Count < 24) inv.TryAdd(ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.knife")));
        Check(inv.TryAdd(new ItemEntry { definitionId = "ammo.light", count = 2 }).Reason == Failure.Full && inv.Count("ammo.light") == 98, "Full pickup rolls back partial stack fill");
        inv = new Inventory(catalog); var weapon = catalog.CreateWeapon("weapon.pistol"); inv.TryAdd(ItemEntry.FromWeapon(weapon));
        var mod = new ItemEntry { definitionId = "attachment.magazine" }; inv.TryAdd(mod);
        Check(inv.TryInstall(weapon.instanceId, mod.instanceId), "Install compatible magazine");
        inv.Find(weapon.instanceId).weapon.ammoInMagazine = 20;
        while(inv.Data.entries.Count < 23) inv.TryAdd(ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.knife")));
        Check(inv.TryUninstall(weapon.instanceId, "attachment.magazine").Reason == Failure.Full && inv.Find(weapon.instanceId).weapon.ammoInMagazine == 20, "Overflow ammo and attachment removal are atomic");
        inv.Remove(inv.Data.entries.Last().instanceId);
        Check(inv.TryUninstall(weapon.instanceId, "attachment.magazine") && inv.Find(weapon.instanceId).weapon.ammoInMagazine == 12 && inv.Count("ammo.light") == 8, "Removing magazine conserves ammunition");
        inv = new Inventory(catalog); weapon = catalog.CreateWeapon("weapon.pistol"); weapon.durability = 50; weapon.ammoInMagazine = 3;
        inv.TryAdd(ItemEntry.FromWeapon(weapon)); inv.TryAdd(new ItemEntry { definitionId = "resource.scrap", count = 10 });
        Check(inv.TryRepair(weapon.instanceId, false).Reason == Failure.TooFar && inv.Count("resource.scrap") == 10, "Repair requires workbench");
        Check(inv.TryRepair(weapon.instanceId, true) && inv.Count("resource.scrap") == 0 && inv.Find(weapon.instanceId).weapon.durability == 100 && inv.Find(weapon.instanceId).weapon.ammoInMagazine == 3, "Repair conserves ammunition and charges exact resources");
        inv.TryAdd(new ItemEntry { definitionId = "ammo.light", count = 50 });
        Check(inv.TransferAmmo(inv.Find(weapon.instanceId).weapon, int.MaxValue) == 9 && inv.Count("ammo.light") == 41, "Reload conserves ammunition");
        var copy = Inventory.Copy(inv.Data); inv.Restore(copy); Check(inv.Find(weapon.instanceId).weapon.ammoInMagazine == 12, "Inventory JSON roundtrip preserves weapon identity and ammo");
        copy.entries.Add(Inventory.Copy(copy.entries[0])); Check(!inv.Validate(copy), "Reject duplicate item IDs");
        var knife = catalog.CreateWeapon("weapon.knife"); inv.TryAdd(ItemEntry.FromWeapon(knife));
        var sight = new ItemEntry { definitionId = "attachment.sight" }; inv.TryAdd(sight);
        Check(inv.TryInstall(knife.instanceId, sight.instanceId).Reason == Failure.Incompatible && inv.Find(sight.instanceId) != null, "Reject incompatible modification without losing item");
        Check(!inv.TryConsume("ammo.light", 100) && inv.Count("ammo.light") == 41, "Insufficient resources do not partially consume stacks");
        File.WriteAllText(args[0], JsonSerializer.Serialize(catalog.items.Select(item => JsonSerializer.SerializeToElement(item, item.GetType(), JsonUtility.Options)), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("DATA_CHECKS_PASSED=" + passed);
    }
}
