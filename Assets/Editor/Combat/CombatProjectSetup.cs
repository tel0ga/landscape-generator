using System.IO;
using System.Linq;
using SurvivalCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class CombatProjectSetup
{
    [MenuItem("Tools/Survival Combat/Create or validate content")]
    public static void CreateContent()
    {
        Directory.CreateDirectory("Assets/Resources/Combat");
        const string catalogPath = "Assets/Resources/Combat/DefaultCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<CombatCatalog>(catalogPath) == null)
        {
            var catalog = CombatDefaults.CreateCatalog();
            AssetDatabase.CreateAsset(catalog, catalogPath);
            foreach (var item in catalog.items) AssetDatabase.AddObjectToAsset(item, catalog);
            EditorUtility.SetDirty(catalog);
        }
        const string inputPath = "Assets/Resources/Combat/CombatActions.inputactions";
        if (!File.Exists(inputPath))
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>(); asset.name = "CombatActions";
            var map = asset.AddActionMap("Combat");
            map.AddAction("AimPosition", InputActionType.PassThrough, "<Pointer>/position").expectedControlType = "Vector2";
            map.AddAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick").expectedControlType = "Vector2";
            void Button(string name, string key, string gamepad = null)
            {
                var action = map.AddAction(name, InputActionType.Button, key);
                if (gamepad != null) action.AddBinding(gamepad);
            }
            Button("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger"); Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Button("Slot1", "<Keyboard>/1"); Button("Slot2", "<Keyboard>/2");
            Button("Next", "<Gamepad>/rightShoulder"); Button("Previous", "<Gamepad>/leftShoulder");
            Button("Throw", "<Keyboard>/g", "<Gamepad>/leftTrigger"); Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            Button("Inventory", "<Keyboard>/tab", "<Gamepad>/start"); Button("Save", "<Keyboard>/f5"); Button("Load", "<Keyboard>/f9");
            File.WriteAllText(inputPath, asset.ToJson()); Object.DestroyImmediate(asset);
        }
        const string scenePath = "Assets/Scenes/CombatArena.unity";
        if (!File.Exists(scenePath))
        {
            
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);
        }
        if (!EditorBuildSettings.scenes.Any(scene => scene.path == scenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("COMBAT_CONTENT_READY");
    }
    [MenuItem("Tools/Survival Combat/Open test arena")]
    public static void OpenArena()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateContent(); EditorSceneManager.OpenScene("Assets/Scenes/CombatArena.unity");
    }
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/CombatArena.unity", "Assets/Scenes/MAIN SCENE.unity" },
            locationPathName = "Builds/Combat/SurvivalCombat.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        Debug.Log("COMBAT_BUILD: " + report.summary.result + "; errors=" + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
