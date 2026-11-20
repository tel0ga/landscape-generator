using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SurvivalCombat
{
    public class CombatInput : MonoBehaviour
    {
        CombatActor actor; InputActionMap actions;
        InputAction aim, stick, attack, reload, slot1, slot2, next, previous, grenade, interact, inventory, save, load;
        public void Initialize(CombatActor value)
        {
            if (actor == value && actions != null) return;
            actions?.Dispose();
            actor = value;
            // This asset is separate from the generated movement wrapper; no generated C# is hand edited.
            var asset = Resources.Load<InputActionAsset>("Combat/CombatActions");
            if (asset != null) actions = InputActionMap.FromJson(asset.ToJson())[0];
            else
            {
                actions = new InputActionMap("Combat");
                actions.AddAction("AimPosition", InputActionType.PassThrough, "<Pointer>/position");
                actions.AddAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick");
                actions.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton").AddBinding("<Gamepad>/rightTrigger");
                actions.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
                actions.AddAction("Slot1", InputActionType.Button, "<Keyboard>/1"); actions.AddAction("Slot2", InputActionType.Button, "<Keyboard>/2");
                actions.AddAction("Next", InputActionType.Button, "<Gamepad>/rightShoulder"); actions.AddAction("Previous", InputActionType.Button, "<Gamepad>/leftShoulder");
                actions.AddAction("Throw", InputActionType.Button, "<Keyboard>/g"); actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
                actions.AddAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
                actions.AddAction("Save", InputActionType.Button, "<Keyboard>/f5"); actions.AddAction("Load", InputActionType.Button, "<Keyboard>/f9");
            }
            aim = actions.FindAction("AimPosition", true); stick = actions.FindAction("AimStick", true); attack = actions.FindAction("Attack", true);
            reload = actions.FindAction("Reload", true); slot1 = actions.FindAction("Slot1", true); slot2 = actions.FindAction("Slot2", true);
            next = actions.FindAction("Next", true); previous = actions.FindAction("Previous", true); grenade = actions.FindAction("Throw", true);
            interact = actions.FindAction("Interact", true); inventory = actions.FindAction("Inventory", true);
            save = actions.FindAction("Save", true); load = actions.FindAction("Load", true); actions.Enable();
        }
        void OnEnable() { actions?.Enable(); }
        void OnDisable() { actions?.Disable(); }
        void OnDestroy() { actions?.Dispose(); }
        void Update()
        {
            if (actor == null) return;
            if (inventory.WasPressedThisFrame()) actor.SetInventoryOpen(!actor.InventoryOpen);
            if (save.WasPressedThisFrame()) actor.Report(actor.World.Save.TrySave());
            if (load.WasPressedThisFrame()) actor.Report(actor.World.Save.TryLoad());
            if (actor.Blocked || Time.timeScale <= 0) return;
            Vector2 gamepad = stick.ReadValue<Vector2>();
            if (gamepad.sqrMagnitude > .04f) actor.Aim.SetAim((Vector2)transform.position + WorldProjection.ToWorld(gamepad.normalized * 10));
            else actor.Aim.AimAtScreen(aim.ReadValue<Vector2>());
            if (slot1.WasPressedThisFrame()) actor.Report(actor.Equipment.TryEquip(0));
            if (slot2.WasPressedThisFrame()) actor.Report(actor.Equipment.TryEquip(1));
            if (next.WasPressedThisFrame() || previous.WasPressedThisFrame()) actor.Report(actor.Equipment.TryEquip(1 - actor.Equipment.ActiveSlot));
            if (reload.WasPressedThisFrame()) actor.Report(actor.Weapons.TryStartReload());
            if (grenade.WasPressedThisFrame()) actor.Report(actor.Weapons.TryThrow(actor.Aim.Context));
            if (interact.WasPressedThisFrame())
            {
                var item = actor.World.Nearest(transform.position, 2.2f);
                if (item != null) actor.Report(actor.TryPickup(item));
            }
            var current = actor.Equipment.Current; var def = current == null ? null : actor.Catalog.Weapon(current.definitionId);
            bool automatic = def != null && def.attackKind != AttackKind.Melee && def.fire.automatic;
            if ((automatic ? attack.IsPressed() : attack.WasPressedThisFrame()) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                actor.Report(actor.Weapons.TryAttack(actor.Aim.Context));
        }
    }
}
