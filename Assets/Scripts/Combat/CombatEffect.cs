using UnityEngine;

namespace SurvivalCombat
{
    public class CombatEffect : MonoBehaviour
    {
        CombatPool pool; float expires; SpriteRenderer sprite; Color colour;
        public static void Line(CombatPool pool, Vector2 from, Vector2 to, Color colour, float lifetime)
        {
            if (pool == null) return;
            var go = pool.Acquire("effect.line", () => { var created = CombatArt.Shape("Tracer", Vector2.zero, Vector2.one, colour); created.AddComponent<CombatEffect>(); return created; });
            var fx = go.GetComponent<CombatEffect>(); fx.pool = pool; fx.expires = Time.time + lifetime;
            fx.sprite = go.GetComponent<SpriteRenderer>(); fx.colour = colour; fx.sprite.color = colour;
            Vector2 delta = to - from; go.transform.position = (from + to) * .5f;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(Mathf.Max(.05f, delta.magnitude), .06f, 1); go.SetActive(true);
        }
        void Update() { if (Time.time >= expires) pool.Release(gameObject); }
        void OnDisable() { pool = null; expires = 0; }
    }
}
