using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CarHybridSystem))]
public class CarHybridSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CarHybridSystem carSystem = (CarHybridSystem)target;

        EditorGUILayout.Space(5);
        GUILayout.Label("🚙 Vehicle Architecture System", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space(5);

        serializedObject.Update();

        // 1. Core Settings
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("1. Core Integration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("resourceManager"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldContainer"));
        EditorGUILayout.EndVertical();

        // 2. Network State
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("2. Network State (Read Only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("playersInCar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isRoadMillMode"));
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        // 3. Engine & Physics
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("3. Engine & Mechanics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useRoadMill"));
        if (carSystem.isRoadMillMode || serializedObject.FindProperty("useRoadMill").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("virtualSpeed"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("motorForce"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("steerForce"));
        EditorGUILayout.EndVertical();

        // 4. Visual Polish
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("4. Visual & Camera Polish", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("carModel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchMultiplier"), new GUIContent("Acceleration Pitch (Tilt)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rollMultiplier"), new GUIContent("Steering Roll (Sway)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltLerpSpeed"), new GUIContent("Tilt Smoothness"));
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minFOV"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxFOV"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("speedForMaxFOV"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fovLerpSpeed"));
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}