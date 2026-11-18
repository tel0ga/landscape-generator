using UnityEngine;

namespace SurvivalCombat
{
    [CreateAssetMenu(menuName = "Survival/Weapon")]
    public class WeaponDefinition : ItemDefinition
    {
        public AttackKind attackKind; public string family = "gun";
        public GameObject heldPrefab; public AudioClip attackSound, reloadSound, impactSound;
        public GameObject muzzleEffect, impactEffect;
        [Min(0)] public float damage = 18;
        public DamageType damageType;
        [Min(.1f)] public float range = 15;
        [Min(1)] public float attacksPerMinute = 240;
        public float criticalChance = .1f, criticalMultiplier = 1.5f, knockback = 2;
        public FireProfile fire = new FireProfile();
        public MeleeProfile melee = new MeleeProfile();
        public ProjectileProfile projectile = new ProjectileProfile();
        public EffectSpec effect = new EffectSpec();
        public float maxDurability = 100, durabilityCost = 1;
        public string repairResourceId = "resource.scrap"; public float repairCostPerPoint = .2f;
        public string[] attachmentSlots = { "sight", "magazine", "muzzle" };
        public Stat[] allowedRolls = { Stat.Damage, Stat.Spread, Stat.ReloadTime, Stat.MaxDurability };
        void OnEnable() { kind = ItemKind.Weapon; stackLimit = 1; }
    }
}
