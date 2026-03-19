using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CarInteractivePart))]
public class CarInteractivePartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CarInteractivePart part = (CarInteractivePart)target;

        EditorGUILayout.Space(5);
        GUILayout.Label("🚪 Interactive Part Settings", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space(5);

        serializedObject.Update();

    
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("actionType"), new GUIContent("Movement Type"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationSpeed"), new GUIContent("Animation Speed"));
        EditorGUILayout.EndVertical();

        InteractActionType type = (InteractActionType)serializedObject.FindProperty("actionType").enumValueIndex;

       
        if (type == InteractActionType.RotateOnly || type == InteractActionType.Both)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Rotation (Doors, Hood, Trunk)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closedRotation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("openRotation"));
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Current as CLOSED", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Closed Rotation");
                part.closedRotation = part.transform.localEulerAngles;
            }
            if (GUILayout.Button("Save Current as OPEN", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Open Rotation");
                part.openRotation = part.transform.localEulerAngles;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

      
        if (type == InteractActionType.TranslateOnly || type == InteractActionType.Both)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Translation (Buttons, Drawers)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closedPosition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("openPosition"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Current as CLOSED", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Closed Position");
                part.closedPosition = part.transform.localPosition;
            }
            if (GUILayout.Button("Save Current as OPEN", GUILayout.Height(25)))
            {
                Undo.RecordObject(part, "Set Open Position");
                part.openPosition = part.transform.localPosition;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

       
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("🛠 Network Debug (Read Only)", EditorStyles.boldLabel);
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isOpen"));
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}