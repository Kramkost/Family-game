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

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("1. Core Integration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("resourceManager"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldContainer"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("2. Network State (Read Only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("playersInCar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isRoadMillMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineSpeedModifier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isEngineDead"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isEngineOn"));
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("3. Engine & Mechanics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useRoadMill"));
        if (carSystem.isRoadMillMode || serializedObject.FindProperty("useRoadMill").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("virtualSpeed"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("motorForce"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("steerForce"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crashThreshold"));
        EditorGUILayout.EndVertical();

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

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("5. Network Visuals (Audio, Lights, Interior)", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineAudio"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("idlePitch"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPitch"));
        EditorGUILayout.Space(5);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("fxAudioSource"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hornSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightSwitchSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("brakeSquealSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineStartSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineStopSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crashSounds"), true);
        EditorGUILayout.Space(5);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("headlights"), true); 
        EditorGUILayout.Space(5);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("steeringWheel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSteeringAngle"));
        EditorGUILayout.Space(5);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasPedal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("brakePedal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pedalTravel"));
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}