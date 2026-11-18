using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SurvivalCombat
{
    public class CombatHUD : MonoBehaviour
    {
        CombatActor actor; Canvas canvas; RectTransform canvasRect; Font font;
        Text status, ammo, slots, condition, prompt, notification, crosshair, detail, effectText;
        Image healthBar, staminaBar, reloadBar, damageFlash, weaponIcon;
        GameObject inventoryPanel; readonly List<Text> cells = new List<Text>();
        readonly List<Image> cellBackgrounds = new List<Image>();
        string selectedId; float notificationUntil, hitUntil, flashUntil; bool critical;
        readonly List<Number> numbers = new List<Number>();
        class Number { public Text label; public Vector2 position; public float began; }
        public void Initialize(CombatActor value)
        {
            actor = value; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Combat HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); root.transform.SetParent(transform);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            canvasRect = root.GetComponent<RectTransform>();
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null) { var events = new GameObject("Combat EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(transform); }
            Panel(canvasRect, "Status background", 22, 22, 440, 238, new Color(.025f, .045f, .07f, .93f));
            status = Label(canvasRect, "Status", 40, 32, 400, 32, 22);
            healthBar = Bar(canvasRect, "Health", 40, 70, 390, new Color(.8f, .22f, .26f));
            staminaBar = Bar(canvasRect, "Stamina", 40, 91, 390, new Color(.3f, .8f, .55f));
            ammo = Label(canvasRect, "Ammo", 90, 119, 345, 34, 25);
            weaponIcon = Panel(canvasRect, "Weapon icon", 40, 122, 35, 20, Color.white).GetComponent<Image>();
            slots = Label(canvasRect, "Slots", 40, 158, 400, 34, 17);
            condition = Label(canvasRect, "Condition", 40, 190, 400, 30, 17);
            reloadBar = Bar(canvasRect, "Reload", 40, 229, 390, new Color(.4f, .7f, 1));
            effectText = Label(canvasRect, "Effects", 40, 270, 700, 32, 20);
            Label(canvasRect, "Controls", 500, 30, 1370, 30, 18).text = "ЛКМ атака  ·  R перезарядка  ·  1/2 оружие  ·  G бросок  ·  E подобрать  ·  Tab инвентарь  ·  F5/F9 сохранить/загрузить";
            prompt = Label(canvasRect, "Pickup prompt", 700, 950, 900, 40, 24);
            notification = Label(canvasRect, "Message", 650, 110, 1100, 45, 25);
            crosshair = Label(canvasRect, "Crosshair", 0, 0, 100, 60, 24); crosshair.alignment = TextAnchor.MiddleCenter;
            damageFlash = Panel(canvasRect, "Damage flash", 0, 0, 1920, 1080, Color.clear).GetComponent<Image>();
            damageFlash.rectTransform.anchorMin = Vector2.zero; damageFlash.rectTransform.anchorMax = Vector2.one; damageFlash.rectTransform.offsetMin = damageFlash.rectTransform.offsetMax = Vector2.zero;
            BuildInventory();
            actor.Inventory.Changed += Refresh; actor.Equipment.Changed += Refresh; actor.Health.Changed += Refresh;
            actor.Stamina.Changed += Refresh; actor.Weapons.Changed += Refresh; actor.Message += ShowFailure;
            actor.InventoryVisibilityChanged += ToggleInventory; actor.GetComponent<StatusEffectController>().Changed += Refresh;
            actor.World.Save.Completed += ShowMessage; Health.AnyDamage += OnDamage;
            Refresh(); ToggleInventory();
        }
        RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        GameObject Panel(Transform parent, string name, float x, float y, float w, float h, Color colour)
        {
            var rect = Rect(parent, name, x, y, w, h); var image = rect.gameObject.AddComponent<Image>(); image.color = colour; image.raycastTarget = false; return rect.gameObject;
        }
        Text Label(Transform parent, string name, float x, float y, float w, float h, int size)
        {
            var rect = Rect(parent, name, x, y, w, h); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = new Color(.9f, .94f, 1); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; return text;
        }
        Image Bar(Transform parent, string name, float x, float y, float w, Color colour)
        {
            Panel(parent, name + " background", x, y, w, 14, new Color(.12f, .16f, .2f));
            var image = Panel(parent, name, x, y, w, 14, colour).GetComponent<Image>();
            image.sprite = CombatArt.Square; image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; return image;
        }
        Button Button(Transform parent, string title, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var go = Panel(parent, title, x, y, w, h, new Color(.13f, .2f, .28f)); var image = go.GetComponent<Image>(); image.raycastTarget = true;
            var button = go.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var text = Label(go.transform, "Label", 8, 4, w - 16, h - 8, 18); text.text = title; text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        void BuildInventory()
        {
            inventoryPanel = Panel(canvasRect, "Inventory", 310, 200, 1300, 720, new Color(.025f, .04f, .065f, .98f));
            inventoryPanel.GetComponent<Image>().raycastTarget = true;
            var parent = inventoryPanel.transform;
            Label(parent, "Title", 28, 18, 1100, 42, 30).text = "СНАРЯЖЕНИЕ  /  ИНВЕНТАРЬ";
            Button(parent, "Закрыть", 1140, 18, 135, 40, () => actor.SetInventoryOpen(false));
            for (int i = 0; i < Inventory.Capacity; i++)
            {
                int index = i;
                var button = Button(parent, "—", 28 + (i % 4) * 167, 83 + (i / 4) * 85, 155, 73, () =>
                {
                    selectedId = index < actor.Inventory.Data.entries.Count ? actor.Inventory.Data.entries[index].instanceId : null; Refresh();
                });
                cells.Add(button.GetComponentInChildren<Text>()); cellBackgrounds.Add(button.GetComponent<Image>());
            }
            detail = Label(parent, "Details", 725, 85, 540, 340, 21);
            Button(parent, "В слот 1", 725, 440, 165, 45, () => Assign(0));
            Button(parent, "В слот 2", 900, 440, 165, 45, () => Assign(1));
            Button(parent, "Выбросить", 1075, 440, 190, 45, () => actor.Report(actor.TryDrop(selectedId)));
            Button(parent, "Установить мод", 725, 495, 260, 45, Install);
            Button(parent, "Снять последний мод", 995, 495, 270, 45, Uninstall);
            Button(parent, "Ремонт у верстака", 725, 550, 260, 45, () => actor.Report(actor.TryRepair(selectedId)));
            Button(parent, "Выбрать метательное", 995, 550, 270, 45, () => actor.Report(actor.Equipment.TrySelectThrowable(selectedId)));
            Button(parent, "Сохранить  F5", 28, 625, 210, 45, () => actor.Report(actor.World.Save.TrySave()));
            Button(parent, "Загрузить  F9", 250, 625, 210, 45, () => actor.Report(actor.World.Save.TryLoad()));
            Label(parent, "Hint", 725, 620, 540, 80, 18).text = "Модификация устанавливается на активное оружие. Ремонт требует металлолом и верстак поблизости. Игра приостановлена.";
        }
        void Assign(int slot) { actor.Report(actor.Equipment.TryAssign(slot, selectedId)); }
        void Install()
        {
            var current = actor.Equipment.Current;
            actor.Report(current == null ? new ActionResult(Failure.NoWeapon) : actor.TryInstall(current.instanceId, selectedId));
        }
        void Uninstall()
        {
            var current = actor.Inventory.Find(selectedId)?.weapon;
            if (current == null || current.installedAttachments.Count == 0) { ShowFailure(Failure.Invalid); return; }
            actor.Report(actor.TryUninstall(current.instanceId, current.installedAttachments[current.installedAttachments.Count - 1]));
        }
        void ToggleInventory() { inventoryPanel.SetActive(actor.InventoryOpen); Refresh(); }
        public void Refresh()
        {
            if (actor == null || status == null) return;
            status.text = actor.Health.Dead ? "ПОГИБ · F9 загрузить сохранение" : $"Здоровье {actor.Health.Current:0}/{actor.Health.maxHealth:0}   Выносливость {actor.Stamina.CurrentStamina:0}";
            healthBar.fillAmount = actor.Health.Current / Mathf.Max(1, actor.Health.maxHealth); staminaBar.fillAmount = actor.Stamina.StaminaPercent;
            var current = actor.Equipment.Current; var def = current == null ? null : actor.Catalog.Weapon(current.definitionId);
            ammo.text = def == null ? "Нет оружия" : def.attackKind == AttackKind.Melee ? def.displayName : $"{def.displayName}  {current.ammoInMagazine} / {actor.Inventory.Count(def.fire.ammoId)}";
            weaponIcon.color = def != null ? def.tint : Color.clear; weaponIcon.sprite = def != null && def.icon != null ? def.icon : CombatArt.Square;
            string Name(int slot) { var item = actor.Inventory.Find(actor.Equipment.Slots[slot]); return item == null ? "—" : actor.Catalog.Find(item.definitionId).displayName; }
            slots.text = $"[1] {Name(0)}    [2] {Name(1)}    Активен: {actor.Equipment.ActiveSlot + 1}";
            var throwable = actor.Inventory.Find(actor.Equipment.ThrowableId);
            condition.text = (current == null ? "" : current.durability <= 0 ? "СЛОМАНО" : $"Прочность {current.durability:0}") + "   G: " + (throwable == null ? "—" : actor.Catalog.Find(throwable.definitionId).displayName + " ×" + throwable.count);
            effectText.text = actor.GetComponent<StatusEffectController>().Description;
            for (int i = 0; i < cells.Count; i++)
            {
                var item = i < actor.Inventory.Data.entries.Count ? actor.Inventory.Data.entries[i] : null;
                cells[i].text = item == null ? "—" : actor.Catalog.Find(item.definitionId).displayName + (item.weapon != null ? "\n" + item.weapon.rarity : " ×" + item.count);
                cellBackgrounds[i].color = item != null && item.instanceId == selectedId ? new Color(.2f, .4f, .55f) : new Color(.09f, .14f, .2f);
            }
            var selected = actor.Inventory.Find(selectedId);
            if (selected == null) { detail.text = "Выбери предмет для сравнения и действий."; return; }
            var selectedDef = actor.Catalog.Find(selected.definitionId); var text = new StringBuilder(selectedDef.displayName + "\n" + selectedDef.description + "\n");
            if (selected.weapon != null)
            {
                var wd = (WeaponDefinition)selectedDef; var stats = WeaponStats.Calculate(wd, selected.weapon, actor.Catalog);
                text.AppendLine($"{selected.weapon.rarity} · {wd.attackKind}\nУрон: {stats.damage:0.0} · Дальность: {wd.range:0.0}\nРазброс: {stats.spread:0.0}° · Перезарядка: {stats.reloadTime:0.00} с\nПрочность: {selected.weapon.durability:0}/{stats.maxDurability:0}\nМагазин: {selected.weapon.ammoInMagazine}/{stats.magazineSize}");
                if (current != null) { var comparison = WeaponStats.Calculate(def, current, actor.Catalog); text.AppendLine($"К активному: урон {stats.damage - comparison.damage:+0.0;-0.0;0}, магазин {stats.magazineSize - comparison.magazineSize:+0;-0;0}"); }
                text.AppendLine("Модификации: " + (selected.weapon.installedAttachments.Count == 0 ? "нет" : string.Join(", ", selected.weapon.installedAttachments.ConvertAll(id => actor.Catalog.Find(id).displayName))));
                text.AppendLine("Ремонт: " + Mathf.CeilToInt((stats.maxDurability - selected.weapon.durability) * wd.repairCostPerPoint) + " металлолома");
            }
            else if (selectedDef.kind == ItemKind.Attachment) text.AppendLine("Слот: " + selectedDef.attachmentSlot + "\nДля: " + string.Join(", ", selectedDef.compatibleFamilies));
            else text.AppendLine("Количество: " + selected.count);
            detail.text = text.ToString();
        }
        void ShowFailure(Failure failure)
        {
            string message = failure == Failure.Empty ? "Нет боеприпасов" : failure == Failure.Broken ? "Оружие сломано" : failure == Failure.Exhausted ? "Не хватает выносливости" : failure == Failure.Full ? "Инвентарь заполнен" : failure == Failure.NoResources ? "Не хватает ресурсов" : failure == Failure.TooFar ? "Нужен верстак поблизости" : failure == Failure.Incompatible ? "Модификация несовместима" : failure == Failure.SaveError ? "Ошибка сохранения или загрузки" : "Действие сейчас недоступно";
            ShowMessage(message);
        }
        void ShowMessage(string message) { notification.text = message; notificationUntil = Time.unscaledTime + 2.5f; }
        void OnDamage(Health target, DamageInfo info, DamageResult result)
        {
            if (info.owner == actor.Health && target != actor.Health) { hitUntil = Time.unscaledTime + .15f; critical = result.Critical; }
            if (target == actor.Health) flashUntil = Time.unscaledTime + .18f;
            var number = numbers.Find(n => !n.label.gameObject.activeSelf);
            if (number == null) { number = new Number { label = Label(canvasRect, "Damage number", 0, 0, 100, 40, 23) }; numbers.Add(number); }
            number.position = info.point; number.began = Time.time; number.label.text = result.Amount.ToString("0") + (result.Critical ? "!" : "");
            number.label.color = result.Critical ? Color.yellow : target == actor.Health ? new Color(1, .4f, .4f) : Color.white; number.label.gameObject.SetActive(true);
        }
        void Update()
        {
            if (actor == null || actor.Aim == null) return;
            reloadBar.fillAmount = actor.Weapons.ReloadProgress;
            if (Time.unscaledTime > notificationUntil) notification.text = "";
            damageFlash.color = Time.unscaledTime < flashUntil ? new Color(1, .1f, .1f, .12f) : Color.clear;
            var camera = Camera.main;
            if (camera != null)
            {
                crosshair.gameObject.SetActive(!actor.Blocked);
                Place(crosshair.rectTransform, camera.WorldToScreenPoint(actor.Aim.Target));
                var current = actor.Equipment.Current; float spread = current == null ? 0 : WeaponStats.Calculate(actor.Catalog.Weapon(current.definitionId), current, actor.Catalog).spread;
                crosshair.text = Time.unscaledTime < hitUntil ? "×" : "+"; crosshair.fontSize = Mathf.RoundToInt(22 + spread);
                crosshair.color = Time.unscaledTime < hitUntil && critical ? Color.yellow : Color.white;
                foreach (var number in numbers)
                {
                    if (!number.label.gameObject.activeSelf) continue;
                    float age = Time.time - number.began;
                    if (age > .7f) number.label.gameObject.SetActive(false);
                    else Place(number.label.rectTransform, camera.WorldToScreenPoint(number.position + Vector2.up * age));
                }
            }
            var nearest = actor.Blocked ? null : actor.World.Nearest(actor.transform.position, 2.2f);
            prompt.text = nearest == null ? "" : "E — подобрать: " + nearest.Label;
        }
        void Place(RectTransform rect, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = local;
        }
        void OnDestroy()
        {
            Health.AnyDamage -= OnDamage;
            if (actor == null) return;
            actor.Inventory.Changed -= Refresh; actor.Equipment.Changed -= Refresh; actor.Health.Changed -= Refresh;
            actor.Stamina.Changed -= Refresh; actor.Weapons.Changed -= Refresh; actor.Message -= ShowFailure;
            actor.InventoryVisibilityChanged -= ToggleInventory;
            if (actor.World != null) actor.World.Save.Completed -= ShowMessage;
            var effects = actor.GetComponent<StatusEffectController>(); if (effects != null) effects.Changed -= Refresh;
        }
    }
}
