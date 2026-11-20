using UnityEditor;
using UnityEngine;
using SnowTracks;

[CustomEditor(typeof(SnowTrackWorld))]
public sealed class SnowTrackWorldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var world=(SnowTrackWorld)target;
        if(!Application.isPlaying) { EditorGUILayout.HelpBox("The scene's EndlessTerain Snow Track Mode selects Off, MobileParticles or FullSnow at startup.",MessageType.Info);return; }
        EditorGUILayout.HelpBox(world.Status,world.Mode!=SnowTrackMode.Off&&!world.TracksAvailable?MessageType.Warning:MessageType.Info);
        EditorGUILayout.Toggle("Tracks available",world.TracksAvailable);
        EditorGUILayout.Toggle("Full snow supported",world.FullSnowAvailable);
        EditorGUILayout.LabelField("CPU masks / GPU textures",world.MaskCount+" / "+world.TextureCount);
        EditorGUILayout.LabelField("Live particle footprints",world.ParticleCount.ToString());
        if(GUILayout.Button("Apply selected mode"))world.SetMode(world.Mode);
    }
}
