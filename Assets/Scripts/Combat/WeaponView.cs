using UnityEngine;

namespace SurvivalCombat
{
    public class WeaponView : MonoBehaviour
    {
        CombatActor actor; Transform pivot, visual; SpriteRenderer weaponRenderer; GameObject custom; string shownId;
        AudioSource audioSource; float recoil; AudioClip fallbackShot;
        public Transform Muzzle { get; private set; }
        public void Initialize(CombatActor value)
        {
            actor = value; pivot = new GameObject("WeaponPivot").transform; pivot.SetParent(transform, false);
            var go = CombatArt.Shape("WeaponVisual", Vector2.zero, new Vector2(.8f, .16f), Color.white, false, 30);
            visual = go.transform; visual.SetParent(pivot, false); visual.localPosition = new Vector3(.4f, 0, 0); weaponRenderer = go.GetComponent<SpriteRenderer>();
            Muzzle = new GameObject("Muzzle").transform; Muzzle.SetParent(pivot, false); Muzzle.localPosition = new Vector3(.9f, 0, 0);
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            fallbackShot = AudioClip.Create("Prototype shot", 1600, 1, 16000, false);
            var samples = new float[1600]; var random = new System.Random(42);
            for (int i = 0; i < samples.Length; i++) samples[i] = ((float)random.NextDouble() * 2 - 1) * Mathf.Exp(-i / 180f) * .15f;
            fallbackShot.SetData(samples, 0); actor.Weapons.Fired += Fired;
        }
        void Fired(WeaponDefinition definition)
        {
            recoil = .16f; audioSource.PlayOneShot(definition.attackSound != null ? definition.attackSound : fallbackShot);
            if (definition.attackKind == AttackKind.Melee)
                CombatEffect.Line(actor.Pool, transform.position, (Vector2)transform.position + WorldProjection.ToWorld(WorldProjection.ToMap(actor.Aim.Direction).normalized * definition.range), new Color(.8f, .9f, 1), .15f);
            else CombatEffect.Line(actor.Pool, Muzzle.position, (Vector2)Muzzle.position + actor.Aim.Direction * .2f, Color.yellow, .05f);
        }
        public void PlayReload(WeaponDefinition definition) { if (definition.reloadSound != null) audioSource.PlayOneShot(definition.reloadSound); }
        void LateUpdate()
        {
            if (actor == null) return;
            var instance = actor.Equipment.Current; var def = instance == null ? null : actor.Catalog.Weapon(instance.definitionId);
            pivot.gameObject.SetActive(def != null && !actor.Health.Dead && !actor.InFlight);
            if (def == null) return;
            if (shownId != def.id)
            {
                shownId = def.id; if (custom != null) Destroy(custom);
                custom = def.heldPrefab != null ? Instantiate(def.heldPrefab, pivot) : null;
                weaponRenderer.enabled = custom == null; weaponRenderer.color = def.tint;
            }
            float angle = Mathf.Atan2(actor.Aim.Direction.y, actor.Aim.Direction.x) * Mathf.Rad2Deg;
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 2);
            pivot.localRotation = Quaternion.Euler(0, 0, angle); pivot.localPosition = new Vector3(-actor.Aim.Direction.x * recoil, -actor.Aim.Direction.y * recoil, 0);
            var body = actor.GetComponentInChildren<SpriteRenderer>();
            int order = body != null && body != weaponRenderer ? body.sortingOrder : 20;
            int weaponOrder = order + (actor.Aim.Direction.y > .1f ? -1 : 1);
            foreach (var sr in pivot.GetComponentsInChildren<SpriteRenderer>()) { sr.sortingOrder = weaponOrder; sr.flipY = actor.Aim.Direction.x < 0; }
        }
        void OnDestroy() { if (actor != null && actor.Weapons != null) actor.Weapons.Fired -= Fired; if (fallbackShot != null) Destroy(fallbackShot); }
    }
}
