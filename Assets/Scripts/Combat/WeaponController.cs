using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    public class WeaponController : MonoBehaviour
    {
        CombatActor actor;
        public WeaponState State { get; private set; } = WeaponState.Ready;
        public float ReloadProgress => State == WeaponState.Reloading ? Mathf.Clamp01((clock - phaseStart) / Mathf.Max(.01f, phaseEnd - phaseStart)) : 0;
        public bool CanManageEquipment => State != WeaponState.Attacking && State != WeaponState.Recovery && State != WeaponState.Switching && actor != null && !actor.Health.Dead && !actor.InFlight;
        public event Action Changed;
        public event Action<WeaponDefinition> Fired;
        float clock, nextAttack, phaseStart, phaseEnd, meleeEnd, recoveryEnd, nextThrow;
        string reloadWeapon;
        AttackContext meleeContext;
        DamageInfo meleeDamage;
        WeaponDefinition meleeDefinition;
        readonly HashSet<Health> hitThisSwing = new HashSet<Health>();
        public void Initialize(CombatActor value) { actor = value; actor.Health.Died += DisableCombat; clock = Time.time; }
        void OnDestroy() { if (actor != null && actor.Health != null) actor.Health.Died -= DisableCombat; }
        void Update() { if (actor != null && Time.timeScale > 0) Tick(Time.time); }
        public void Tick(float now)
        {
            clock = now;
            if (actor.Health.Dead || actor.InFlight) { if (State != WeaponState.Disabled) DisableCombat(); return; }
            if (State == WeaponState.Disabled) SetState(WeaponState.Ready);
            if (State == WeaponState.Reloading && clock >= phaseEnd)
            {
                var current = actor.Equipment.Current;
                if (current == null || current.instanceId != reloadWeapon) { CancelReload(); return; }
                var def = actor.Catalog.Weapon(current.definitionId);
                actor.Inventory.TransferAmmo(current, def.fire.reloadOneAtATime ? 1 : int.MaxValue);
                var stats = WeaponStats.Calculate(def, current, actor.Catalog);
                if (def.fire.reloadOneAtATime && current.ammoInMagazine < stats.magazineSize && actor.Inventory.Count(def.fire.ammoId) > 0)
                { phaseStart = clock; phaseEnd = clock + stats.reloadTime; Changed?.Invoke(); }
                else { reloadWeapon = null; SetState(WeaponState.Ready); }
            }
            if (State == WeaponState.Attacking && clock >= phaseStart)
            {
                // A stalled frame still evaluates the active window once before recovery.
                CombatPhysics.Melee(actor, meleeContext, meleeDefinition, meleeDamage, hitThisSwing);
                if (clock >= meleeEnd) SetState(WeaponState.Recovery);
            }
            if ((State == WeaponState.Recovery && clock >= recoveryEnd) || (State == WeaponState.Switching && clock >= phaseEnd)) SetState(WeaponState.Ready);
        }
        void SetState(WeaponState state) { State = state; Changed?.Invoke(); }
        public void DisableCombat() { reloadWeapon = null; hitThisSwing.Clear(); SetState(WeaponState.Disabled); }
        public void ResetTransient() { nextAttack = nextThrow = 0; reloadWeapon = null; hitThisSwing.Clear(); SetState(WeaponState.Ready); }
        ActionResult Gate()
        {
            if (actor == null || actor.Blocked || Time.timeScale <= 0) return new ActionResult(Failure.Disabled);
            if (State != WeaponState.Ready) return new ActionResult(Failure.Busy);
            if (clock < nextAttack) return new ActionResult(Failure.Cooldown);
            return ActionResult.Ok;
        }
        public ActionResult TryAttack(AttackContext context)
        {
            var gate = Gate(); if (!gate) return gate;
            var instance = actor.Equipment.Current;
            if (instance == null) return new ActionResult(Failure.NoWeapon);
            var def = actor.Catalog.Weapon(instance.definitionId);
            if (instance.durability <= 0) return new ActionResult(Failure.Broken);
            if (def.attackKind != AttackKind.Melee && instance.ammoInMagazine <= 0) return new ActionResult(Failure.Empty);
            if (def.attackKind == AttackKind.Melee && !actor.Stamina.TryConsumeStamina(def.melee.staminaCost)) return new ActionResult(Failure.Exhausted);
            var stats = WeaponStats.Calculate(def, instance, actor.Catalog);
            if (def.attackKind != AttackKind.Melee) instance.ammoInMagazine--;
            instance.durability = Mathf.Max(0, instance.durability - Mathf.Max(0, def.durabilityCost));
            nextAttack = clock + 60f / Mathf.Max(1, def.attacksPerMinute);
            var damage = new DamageInfo { attackId = Guid.NewGuid().ToString("N"), owner = actor.Health, damage = stats.damage,
                type = def.damageType, criticalChance = def.criticalChance, criticalMultiplier = def.criticalMultiplier,
                knockback = def.knockback, direction = context.mapDirection, effect = def.effect.Copy() };
            if (def.attackKind == AttackKind.Melee)
            {
                meleeContext = context; meleeDamage = damage; meleeDefinition = def; hitThisSwing.Clear();
                phaseStart = clock + Mathf.Max(0, def.melee.windup);
                meleeEnd = phaseStart + Mathf.Max(.01f, def.melee.activeTime);
                recoveryEnd = meleeEnd + Mathf.Max(0, def.melee.recovery);
                SetState(WeaponState.Attacking);
            }
            else if (def.attackKind == AttackKind.Hitscan) CombatPhysics.Hitscan(actor, context, def, stats, damage);
            else CombatProjectile.Spawn(actor.Pool, actor, context, def, damage);
            actor.Inventory.NotifyChanged(); Fired?.Invoke(def); return ActionResult.Ok;
        }
        public ActionResult TryStartReload()
        {
            if (actor == null || actor.Blocked || Time.timeScale <= 0) return new ActionResult(Failure.Disabled);
            if (State != WeaponState.Ready) return new ActionResult(Failure.Busy);
            var current = actor.Equipment.Current; if (current == null) return new ActionResult(Failure.NoWeapon);
            var def = actor.Catalog.Weapon(current.definitionId); var stats = WeaponStats.Calculate(def, current, actor.Catalog);
            if (def.attackKind == AttackKind.Melee || current.ammoInMagazine >= stats.magazineSize) return new ActionResult(Failure.Invalid);
            if (actor.Inventory.Count(def.fire.ammoId) == 0) return new ActionResult(Failure.Empty);
            reloadWeapon = current.instanceId; phaseStart = clock; phaseEnd = clock + stats.reloadTime;
            SetState(WeaponState.Reloading); actor.View?.PlayReload(def); return ActionResult.Ok;
        }
        public void CancelReload() { if (State == WeaponState.Reloading) { reloadWeapon = null; SetState(WeaponState.Ready); } }
        public ActionResult TrySwitch()
        {
            if (!CanManageEquipment || actor.InventoryOpen || Time.timeScale <= 0) return new ActionResult(Failure.Busy);
            CancelReload(); phaseEnd = clock + .18f; SetState(WeaponState.Switching); return ActionResult.Ok;
        }
        public ActionResult TryThrow(AttackContext context)
        {
            var gate = Gate(); if (!gate) return gate;
            if (clock < nextThrow) return new ActionResult(Failure.Cooldown);
            var entry = actor.Inventory.Find(actor.Equipment.ThrowableId);
            if (entry == null) return new ActionResult(Failure.Empty);
            var def = actor.Catalog.Find(entry.definitionId);
            if (def.kind != ItemKind.Throwable) return new ActionResult(Failure.Invalid);
            CombatGrenade.Spawn(actor.Pool, actor, context, def.throwable);
            entry.count--; if (entry.count == 0) actor.Inventory.Remove(entry.instanceId); else actor.Inventory.NotifyChanged();
            nextThrow = nextAttack = clock + .5f; return ActionResult.Ok;
        }
    }
}
