using UnityEditor;
using UnityEngine;
using Kotenkoff; 

[CustomEditor(typeof(TractorManager))]
public class TractorManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        TractorManager manager = (TractorManager)target;

        EditorGUILayout.Space(5);
        GUILayout.Label("🚛 Tractor Spawner System", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space(5);

        serializedObject.Update();

        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("1. Setup & References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tractorPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("carTransform"));
        EditorGUILayout.EndVertical();

       
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("2. Virtual Simulation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnDistance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("virtualSpeed"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("3. Network State (Read Only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distanceToTractor"));
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}