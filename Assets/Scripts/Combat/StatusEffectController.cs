using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalCombat
{
    public class StatusEffectController : MonoBehaviour
    {
        class Active { public EffectSpec spec; public Health source; public float end, next; }
        readonly Dictionary<EffectKind, Active> effects = new Dictionary<EffectKind, Active>();
        readonly List<EffectKind> removal = new List<EffectKind>();
        Health health;
        public event Action Changed;
        public float MovementMultiplier { get; private set; } = 1;
        public string Description => string.Join(", ", effects.Keys);
        void Awake() { health = GetComponent<Health>(); }
        public void Apply(EffectSpec spec, Health source)
        {
            if (spec == null || spec.kind == EffectKind.None || spec.duration <= 0) return;
            // Refresh a single instance; repeated hits do not multiply DOT tick rates.
            if (effects.TryGetValue(spec.kind, out var active))
            { active.end = Mathf.Max(active.end, Time.time + spec.duration); active.spec = spec.Copy(); active.source = source; }
            else effects.Add(spec.kind, new Active { spec = spec.Copy(), source = source, end = Time.time + spec.duration, next = Time.time + Mathf.Max(.05f, spec.interval) });
            Changed?.Invoke();
        }
        public void Clear() { effects.Clear(); MovementMultiplier = 1; Changed?.Invoke(); }
        void OnDisable() { Clear(); }
        void Update()
        {
            if (Time.timeScale <= 0) return;
            if (health == null || health.Dead) { if (effects.Count > 0) Clear(); return; }
            MovementMultiplier = 1; removal.Clear();
            foreach (var pair in new List<KeyValuePair<EffectKind, Active>>(effects))
            {
                var active = pair.Value;
                if (Time.time >= active.end) { removal.Add(pair.Key); continue; }
                if (pair.Key == EffectKind.Slow) { MovementMultiplier = Mathf.Min(MovementMultiplier, 1 - Mathf.Clamp01(active.spec.slow)); continue; }
                if (Time.time >= active.next)
                {
                    active.next = Time.time + Mathf.Max(.05f, active.spec.interval);
                    health.ReceiveDamage(new DamageInfo { attackId = Guid.NewGuid().ToString("N"), owner = active.source,
                        damage = active.spec.damage, type = pair.Key == EffectKind.Burning ? DamageType.Fire : DamageType.Physical,
                        point = transform.position, allowSelf = true });
                }
            }
            foreach (var kind in removal) effects.Remove(kind);
            if (removal.Count > 0) Changed?.Invoke();
        }
    }
}
