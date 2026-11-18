using System;
using UnityEngine;

namespace SurvivalCombat
{
    public class EquipmentController : MonoBehaviour
    {
        CombatActor actor;
        public string[] Slots { get; private set; } = new string[2];
        public int ActiveSlot { get; private set; }
        public string ThrowableId { get; private set; }
        public WeaponInstance Current => actor?.Inventory.Find(Slots[ActiveSlot])?.weapon;
        public event Action Changed;
        public void Initialize(CombatActor value) { actor = value; actor.Inventory.Changed += OnInventoryChanged; }
        void OnDestroy() { if (actor != null && actor.Inventory != null) actor.Inventory.Changed -= OnInventoryChanged; }
        void OnInventoryChanged()
        {
            for (int i = 0; i < 2; i++) if (actor.Inventory.Find(Slots[i]) == null) Slots[i] = null;
            if (actor.Inventory.Find(ThrowableId) == null) ThrowableId = null;
            Changed?.Invoke();
        }
        public ActionResult TryEquip(int slot)
        {
            if (slot < 0 || slot > 1) return new ActionResult(Failure.Invalid);
            var result = actor.Weapons.TrySwitch(); if (!result) return result;
            ActiveSlot = slot; Changed?.Invoke(); return ActionResult.Ok;
        }
        public ActionResult TryAssign(int slot, string instanceId)
        {
            if (slot < 0 || slot > 1 || actor.Inventory.Find(instanceId)?.weapon == null) return new ActionResult(Failure.Invalid);
            if (!actor.Weapons.CanManageEquipment) return new ActionResult(Failure.Busy);
            actor.Weapons.CancelReload();
            int other = 1 - slot;
            if (Slots[other] == instanceId) Slots[other] = Slots[slot];
            Slots[slot] = instanceId; Changed?.Invoke(); return ActionResult.Ok;
        }
        public ActionResult TrySelectThrowable(string instanceId)
        {
            var item = actor.Inventory.Find(instanceId);
            if (item == null || actor.Catalog.Find(item.definitionId).kind != ItemKind.Throwable) return new ActionResult(Failure.Invalid);
            ThrowableId = instanceId; Changed?.Invoke(); return ActionResult.Ok;
        }
        public void Restore(string[] slots, int active, string throwable)
        {
            Slots = slots != null && slots.Length == 2 ? (string[])slots.Clone() : new string[2];
            ActiveSlot = Mathf.Clamp(active, 0, 1); ThrowableId = throwable; OnInventoryChanged();
        }
    }
}
