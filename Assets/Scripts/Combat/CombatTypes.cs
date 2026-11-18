using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    public enum ItemKind { Weapon, Ammo, Resource, Throwable, Attachment }
    public enum AttackKind { Hitscan, Melee, Projectile }
    public enum DamageType { Physical, Fire, Poison }
    public enum Rarity { Common, Uncommon, Rare }
    public enum WeaponState { Ready, Attacking, Recovery, Reloading, Switching, Disabled }
    public enum Stat { Damage, Spread, ReloadTime, MaxDurability, MagazineSize }
    public enum EffectKind { None, Burning, Bleeding, Slow }
    public enum Failure { None, Disabled, Busy, Cooldown, NoWeapon, Empty, Broken, Exhausted, Full, Invalid, TooFar, NoResources, Incompatible, SaveError }

    public readonly struct ActionResult
    {
        public readonly Failure Reason;
        public bool Success => Reason == Failure.None;
        public ActionResult(Failure reason) { Reason = reason; }
        public static ActionResult Ok => new ActionResult(Failure.None);
        public static implicit operator bool(ActionResult result) => result.Success;
    }

    [Serializable] public class StatModifier { public Stat stat; public float flat; public float percent; }
    [Serializable] public class EffectSpec
    {
        public EffectKind kind; public float duration = 3; public float interval = 1;
        public float damage = 3; [Range(0, 1)] public float slow = .3f;
        public EffectSpec Copy() => (EffectSpec)MemberwiseClone();
    }
    [Serializable] public class FireProfile
    {
        public string ammoId = "ammo.light"; public int magazineSize = 12;
        public bool automatic; public int pellets = 1; public float spread = 3;
        public float reloadTime = 1.4f; public bool reloadOneAtATime;
    }
    [Serializable] public class MeleeProfile
    {
        public float angle = 90; public float windup = .15f;
        public float activeTime = .12f; public float recovery = .3f; public float staminaCost = 12;
    }
    [Serializable] public class ProjectileProfile
    {
        public GameObject prefab; public float speed = 22; public float lifetime = 4; public float radius = .08f;
    }
    [Serializable] public class ThrowProfile
    {
        public bool incendiary; public float range = 7; public float travelTime = .65f; public float fuse = 1.5f;
        public float damage = 55; public float radius = 3; public float fireDuration = 5; public float fireInterval = .7f;
    }
    [Serializable] public class WeaponInstance
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string definitionId;
        public int ammoInMagazine;
        public float durability;
        public Rarity rarity;
        public List<StatModifier> rolledModifiers = new List<StatModifier>();
        public List<string> installedAttachments = new List<string>();
    }
    [Serializable] public class ItemEntry : ISerializationCallbackReceiver
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string definitionId; public int count = 1; public WeaponInstance weapon;
        // Unity's inline serializer materializes null custom classes as empty objects.
        // A stack must remain a stack after cloning or loading JSON.
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            if (weapon != null && string.IsNullOrEmpty(weapon.definitionId)) weapon = null;
        }
        public static ItemEntry FromWeapon(WeaponInstance weapon) => new ItemEntry
        { instanceId = weapon.instanceId, definitionId = weapon.definitionId, count = 1, weapon = weapon };
    }
    [Serializable] public class InventoryData { public List<ItemEntry> entries = new List<ItemEntry>(); }
    public struct WeaponStats
    {
        public float damage, spread, reloadTime, maxDurability;
        public int magazineSize;
        public static WeaponStats Calculate(WeaponDefinition definition, WeaponInstance instance, CombatCatalog catalog)
        {
            var all = new List<StatModifier>(instance.rolledModifiers);
            foreach (string id in instance.installedAttachments)
            {
                var attachment = catalog.Find(id);
                if (attachment != null) all.AddRange(attachment.modifiers);
            }
            return new WeaponStats
            {
                damage = Mathf.Max(0, Apply(definition.damage, Stat.Damage, all)),
                spread = Mathf.Clamp(Apply(definition.fire.spread, Stat.Spread, all), 0, 180),
                reloadTime = Mathf.Max(.05f, Apply(definition.fire.reloadTime, Stat.ReloadTime, all)),
                maxDurability = Mathf.Max(1, Apply(definition.maxDurability, Stat.MaxDurability, all)),
                magazineSize = Mathf.Max(1, Mathf.RoundToInt(Apply(definition.fire.magazineSize, Stat.MagazineSize, all)))
            };
        }
        static float Apply(float value, Stat stat, List<StatModifier> modifiers)
        {
            float flat = 0, percent = 0;
            foreach (var modifier in modifiers) if (modifier.stat == stat) { flat += modifier.flat; percent += modifier.percent; }
            return (value + flat) * (1 + percent);
        }
    }
    public struct AttackContext
    {
        public Vector2 origin, direction, target;
        public AttackContext(Vector2 origin, Vector2 direction, Vector2 target)
        { this.origin = origin; this.direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right; this.target = target; }
    }
    public struct DamageInfo
    {
        public string attackId; public Health owner; public float damage; public DamageType type;
        public Vector2 point, direction; public float criticalChance, criticalMultiplier, knockback;
        public bool allowSelf; public EffectSpec effect;
    }
    public readonly struct DamageResult
    {
        public readonly float Amount; public readonly bool Critical, Killed;
        public DamageResult(float amount, bool critical, bool killed) { Amount = amount; Critical = critical; Killed = killed; }
    }
    public interface IDamageable { DamageResult ReceiveDamage(DamageInfo info); }
}
