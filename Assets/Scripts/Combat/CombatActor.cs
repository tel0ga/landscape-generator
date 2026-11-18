using System;
using UnityEngine;

namespace SurvivalCombat
{
    [DisallowMultipleComponent]
    public class CombatActor : MonoBehaviour
    {
        public CombatCatalog Catalog { get; private set; }
        public Inventory Inventory { get; private set; }
        public Health Health { get; private set; }
        public StaminaSystem Stamina { get; private set; }
        public WeaponController Weapons { get; private set; }
        public EquipmentController Equipment { get; private set; }
        public WeaponView View { get; private set; }
        public AimController Aim { get; private set; }
        public CombatPool Pool { get; private set; }
        public CombatWorld World { get; private set; }
        public FlyingMode Flight;
        public bool InventoryOpen { get; private set; }
        public bool InFlight => Flight != null && Flight.enabled && Flight.isFlying;
        public bool Blocked => InventoryOpen || Health == null || Health.Dead || InFlight;
        public event Action InventoryVisibilityChanged;
        public event Action<Failure> Message;
        float previousTimeScale = 1;
        public void Initialize(CombatCatalog catalog, CombatPool pool, CombatWorld world, bool starterItems = true)
        {
            if (Inventory != null) return;
            Catalog = catalog; Pool = pool; World = world; Inventory = new Inventory(catalog);
            Health = GetComponent<Health>() ?? gameObject.AddComponent<Health>();
            Stamina = GetComponent<StaminaSystem>() ?? gameObject.AddComponent<StaminaSystem>();
            if (GetComponent<StatusEffectController>() == null) gameObject.AddComponent<StatusEffectController>();
            Equipment = GetComponent<EquipmentController>() ?? gameObject.AddComponent<EquipmentController>();
            Weapons = GetComponent<WeaponController>() ?? gameObject.AddComponent<WeaponController>(); Equipment.Initialize(this); Weapons.Initialize(this);
            Aim = GetComponent<AimController>() ?? gameObject.AddComponent<AimController>(); Aim.Initialize(this);
            View = GetComponent<WeaponView>() ?? gameObject.AddComponent<WeaponView>(); View.Initialize(this);
            if (starterItems)
            {
                foreach (var id in new[] { "weapon.pistol", "weapon.knife" }) AddStarter(ItemEntry.FromWeapon(catalog.CreateWeapon(id)));
                AddStarter(new ItemEntry { definitionId = "ammo.light", count = 90 });
                AddStarter(new ItemEntry { definitionId = "resource.scrap", count = 40 });
                AddStarter(new ItemEntry { definitionId = "throw.grenade", count = 3 });
                Equipment.Restore(new[] { Inventory.Data.entries[0].instanceId, Inventory.Data.entries[1].instanceId }, 0,
                    Inventory.Data.entries.Find(e => e.definitionId == "throw.grenade").instanceId);
            }
        }
        void AddStarter(ItemEntry entry)
        {
            var result = Inventory.TryAdd(entry);
            if (!result) throw new InvalidOperationException("Cannot add starter item " + entry.definitionId + ": " + result.Reason);
        }
        public void Report(ActionResult result)
        { if (!result && result.Reason != Failure.Cooldown && result.Reason != Failure.Busy) Message?.Invoke(result.Reason); }
        public void Tell(Failure reason) => Message?.Invoke(reason);
        public void SetInventoryOpen(bool open)
        {
            if (open == InventoryOpen) return;
            InventoryOpen = open;
            if (open) { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            else Time.timeScale = previousTimeScale;
            InventoryVisibilityChanged?.Invoke();
        }
        void OnDisable() { if (InventoryOpen) SetInventoryOpen(false); Weapons?.CancelReload(); }
        public ActionResult TryPickup(WorldItem item)
        {
            if (Blocked || item == null || World == null) return new ActionResult(Failure.Disabled);
            if (Vector2.Distance(transform.position, item.transform.position) > 2.2f) return new ActionResult(Failure.TooFar);
            if (CombatPhysics.WallBetween(transform.position, item.transform.position)) return new ActionResult(Failure.TooFar);
            return World.TryPickup(this, item.RecordId);
        }
        public ActionResult TryDrop(string id)
        {
            if (!Weapons.CanManageEquipment || World == null) return new ActionResult(Failure.Busy);
            var entry = Inventory.Find(id); if (entry == null) return new ActionResult(Failure.Invalid);
            Vector2 position = (Vector2)transform.position + Aim.Direction * .9f;
            if (CombatPhysics.WallBetween(transform.position, position)) position = transform.position;
            if (!World.AddItem(entry, position)) return new ActionResult(Failure.Invalid);
            Weapons.CancelReload(); Inventory.Remove(id); return ActionResult.Ok;
        }
        public ActionResult TryRepair(string id) => Weapons.CanManageEquipment ? Inventory.TryRepair(id, Workbench.Near(transform.position)) : new ActionResult(Failure.Busy);
        public ActionResult TryInstall(string weaponId, string attachmentId)
        {
            if (!Weapons.CanManageEquipment) return new ActionResult(Failure.Busy);
            Weapons.CancelReload(); return Inventory.TryInstall(weaponId, attachmentId);
        }
        public ActionResult TryUninstall(string weaponId, string attachmentId)
        {
            if (!Weapons.CanManageEquipment) return new ActionResult(Failure.Busy);
            Weapons.CancelReload(); return Inventory.TryUninstall(weaponId, attachmentId);
        }
    }
}
