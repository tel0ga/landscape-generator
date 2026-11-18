using UnityEngine;

namespace SurvivalCombat
{
    [CreateAssetMenu(menuName = "Survival/Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string id; public string displayName; [TextArea] public string description;
        public ItemKind kind; public Sprite icon; public GameObject worldPrefab;
        [Min(1)] public int stackLimit = 99;
        public ThrowProfile throwable = new ThrowProfile();
        public string attachmentSlot;
        public string[] compatibleFamilies = new string[0];
        public StatModifier[] modifiers = new StatModifier[0];
        public Color tint = Color.white;
    }
}
