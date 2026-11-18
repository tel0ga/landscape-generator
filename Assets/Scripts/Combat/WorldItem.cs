using UnityEngine;
namespace SurvivalCombat
{
    public class WorldItem : MonoBehaviour
    {
        public string RecordId { get; private set; }
        public string Label { get; private set; }
        public void Bind(string id, string label) { RecordId = id; Label = label; }
        void OnDisable() { RecordId = null; Label = null; }
    }
}
