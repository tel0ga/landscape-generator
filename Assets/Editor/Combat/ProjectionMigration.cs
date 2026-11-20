using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ProjectionMigration
{
    public static void MigrateProjectScenes()
    {
        foreach (string path in new[] { "Assets/Scenes/MAIN SCENE.unity", "Assets/Scenes/CombatArena.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Migrate();
            EditorSceneManager.SaveScene(scene);
        }
        UnityEngine.Debug.Log("PROJECTION_SCENES_MIGRATED");
    }
    [MenuItem("Tools/World Projection/Migrate active scene to 2 to 1")]
    public static void Migrate()
    {
        var scene = SceneManager.GetActiveScene();
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Project scene 2:1");
        foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Project scene 2:1");
        SceneProjectionState.Apply(scene);
        Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
    }
}
