using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalCombat
{
    [DefaultExecutionOrder(1000)]
    public class CombatRuntime : MonoBehaviour
    {
        CombatActor player;
        bool built;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MAIN SCENE" && scene.name != "CombatArena") return;
            var existing = FindAnyObjectByType<CombatRuntime>();
            if (existing != null) { existing.Build(scene.name == "CombatArena"); return; }
            var root = new GameObject("Combat systems"); root.AddComponent<CombatRuntime>().Build(scene.name == "CombatArena");
        }
        public void Build(bool arena)
        {
            if (built) return;
            // Establish rendering/audio before initializing inventory or UI.
            var camera = Camera.main;
            if (camera == null)
            {
                camera = FindAnyObjectByType<Camera>();
                if (camera == null) camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            if (arena)
            {
                camera.orthographic = true; camera.orthographicSize = 9;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.transform.rotation = Quaternion.identity;
            }
            if (FindAnyObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            var catalog = Resources.Load<CombatCatalog>("Combat/DefaultCatalog");
            if (catalog == null) catalog = CombatDefaults.CreateCatalog();
            var pool = GetComponent<CombatPool>() ?? gameObject.AddComponent<CombatPool>();
            var world = GetComponent<CombatWorld>() ?? gameObject.AddComponent<CombatWorld>();
            var generator = FindAnyObjectByType<MapGenerator>();
            string key = SceneManager.GetActiveScene().name;
            if (generator != null) key += "|" + JsonUtility.ToJson(new WorldSettings(generator));
            world.Initialize(catalog, pool, key);
            var movement = FindAnyObjectByType<PlayerMovement>();
            if (movement == null)
            {
                var go = new GameObject("Player"); go.tag = "Player";
                var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.interpolation = RigidbodyInterpolation2D.Interpolate;
                go.AddComponent<CapsuleCollider2D>().size = new Vector2(.65f, .85f);
                var picture = CombatArt.Shape("BodyVisual", Vector2.zero, new Vector2(.65f, .85f), new Color(.25f, .75f, .95f), true, 20); picture.transform.SetParent(go.transform, false);
                movement = go.AddComponent<PlayerMovement>();
            }
            if (movement.GetComponentInChildren<Collider2D>(true) == null) movement.gameObject.AddComponent<CapsuleCollider2D>().size = new Vector2(.6f, .8f);
            player = movement.GetComponent<CombatActor>() ?? movement.gameObject.AddComponent<CombatActor>();
            player.Initialize(catalog, pool, world); world.Player = player;
            player.Flight = FindAnyObjectByType<FlyingMode>();
            (movement.GetComponent<CombatInput>() ?? movement.gameObject.AddComponent<CombatInput>()).Initialize(player);
            (GetComponent<CombatHUD>() ?? gameObject.AddComponent<CombatHUD>()).Initialize(player);
            (camera.GetComponent<CameraFollow>() ?? camera.gameObject.AddComponent<CameraFollow>()).SetTarget(player.transform, player.Flight);
            if (arena) BuildArena(world, pool, catalog);
            else
            {
                var bench = CombatArt.Shape("Верстак", (Vector2)player.transform.position + new Vector2(1.5f, -1), new Vector2(1.2f, .6f), new Color(.4f, .25f, .1f));
                bench.AddComponent<Workbench>();
                world.AddItem(ItemEntry.FromWeapon(catalog.CreateWeapon("weapon.rifle", Rarity.Uncommon, 20)), (Vector2)player.transform.position + Vector2.up * 2, "starter.rifle");
            }
            built = true;
        }
        void Start() { if (player != null && player.Flight != null) player.Flight.isFlying = false; }
        static void BuildArena(CombatWorld world, CombatPool pool, CombatCatalog catalog)
        {
            CombatArt.Shape("Arena floor", Vector2.zero, new Vector2(36, 24), new Color(.09f, .13f, .17f), false, -100);
            foreach (var wall in new[] { new Vector4(0, 12, 36, .5f), new Vector4(0, -12, 36, .5f), new Vector4(-18, 0, .5f, 24), new Vector4(18, 0, .5f, 24), new Vector4(5, 2, .25f, 5) })
                CombatArt.Shape("Wall", new Vector2(wall.x, wall.y), new Vector2(wall.z, wall.w), new Color(.35f, .4f, .5f), false, 5).AddComponent<BoxCollider2D>();
            for (int i = 0; i < 5; i++)
            {
                var target = CombatArt.Shape("Target " + i, new Vector2(-6 + i * 3, 5), Vector2.one, new Color(.8f, .25f, .2f), true, 20);
                target.AddComponent<CircleCollider2D>().radius = .5f;
                target.AddComponent<Health>().maxHealth = 100;
                target.AddComponent<PersistentDestructible>().Bind("arena.target." + i);
            }
            var bench = CombatArt.Shape("Workbench", new Vector2(-2, -2), new Vector2(1.5f, .7f), new Color(.55f, .35f, .15f)); bench.AddComponent<Workbench>();
            int index = 0;
            foreach (var item in catalog.items)
            {
                ItemEntry entry = item is WeaponDefinition ? ItemEntry.FromWeapon(catalog.CreateWeapon(item.id, Rarity.Rare, 42 + index)) : new ItemEntry { definitionId = item.id, count = item.kind == ItemKind.Ammo ? 50 : item.kind == ItemKind.Throwable ? 4 : 1 };
                world.AddItem(entry, new Vector2(-8 + (index % 9) * 2, -4 - index / 9 * 2), "arena.loot." + item.id); index++;
            }
        }
        [System.Serializable] class WorldSettings
        {
            public int seed, octaves; public float scale, persistence, lacunarity, upperHeight; public Vector2 offset;
            public WorldSettings(MapGenerator map) { seed = map.seed; octaves = map.octaves; scale = map.noiseScale; persistence = map.persistance; lacunarity = map.lacunarity; upperHeight = map.upperHeight; offset = map.offset; }
        }
    }
}
