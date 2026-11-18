#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SurvivalCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CombatTests
{
    CombatCatalog catalog;
    [Test] public void ManuallyAddedCombatComponentsAreReusedAndDoNotBlockMovement()
    {
        var go = new GameObject("Manually configured player");
        try
        {
            var actor = go.AddComponent<CombatActor>();
            var health = go.AddComponent<Health>(); health.ResetHealth();
            var aim = go.AddComponent<AimController>();
            var view = go.AddComponent<WeaponView>();
            var equipment = go.AddComponent<EquipmentController>();
            var weapons = go.AddComponent<WeaponController>();
            var audio = go.AddComponent<AudioSource>();
            var pool = go.AddComponent<CombatPool>();
            actor.Initialize(catalog, pool, null);
            Assert.AreSame(health, actor.Health);
            Assert.AreSame(aim, actor.Aim);
            Assert.AreSame(view, actor.View);
            Assert.AreSame(equipment, actor.Equipment);
            Assert.AreSame(weapons, actor.Weapons);
            Assert.IsFalse(go.GetComponent<CombatActor>().Blocked);
            var inventory = actor.Inventory;
            actor.Initialize(catalog, pool, null);
            Assert.AreSame(inventory, actor.Inventory, "Repeated setup must not reset the inventory.");
            Assert.AreEqual(1, go.GetComponents<CombatActor>().Length);
            Assert.AreEqual(1, go.GetComponents<AimController>().Length);
            Assert.AreEqual(1, go.GetComponents<WeaponView>().Length);
            Assert.AreEqual(1, go.GetComponents<AudioSource>().Length);
            actor.Health.Restore(0);
            Assert.IsTrue(actor.Blocked, "Death must still block movement.");
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void UnityJsonPreservesStacksAndStarterLoadout()
    {
        var inventory = new Inventory(catalog);
        foreach (var id in new[] { "weapon.pistol", "weapon.knife" })
            Assert.IsTrue(inventory.TryAdd(ItemEntry.FromWeapon(catalog.CreateWeapon(id))));
        foreach (var id in new[] { "ammo.light", "resource.scrap", "throw.grenade" })
            Assert.IsTrue(inventory.TryAdd(new ItemEntry { definitionId = id, count = 3 }), id);
        var restored = Inventory.Copy(inventory.Data);
        Assert.IsTrue(inventory.Validate(restored));
        Assert.AreEqual(5, restored.entries.Count);
        foreach (var entry in restored.entries)
            if (catalog.Find(entry.definitionId).kind != ItemKind.Weapon) Assert.IsNull(entry.weapon);
        var go = new GameObject("Starter actor");
        try
        {
            var actor = go.AddComponent<CombatActor>();
            Assert.DoesNotThrow(() => actor.Initialize(catalog, go.AddComponent<CombatPool>(), null));
            Assert.IsNotNull(actor.Equipment.Current);
            Assert.AreEqual(3, actor.Inventory.Count("throw.grenade"));
        }
        finally { Object.DestroyImmediate(go); }
    }
    [SetUp] public void Setup() { catalog = CombatDefaults.CreateCatalog(); Time.timeScale = 1; }
    [TearDown] public void Cleanup()
    {
        if (catalog != null) foreach (var item in catalog.items) Object.DestroyImmediate(item);
        Object.DestroyImmediate(catalog); Time.timeScale = 1;
    }
    [Test] public void InstancesAreIndependentAndRollsAreStable()
    {
        var a = catalog.CreateWeapon("weapon.pistol", Rarity.Rare, 42); var b = catalog.CreateWeapon("weapon.pistol", Rarity.Rare, 42);
        a.ammoInMagazine--; a.durability--;
        Assert.AreNotEqual(a.instanceId, b.instanceId); Assert.AreEqual(12, b.ammoInMagazine); Assert.AreEqual(2, a.rolledModifiers.Count);
        Assert.AreNotEqual(a.rolledModifiers[0].stat, a.rolledModifiers[1].stat);
        Assert.AreEqual(a.rolledModifiers[0].percent, b.rolledModifiers[0].percent);
    }
    [Test] public void FullInventoryRejectsPickupWithoutPartialStackChanges()
    {
        var inventory = new Inventory(catalog);
        inventory.TryAdd(new ItemEntry { definitionId = "ammo.light", count = 98 });
        for (int i = 1; i < 24; i++) Assert.IsTrue(inventory.TryAdd(ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.knife"))));
        var incoming = new ItemEntry { definitionId = "ammo.light", count = 2 };
        Assert.AreEqual(Failure.Full, inventory.TryAdd(incoming).Reason); Assert.AreEqual(98, inventory.Count("ammo.light")); Assert.AreEqual(2, incoming.count);
    }
    [Test] public void MagazineRemovalIsAtomicWhenOverflowAmmoCannotFit()
    {
        var inventory = new Inventory(catalog); var weapon = catalog.CreateWeapon("weapon.pistol"); inventory.TryAdd(ItemEntry.FromWeapon(weapon));
        var mod = new ItemEntry { definitionId = "attachment.magazine" }; inventory.TryAdd(mod);
        Assert.IsTrue(inventory.TryInstall(weapon.instanceId, mod.instanceId));
        inventory.Find(weapon.instanceId).weapon.ammoInMagazine = 20;
        while (inventory.Data.entries.Count < 23) inventory.TryAdd(ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.knife")));
        Assert.AreEqual(Failure.Full, inventory.TryUninstall(weapon.instanceId, "attachment.magazine").Reason);
        Assert.AreEqual(20, inventory.Find(weapon.instanceId).weapon.ammoInMagazine);
        Assert.Contains("attachment.magazine", inventory.Find(weapon.instanceId).weapon.installedAttachments);
    }
    [Test] public void RepairConsumesExactCostAndPreservesAmmo()
    {
        var inventory = new Inventory(catalog); var weapon = catalog.CreateWeapon("weapon.pistol"); weapon.durability = 50; weapon.ammoInMagazine = 3;
        inventory.TryAdd(ItemEntry.FromWeapon(weapon)); inventory.TryAdd(new ItemEntry { definitionId = "resource.scrap", count = 10 });
        Assert.AreEqual(Failure.TooFar, inventory.TryRepair(weapon.instanceId, false).Reason);
        Assert.IsTrue(inventory.TryRepair(weapon.instanceId, true)); Assert.AreEqual(0, inventory.Count("resource.scrap"));
        Assert.AreEqual(100, inventory.Find(weapon.instanceId).weapon.durability); Assert.AreEqual(3, inventory.Find(weapon.instanceId).weapon.ammoInMagazine);
    }
    [TestCase(100, 100, .5f, false, 25)]
    [TestCase(100, 0, 0, true, 150)]
    [TestCase(100, -100, 2, false, 10)]
    public void DamageFormula(float raw, float armour, float resistance, bool critical, float expected)
    { Assert.AreEqual(expected, DamageResolver.Calculate(new DamageInfo { damage = raw, criticalMultiplier = 1.5f }, armour, resistance, critical), .001f); }
    [Test] public void DeathIsEmittedOnceAndDamageIsClampedToHealth()
    {
        var go = new GameObject("Target");
        try
        {
            var health = go.AddComponent<Health>(); health.ResetHealth(); int deaths = 0; health.Died += () => deaths++;
            var hit = new DamageInfo { damage = 200 };
            Assert.AreEqual(100, health.ReceiveDamage(hit).Amount); Assert.AreEqual(0, health.ReceiveDamage(hit).Amount); Assert.AreEqual(1, deaths);
        }
        finally { Object.DestroyImmediate(go); }
    }
    CombatActor Actor(GameObject go, CombatWorld world = null)
    {
        var pool = go.AddComponent<CombatPool>(); var actor = go.AddComponent<CombatActor>();
        actor.Initialize(catalog, pool, world); actor.Health.ResetHealth(); actor.Stamina.RestoreStamina(100); actor.Weapons.enabled = false; actor.Weapons.Tick(0); return actor;
    }
    [Test] public void ReloadCancelAndSwitchDoNotDuplicateAmmoOrResetCooldown()
    {
        var go = new GameObject("Actor");
        try
        {
            var actor = Actor(go); var gun = actor.Equipment.Current; gun.ammoInMagazine = 0;
            Assert.IsTrue(actor.Weapons.TryStartReload()); actor.Weapons.CancelReload(); actor.Weapons.Tick(10);
            Assert.AreEqual(0, gun.ammoInMagazine); Assert.AreEqual(90, actor.Inventory.Count("ammo.light"));
            Assert.IsTrue(actor.Weapons.TryStartReload()); actor.Weapons.Tick(12);
            Assert.AreEqual(12, gun.ammoInMagazine); Assert.AreEqual(78, actor.Inventory.Count("ammo.light"));
            var definition = catalog.Weapon(gun.definitionId); definition.attacksPerMinute = 30;
            Assert.IsTrue(actor.Weapons.TryAttack(new AttackContext(Vector2.zero, Vector2.right, Vector2.right * 10)));
            Assert.IsTrue(actor.Equipment.TryEquip(1)); actor.Weapons.Tick(12.2f); Assert.IsTrue(actor.Equipment.TryEquip(0)); actor.Weapons.Tick(12.4f);
            Assert.AreEqual(Failure.Cooldown, actor.Weapons.TryAttack(new AttackContext()).Reason);
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void SingleShellReloadKeepsCompletedShellWhenCancelled()
    {
        var go = new GameObject("Actor");
        try
        {
            var actor = Actor(go); var shotgun = catalog.CreateWeapon("weapon.shotgun"); shotgun.ammoInMagazine = 0;
            actor.Inventory.TryAdd(ItemEntry.FromWeapon(shotgun)); actor.Inventory.TryAdd(new ItemEntry { definitionId = "ammo.shell", count = 10 });
            actor.Equipment.TryAssign(0, shotgun.instanceId); actor.Weapons.TryStartReload(); actor.Weapons.Tick(.7f); actor.Weapons.CancelReload();
            Assert.AreEqual(1, actor.Equipment.Current.ammoInMagazine); Assert.AreEqual(9, actor.Inventory.Count("ammo.shell"));
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void EmptyBrokenAndExhaustedAttacksSpendNothing()
    {
        var go = new GameObject("Actor");
        try
        {
            var actor = Actor(go); var gun = actor.Equipment.Current; gun.ammoInMagazine = 0;
            Assert.AreEqual(Failure.Empty, actor.Weapons.TryAttack(new AttackContext()).Reason); Assert.AreEqual(100, gun.durability);
            gun.ammoInMagazine = 2; gun.durability = 0;
            Assert.AreEqual(Failure.Broken, actor.Weapons.TryAttack(new AttackContext()).Reason); Assert.AreEqual(2, gun.ammoInMagazine);
            actor.Equipment.TryEquip(1); actor.Weapons.Tick(1); actor.Stamina.RestoreStamina(0);
            Assert.AreEqual(Failure.Exhausted, actor.Weapons.TryAttack(new AttackContext()).Reason); Assert.AreEqual(100, actor.Equipment.Current.durability);
            actor.SetInventoryOpen(true); Assert.AreEqual(Failure.Disabled, actor.Weapons.TryAttack(new AttackContext()).Reason); actor.SetInventoryOpen(false);
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void MultipleCollidersDoNotMultiplyMeleeOrExplosionDamageAndWallsBlock()
    {
        var source = new GameObject("Actor"); var target = new GameObject("Target"); var wall = new GameObject("Wall");
        try
        {
            var actor = Actor(source); target.transform.position = Vector2.right;
            var health = target.AddComponent<Health>(); health.ResetHealth(); target.AddComponent<CircleCollider2D>(); target.AddComponent<BoxCollider2D>();
            wall.transform.position = new Vector2(.5f, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.05f, 3); Physics2D.SyncTransforms();
            var hits = new HashSet<Health>(); var info = new DamageInfo { owner = actor.Health, damage = 10 };
            CombatPhysics.Melee(actor, new AttackContext(Vector2.zero, Vector2.right, Vector2.right), catalog.Weapon("weapon.knife"), info, hits);
            Assert.AreEqual(100, health.Current);
            wall.SetActive(false); Physics2D.SyncTransforms();
            CombatPhysics.Melee(actor, new AttackContext(Vector2.zero, Vector2.right, Vector2.right), catalog.Weapon("weapon.knife"), info, hits);
            CombatPhysics.Melee(actor, new AttackContext(Vector2.zero, Vector2.right, Vector2.right), catalog.Weapon("weapon.knife"), info, hits);
            Assert.AreEqual(90, health.Current);
            CombatPhysics.Radial(Vector2.zero, 3, info, false); Assert.AreEqual(80, health.Current);
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(target); Object.DestroyImmediate(wall); }
    }
    [Test] public void WorldPickupUnloadAndSaveRoundTripPreserveIdentity()
    {
        var root = new GameObject("World"); var go = new GameObject("Player"); string path = System.IO.Path.Combine(Application.temporaryCachePath, "combat-test-" + Guid.NewGuid() + ".json");
        try
        {
            var world = root.AddComponent<CombatWorld>(); world.Initialize(catalog, root.AddComponent<CombatPool>(), "test");
            var actor = Actor(go, world); world.Player = actor;
            var item = ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.rifle", Rarity.Rare, 55));
            Assert.IsTrue(world.AddItem(item, Vector2.zero, "generated.1")); string recordId = world.Data.items[0].recordId;
            Assert.IsTrue(world.TryPickup(actor, recordId)); Assert.IsFalse(world.AddItem(item, Vector2.zero, "generated.1"));
            Assert.IsTrue(actor.TryDrop(item.instanceId));
            Assert.AreEqual(item.instanceId, world.Data.items[0].item.instanceId);
            world.MarkDestroyed("tree.1");
            var save = new CombatSaveService(world, path); Assert.IsTrue(save.TrySave());
            var duplicate = save.Capture(); duplicate.world.items.Add(Inventory.Copy(duplicate.world.items[0])); Assert.IsFalse(save.Validate(duplicate));
            actor.Health.ReceiveDamage(new DamageInfo { damage = 25 }); Assert.IsTrue(save.TryLoad()); Assert.AreEqual(100, actor.Health.Current);
            Assert.IsTrue(world.IsDestroyed("tree.1")); Assert.AreEqual(item.instanceId, world.Data.items[0].item.instanceId);
            Assert.IsTrue(save.TrySave()); File.WriteAllText(path, "broken json"); Assert.IsTrue(save.TryLoad(), "Backup should recover corrupt primary.");
        }
        finally
        {
            Object.DestroyImmediate(go); Object.DestroyImmediate(root);
            foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
    [UnityTest] public IEnumerator FastProjectileStopsAtThinWallAndPoolResetsState()
    {
        yield return new EnterPlayMode();
        // SetUp objects are not retained across a domain reload.
        var localCatalog = CombatDefaults.CreateCatalog(); var go = new GameObject("Actor"); var target = new GameObject("Target"); var wall = new GameObject("Thin wall");
        try
        {
            var actor = go.AddComponent<CombatActor>(); var pool = go.AddComponent<CombatPool>(); actor.Initialize(localCatalog, pool, null);
            var definition = localCatalog.Weapon("weapon.crossbow"); definition.projectile.speed = 500;
            target.transform.position = new Vector2(4, 0); target.AddComponent<BoxCollider2D>(); var health = target.AddComponent<Health>();
            wall.transform.position = new Vector2(2, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.01f, 3); Physics2D.SyncTransforms();
            CombatProjectile.Spawn(pool, actor, new AttackContext(Vector2.zero, Vector2.right, Vector2.right * 10), definition, new DamageInfo { owner = actor.Health, damage = 20 });
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(100, health.Current); Assert.AreEqual(0, go.GetComponentsInChildren<CombatProjectile>().Length);
            wall.SetActive(false); Physics2D.SyncTransforms();
            CombatProjectile.Spawn(pool, actor, new AttackContext(Vector2.zero, Vector2.right, Vector2.right * 10), definition, new DamageInfo { owner = actor.Health, damage = 7 });
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(93, health.Current);
        }
        finally { Object.Destroy(go); Object.Destroy(target); Object.Destroy(wall); foreach (var item in localCatalog.items) Object.Destroy(item); Object.Destroy(localCatalog); }
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif
