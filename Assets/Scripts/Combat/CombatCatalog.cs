using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    [CreateAssetMenu(menuName = "Survival/Catalog")]
    public class CombatCatalog : ScriptableObject
    {
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public ItemDefinition Find(string id) => items.Find(item => item != null && item.id == id);
        public WeaponDefinition Weapon(string id) => Find(id) as WeaponDefinition;
        public WeaponInstance CreateWeapon(string id, Rarity rarity = Rarity.Common, int seed = 0)
        {
            var def = Weapon(id);
            if (def == null) throw new ArgumentException("Unknown weapon: " + id);
            var item = new WeaponInstance { definitionId = id, rarity = rarity };
            var random = new System.Random(seed);
            var choices = new List<Stat>(def.allowedRolls);
            for (int i = 0; i < (int)rarity && choices.Count > 0; i++)
            {
                int index = random.Next(choices.Count); var stat = choices[index]; choices.RemoveAt(index);
                float bonus = .08f + (float)random.NextDouble() * .17f;
                item.rolledModifiers.Add(new StatModifier { stat = stat, percent = stat == Stat.Spread || stat == Stat.ReloadTime ? -bonus : bonus });
            }
            var stats = WeaponStats.Calculate(def, item, this);
            item.durability = stats.maxDurability;
            item.ammoInMagazine = def.attackKind == AttackKind.Melee ? 0 : stats.magazineSize;
            return item;
        }
    }
}
