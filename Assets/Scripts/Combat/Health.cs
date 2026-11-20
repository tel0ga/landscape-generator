using System;
using UnityEngine;

namespace SurvivalCombat
{
    public static class DamageResolver
    {
        public static float Calculate(DamageInfo info, float armour, float resistance, bool critical)
        {
            float amount = Mathf.Max(0, info.damage) * (critical ? Mathf.Max(1, info.criticalMultiplier) : 1);
            if (info.type == DamageType.Physical) amount *= 100f / (100f + Mathf.Max(0, armour));
            return amount * (1 - Mathf.Clamp(resistance, 0, .9f));
        }
    }
    public class Health : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100, armour;
        [Range(0, .9f)] public float physicalResistance, fireResistance, poisonResistance;
        public float Current { get; private set; }
        public bool Dead => Current <= 0;
        public event Action Changed;
        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action Died;
        public static event Action<Health, DamageInfo, DamageResult> AnyDamage;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() { AnyDamage = null; }
        void Awake() { ResetHealth(); }
        public void ResetHealth() { Restore(Mathf.Max(1, maxHealth)); }
        public void Restore(float value)
        {
            Current = Mathf.Clamp(value, 0, Mathf.Max(1, maxHealth));
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = !Dead;
            Changed?.Invoke();
        }
        public DamageResult ReceiveDamage(DamageInfo info)
        {
            if (Dead || (!info.allowSelf && info.owner == this)) return default;
            bool critical = UnityEngine.Random.value < Mathf.Clamp01(info.criticalChance);
            float resistance = info.type == DamageType.Fire ? fireResistance : info.type == DamageType.Poison ? poisonResistance : physicalResistance;
            float amount = Mathf.Min(Current, DamageResolver.Calculate(info, armour, resistance, critical));
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return default;
            Current = Mathf.Max(0, Current - amount);
            var result = new DamageResult(amount, critical, Dead);
            if (info.knockback > 0)
            {
                var movement = GetComponent<PlayerMovement>();
                if (movement != null) movement.AddKnockback(info.direction.normalized * info.knockback);
                else if (TryGetComponent<Rigidbody2D>(out var body)) body.AddForce(WorldProjection.ToWorld(info.direction.normalized * info.knockback), ForceMode2D.Impulse);
            }
            if (!Dead && info.effect != null && info.effect.kind != EffectKind.None)
                (GetComponent<StatusEffectController>() ?? gameObject.AddComponent<StatusEffectController>()).Apply(info.effect, info.owner);
            Changed?.Invoke(); Damaged?.Invoke(info, result); AnyDamage?.Invoke(this, info, result);
            if (Dead)
            {
                foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                if (TryGetComponent<Rigidbody2D>(out var body)) body.linearVelocity = Vector2.zero;
                Died?.Invoke();
            }
            return result;
        }
    }
}
