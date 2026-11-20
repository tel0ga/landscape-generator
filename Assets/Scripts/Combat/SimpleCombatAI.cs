using UnityEngine;
namespace SurvivalCombat
{
    // Optional AI command source: uses exactly the same weapon and damage rules as the player.
    public class SimpleCombatAI : MonoBehaviour
    {
        public CombatActor target; CombatActor actor;
        void Awake() { actor = GetComponent<CombatActor>(); }
        void Update()
        {
            if (actor == null || actor.Weapons == null || target == null || actor.Blocked || target.Health.Dead || Time.timeScale <= 0) return;
            actor.Aim.SetAim(target.transform.position);
            var current = actor.Equipment.Current;
            if (current == null) return;
            var definition = actor.Catalog.Weapon(current.definitionId);
            if (WorldProjection.Distance(transform.position, target.transform.position) > definition.range || CombatPhysics.WallBetween(transform.position, target.transform.position)) return;
            if (definition.attackKind != AttackKind.Melee && current.ammoInMagazine == 0) actor.Weapons.TryStartReload();
            else actor.Weapons.TryAttack(actor.Aim.Context);
        }
    }
}
