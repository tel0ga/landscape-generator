using UnityEngine;
namespace SurvivalCombat
{
    public class Workbench : MonoBehaviour
    {
        public static bool Near(Vector2 position)
        {
            foreach (var bench in FindObjectsByType<Workbench>())
                if (Vector2.Distance(position, bench.transform.position) <= 2.5f && !CombatPhysics.WallBetween(position, bench.transform.position)) return true;
            return false;
        }
    }
}
