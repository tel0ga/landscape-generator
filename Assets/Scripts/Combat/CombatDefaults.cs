using UnityEngine;

namespace SurvivalCombat
{
    public static class CombatDefaults
    {
        public static CombatCatalog CreateCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<CombatCatalog>(); catalog.name = "DefaultCatalog";
            ItemDefinition Item(string id, string label, ItemKind kind, Color tint, int stack = 99)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>(); item.name = id; item.id = id;
                item.displayName = label; item.kind = kind; item.tint = tint; item.stackLimit = stack; catalog.items.Add(item); return item;
            }
            Item("ammo.light", "Лёгкие патроны", ItemKind.Ammo, Color.yellow);
            Item("ammo.shell", "Картечь", ItemKind.Ammo, new Color(1, .5f, .2f));
            Item("ammo.arrow", "Стрелы", ItemKind.Ammo, Color.green);
            Item("resource.scrap", "Металлолом", ItemKind.Resource, Color.gray);
            var grenade = Item("throw.grenade", "Граната", ItemKind.Throwable, Color.green, 10);
            grenade.throwable.damage = 65;
            var molotov = Item("throw.molotov", "Зажигательная бутылка", ItemKind.Throwable, new Color(1, .3f, .1f), 10);
            molotov.throwable.incendiary = true; molotov.throwable.fuse = .65f; molotov.throwable.damage = 35;
            var sight = Item("attachment.sight", "Коллиматор", ItemKind.Attachment, Color.cyan, 1);
            sight.attachmentSlot = "sight"; sight.compatibleFamilies = new[] { "gun", "bow" };
            sight.modifiers = new[] { new StatModifier { stat = Stat.Spread, percent = -.35f } };
            var magazine = Item("attachment.magazine", "Большой магазин", ItemKind.Attachment, Color.cyan, 1);
            magazine.attachmentSlot = "magazine"; magazine.compatibleFamilies = new[] { "gun" };
            magazine.modifiers = new[] { new StatModifier { stat = Stat.MagazineSize, flat = 8 }, new StatModifier { stat = Stat.ReloadTime, percent = .15f } };
            var muzzle = Item("attachment.muzzle", "Компенсатор", ItemKind.Attachment, Color.cyan, 1);
            muzzle.attachmentSlot = "muzzle"; muzzle.compatibleFamilies = new[] { "gun" };
            muzzle.modifiers = new[] { new StatModifier { stat = Stat.Spread, percent = -.2f } };
            WeaponDefinition Weapon(string id, string label, AttackKind kind, float damage, float range, float rate, Color tint)
            {
                var def = ScriptableObject.CreateInstance<WeaponDefinition>(); def.name = id; def.id = id; def.displayName = label;
                def.attackKind = kind; def.damage = damage; def.range = range; def.attacksPerMinute = rate; def.tint = tint;
                catalog.items.Add(def); return def;
            }
            Weapon("weapon.pistol", "Пистолет", AttackKind.Hitscan, 20, 18, 240, new Color(.75f, .8f, .9f));
            var rifle = Weapon("weapon.rifle", "Автомат", AttackKind.Hitscan, 12, 22, 600, new Color(.6f, .7f, .4f));
            rifle.fire.automatic = true; rifle.fire.magazineSize = 30; rifle.fire.spread = 7; rifle.fire.reloadTime = 2;
            var shotgun = Weapon("weapon.shotgun", "Дробовик", AttackKind.Hitscan, 8, 10, 75, new Color(.7f, .4f, .2f));
            shotgun.fire.ammoId = "ammo.shell"; shotgun.fire.magazineSize = 6; shotgun.fire.pellets = 7;
            shotgun.fire.spread = 22; shotgun.fire.reloadOneAtATime = true; shotgun.fire.reloadTime = .65f;
            var knife = Weapon("weapon.knife", "Нож", AttackKind.Melee, 24, 1.5f, 150, Color.white);
            knife.family = "melee"; knife.attachmentSlots = new string[0]; knife.melee.staminaCost = 8;
            knife.effect = new EffectSpec { kind = EffectKind.Bleeding, duration = 3, damage = 2 };
            var axe = Weapon("weapon.axe", "Топор", AttackKind.Melee, 45, 2.1f, 60, new Color(.9f, .5f, .3f));
            axe.family = "melee"; axe.attachmentSlots = new string[0]; axe.melee.angle = 130; axe.melee.windup = .3f; axe.melee.recovery = .5f; axe.melee.staminaCost = 22;
            var spear = Weapon("weapon.spear", "Копьё", AttackKind.Melee, 30, 3, 90, Color.gray);
            spear.family = "melee"; spear.attachmentSlots = new string[0]; spear.melee.angle = 25; spear.melee.staminaCost = 14;
            var bow = Weapon("weapon.bow", "Лук", AttackKind.Projectile, 32, 20, 100, new Color(.7f, .5f, .25f));
            bow.family = "bow"; bow.fire.ammoId = "ammo.arrow"; bow.fire.magazineSize = 1; bow.fire.reloadTime = .5f; bow.attachmentSlots = new[] { "sight" };
            var crossbow = Weapon("weapon.crossbow", "Арбалет", AttackKind.Projectile, 55, 25, 60, new Color(.45f, .3f, .2f));
            crossbow.family = "bow"; crossbow.fire.ammoId = "ammo.arrow"; crossbow.fire.magazineSize = 1; crossbow.fire.reloadTime = 1.5f; crossbow.projectile.speed = 35; crossbow.attachmentSlots = new[] { "sight" };
            return catalog;
        }
    }
}
