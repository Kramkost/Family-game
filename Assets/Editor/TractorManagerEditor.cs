using Kotenkoff;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TractorManager))]
public class TractorManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TractorManager tractor =  (TractorManager)target;
        
        serializedObject.Update();

        GUILayout.Label("Настройки:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isMoving"), new GUIContent("В движении?", "В движении ли Тягач"));
        
        EditorGUILayout.Space(2.5f);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distanceToTractor"), new GUIContent("Расстояние до Тягача", "Текущее расстояние до Тягача"));
        
        EditorGUILayout.Space(3);
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeBetweenSteps"), new GUIContent("Время между шагами", "Время, которое пройдет после предыдущего шага"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stepRange"), new GUIContent("Расстояние шага", "Расстояние, которое пройдёт Тягач за один шаг"));
        
        serializedObject.ApplyModifiedProperties();
    }
}
